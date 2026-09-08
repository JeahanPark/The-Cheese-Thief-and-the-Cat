using System.Collections.Generic;
using CTC.Runtime.Map;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 로비 흐름 조립자 (MVC의 Controller). 상태·스폰·맵을 직접 들고 있지 않고
    /// `LobbySession`(상태) · `PlayerSpawner`(스폰) · `MapDirector`(맵)에 위임한다.
    ///
    /// 개편 3단계(2026-07-31) 전에는 이 클래스가 290줄로 넷을 다 겸했다. 지금은 넘겨주는 역할만 한다.
    /// 옛 이름 `LobbyManager`에서 리네임(2026-07-31) — 규약상 Manager는 씬을 넘어 사는 글로벌 싱글톤이고
    /// 이건 씬 오브젝트라 Controller가 맞다.
    ///
    /// 셋 다 같은 in-scene NetworkObject에 동승한다(NetworkBehaviour 다중 부착).
    /// </summary>
    public class LobbyController : NetworkBehaviour
    {
        public const int CatCapacity = LobbySession.CAT_CAPACITY;
        public const float FORTRESS_ENTRANCE_OFFSET_METERS = PlayerSpawner.FORTRESS_ENTRANCE_OFFSET_METERS;

        private LobbySession m_Session;
        private PlayerSpawner m_Spawner;
        private MapDirector m_MapDirector;
        private Map.AlcoveSpawner m_AlcoveSpawner;
        private Map.LootBoxSpawner m_BoxSpawner;
        private Map.ShopSpotSpawner m_ShopSpawner;
        private Match.MatchRandom m_Random;

        private void Awake()
        {
            m_Session = GetComponent<LobbySession>();
            m_Spawner = GetComponent<PlayerSpawner>();
            m_MapDirector = GetComponent<MapDirector>();
            m_AlcoveSpawner = GetComponent<Map.AlcoveSpawner>();
            m_BoxSpawner = GetComponent<Map.LootBoxSpawner>();
            m_ShopSpawner = GetComponent<Map.ShopSpotSpawner>();
            m_Random = GetComponent<Match.MatchRandom>();
        }

        // ---- 상태 조회 (View·QA가 읽는다) ----

        public bool GameStarted => m_Session != null && m_Session.GameStarted;

        /// <summary>시작 여부 스트림(View용). 세션이 없으면 false 하나만 흘린다.</summary>
        public Observable<bool> GameStartedChanged =>
            m_Session != null ? m_Session.GameStartedChanged : Observable.Return(false);

        /// <summary>
        /// 로비 화면에 영향을 주는 모든 변화를 하나로 합친 신호 — 스폰·참가자·역할·시작·맵.
        /// View는 이걸 구독해 **바뀔 때만** 다시 그린다(개편 3b의 마무리, 2026-07-31).
        ///
        /// 필드마다 스트림을 쪼개지 않은 건 로비 표시가 통짜 텍스트·버튼 몇 개라서다 —
        /// 어차피 전부 다시 그리는데 델타를 나르면 배선만 늘고 얻는 게 없다.
        /// 구독 즉시 한 번 흘려(`Prepend`) 첫 변화 전까지 화면이 비는 것을 막는다.
        /// </summary>
        public Observable<Unit> ViewChanged
        {
            get
            {
                if (m_Session == null) return Observable.Return(Unit.Default);

                Observable<Unit> merged = Observable.Merge(
                    m_Session.SpawnChanged,
                    m_Session.PlayersChanged,
                    m_Session.GameStartedChanged.AsUnitObservable());

                if (m_MapDirector != null)
                    merged = Observable.Merge(merged, m_MapDirector.MapIndexChanged.AsUnitObservable());

                return merged.Prepend(Unit.Default);
            }
        }
        public int PlayerCount => m_Session != null ? m_Session.PlayerCount : 0;

        /// <summary>쥐를 고른 사람 수 — 치즈 총량이 여기 비례한다(2026-08-10).</summary>
        public int MouseCount => m_Session != null ? m_Session.MouseCount : 0;
        public LobbyPlayerState GetPlayer(int _index) => m_Session.GetPlayer(_index);
        public bool CanStart => m_Session != null && m_Session.CanStart;
        public int IndexOfClient(ulong _clientId) => m_Session != null ? m_Session.IndexOf(_clientId) : -1;

        public int MapCount => m_MapDirector != null ? m_MapDirector.MapCount : 0;
        public int MapIndex => m_MapDirector != null ? m_MapDirector.MapIndex : 0;
        public MapLayout ActiveLayout => m_MapDirector != null ? m_MapDirector.ActiveLayout : null;
        public string MapNameAt(int _index) => m_MapDirector != null ? m_MapDirector.MapNameAt(_index) : "";

        // ---- 입력 → 요청 (Controller의 본체) ----

        /// <summary>로컬 클라이언트의 역할 선택 요청(UI에서 호출).</summary>
        public void SelectRole(RoleId _role)
        {
            if (m_Session != null) m_Session.SelectRole(_role);
        }

        /// <summary>서버 전용 — 역할을 통째로 다시 앉힌다(3판 시리즈의 고양이 재추첨).</summary>
        public void ServerAssignRoles(System.Collections.Generic.IReadOnlyDictionary<ulong, RoleId> _roles)
        {
            if (m_Session != null) m_Session.ServerAssignRoles(_roles);
        }

        /// <summary>맵 선택 요청 — 서버가 방장인지 확인한다.</summary>
        public void SelectMap(int _index)
        {
            if (m_MapDirector != null) m_MapDirector.SelectMap(_index);
        }

        /// <summary>호스트 UI에서 호출. 서버에서만 동작.</summary>
        public void RequestStartGame()
        {
            if (!IsServer || m_Session == null) return;
            if (!m_Session.ServerBeginGame()) return;

            // 판 시드를 **스포너보다 먼저** 굴린다 — 상점 자리·품절·직업권이 전부 이 값을 읽는다.
            // 순서가 뒤집히면 그 판만 옛 시드로 서고 아무 에러도 안 난다(테크 v3 계획 3).
            if (m_Random != null) m_Random.ServerRoll();

            if (m_Spawner != null) m_Spawner.ServerSpawnAll(m_Session, ActiveLayout);
            // 벽감은 판이 시작할 때 맵이 깐다(요새·창고는 쥐가 산다)
            if (m_AlcoveSpawner != null) m_AlcoveSpawner.ServerSpawnAll(ActiveLayout);
            if (m_BoxSpawner != null) m_BoxSpawner.ServerSpawnAll(ActiveLayout);
            if (m_ShopSpawner != null) m_ShopSpawner.ServerSpawnAll(ActiveLayout);   // 상점 자리(테크 v3)
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>QA 전용 — 인원 조건을 건너뛰고 판을 연다(봇만 도는 측정 판).
        /// 개발 빌드까지 열어 둔 이유: 봇전 버튼(<see cref="DevStartBotMatch"/>)이 이걸 탄다.</summary>
        public void DevForceStartGame()
        {
            if (!IsServer || m_Session == null) return;
            if (!m_Session.ServerForceBeginGame()) return;

            // 여기도 똑같이 — 빠뜨리면 **봇 계측 판만 시드가 0**이 되어 무작위가 통째로 죽는다.
            if (m_Random != null) m_Random.ServerRoll();

            if (m_Spawner != null) m_Spawner.ServerSpawnAll(m_Session, ActiveLayout);
            if (m_AlcoveSpawner != null) m_AlcoveSpawner.ServerSpawnAll(ActiveLayout);
            if (m_BoxSpawner != null) m_BoxSpawner.ServerSpawnAll(ActiveLayout);
            if (m_ShopSpawner != null) m_ShopSpawner.ServerSpawnAll(ActiveLayout);   // 상점 자리(테크 v3)
        }

        /// <summary>
        /// 개발 빌드 전용 — **혼자서 봇 상대 2:8**을 연다(로비의 "봇전 시작" 버튼).
        /// 내 역할을 읽어 반대편을 봇으로 채운다: 고양이면 봇 쥐 8 + 봇 고양이 1,
        /// 쥐면 봇 쥐 7 + 봇 고양이 2 — 어느 쪽이든 판은 2:8이다.
        /// </summary>
        public void DevStartBotMatch()
        {
            if (!IsServer) return;

            RoleId mine = LocalRole();
            if (mine != RoleId.Cat && mine != RoleId.Mouse) return;

            DevForceStartGame();
            SpawnBotsWhenReady(
                mine == RoleId.Cat ? 8 : 7,
                mine == RoleId.Cat ? 1 : 2).Forget();
        }

        private RoleId LocalRole()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || m_Session == null) return RoleId.None;

            for (int i = 0; i < PlayerCount; i++)
            {
                LobbyPlayerState p = GetPlayer(i);
                if (p.ClientId == nm.LocalClientId) return p.Role;
            }
            return RoleId.None;
        }

        // 맵을 짓고 폴리곤을 굽는 데 몇 프레임 걸린다 — 폴리곤이 서기 전에 봇을 세우면
        // 한 발도 못 뗀다. 판이 열리고 폴리곤이 잡힐 때까지 기다렸다 세운다(상한 15초).
        private async UniTask SpawnBotsWhenReady(int _mice, int _cats)
        {
            var cancel = this.GetCancellationTokenOnDestroy();
            float deadline = Time.time + 15f;
            while (Time.time < deadline)
            {
                await UniTask.Delay(250, cancellationToken: cancel);
                if (!GameStarted) continue;

                UnityEngine.AI.NavMeshTriangulation tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
                if (tri.indices != null && tri.indices.Length > 0) break;
            }

            var bots = Bots.TestBotManager.Instance;
            if (bots != null) bots.ServerSpawn(_mice, _cats);
        }
