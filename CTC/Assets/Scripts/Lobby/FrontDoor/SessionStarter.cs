using System.Collections.Generic;
using System.Threading;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby.Browser;
using CTC.Runtime.Networking;
using CTC.Runtime.UI.Popup;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Netcode;
using UnityEngine;
using SteamLobby = Steamworks.Data.Lobby;

namespace CTC.Runtime.Lobby.FrontDoor
{
    /// <summary>
    /// 대기실에 들어가기까지의 **접속 절차**를 맡는 Worker(규칙 6, 2026-09-14 R.E.P.O.식 앞단).
    /// 메인(Intro)과 서버 목록(Lobby) 두 씬이 같이 쓴다 — 화면은 버튼만 받고 여기를 부른다.
    ///
    /// | 절차 | 스팀 | 스팀 없음(에디터·개발 빌드) |
    /// |---|---|---|
    /// | 방 만들기 | 비밀번호 → 친구 전용 방 | 비밀번호 → 로컬 호스트 |
    /// | 공개 방 만들기 | 이름 → 공개 방 | 로컬 호스트 |
    /// | 목록 참가 | 스팀 방 참가(비밀번호 있으면 묻기) | 로컬 참가 |
    /// | 친구 방 참가 | 친구 오버레이(참가는 초대 수락으로) | 로컬 참가 → 비밀번호 거절이면 묻고 다시 |
    /// | 빠른 참가 | 가장 찬 공개 방 → 없으면 공개 방 생성 | 로컬 호스트가 있으면 참가, 없으면 호스트 |
    ///
    /// **실패하면 반드시 풀린다.** 예전 로비는 비동기 실패를 몰라 버튼이 영영 잠겼다(2026-09-13 조사 §6-1).
    /// 성공하면 풀지 않는다 — 곧 대기실로 씬이 넘어가고, 그 사이 연타를 막아야 한다.
    /// </summary>
    public class SessionStarter : MonoBehaviour
    {
        private const float CONNECT_TIMEOUT_SECONDS = 15f;
        private const string FAIL_TITLE = "연결 실패";

        private readonly ReactiveProperty<bool> m_Connecting = new ReactiveProperty<bool>(false);
        private SteamLobbyManager m_Steam;
        private bool m_PromptCancelled;

        /// <summary>접속 절차가 도는 중 — 화면이 버튼을 잠그고 「연결 중」을 띄운다.</summary>
        public ReadOnlyReactiveProperty<bool> Connecting => m_Connecting;

        /// <summary>
        /// **임시(2026-09-14, 사용자 지시): 에디터에서는 스팀이 떠 있어도 스팀 경로를 안 탄다.**
        /// 스팀 클라이언트가 켜져 있으면 호스트만 Facepunch로 열리고 MPPM 클론(UTP)이 못 붙어 3인 테스트가 깨졌다.
        /// 스팀 경로 검증은 개발용 빌드로 한다. 스팀 실측이 끝나면 이 분기를 걷는다.
        /// </summary>
        private bool SteamReady
        {
            get
            {
#if UNITY_EDITOR
                return false;
#else
                return SteamBootstrap.Ready && m_Steam != null;
#endif
            }
        }

        private void Start()
        {
            m_Steam = FindAnyObjectByType<SteamLobbyManager>();
            SteamLobbyManager.PasswordPrompt = PromptPassword;
            SteamLobbyManager.ExternalJoinStarted += OnExternalJoin;

            // 앞 화면에서 끊겨 돌아왔으면 사유를 보여 준다.
            if (FrontDoorNotice.TryConsume(out string title, out string body))
                MessagePopup.Notify(title, body).Forget();
        }

        private void OnDestroy()
        {
            if (SteamLobbyManager.PasswordPrompt == (System.Func<UniTask<string>>)PromptPassword)
                SteamLobbyManager.PasswordPrompt = null;
            SteamLobbyManager.ExternalJoinStarted -= OnExternalJoin;
            m_Connecting.Dispose();
        }

        // ── 화면이 부르는 자리 ────────────────────────────────────

        /// <summary>메인 「방 만들기」 — 비밀번호를 묻고(비우면 없음) 친구 전용 방을 연다.</summary>
        public void HostFriends() => Run(HostFriendsAsync);

        /// <summary>서버 목록 「새로 만들기」 — 이름을 받아 공개 방(비밀번호 없음).</summary>
        public void HostPublic(string _name) => Run(_ct => HostPublicAsync(_name, _ct));

