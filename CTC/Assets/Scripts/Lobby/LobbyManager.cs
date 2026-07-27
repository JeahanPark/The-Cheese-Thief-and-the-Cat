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

        [SerializeField] private GameObject catPrefab;
        [SerializeField] private GameObject mousePrefab;
        [SerializeField] private GameObject fortressPrefab;
        [SerializeField] private CTC.Runtime.Map.MapLayout m_ActiveLayout; // 씬 굽기(Setup)가 배선

        private NetworkList<LobbyPlayerState> _players;
        private readonly NetworkVariable<bool> _gameStarted = new NetworkVariable<bool>();

        public bool GameStarted => _gameStarted.Value;
        public int PlayerCount => _players.Count;
        public LobbyPlayerState GetPlayer(int index) => _players[index];
        public bool CanStart => LobbyRoleLogic.CanStart(SnapshotRoles());

        private void Awake()
        {
            // NetworkList는 필드 초기화 대신 Awake에서 생성 (NGO 권장 — 도메인 리로드 누수 방지)
            _players = new NetworkList<LobbyPlayerState>();
        }

        public override void OnNetworkSpawn()
        {
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
            if (NetworkManager == null) return;
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void OnClientConnected(ulong clientId)
        {
            EnsureEntry(clientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            int idx = IndexOf(clientId);
            if (idx >= 0) _players.RemoveAt(idx);
            Debug.Log($"[Lobby] 접속 해제 client={clientId} → 인원 {_players.Count}");
        }

        private void EnsureEntry(ulong clientId)
        {
            if (IndexOf(clientId) < 0)
            {
                _players.Add(new LobbyPlayerState { ClientId = clientId, Role = RoleId.None });
                Debug.Log($"[Lobby] 접속 client={clientId} → 인원 {_players.Count}");
            }
        }

        private int IndexOf(ulong clientId)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == clientId) return i;
            }
            return -1;
        }

        public int IndexOfClient(ulong clientId) => IndexOf(clientId);

        public const float FORTRESS_ENTRANCE_OFFSET_METERS = 1.8f;

        // 활성 MapLayout의 요새자리를 (pos, yaw) 리스트로 — 미배선이면 빈 리스트.
        private List<(Vector3 pos, float yaw)> FortressSpots()
        {
            var list = new List<(Vector3, float)>();
            if (m_ActiveLayout == null) return list;
            foreach (var f in m_ActiveLayout.Fortresses)
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
            var roles = new List<RoleId>(_players.Count);
            for (int i = 0; i < _players.Count; i++) roles.Add(_players[i].Role);
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
            if (_gameStarted.Value) return;

            ulong sender = rpcParams.Receive.SenderClientId;
            int idx = IndexOf(sender);
            if (!LobbyRoleLogic.CanSelect(SnapshotRoles(), idx, role, CatCapacity)) return;

            LobbyPlayerState st = _players[idx];
            st.Role = role;
            _players[idx] = st;
        }

        /// <summary>호스트 UI에서 호출. 서버에서만 동작.</summary>
        public void RequestStartGame()
        {
            if (!IsServer || _gameStarted.Value) return;
            if (!LobbyRoleLogic.CanStart(SnapshotRoles())) return;

            _gameStarted.Value = true;
            SpawnPlayers();
        }

        /// <summary>매치 종료 후 로비 복귀(MatchManager 재시작 스윕 뒤 호출). 역할은 유지.</summary>
        public void ResetToLobby()
        {
            if (!IsServer) return;
            _gameStarted.Value = false;
        }

        private void SpawnPlayers()
        {
            List<RoleId> roles = SnapshotRoles();
            var forts = FortressSpots();
            for (int i = 0; i < _players.Count; i++)
            {
                LobbyPlayerState p = _players[i];
                GameObject prefab = p.Role == RoleId.Cat ? catPrefab : mousePrefab;
                Vector3 pos = SpawnPosition(roles, i, forts);
                GameObject go = Instantiate(prefab, pos, Quaternion.identity);
                go.GetComponent<NetworkObject>().SpawnAsPlayerObject(p.ClientId);
                if (p.Role == RoleId.Mouse)
                {
                    SpawnInitialFortress(MouseOrderOf(roles, i), forts);
                }
            }
        }

        // index 앞까지의 쥐 수 = 이 쥐의 요새 순번 (SpawnPosition의 nth와 동일 공식)
        private static int MouseOrderOf(List<RoleId> roles, int index)
        {
            int nth = 0;
            for (int i = 0; i < index; i++)
            {
                if (roles[i] == RoleId.Mouse) nth++;
            }
            return nth;
        }

        // v3 초기 벙커: 쥐마다 자기 요새를 서버가 동적 스폰 — 재시작 스윕이 자동 정리.
        // 쥐가 요새자리보다 많으면 초과분은 기존 요새를 공유(중복 건설 금지 — 리뷰 I1).
        private void SpawnInitialFortress(int mouseOrder, List<(Vector3 pos, float yaw)> forts)
        {
            if (fortressPrefab == null || forts.Count == 0) return;
            if (mouseOrder >= forts.Count) return;

            (Vector3 pos, float yaw) = forts[mouseOrder % forts.Count];
            GameObject go = Instantiate(fortressPrefab, pos, Quaternion.Euler(0f, yaw, 0f));
            go.GetComponent<NetworkObject>().Spawn(true);
        }

        // ---- MCP/QA 자동화용 진입점 (u_play invoke, 클론 커맨드에서 사용) ----
        public void DevSelectMouse() => SelectRole(RoleId.Mouse);
        public void DevSelectCat() => SelectRole(RoleId.Cat);
        public void DevStartGame() => RequestStartGame();
    }
}