#endif

        /// <summary>매치 종료 후 로비 복귀(MatchManager 재시작 스윕 뒤 호출). 역할은 유지.</summary>
        public void ResetToLobby()
        {
            if (m_Session != null) m_Session.ServerResetToLobby();
            if (m_AlcoveSpawner != null) m_AlcoveSpawner.ServerDespawnAll();
            if (m_BoxSpawner != null) m_BoxSpawner.ServerDespawnAll();
            if (m_ShopSpawner != null) m_ShopSpawner.ServerDespawnAll();
        }

        // ---- 스폰 공식 (소유자 클라가 자기 자리를 계산할 때) ----

        /// <summary>순수 공식 위임 — 테스트는 <see cref="PlayerSpawner.SpawnPosition"/>을 직접 부른다.</summary>
        public static Vector3 SpawnPosition(
            IReadOnlyList<RoleId> _roles, int _index, IReadOnlyList<(Vector3 pos, float yaw)> _fortresses)
            => PlayerSpawner.SpawnPosition(_roles, _index, _fortresses);

        /// <summary>소유 클라이언트가 스폰 시 자기 자리를 계산할 때 사용(서버 공식과 동일).</summary>
        public Vector3 SpawnPositionOf(int _index) =>
            PlayerSpawner.SpawnPosition(
                m_Session.SnapshotRoles(), _index, PlayerSpawner.FortressSpots(ActiveLayout));

        // ---- MCP/QA 자동화용 진입점 (u_play invoke, 클론 커맨드에서 사용) ----
        public void DevSelectMouse() => SelectRole(RoleId.Mouse);
        public void DevSelectCat() => SelectRole(RoleId.Cat);
        public void DevStartGame() => RequestStartGame();
        public void DevSelectMap(int _index) => SelectMap(_index);
    }
}