        /// <summary>서버 목록에서 고른 방. 스팀 핸들이 없으면(에디터 가짜 목록) 로컬 참가.</summary>
        public void JoinListed(SteamLobby? _lobby) => Run(_ct => JoinListedAsync(_lobby, _ct));

        /// <summary>메인 「친구 방 참가」.</summary>
        public void JoinFriend() => Run(JoinFriendAsync);

        /// <summary>「공개 게임 → 빠른 참가」.</summary>
        public void QuickMatch() => Run(QuickMatchAsync);

        /// <summary>초대를 받고 켜졌으면 붙는다 — 인트로가 준비 체크를 마친 뒤 부른다.</summary>
        public bool TryJoinPendingInvite() => m_Steam != null && m_Steam.TryConsumePendingInvite();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>QA 전용 — 팝업 없이 비밀번호 방을 연다(스팀이면 친구 전용, 아니면 로컬).</summary>
        public void DevHostFriends(string _password)
        {
            Run(async _ct =>
            {
                if (SteamReady)
                    return await m_Steam.CreateLobby(SteamLobbyLogic.DefaultRoomName(SafeSteamName()), false, _password);
                return await HostLocalOrFail(_password);
            });
        }

        /// <summary>QA 전용 — 로컬 호스트에 이 비밀번호로 **한 번만** 붙어 본다. 실패하면 알림만 뜬다.</summary>
        public void DevJoinLocalOnce(string _password)
        {
            Run(async _ct =>
            {
                await DropPendingClient(_ct);
                if (!StartLocalClient(_password)) return false;
                string reason = await AwaitClientResult(_ct);
                if (reason == null) return true;
                await WaitNetworkIdle(_ct);
                return false;
            });
        }
#endif

        // ── 절차 ─────────────────────────────────────────────────

        /// <returns>대기실로 넘어가는 중이면 true(잠금 유지), 아니면 false(풀기).</returns>
        private delegate UniTask<bool> Step(CancellationToken _ct);

        private void Run(Step _step)
        {
            if (m_Connecting.Value) return;
            RunAsync(_step).Forget();
        }

        private async UniTaskVoid RunAsync(Step _step)
        {
            m_Connecting.Value = true;
            bool handedOff = false;
            try
            {
                handedOff = await _step(this.GetCancellationTokenOnDestroy());
            }
            catch (System.OperationCanceledException)
            {
                // 씬이 넘어갔다 — 할 일 없음
            }
            finally
            {
                if (this != null && !handedOff) m_Connecting.Value = false;
            }
        }

        private async UniTask<bool> HostFriendsAsync(CancellationToken _ct)
        {
            string password = await AskText(TextInputPopup.Mode.Password,
                "방 비밀번호", "친구 초대로만 들어오는 방입니다. 비워 두면 비밀번호 없이 열립니다.");
            if (password == null) return false;

            if (SteamReady)
            {
                string owner = SafeSteamName();
                if (await m_Steam.CreateLobby(SteamLobbyLogic.DefaultRoomName(owner), false, password)) return true;
                await Fail("방을 만들지 못했습니다.");
                return false;
            }
            return await HostLocalOrFail(password);
        }

        private async UniTask<bool> HostPublicAsync(string _name, CancellationToken _ct)
        {
            if (SteamReady)
            {
                if (await m_Steam.CreateLobby(_name, true, "")) return true;
                await Fail("방을 만들지 못했습니다.");
                return false;
            }
            return await HostLocalOrFail("");
        }

        private async UniTask<bool> JoinListedAsync(SteamLobby? _lobby, CancellationToken _ct)
        {
            if (SteamReady && _lobby.HasValue)
            {
                m_PromptCancelled = false;
                if (!await m_Steam.JoinLobby(_lobby.Value))
                {
                    if (!m_PromptCancelled) await Fail("방에 들어가지 못했습니다. 목록을 새로고침해 보세요.");
                    return false;
                }
                return await AwaitClientOrNotify(_ct);
            }
            return await JoinLocalWithPasswordRetry(_ct);
        }

        private async UniTask<bool> JoinFriendAsync(CancellationToken _ct)
        {
            if (SteamReady)
            {
                // 참가는 초대 수락 콜백(OnExternalJoin)으로 들어온다 — 여기선 오버레이만 연다.
                if (!SteamLobbyManager.OpenFriendsOverlay())
                    await Fail("스팀 오버레이를 열 수 없습니다. 스팀 설정에서 게임 내 오버레이를 켜 주세요.");
                return false;
            }
            return await JoinLocalWithPasswordRetry(_ct);
        }

