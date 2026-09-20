using System.Collections.Generic;
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
    ///
    /// **UTP 복원은 NGO가 완전히 내려간 뒤에 한다**(2026-09-20). `NetworkManager.Shutdown()`은 플래그만 세우고
    /// 실제 정리(`ShutdownInternal` → `transport.Shutdown()`)는 **다음 네트워크 업데이트**에서 돈다. 그 사이에
    /// `NetworkConfig.NetworkTransport`를 UTP로 바꿔 놓으면 NGO는 그 순간의 트랜스포트, 즉 **UTP를 내리고 Facepunch는
    /// 손대지 않는다** — 릴레이 소켓이 안 닫히고 `OnTransportEvent` 구독이 남는다. 그래서 (1) 다음 스팀 세션부터
    /// 같은 이벤트가 두 번씩 들어가 참가가 깨지고, (2) 다시 호스트하면 리슨 소켓 생성이 「이미 있다」로 실패해 친구가
    /// 두 번째 방에 못 들어오고, (3) 방장이 나가도 손님은 끊기지 않고 대기실에 방치됐다. 「방 만들기 → 나가기 →
    /// 빠른 참가 → 에러」의 원인. 지금은 <see cref="OnNetworkStopped"/>(NGO `OnClientStopped`/`OnServerStopped`)가 복원한다.
    /// </summary>
    public class SteamLobbyManager : MonoBehaviour
    {
        private const int LIST_MAX_RESULTS = 50;

        private SteamLobby? m_CurrentLobby;
        private bool m_Busy; // 생성/참여 진행 중 — await 도중 재진입 차단(버튼 연타·오버레이 콜백 중복)
        private NetworkManager m_Net; // 구독한 매니저 — OnDisable에서 같은 것을 푼다

        // 방금 나온 내 방 — 스팀 백엔드가 빈 방을 지우기 전에 목록에 잠깐 남는다(SteamLobbyLogic.IsRecentlyLeft).
        private ulong m_LastLeftLobbyId;
        private float m_LastLeftAt = float.NegativeInfinity;

        public bool InLobby => m_CurrentLobby.HasValue;

        /// <summary>마지막 목록 요청에서 **다른 프로토콜 버전**이라 숨긴 방 수 — 화면이 「왜 비었나」를 설명하는 데 쓴다.</summary>
        public int HiddenByVersion { get; private set; }

        /// <summary>
        /// 들어가려는 방에 비밀번호가 있을 때 묻는 자리 — 화면 쪽 Worker(<c>SessionStarter</c>)가 채운다.
        /// null을 돌려주면 취소다. 비어 있으면(묻는 화면이 없는 씬) 빈 비밀번호로 시도하고 관문이 거절한다.
        /// </summary>
        public static System.Func<UniTask<string>> PasswordPrompt;

        /// <summary>오버레이·초대로 **화면을 거치지 않고** 참가가 시작됐을 때 — 화면이 "연결 중"을 띄운다.</summary>
        public static event System.Action<UniTask<bool>> ExternalJoinStarted;

        private void OnEnable()
        {
            SteamFriends.OnGameLobbyJoinRequested += OnJoinRequested;              // 초대 수락 · 로비 기준 「게임 참가」
            SteamFriends.OnGameRichPresenceJoinRequested += OnRichPresenceJoinRequested; // 켜진 게임에서의 친구 목록 「게임 참가」

            // 같은 프리팹에 NetworkManager가 동승한다 — Singleton이 아직 안 섰으면 옆의 것을 잡는다.
            m_Net = NetworkManager.Singleton;
            if (m_Net == null) m_Net = GetComponent<NetworkManager>();
            if (m_Net != null)
            {
                m_Net.OnClientDisconnectCallback += OnNetworkDisconnected;
                m_Net.OnClientStopped += OnNetworkStopped;
                m_Net.OnServerStopped += OnNetworkStopped;
            }
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
            SteamFriends.OnGameRichPresenceJoinRequested -= OnRichPresenceJoinRequested;
            if (m_Net != null)
            {
                m_Net.OnClientDisconnectCallback -= OnNetworkDisconnected;
                m_Net.OnClientStopped -= OnNetworkStopped;
                m_Net.OnServerStopped -= OnNetworkStopped;
            }
            m_Net = null;
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

        /// <summary>
        /// NGO가 **다 내려갔다**(`ShutdownInternal` 끝 — 트랜스포트 Shutdown·구독 해제가 끝난 뒤). 이제 트랜스포트를 바꿔도 안전하다.
        /// 세션이 먼저 죽었는데(트랜스포트 실패·강제 종료) 아무도 방을 안 나갔으면 여기서 나간다 — 안 나가면
        /// <see cref="InLobby"/>가 남아 다음 방 만들기가 영영 거절된다.
        /// </summary>
        private void OnNetworkStopped(bool _wasHostOrClient)
        {
            if (InLobby)
            {
                Debug.LogWarning("[SteamLobby] NGO가 먼저 내려갔다 — 방을 정리한다");
                LeaveLobby();
                return;
            }
            UseLocalTransport();
        }

        // 이미 NGO 세션이 살아 있거나 내려가는 중이면 새 접속 금지(중복 연결 = 로비 인원 유령 항목 원인).
        // ShutdownInProgress도 본다 — 그 한 프레임에 StartHost/StartClient를 부르면 NGO가 "이미 시작됨"으로 거절한다.
        private static bool NetworkBusy()
        {
            NetworkManager nm = NetworkManager.Singleton;
            return nm == null || nm.IsListening || nm.IsClient || nm.IsServer || nm.ShutdownInProgress;
        }

        private void OnDestroy()
        {
            LeaveLobby();
        }

        /// <summary>
        /// 호스트: 로비 생성 → 스팀 트랜스포트로 StartHost. 공개 방은 비밀번호를 받지 않는다(R.E.P.O.와 같다).
        /// 비밀번호는 <see cref="ConnectionGate"/>에만 두고, 방 데이터에는 유무만 적는다.
        ///
        /// **친구 전용 방은 서버 목록에 안 뜬다** — 스팀 문서: `RequestLobbyList`는 Public·Invisible이면서 joinable인 방만 준다.
        /// 친구는 초대나 친구 목록 「게임 참가」로 들어온다(<see cref="PublishConnect"/>).
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
            lobby.SetData(SteamLobbyLogic.HOST_KEY, SafeSteamName());
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
            Debug.Log($"[SteamLobby] 방 생성: '{name}' ({(_isPublic ? "공개" : "친구만")}, 비밀번호 {(LobbyPasswordLogic.HasPassword(password) ? "있음" : "없음")}, id={lobby.Id.Value}, ver={ProtocolString()})");
            return true;
        }

        /// <summary>
        /// 공개 방 목록(우리 태그·같은 버전·빈자리 있음). 스팀은 친구 전용 방과 닫힌(`SetJoinable(false)`) 방을
        /// 검색에서 스스로 뺀다 — 그래서 매치 중인 방은 목록에 안 뜬다.
        ///
        /// 버전은 **여기서** 거른다(2026-09-20). 서버 필터(`WithKeyValue(ver)`)로 걸면 「다른 버전 방이 있다」는 사실 자체가
        /// 안 보여, 빌드가 어긋난 두 사람은 목록이 왜 비었는지 알 길이 없었다. 숨긴 수는 <see cref="HiddenByVersion"/>.
        /// </summary>
        public async UniTask<SteamLobby[]> RefreshLobbies()
        {
            if (!SteamBootstrap.Ready) return System.Array.Empty<SteamLobby>();

            SteamLobby[] found = await SteamMatchmaking.LobbyList
                .WithKeyValue(SteamLobbyLogic.GAME_TAG_KEY, SteamLobbyLogic.GAME_TAG)
                .WithSlotsAvailable(1)
                .FilterDistanceWorldwide()
                .WithMaxResults(LIST_MAX_RESULTS)
                .RequestAsync() ?? System.Array.Empty<SteamLobby>();

            string version = ProtocolString();
            float now = Time.realtimeSinceStartup;
            int otherVersion = 0;
            var list = new List<SteamLobby>(found.Length);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i].GetData(SteamLobbyLogic.VERSION_KEY) != version)
                {
                    otherVersion++;
                    continue;
                }
                // 방금 내가 나온 방 — 스팀이 아직 안 지웠을 뿐이다. 들어가 봐야 DoesntExist다.
                if (SteamLobbyLogic.IsRecentlyLeft(found[i].Id.Value, m_LastLeftLobbyId, m_LastLeftAt, now)) continue;
                list.Add(found[i]);
            }
            HiddenByVersion = otherVersion;

            Debug.Log($"[SteamLobby] 방 목록 {list.Count}개 (다른 버전 {otherVersion}개 숨김, ver={version})");
            return list.ToArray();
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
        /// **방 번호만 알 때** 들어간다 — 오버레이 초대로 게임이 켜진 경우(`+connect_lobby`)와 켜진 게임에서의
        /// 친구 목록 「게임 참가」(리치 프레즌스)다. 목록에 없는 방일 수 있으므로 <see cref="JoinLobby"/>와 달리
        /// 핸들을 스팀에서 직접 받아 온다.
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
                // 들어간 뒤라 Owner를 읽을 수 있다(스팀: 방의 멤버여야 GetLobbyOwner가 유효하다).
                UseSteamTransport(_lobby.Owner.Id);
                if (!NetworkManager.Singleton.StartClient())
                {
                    Debug.LogError("[SteamLobby] StartClient 실패 — 로비 정리");
                    LeaveLobby();
                    return false;
                }
                PublishConnect(_lobby);
                Debug.Log($"[SteamLobby] 참여: {_lobby.GetData(SteamLobbyLogic.NAME_KEY)} (방장 {_lobby.Owner.Name}, id={_lobby.Id.Value})");
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
                bool ok = m_CurrentLobby.Value.SetJoinable(_accepting);
                Debug.Log($"[SteamLobby] 입장 {(_accepting ? "열림" : "닫힘")} → SetJoinable({_accepting}) = {ok}");
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

        private static string SafeSteamName()
        {
            try
            {
                return SteamClient.Name;
            }
            catch (System.Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// 친구목록의 "게임 참가"를 켠다. 이 값이 없으면 친구가 우리를 볼 수는 있어도 **따라 들어올 수가 없다**.
        /// 스팀은 이 문자열을 그대로 커맨드라인으로 넘겨 주므로, 꺼진 게임을 켜는 경로와 규약이 같다.
        /// 켜진 게임에는 <see cref="OnRichPresenceJoinRequested"/>로 같은 문자열이 온다.
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

        /// <summary>
        /// 스팀 방에서 나간다(Disconnect 버튼/파괴/끊김 공용). **NGO를 먼저 `Shutdown()`하고 부른다.**
        /// 방 이탈·프레즌스 정리는 즉시 하고, UTP 복원은 NGO가 다 내려간 뒤(<see cref="OnNetworkStopped"/>)에 한다 —
        /// 지금 바꾸면 NGO가 엉뚱한 트랜스포트를 내린다(클래스 주석). NGO가 안 돌고 있으면 바로 복원한다.
        /// </summary>
        public void LeaveLobby()
        {
            if (!InLobby) return;

            SteamLobby lobby = m_CurrentLobby.Value;
            m_CurrentLobby = null;
            m_LastLeftLobbyId = lobby.Id.Value;
            m_LastLeftAt = Time.realtimeSinceStartup;

            // 스팀이 이미 내려갔으면 이탈을 시도하지 않는다 — Facepunch의 Lobby.Leave()는
            // SteamClient.Shutdown() 뒤에 부르면 내부에서 NRE를 던진다. 프로세스가 죽으면
            // 스팀이 알아서 방에서 빼 주므로 여기서 못 나가도 유령 방은 안 남는다.
            if (SteamBootstrap.Ready)
            {
                try { lobby.Leave(); }
                catch (System.Exception e) { Debug.LogWarning($"[SteamLobby] 이탈 실패(무시): {e.Message}"); }

                // 안 지우면 친구 목록에 "참가"가 남아 **이미 없는 방**으로 사람을 보낸다.
                try { SteamFriends.ClearRichPresence(); }
                catch (System.Exception e) { Debug.LogWarning($"[SteamLobby] 프레즌스 정리 실패(무시): {e.Message}"); }
            }

            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsListening || nm.ShutdownInProgress))
            {
                if (!nm.ShutdownInProgress)
                {
                    // 세션을 안 내리고 방만 나가는 호출자는 없어야 한다 — 있으면 여기서 내린다(스팀 세션은 끝났다).
                    Debug.LogWarning("[SteamLobby] 세션이 살아 있는 채로 방을 나간다 — NGO를 내린다");
                    nm.Shutdown();
                }
                Debug.Log("[SteamLobby] 로비 이탈 — NGO가 내려가면 UTP 복원");
                return;
            }

            UseLocalTransport();
            Debug.Log("[SteamLobby] 로비 이탈 — UTP 복원");
        }

        // 친구 초대 수락/오버레이 참여 클릭 콜백(로비 기준)
        private void OnJoinRequested(SteamLobby _lobby, SteamId _friendId)
        {
            AnnounceExternalJoin(JoinLobby(_lobby));
        }

        // 켜진 게임에서 친구 목록 「게임 참가」를 눌렀다 — 리치 프레즌스 connect 값이 그대로 온다(2026-09-20).
        // connect 키를 적어 두면 스팀은 이쪽 콜백을 쓴다. 로비 콜백만 받던 동안 이 길이 조용히 죽어 있었다.
        private void OnRichPresenceJoinRequested(Friend _friend, string _connect)
        {
            if (!SteamInviteLogic.TryParseConnectString(_connect, out ulong lobbyId))
            {
                Debug.LogWarning($"[SteamLobby] 친구 목록 참가 문자열을 못 읽었다: '{_connect}'");
                return;
            }

            Debug.Log($"[SteamLobby] 친구 목록 참가(리치 프레즌스) → 방 {lobbyId} (from {_friend.Name})");
            AnnounceExternalJoin(JoinLobbyById(lobbyId));
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
