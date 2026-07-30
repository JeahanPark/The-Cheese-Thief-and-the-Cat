using System.Collections.Generic;
using CTC.Runtime.Core;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 로비의 **네트워크 상태만** 소유한다 — 참가자 목록·역할·시작 여부 (MVC의 Model).
    /// 스폰·맵·UI는 각각 PlayerSpawner·MapDirector·LobbyController가 맡는다(개편 3단계, 2026-07-31).
    ///
    /// LobbyController와 같은 in-scene NetworkObject에 동승한다 — NetworkBehaviour를 여러 개 붙이면
    /// RPC 발신자 식별(RpcParams.Receive.SenderClientId)이 각자 그대로 동작한다.
    /// </summary>
    public class LobbySession : NetworkBehaviour
    {
        public const int CAT_CAPACITY = 1;
        private const float RECONCILE_INTERVAL_SECONDS = 1f;

        private NetworkList<LobbyPlayerState> m_Players;
        private readonly NetworkVariable<bool> m_GameStarted = new NetworkVariable<bool>();

        private float m_ReconcileTimer;

        public bool GameStarted => m_GameStarted.Value;

        /// <summary>시작 여부 스트림 — View는 이걸 구독해 표시/숨김을 정한다(매 프레임 조회 대신).</summary>
        public Observable<bool> GameStartedChanged => m_GameStarted.AsObservableWithCurrent();
        public int PlayerCount => m_Players.Count;
        public LobbyPlayerState GetPlayer(int _index) => m_Players[_index];
        public bool CanStart => LobbyRoleLogic.CanStart(SnapshotRoles());

        private void Awake()
        {
            // NetworkList는 필드 초기화 대신 Awake에서 생성 (NGO 권장 — 도메인 리로드 누수 방지)
            m_Players = new NetworkList<LobbyPlayerState>();
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

        // ---- 역할 ----

        /// <summary>로컬 클라이언트의 역할 선택 요청. 판정 권위는 서버.</summary>
        public void SelectRole(RoleId _role)
        {
            SelectRoleRpc(_role);
        }

        [Rpc(SendTo.Server)]
        private void SelectRoleRpc(RoleId _role, RpcParams _rpcParams = default)
        {
            if (m_GameStarted.Value) return;

            ulong sender = _rpcParams.Receive.SenderClientId;
            int idx = IndexOf(sender);
            if (!LobbyRoleLogic.CanSelect(SnapshotRoles(), idx, _role, CAT_CAPACITY)) return;

            LobbyPlayerState st = m_Players[idx];
            st.Role = _role;
            m_Players[idx] = st;
        }

        // ---- 시작·복귀 (서버 전용) ----

        public bool ServerBeginGame()
        {
            if (!IsServer || m_GameStarted.Value) return false;
            if (!LobbyRoleLogic.CanStart(SnapshotRoles())) return false;

            m_GameStarted.Value = true;
            return true;
        }

        /// <summary>매치 종료 후 로비 복귀(MatchManager 재시작 스윕 뒤). 역할은 유지.</summary>
        public void ServerResetToLobby()
        {
            if (!IsServer) return;
            m_GameStarted.Value = false;
        }

        // ---- 참가자 목록 ----

        public List<RoleId> SnapshotRoles()
        {
            var roles = new List<RoleId>(m_Players.Count);
            for (int i = 0; i < m_Players.Count; i++) roles.Add(m_Players[i].Role);
            return roles;
        }

        public int IndexOf(ulong _clientId)
        {
            for (int i = 0; i < m_Players.Count; i++)
            {
                if (m_Players[i].ClientId == _clientId) return i;
            }
            return -1;
        }

        private void OnClientConnected(ulong _clientId) => EnsureEntry(_clientId);

        private void OnClientDisconnected(ulong _clientId)
        {
            int idx = IndexOf(_clientId);
            if (idx >= 0) m_Players.RemoveAt(idx);
            Debug.Log($"[Lobby] 접속 해제 client={_clientId} → 인원 {m_Players.Count}");
        }

        private void EnsureEntry(ulong _clientId)
        {
            if (IndexOf(_clientId) >= 0) return;

            m_Players.Add(new LobbyPlayerState { ClientId = _clientId, Role = RoleId.None });
            Debug.Log($"[Lobby] 접속 client={_clientId} → 인원 {m_Players.Count}");
        }

        // 서버 전용: 실제 연결과 목록을 주기적으로 대조해 유령 항목 제거.
        // 반쯤 실패한 접속(연결 콜백은 왔으나 세션이 끊긴 경우)이 목록에 남아
        // 인원수 불일치·스폰 어긋남을 만드는 것을 막는다 (2026-07-27 실측 버그).
        private void Update()
        {
            if (!IsSpawned || !IsServer || m_GameStarted.Value) return;

            m_ReconcileTimer += Time.deltaTime;
            if (m_ReconcileTimer < RECONCILE_INTERVAL_SECONDS) return;
            m_ReconcileTimer = 0f;

            for (int i = m_Players.Count - 1; i >= 0; i--)
            {
                ulong id = m_Players[i].ClientId;
                if (IsConnected(id)) continue;

                m_Players.RemoveAt(i);
                Debug.LogWarning($"[Lobby] 유령 항목 제거 client={id} → 인원 {m_Players.Count}");
            }
        }

        private bool IsConnected(ulong _clientId)
        {
            IReadOnlyList<ulong> ids = NetworkManager.ConnectedClientsIds;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == _clientId) return true;
            }
            return false;
        }
    }
}
