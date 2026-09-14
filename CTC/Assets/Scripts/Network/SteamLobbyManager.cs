using Cysharp.Threading.Tasks;
using Steamworks;
using SteamLobby = Steamworks.Data.Lobby;
using Steamworks.Data;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 스팀 로비 매칭: 방 생성(친구 전용+비밀번호 / 공개) → 공개 방 검색·참여 + 오버레이 초대.
    /// 통신은 Facepunch 트랜스포트(SteamNetworkingSockets — 직결 실패 시 밸브 릴레이 자동).
    /// 스팀 경로 진입 시에만 트랜스포트 교체, 종료 시 UTP 복원 — 에디터/MPPM QA 불변.
    ///
    /// 2026-09-14 R.E.P.O.식 앞단: 생성·참여가 **결과를 돌려준다**(`UniTask&lt;bool&gt;`). 예전엔 async void라
    /// 실패해도 화면이 몰라서 버튼이 영영 잠겼다. 방은 **닫힌 채로** 만들고 대기실이 서면
    /// <see cref="ConnectionGate"/>가 연다 — 스팀 방의 `SetJoinable`이 그 잠금을 따라간다.
    /// </summary>
    public class SteamLobbyManager : MonoBehaviour
    {
        private const int LIST_MAX_RESULTS = 50;

        private SteamLobby? m_CurrentLobby;
        private bool m_Busy; // 생성/참여 진행 중 — await 도중 재진입 차단(버튼 연타·오버레이 콜백 중복)

        public bool InLobby => m_CurrentLobby.HasValue;

        /// <summary>
        /// 들어가려는 방에 비밀번호가 있을 때 묻는 자리 — 화면 쪽 Worker(<c>SessionStarter</c>)가 채운다.
        /// null을 돌려주면 취소다. 비어 있으면(묻는 화면이 없는 씬) 빈 비밀번호로 시도하고 관문이 거절한다.
        /// </summary>
        public static System.Func<UniTask<string>> PasswordPrompt;

        /// <summary>오버레이·초대로 **화면을 거치지 않고** 참가가 시작됐을 때 — 화면이 "연결 중"을 띄운다.</summary>
        public static event System.Action<UniTask<bool>> ExternalJoinStarted;

        private void OnEnable()
        {
            SteamFriends.OnGameLobbyJoinRequested += OnJoinRequested; // 친구 초대/참여 클릭
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientDisconnectCallback += OnNetworkDisconnected;
            ConnectionGate.AcceptingChanged += OnAcceptingChanged;

            // **종료는 OnDestroy가 아니라 여기서 받는다**(2026-08-20). 둘 다 DontDestroyOnLoad라
            // SteamBootstrap.OnDestroy(SteamClient.Shutdown)가 먼저 돌 수 있고, 그러면
            // Lobby.Leave()가 내부에서 NullReferenceException을 던진다(개발용 빌드 종료 로그로 확인).
            // Application.quitting은 씬 오브젝트 파괴 **전에** 발화하므로 스팀이 아직 살아 있다.
            Application.quitting -= LeaveLobby;
            Application.quitting += LeaveLobby;
        }

        private void OnDisable()
        {
            SteamFriends.OnGameLobbyJoinRequested -= OnJoinRequested;
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientDisconnectCallback -= OnNetworkDisconnected;
            ConnectionGate.AcceptingChanged -= OnAcceptingChanged;
            Application.quitting -= LeaveLobby;
        }

        /// <summary>
        /// 초대를 받고 켜진 경우 꺼내 붙는다. 인트로의 준비 체크가 끝난 뒤 **화면이 부른다** —
        /// 매니저가 서는 순간엔 스팀이 아직 안 떴을 수 있어서다(2026-09-14: 매니저가 인트로로 올라왔다).
        /// </summary>
        public bool TryConsumePendingInvite()
        {
            if (!SteamBootstrap.Ready || !SteamInvite.HasPending) return false;
            if (!SteamInvite.TryConsume(out ulong invited)) return false;

            AnnounceExternalJoin(JoinLobbyById(invited));
            return true;
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
        private static bool NetworkBusy()
        {
            NetworkManager nm = NetworkManager.Singleton;
            return nm == null || nm.IsListening || nm.IsClient || nm.IsServer;
        }

        private void OnDestroy()
        {
            LeaveLobby();
        }

        /// <summary>
        /// 호스트: 로비 생성 → 스팀 트랜스포트로 StartHost. 공개 방은 비밀번호를 받지 않는다(R.E.P.O.와 같다).
        /// 비밀번호는 <see cref="ConnectionGate"/>에만 두고, 방 데이터에는 유무만 적는다.
        /// </summary>
        public async UniTask<bool> CreateLobby(string _roomName, bool _isPublic, string _password)
        {
            if (!SteamBootstrap.Ready || InLobby || m_Busy || NetworkBusy()) return false;
            m_Busy = true;

            SteamLobby? created = await SteamMatchmaking.CreateLobbyAsync(SteamLobbyLogic.MAX_PLAYERS);
            m_Busy = false;
            if (!created.HasValue)
            {
                Debug.LogError("[SteamLobby] 로비 생성 실패");
                return false;
            }

            SteamLobby lobby = created.Value;
            string password = _isPublic ? "" : _password;
            string name = string.IsNullOrWhiteSpace(_roomName)
                ? SteamLobbyLogic.DefaultRoomName(SteamClient.Name)
                : _roomName.Trim();
            lobby.SetData(SteamLobbyLogic.GAME_TAG_KEY, SteamLobbyLogic.GAME_TAG);
            lobby.SetData(SteamLobbyLogic.NAME_KEY, name);
            lobby.SetData(SteamLobbyLogic.PASSWORD_KEY, SteamLobbyLogic.PasswordFlag(LobbyPasswordLogic.HasPassword(password)));
            lobby.SetData(SteamLobbyLogic.VERSION_KEY, ProtocolString());
            if (_isPublic) lobby.SetPublic(); else lobby.SetFriendsOnly();
            // 닫힌 채로 만든다 — 대기실이 서면 관문이 연다(OnAcceptingChanged).
            lobby.SetJoinable(false);
            m_CurrentLobby = lobby;

            ConnectionGate.SetHostPassword(password);
            ConnectionGate.SetAccepting(false);

            UseSteamTransport(0); // 호스트는 target 불필요
            if (!NetworkManager.Singleton.StartHost())
            {
                Debug.LogError("[SteamLobby] StartHost 실패 — 로비 정리");
                ConnectionGate.SetHostPassword("");
                ConnectionGate.SetAccepting(true);
                LeaveLobby();
                return false;
            }
            PublishConnect(lobby);
            Debug.Log($"[SteamLobby] 방 생성: '{name}' ({(_isPublic ? "공개" : "친구만")}, 비밀번호 {(LobbyPasswordLogic.HasPassword(password) ? "있음" : "없음")})");
            return true;
        }

        /// <summary>
        /// 공개 방 목록(우리 태그·같은 버전·빈자리 있음). 스팀은 친구 전용 방과 닫힌(`SetJoinable(false)`) 방을
        /// 검색에서 스스로 뺀다 — 그래서 매치 중인 방은 목록에 안 뜬다.
        /// </summary>
        public async UniTask<SteamLobby[]> RefreshLobbies()
        {
            if (!SteamBootstrap.Ready) return System.Array.Empty<SteamLobby>();

            SteamLobby[] found = await SteamMatchmaking.LobbyList
                .WithKeyValue(SteamLobbyLogic.GAME_TAG_KEY, SteamLobbyLogic.GAME_TAG)
                .WithKeyValue(SteamLobbyLogic.VERSION_KEY, ProtocolString())
                .WithSlotsAvailable(1)
                .FilterDistanceWorldwide()
                .WithMaxResults(LIST_MAX_RESULTS)
                .RequestAsync() ?? System.Array.Empty<SteamLobby>();
            Debug.Log($"[SteamLobby] 방 목록 {found.Length}개");
            return found;
        }

        /// <summary>클라: 로비 참여 → (비밀번호) → 방장 SteamId로 StartClient. 연결까지 기다리지는 않는다.</summary>
        public async UniTask<bool> JoinLobby(SteamLobby _lobby)
        {
            if (!SteamBootstrap.Ready || InLobby || m_Busy || NetworkBusy()) return false;
            m_Busy = true;

            RoomEnter result = await _lobby.Join();
            if (result != RoomEnter.Success)
            {
                m_Busy = false;
                Debug.LogError($"[SteamLobby] 참여 실패: {result}");
                return false;
            }
            return await EnterJoinedLobby(_lobby);
        }

        /// <summary>
        /// **방 번호만 알 때** 들어간다 — 오버레이 초대로 게임이 켜진 경우다(`+connect_lobby`).
        /// 목록에 없는 방일 수 있으므로 <see cref="JoinLobby"/>와 달리 핸들을 스팀에서 직접 받아 온다.
        /// </summary>
        public async UniTask<bool> JoinLobbyById(ulong _lobbyId)
        {
            if (!SteamBootstrap.Ready || InLobby || m_Busy || NetworkBusy()) return false;
            m_Busy = true;

            SteamLobby? joined = await SteamMatchmaking.JoinLobbyAsync(_lobbyId);
            if (!joined.HasValue)
            {
                m_Busy = false;
                Debug.LogError($"[SteamLobby] 초대받은 방에 못 들어갔다: {_lobbyId}");
                return false;
            }
            return await EnterJoinedLobby(joined.Value);
        }

        // 스팀 방에는 들어갔다 — 비밀번호를 묻고 NGO를 붙인다. m_Busy는 여기서 푼다.
        private async UniTask<bool> EnterJoinedLobby(SteamLobby _lobby)
        {
            try
            {
                string password = "";
                if (SteamLobbyLogic.IsPasswordFlag(_lobby.GetData(SteamLobbyLogic.PASSWORD_KEY)) && PasswordPrompt != null)
                {
                    password = await PasswordPrompt();
                    if (password == null)
                    {
                        Debug.Log("[SteamLobby] 비밀번호 입력 취소 — 방에서 나간다");
                        _lobby.Leave();
                        return false;
                    }
                }

                // await 사이에 다른 경로(오버레이 콜백 등)가 세션을 잡았을 수 있다 — 재확인 후 이탈
                if (NetworkBusy())
                {
                    Debug.LogWarning("[SteamLobby] 이미 세션 진행 중 — 중복 참여 취소");
                    _lobby.Leave();
                    return false;
                }
                m_CurrentLobby = _lobby;

                ConnectionGate.SetClientPassword(password);
                UseSteamTransport(_lobby.Owner.Id);
                if (!NetworkManager.Singleton.StartClient())
                {
                    Debug.LogError("[SteamLobby] StartClient 실패 — 로비 정리");
                    LeaveLobby();
                    return false;
                }
                PublishConnect(_lobby);
                Debug.Log($"[SteamLobby] 참여: {_lobby.GetData(SteamLobbyLogic.NAME_KEY)} (방장 {_lobby.Owner.Name})");
                return true;
            }
            finally
            {
                m_Busy = false;
            }
        }

        /// <summary>친구 목록 오버레이를 연다 — R.E.P.O. `Join Game`. 참가는 초대 수락 콜백으로 들어온다.</summary>
        public static bool OpenFriendsOverlay()
        {
            if (!SteamBootstrap.Ready) return false;
            try
            {
                SteamFriends.OpenOverlay("friends");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SteamLobby] 오버레이 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>대기실에서 초대 — 방 초대 오버레이.</summary>
        public void OpenInviteOverlay()
        {
            if (!SteamBootstrap.Ready || !InLobby) return;
            SteamFriends.OpenGameInviteOverlay(m_CurrentLobby.Value.Id);
        }

        // 관문이 열리고 닫힐 때 스팀 방도 같이 — 닫힌 방은 검색에도 안 뜨고 초대로도 못 들어온다.
        private void OnAcceptingChanged(bool _accepting)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (!InLobby || nm == null || !nm.IsServer || !SteamBootstrap.Ready) return;
            try
            {
                m_CurrentLobby.Value.SetJoinable(_accepting);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SteamLobby] 입장 잠금 반영 실패(무시): {e.Message}");
            }
        }

        private static string ProtocolString()
        {
            NetworkManager nm = NetworkManager.Singleton;
            ushort protocol = nm != null ? nm.NetworkConfig.ProtocolVersion : NetVersionLogic.Protocol(Application.version);
            return protocol.ToString();
        }

        /// <summary>
        /// 친구목록의 "게임 참가"를 켠다. 이 값이 없으면 친구가 우리를 볼 수는 있어도 **따라 들어올 수가 없다**.
        /// 스팀은 이 문자열을 그대로 커맨드라인으로 넘겨 주므로, 꺼진 게임을 켜는 경로와 규약이 같다.
        /// </summary>
        private static void PublishConnect(SteamLobby _lobby)
        {
            if (!SteamBootstrap.Ready) return;

            try
            {
                SteamFriends.SetRichPresence(
                    SteamInviteLogic.RICH_PRESENCE_CONNECT_KEY,
                    SteamInviteLogic.ConnectValue(_lobby.Id.Value));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SteamLobby] 리치 프레즌스 실패(무시): {e.Message}");
            }
        }

        /// <summary>연결 종료 시 로비 이탈 + UTP 복원 (Disconnect 버튼/파괴 공용).</summary>
        public void LeaveLobby()
        {
            if (!InLobby) return;

            // 스팀이 이미 내려갔으면 이탈을 시도하지 않는다 — Facepunch의 Lobby.Leave()는
            // SteamClient.Shutdown() 뒤에 부르면 내부에서 NRE를 던진다. 프로세스가 죽으면
            // 스팀이 알아서 방에서 빼 주므로 여기서 못 나가도 유령 방은 안 남는다.
            if (SteamBootstrap.Ready)
            {
                try { m_CurrentLobby.Value.Leave(); }
                catch (System.Exception e) { Debug.LogWarning($"[SteamLobby] 이탈 실패(무시): {e.Message}"); }
            }

            m_CurrentLobby = null;
            UseLocalTransport();

            // 안 지우면 친구 목록에 "참가"가 남아 **이미 없는 방**으로 사람을 보낸다.
            if (SteamBootstrap.Ready)
            {
                try { SteamFriends.ClearRichPresence(); }
                catch (System.Exception e) { Debug.LogWarning($"[SteamLobby] 프레즌스 정리 실패(무시): {e.Message}"); }
            }

            Debug.Log("[SteamLobby] 로비 이탈 — UTP 복원");
        }

        // 친구 초대 수락/오버레이 참여 클릭 콜백
        private void OnJoinRequested(SteamLobby _lobby, SteamId _friendId)
        {
            AnnounceExternalJoin(JoinLobby(_lobby));
        }

        // UniTask는 **한 번만** 기다릴 수 있다 — 받을 화면이 없으면 여기서 흘려보낸다.
        private static void AnnounceExternalJoin(UniTask<bool> _join)
        {
            if (ExternalJoinStarted != null) ExternalJoinStarted.Invoke(_join);
            else _join.Forget();
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
