using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 접속 후 인게임 전까지의 로비 상태를 소유한다(in-scene NetworkObject).
    /// 서버가 참가자 목록·역할을 NetworkList로 복제하고, 시작 시 역할별 프리팹을 스폰한다.
    /// NetworkManager.PlayerPrefab 자동 스폰은 꺼져 있어야 한다(SetupNetworkingScene이 보장).
    /// </summary>
    public class LobbyManager : NetworkBehaviour
    {
        public const int CatCapacity = 1;

        [SerializeField] private GameObject m_CatPrefab;
        [SerializeField] private GameObject m_MousePrefab;

        // v3.5: 맵이 런타임에 지어진다. 로비에서 호스트가 고르면 전원이 그 맵을 다시 짓는다.
        [SerializeField] private CTC.Runtime.Map.MapLayout[] m_MapLayouts;
        [SerializeField] private CTC.Runtime.Map.MapBuilder m_MapBuilder;
        [SerializeField] private Economy.CheeseSpotDirector m_CheeseDirector;

        // v3.4: 초기 요새 지급 폐지. 시드머니는 미끼 1개(50)분 — 요새(220)는 벌어서 짓는다.
        // 220을 주면 t=0 요새와 사실상 같고, 기관총(600) 진척을 37% 앞당겨 고양이가 이중 너프된다.
        // (구) 인스펙터 시작 치즈 — CSV로 옮겨 더 이상 읽지 않는다. 개편 2단계에서 필드째 제거.
        [SerializeField] private int m_MouseStartCheese = 50;

        private NetworkList<LobbyPlayerState> m_Players;
        private readonly NetworkVariable<bool> m_GameStarted = new NetworkVariable<bool>();
        private readonly NetworkVariable<int> m_MapIndex = new NetworkVariable<int>();

        public bool GameStarted => m_GameStarted.Value;
        public int PlayerCount => m_Players.Count;
        public LobbyPlayerState GetPlayer(int index) => m_Players[index];
        public bool CanStart => LobbyRoleLogic.CanStart(SnapshotRoles());

        public int MapCount => m_MapLayouts != null ? m_MapLayouts.Length : 0;
        public int MapIndex => LobbyRoleLogic.ClampMapIndex(m_MapIndex.Value, MapCount);

        /// <summary>선택된 맵. 스폰 공식·요새 자리가 이걸 읽는다.</summary>
        public CTC.Runtime.Map.MapLayout ActiveLayout =>
            MapCount == 0 ? null : m_MapLayouts[MapIndex];

        public string MapNameAt(int _index) =>
            MapCount == 0 ? "" : m_MapLayouts[LobbyRoleLogic.ClampMapIndex(_index, MapCount)].DisplayName;

        private void Awake()
        {
            // NetworkList는 필드 초기화 대신 Awake에서 생성 (NGO 권장 — 도메인 리로드 누수 방지)
            m_Players = new NetworkList<LobbyPlayerState>();
        }

        public override void OnNetworkSpawn()
        {
            // 지오메트리는 복제하지 않는다 — 전 클라가 같은 레이아웃으로 각자 짓는다
            m_MapIndex.OnValueChanged += OnMapIndexChanged;
            RebuildMap();

            if (!IsServer) return;

            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
            {
                EnsureEntry(id);
            }
        }

        public override void OnNetworkDespawn()
        {
            m_MapIndex.OnValueChanged -= OnMapIndexChanged;
            if (NetworkManager == null) return;
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        // ---- 맵 선택 (호스트 전용) ----

        /// <summary>로컬 UI/QA 진입점. 실제 권한 판정은 서버.</summary>
        public void SelectMap(int _index)
        {
            SelectMapRpc(_index);
        }

        [Rpc(SendTo.Server)]
        private void SelectMapRpc(int _index, RpcParams rpcParams = default)
        {
            // 방장만 바꾼다 — 시작 버튼과 같은 권한 구조
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            if (m_GameStarted.Value) return;

            m_MapIndex.Value = LobbyRoleLogic.ClampMapIndex(_index, MapCount);
        }

        private void OnMapIndexChanged(int _previous, int _current)
        {
            RebuildMap();
        }

        // 맵을 새로 짓고 치즈 감독에 재바인딩한다. 순서가 중요 — 지점이 만들어진 뒤에
        // 복제 상태를 붙여야 인덱스가 어긋나지 않는다.
        private void RebuildMap()
        {
            if (m_MapBuilder == null || ActiveLayout == null) return;

            m_MapBuilder.Build(ActiveLayout);
            if (m_CheeseDirector != null) m_CheeseDirector.Rebind(m_MapBuilder.Spots);
        }

        private void OnClientConnected(ulong clientId)
        {
            EnsureEntry(clientId);
        }

        // 서버 전용: 실제 연결과 목록을 주기적으로 대조해 유령 항목 제거.
        // 반쯤 실패한 접속(연결 콜백은 왔으나 세션이 끊긴 경우)이 목록에 남아
        // 인원수 불일치·스폰 어긋남을 만드는 것을 막는다 (2026-07-27 실측 버그).
        private float m_ReconcileTimer;

        private void Update()
        {
            if (!IsSpawned || !IsServer || m_GameStarted.Value) return;

            m_ReconcileTimer += Time.deltaTime;
            if (m_ReconcileTimer < 1f) return;
            m_ReconcileTimer = 0f;

            for (int i = m_Players.Count - 1; i >= 0; i--)
            {
                ulong id = m_Players[i].ClientId;
                if (IsConnected(id)) continue;

                m_Players.RemoveAt(i);
                Debug.LogWarning($"[Lobby] 유령 항목 제거 client={id} → 인원 {m_Players.Count}");
            }
        }

        private bool IsConnected(ulong clientId)
        {
            IReadOnlyList<ulong> ids = NetworkManager.ConnectedClientsIds;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == clientId) return true;
            }
            return false;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            int idx = IndexOf(clientId);
            if (idx >= 0) m_Players.RemoveAt(idx);
            Debug.Log($"[Lobby] 접속 해제 client={clientId} → 인원 {m_Players.Count}");
        }

        private void EnsureEntry(ulong clientId)
        {
            if (IndexOf(clientId) < 0)
            {
                m_Players.Add(new LobbyPlayerState { ClientId = clientId, Role = RoleId.None });
                Debug.Log($"[Lobby] 접속 client={clientId} → 인원 {m_Players.Count}");
            }
        }

        private int IndexOf(ulong clientId)
        {
            for (int i = 0; i < m_Players.Count; i++)
            {
                if (m_Players[i].ClientId == clientId) return i;
            }
            return -1;
        }

        public int IndexOfClient(ulong clientId) => IndexOf(clientId);

        public const float FORTRESS_ENTRANCE_OFFSET_METERS = 1.8f;

        // 활성 MapLayout의 요새자리를 (pos, yaw) 리스트로 — 미배선이면 빈 리스트.
        private List<(Vector3 pos, float yaw)> FortressSpots()
        {
            var list = new List<(Vector3, float)>();
            if (ActiveLayout == null) return list;
            foreach (var f in ActiveLayout.Fortresses)
                list.Add((new Vector3(f.Pos.x, 0f, f.Pos.y), f.YawDeg));
            return list;
        }

        /// <summary>
        /// 결정적 스폰 공식(서버 스폰·소유자 초기 텔레포트 공용) —
        /// k번째 고양이는 중앙 라인, m번째 쥐는 자기 요새 입구 앞.
        /// 요새자리는 MapLayout 주입(Plan 11) — 순수 공식이라 테스트는 리스트를 직접 전달.
        /// </summary>
        public static Vector3 SpawnPosition(
            IReadOnlyList<RoleId> roles, int index, IReadOnlyList<(Vector3 pos, float yaw)> fortresses)
        {
            int nth = 0; // 같은 역할 내 순번
            for (int i = 0; i < index; i++)
            {
                if (roles[i] == roles[index]) nth++;
            }

            if (roles[index] == RoleId.Cat) return new Vector3(nth * 2f, 0.1f, 0f);

            if (fortresses.Count == 0) return new Vector3(0f, 0.1f, 0f); // 방어 — layout 미배선
            (Vector3 pos, float yaw) = fortresses[nth % fortresses.Count];
            Vector3 entranceDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 spawn = pos + entranceDir * FORTRESS_ENTRANCE_OFFSET_METERS;
            // 요새보다 쥐가 많아 같은 요새를 공유하면 옆으로 비껴 스폰 — 동일 좌표 CC 중첩 방지
            int wave = nth / fortresses.Count;
            if (wave > 0)
            {
                spawn += Quaternion.Euler(0f, yaw, 0f) * Vector3.right * (0.7f * wave);
            }
            spawn.y = 0.1f;
            return spawn;
        }

        /// <summary>소유 클라이언트가 스폰 시 자기 자리를 계산할 때 사용(서버 공식과 동일).</summary>
        public Vector3 SpawnPositionOf(int index) => SpawnPosition(SnapshotRoles(), index, FortressSpots());

        private List<RoleId> SnapshotRoles()
        {
            var roles = new List<RoleId>(m_Players.Count);
            for (int i = 0; i < m_Players.Count; i++) roles.Add(m_Players[i].Role);
            return roles;
        }

        /// <summary>로컬 클라이언트의 역할 선택 요청(UI에서 호출).</summary>
        public void SelectRole(RoleId role)
        {
            SelectRoleRpc(role);
        }

        [Rpc(SendTo.Server)]
        private void SelectRoleRpc(RoleId role, RpcParams rpcParams = default)
        {
            if (m_GameStarted.Value) return;

            ulong sender = rpcParams.Receive.SenderClientId;
            int idx = IndexOf(sender);
            if (!LobbyRoleLogic.CanSelect(SnapshotRoles(), idx, role, CatCapacity)) return;

            LobbyPlayerState st = m_Players[idx];
            st.Role = role;
            m_Players[idx] = st;
        }

        /// <summary>호스트 UI에서 호출. 서버에서만 동작.</summary>
        public void RequestStartGame()
        {
            if (!IsServer || m_GameStarted.Value) return;
            if (!LobbyRoleLogic.CanStart(SnapshotRoles())) return;

            m_GameStarted.Value = true;
            SpawnPlayers();
        }

        /// <summary>매치 종료 후 로비 복귀(MatchManager 재시작 스윕 뒤 호출). 역할은 유지.</summary>
        public void ResetToLobby()
        {
            if (!IsServer) return;
            m_GameStarted.Value = false;
        }

        private void SpawnPlayers()
        {
            List<RoleId> roles = SnapshotRoles();
            var forts = FortressSpots();
            for (int i = 0; i < m_Players.Count; i++)
            {
                LobbyPlayerState p = m_Players[i];
                GameObject prefab = p.Role == RoleId.Cat ? m_CatPrefab : m_MousePrefab;
                Vector3 pos = SpawnPosition(roles, i, forts);
                GameObject go = Instantiate(prefab, pos, Quaternion.identity);
                go.GetComponent<NetworkObject>().SpawnAsPlayerObject(p.ClientId);
                if (p.Role == RoleId.Mouse)
                {
                    // 스폰 직후 서버 권위로 지급 — 요새 1채분. 사는 시점·자리는 쥐의 선택.
                    var wallet = go.GetComponent<CTC.Runtime.Economy.CheeseWallet>();
                    // 시작 치즈는 CSV(BalanceInfo.csv)가 원본 — 인스펙터 값은 쓰지 않는다(2026-07-31)
                    if (wallet != null) wallet.AddCheese(CTC.Runtime.Info.InfoManager.Instance.Balance.MouseStartCheese);
                }
            }
        }

        // ---- MCP/QA 자동화용 진입점 (u_play invoke, 클론 커맨드에서 사용) ----
        public void DevSelectMouse() => SelectRole(RoleId.Mouse);
        public void DevSelectCat() => SelectRole(RoleId.Cat);
        public void DevStartGame() => RequestStartGame();
        public void DevSelectMap(int _index) => SelectMap(_index);
    }
}
