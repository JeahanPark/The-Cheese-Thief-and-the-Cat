using Steamworks;
using SteamLobby = Steamworks.Data.Lobby;
using Steamworks.Data;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 스팀 로비 매칭(멧챠 카멜레온식): 방 생성(공개/친구만) → 공개 방 목록 참여 + 오버레이 초대.
    /// 통신은 Facepunch 트랜스포트(SteamNetworkingSockets — 직결 실패 시 밸브 릴레이 자동).
    /// 스팀 경로 진입 시에만 트랜스포트 교체, 종료 시 UTP 복원 — 에디터/MPPM QA 불변.
    /// </summary>
    public class SteamLobbyManager : MonoBehaviour
    {
        private SteamLobby? m_CurrentLobby;
        private bool m_Busy; // 생성/참여 진행 중 — await 도중 재진입 차단(버튼 연타·오버레이 콜백 중복)

        public bool InLobby => m_CurrentLobby.HasValue;

        private void OnEnable()
        {
            SteamFriends.OnGameLobbyJoinRequested += OnJoinRequested; // 친구 초대/참여 클릭
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientDisconnectCallback += OnNetworkDisconnected;
        }

        private void OnDisable()
        {
            SteamFriends.OnGameLobbyJoinRequested -= OnJoinRequested;
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientDisconnectCallback -= OnNetworkDisconnected;
        }

        // 접속 실패·강제 종료 시 로비까지 정리 — 유령 세션으로 재시도가 막히는 것 방지
        private void OnNetworkDisconnected(ulong _clientId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || nm.LocalClientId != _clientId) return;

            Debug.LogWarning("[SteamLobby] 연결 종료 감지 — 세션 정리");
            nm.Shutdown();
            LeaveLobby();
        }

        // 이미 NGO 세션이 살아 있으면 새 접속 금지(중복 연결 = 로비 인원 유령 항목 원인)
        private bool NetworkBusy()
        {
            NetworkManager nm = NetworkManager.Singleton;
            return nm == null || nm.IsListening || nm.IsClient || nm.IsServer;
        }

        private void OnDestroy()
        {
            LeaveLobby();
        }

        /// <summary>호스트: 로비 생성 → 스팀 트랜스포트로 StartHost.</summary>
        public async void CreateLobby(string _roomName, bool _isPublic)
        {
            if (!SteamBootstrap.Ready || InLobby || m_Busy || NetworkBusy()) return;
            m_Busy = true;

            SteamLobby? created = await Steamworks.SteamMatchmaking.CreateLobbyAsync(SteamLobbyLogic.MAX_PLAYERS);
            m_Busy = false;
            if (!created.HasValue)
            {
                Debug.LogError("[SteamLobby] 로비 생성 실패");
                return;
            }

            SteamLobby lobby = created.Value;
            string name = string.IsNullOrWhiteSpace(_roomName)
                ? SteamLobbyLogic.DefaultRoomName(SteamClient.Name)
                : _roomName.Trim();
            lobby.SetData(SteamLobbyLogic.GAME_TAG_KEY, SteamLobbyLogic.GAME_TAG);
            lobby.SetData(SteamLobbyLogic.NAME_KEY, name);
            if (_isPublic) lobby.SetPublic(); else lobby.SetFriendsOnly();
            lobby.SetJoinable(true);
            m_CurrentLobby = lobby;

            UseSteamTransport(0); // 호스트는 target 불필요
            if (!NetworkManager.Singleton.StartHost())
            {
                Debug.LogError("[SteamLobby] StartHost 실패 — 로비 정리");
                LeaveLobby();
                return;
            }
            Debug.Log($"[SteamLobby] 방 생성: '{name}' ({(_isPublic ? "공개" : "친구만")})");
        }

        /// <summary>공개 방 목록(우리 태그만). 콜백으로 결과 전달.</summary>
        public async void RefreshLobbies(System.Action<SteamLobby[]> _onDone)
        {
            if (!SteamBootstrap.Ready) { _onDone(System.Array.Empty<SteamLobby>()); return; }

            SteamLobby[] found = await SteamMatchmaking.LobbyList
                .WithKeyValue(SteamLobbyLogic.GAME_TAG_KEY, SteamLobbyLogic.GAME_TAG)
                .WithMaxResults(20)
                .RequestAsync() ?? System.Array.Empty<SteamLobby>();
            Debug.Log($"[SteamLobby] 방 목록 {found.Length}개");
            _onDone(found);
        }

        /// <summary>클라: 로비 참여 → 방장 SteamId로 StartClient.</summary>
        public async void JoinLobby(SteamLobby _lobby)
        {
            if (!SteamBootstrap.Ready || InLobby || m_Busy || NetworkBusy()) return;
            m_Busy = true;

            RoomEnter result = await _lobby.Join();
            m_Busy = false;
            if (result != RoomEnter.Success)
            {
                Debug.LogError($"[SteamLobby] 참여 실패: {result}");
                return;
            }
            // await 사이에 다른 경로(오버레이 콜백 등)가 세션을 잡았을 수 있다 — 재확인 후 이탈
            if (NetworkBusy())
            {
                Debug.LogWarning("[SteamLobby] 이미 세션 진행 중 — 중복 참여 취소");
                _lobby.Leave();
                return;
            }
            m_CurrentLobby = _lobby;

            UseSteamTransport(_lobby.Owner.Id);
            if (!NetworkManager.Singleton.StartClient())
            {
                Debug.LogError("[SteamLobby] StartClient 실패 — 로비 정리");
                LeaveLobby();
                return;
            }
            Debug.Log($"[SteamLobby] 참여: {_lobby.GetData(SteamLobbyLogic.NAME_KEY)} (방장 {_lobby.Owner.Name})");
        }

        /// <summary>연결 종료 시 로비 이탈 + UTP 복원 (Disconnect 버튼/파괴 공용).</summary>
        public void LeaveLobby()
        {
            if (!InLobby) return;
            m_CurrentLobby.Value.Leave();
            m_CurrentLobby = null;
            UseLocalTransport();
            Debug.Log("[SteamLobby] 로비 이탈 — UTP 복원");
        }

        // 친구 초대 수락/오버레이 참여 클릭 콜백
        private void OnJoinRequested(SteamLobby _lobby, SteamId _friendId)
        {
            JoinLobby(_lobby);
        }

        private static void UseSteamTransport(ulong _targetSteamId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            var steam = nm.GetComponent<Netcode.Transports.Facepunch.FacepunchTransport>();
            steam.targetSteamId = _targetSteamId;
            nm.NetworkConfig.NetworkTransport = steam;
        }

        private static void UseLocalTransport()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;
            var utp = nm.GetComponent<UnityTransport>();
            if (utp != null) nm.NetworkConfig.NetworkTransport = utp;
        }
    }
}