        private async UniTask<bool> QuickMatchAsync(CancellationToken _ct)
        {
            if (SteamReady)
            {
                SteamLobby[] found = await m_Steam.RefreshLobbies();
                var rooms = new List<LobbyRoomInfo>(found.Length);
                for (int i = 0; i < found.Length; i++)
                {
                    // 비밀번호 방은 빠른 참가 후보가 아니다(공개 방엔 원래 없지만 남의 수정판을 막는다).
                    bool locked = SteamLobbyLogic.IsPasswordFlag(found[i].GetData(SteamLobbyLogic.PASSWORD_KEY));
                    rooms.Add(new LobbyRoomInfo(found[i].GetData(SteamLobbyLogic.NAME_KEY), "",
                        locked ? found[i].MaxMembers : found[i].MemberCount, found[i].MaxMembers, i));
                }

                int pick = ServerListLogic.PickQuickMatch(rooms);
                if (pick >= 0 && await m_Steam.JoinLobby(found[pick]) && await AwaitClientResult(_ct) == null)
                    return true;

                await WaitNetworkIdle(_ct);
                if (await m_Steam.CreateLobby(SteamLobbyLogic.DefaultRoomName(SafeSteamName()), true, "")) return true;
                await Fail("공개 방을 찾거나 만들지 못했습니다.");
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (HostPortFallback.IsLocalHostLikelyUp())
            {
                if (StartLocalClient("") && await AwaitClientResult(_ct) == null) return true;
                await WaitNetworkIdle(_ct);
            }
#endif
            return await HostLocalOrFail("");
        }

        // ── 외부에서 시작된 참가(초대 수락·초대받고 켜짐) ─────────────

        private void OnExternalJoin(UniTask<bool> _join)
        {
            if (m_Connecting.Value)
            {
                _join.Forget();
                return;
            }
            RunAsync(async _ct =>
            {
                m_PromptCancelled = false;
                if (!await _join)
                {
                    if (!m_PromptCancelled) await Fail("초대받은 방에 들어가지 못했습니다.");
                    return false;
                }
                return await AwaitClientOrNotify(_ct);
            }).Forget();
        }

        // ── 로컬(스팀 없음) ─────────────────────────────────────

        private async UniTask<bool> HostLocalOrFail(string _password)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.IsListening)
            {
                await Fail("네트워크가 준비되지 않았습니다.");
                return false;
            }

            ConnectionGate.SetHostPassword(_password);
            ConnectionGate.SetAccepting(false);   // 대기실이 서면 연다
            if (HostPortFallback.StartHost(nm))
            {
                GameDebug.Log($"[SessionStarter] 스팀 없음 — 로컬 호스트 (비밀번호 {(LobbyPasswordLogic.HasPassword(_password) ? "있음" : "없음")})");
                return true;
            }
            ConnectionGate.SetHostPassword("");
            ConnectionGate.SetAccepting(true);
            await Fail("방을 열지 못했습니다.");
            return false;
#else
            await Fail("스팀에 연결되지 않아 방을 열 수 없습니다.");
            return false;
#endif
        }

