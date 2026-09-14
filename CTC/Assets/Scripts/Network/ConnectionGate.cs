using CTC.Runtime.Core;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 모든 접속이 지나는 관문 (2026-09-14). 판정은 <see cref="ConnectionGateLogic"/>에 있고 여기선 NGO에 잇기만 한다.
    ///
    /// **설치는 <see cref="NetworkManager.OnInstantiated"/>에서 한다.** NetworkManager가 서는 곳이 둘이다 —
    /// 로비부터 들고 다니는 `NetworkRoot` 프리팹과 단독 실행용 `Game.unity`. 둘 다 프리팹·씬을 다시 굽지 않고
    /// 같은 관문이 걸린다. 앱 시작 전에 구독하므로 첫 씬의 NetworkManager도 놓치지 않는다.
    ///
    /// **잠금의 기본값은 열림이다.** 대기실을 안 거치는 `Game.unity` 단독 실행(wesqa·MPPM)이 막히면 안 된다.
    /// 대기실이 서면 열고, 방장이 시작하면 닫는다(<c>RoomDirector</c>).
    ///
    /// 호스트의 비밀번호는 메모리에만 있다 — 스팀 방 데이터에는 **유무만** 적는다(누구나 읽는다).
    /// </summary>
    public static class ConnectionGate
    {
        private static string s_HostPassword = "";
        private static bool s_Accepting = true;

        /// <summary>잠금이 바뀔 때 — 스팀 방의 `SetJoinable`이 따라간다.</summary>
        public static event System.Action<bool> AcceptingChanged;

        public static bool Accepting => s_Accepting;

        public static bool HostHasPassword => LobbyPasswordLogic.HasPassword(s_HostPassword);

        /// <summary>마지막으로 **이 기기가** 관문에 거절당한 사유 코드(클라이언트 쪽). 거절이 아니면(트랜스포트 끊김 포함) 빈 값.</summary>
        public static string LastRejectReason { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Boot()
        {
            // 도메인 리로드를 끈 채 플레이하면 정적 값이 남는다 — 매번 비운다.
            s_HostPassword = "";
            s_Accepting = true;
            LastRejectReason = "";
            AcceptingChanged = null;

            NetworkManager.OnInstantiated -= Install;
            NetworkManager.OnInstantiated += Install;
        }

        private static void Install(NetworkManager _nm)
        {
            if (_nm == null || _nm.NetworkConfig == null) return;

            _nm.NetworkConfig.ConnectionApproval = true;
            // 콜백은 하나만 걸린다(NGO가 두 번째를 예외로 막는다) — 이 매니저에 이미 우리 것이 있으면 둔다.
            if (_nm.ConnectionApprovalCallback == null) _nm.ConnectionApprovalCallback = Approve;

            _nm.OnServerStopped -= OnServerStopped;
            _nm.OnServerStopped += OnServerStopped;
            _nm.OnClientDisconnectCallback -= OnClientDisconnect;
            _nm.OnClientDisconnectCallback += OnClientDisconnect;
        }

        /// <summary>호스트를 열기 **직전에** 부른다. 빈 값이면 비밀번호 없는 방.</summary>
        public static void SetHostPassword(string _raw)
        {
            s_HostPassword = LobbyPasswordLogic.Normalize(_raw);
        }

        /// <summary>참가를 시작하기 **직전에** 부른다 — 보낼 비밀번호를 싣는다.</summary>
        public static void SetClientPassword(string _raw)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.NetworkConfig.ConnectionData = LobbyPasswordLogic.ToPayload(_raw);
            LastRejectReason = "";
        }

        public static void SetAccepting(bool _accepting)
        {
            if (s_Accepting == _accepting) return;
            s_Accepting = _accepting;
            GameDebug.Log($"[Gate] 입장 {(_accepting ? "열림" : "닫힘")}");
            AcceptingChanged?.Invoke(_accepting);   // 순수 C# 이벤트 — 규칙 9 예외
        }

        private static void Approve(NetworkManager.ConnectionApprovalRequest _request,
            NetworkManager.ConnectionApprovalResponse _response)
        {
            NetworkManager nm = NetworkManager.Singleton;
            bool self = nm != null && _request.ClientNetworkId == NetworkManager.ServerClientId;
            int connected = nm != null ? nm.ConnectedClientsIds.Count : 0;

            string reason = ConnectionGateLogic.Decide(self, s_Accepting, connected,
                SteamLobbyLogic.MAX_PLAYERS, s_HostPassword, LobbyPasswordLogic.FromPayload(_request.Payload));

            _response.Approved = reason == null;
            _response.CreatePlayerObject = nm != null && nm.NetworkConfig.PlayerPrefab != null;
            _response.Pending = false;
            if (reason != null)
            {
                _response.Reason = reason;
                GameDebug.Log($"[Gate] 거절 client={_request.ClientNetworkId}: {reason}");
            }
        }

        // 호스트가 내려가면 다음 방을 위해 원래대로 — 비밀번호가 다음 방으로 새면 안 된다.
        private static void OnServerStopped(bool _wasHost)
        {
            s_HostPassword = "";
            SetAccepting(true);
        }

        private static void OnClientDisconnect(ulong _clientId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || _clientId != nm.LocalClientId) return;
            string reason = nm.DisconnectReason;
            // 관문 코드만 적는다 — 트랜스포트 끊김 문장까지 적으면 "거절당했다"로 잘못 읽힌다.
            LastRejectReason = ConnectionGateLogic.IsGateCode(reason) ? reason : "";
        }
    }
}
