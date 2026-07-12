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
        }

        private void EnsureEntry(ulong clientId)
        {
            if (IndexOf(clientId) < 0)
            {
                _players.Add(new LobbyPlayerState { ClientId = clientId, Role = RoleId.None });
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

        /// <summary>소유 클라이언트가 스폰 시 자기 자리를 계산할 때 사용(서버 스폰 공식과 동일).</summary>
        public int IndexOfClient(ulong clientId) => IndexOf(clientId);

        public static Vector3 SpawnPosition(int index, int playerCount)
        {
            return new Vector3(index * 2f - (playerCount - 1), 0.1f, 0f);
        }

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
            for (int i = 0; i < _players.Count; i++)
            {
                LobbyPlayerState p = _players[i];
                GameObject prefab = p.Role == RoleId.Cat ? catPrefab : mousePrefab;
                Vector3 pos = SpawnPosition(i, _players.Count);
                GameObject go = Instantiate(prefab, pos, Quaternion.identity);
                go.GetComponent<NetworkObject>().SpawnAsPlayerObject(p.ClientId);
                if (p.Role == RoleId.Mouse)
                {
                    go.GetComponent<Economy.CheeseWallet>().AddCheese(Economy.SurvivalLogic.STARTING_CHEESE);
                }
            }
        }

        // ---- MCP/QA 자동화용 진입점 (u_play invoke, 클론 커맨드에서 사용) ----
        public void DevSelectMouse() => SelectRole(RoleId.Mouse);
        public void DevSelectCat() => SelectRole(RoleId.Cat);
        public void DevStartGame() => RequestStartGame();
    }
}