        private async UniTask<bool> JoinLocalWithPasswordRetry(CancellationToken _ct)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string password = "";
            while (true)
            {
                await DropPendingClient(_ct);
                if (!StartLocalClient(password))
                {
                    await Fail("방에 연결하지 못했습니다.");
                    return false;
                }

                string reason = await AwaitClientResult(_ct);
                if (reason == null) return true;

                await WaitNetworkIdle(_ct);
                if (!ConnectionGateLogic.AsksPassword(reason))
                {
                    await Fail(ConnectionGateLogic.MessageOf(reason));
                    return false;
                }

                string hint = reason == ConnectionGateLogic.PW_WRONG
                    ? "비밀번호가 틀렸습니다. 다시 입력해 주세요."
                    : "비밀번호가 필요한 방입니다.";
                password = await AskText(TextInputPopup.Mode.Password, "방 비밀번호", hint);
                if (password == null) return false;
            }
#else
            await Fail("스팀에 연결되지 않아 참가할 수 없습니다.");
            return false;
#endif
        }

        private static bool StartLocalClient(string _password)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.IsListening) return false;

            HostPortFallback.ApplyHostPortToTransport(nm);
            ConnectionGate.SetClientPassword(_password);
            return nm.StartClient();
        }

        // ── 공용 ───────────────────────────────────────────────

        /// <summary>붙었으면 null, 거절·끊김·시간 초과면 사유 코드(모르면 빈 값). 실패 시 세션은 정리된 상태다.</summary>
        private async UniTask<string> AwaitClientResult(CancellationToken _ct)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return "";

            bool connected = false;
            bool dropped = false;
            System.Action<ulong> onConnected = _id => { if (_id == nm.LocalClientId) connected = true; };
            // 클라이언트에게 오는 끊김은 자기 것뿐이다. 거절이면 아직 clientId를 못 받아 번호로는 못 거른다.
            System.Action<ulong> onDropped = _ => dropped = true;
            nm.OnClientConnectedCallback += onConnected;
            nm.OnClientDisconnectCallback += onDropped;
            try
            {
                float deadline = Time.realtimeSinceStartup + CONNECT_TIMEOUT_SECONDS;
                await UniTask.WaitUntil(() => connected || dropped || !nm.IsListening
                                              || Time.realtimeSinceStartup > deadline, cancellationToken: _ct);
            }
            finally
            {
                if (nm != null)
                {
                    nm.OnClientConnectedCallback -= onConnected;
                    nm.OnClientDisconnectCallback -= onDropped;
                }
            }

            if (connected) return null;

            string reason = !string.IsNullOrEmpty(ConnectionGate.LastRejectReason)
                ? ConnectionGate.LastRejectReason
                : nm.DisconnectReason ?? "";
            if (nm.IsListening) nm.Shutdown();   // 시간 초과 — 붙는 중인 세션을 걷는다
            if (m_Steam != null) m_Steam.LeaveLobby();
            GameDebug.LogWarning($"[SessionStarter] 접속 실패: '{reason}'");
            return reason;
        }

        private async UniTask<bool> AwaitClientOrNotify(CancellationToken _ct)
        {
            string reason = await AwaitClientResult(_ct);
            if (reason == null) return true;
            await Fail(ConnectionGateLogic.MessageOf(reason));
            return false;
        }

        /// <summary>
        /// 붙지도 못하고 두드리기만 하는 클라이언트 시도를 걷는다 — MPPM 클론의 자동 접속이 호스트가 뜨기 전부터
        /// UTP 재시도(최대 60초)를 돌고 있으면 새 참가가 IsListening에 막힌다(2026-09-14 qa_v56 ③·⑥에서 잡힘).
        /// 이미 붙었거나 호스트면 건드리지 않는다.
        /// </summary>
        private static async UniTask DropPendingClient(CancellationToken _ct)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || nm.IsServer || nm.IsConnectedClient) return;
            await WaitNetworkIdle(_ct);
        }

        /// <summary>끊긴 세션이 완전히 내려갈 때까지 — 곧바로 다시 Start하면 NGO가 거절한다.</summary>
        private static async UniTask WaitNetworkIdle(CancellationToken _ct)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (nm.IsListening && !nm.ShutdownInProgress) nm.Shutdown();
            await UniTask.WaitUntil(() => !nm.ShutdownInProgress && !nm.IsListening, cancellationToken: _ct);
            await UniTask.NextFrame(_ct);
        }

        private async UniTask<string> PromptPassword()
        {
            string password = await AskText(TextInputPopup.Mode.Password, "방 비밀번호", "비밀번호가 필요한 방입니다.");
            m_PromptCancelled = password == null;
            return password;
        }

        private static async UniTask<string> AskText(TextInputPopup.Mode _mode, string _title, string _hint)
        {
            PopupManager popups = PopupManager.Instance;
            if (popups == null) return _mode == TextInputPopup.Mode.Password ? "" : null;

            TextInputPopup popup = popups.Open<TextInputPopup>();
            if (popup == null) return _mode == TextInputPopup.Mode.Password ? "" : null;
            popup.Setup(_mode, _title, _hint);
            return await popup.Result;
        }

        private static UniTask Fail(string _body) => MessagePopup.Notify(FAIL_TITLE, _body);

        private static string SafeSteamName()
        {
            try
            {
                return Steamworks.SteamClient.Name;
            }
            catch (System.Exception)
            {
                return "";
            }
        }
    }
}
