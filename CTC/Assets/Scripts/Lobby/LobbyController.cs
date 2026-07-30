using System.Collections.Generic;
using CTC.Runtime.Map;
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

        private void Awake()
        {
            m_Session = GetComponent<LobbySession>();
            m_Spawner = GetComponent<PlayerSpawner>();
            m_MapDirector = GetComponent<MapDirector>();
        }

        // ---- 상태 조회 (View·QA가 읽는다) ----

        public bool GameStarted => m_Session != null && m_Session.GameStarted;

        /// <summary>시작 여부 스트림(View용). 세션이 없으면 false 하나만 흘린다.</summary>
        public Observable<bool> GameStartedChanged =>
            m_Session != null ? m_Session.GameStartedChanged : Observable.Return(false);
        public int PlayerCount => m_Session != null ? m_Session.PlayerCount : 0;
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

            if (m_Spawner != null) m_Spawner.ServerSpawnAll(m_Session, ActiveLayout);
        }

        /// <summary>매치 종료 후 로비 복귀(MatchManager 재시작 스윕 뒤 호출). 역할은 유지.</summary>
        public void ResetToLobby()
        {
            if (m_Session != null) m_Session.ServerResetToLobby();
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
