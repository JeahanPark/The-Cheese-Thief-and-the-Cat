using CTC.Runtime.Core;
using CTC.Runtime.Lobby.FrontDoor;
using CTC.Runtime.UI.Popup;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CTC.Runtime.Intro
{
    /// <summary>
    /// 메인 메뉴의 입력만 받는다(CTC 규칙 5) — 버튼은 R3 구독, 핸들러 이름은 OnClick~(규칙 2·4).
    ///
    /// 2026-09-14 R.E.P.O.식 앞단: 「게임 시작」이 셋으로 갈렸다 —
    /// **방 만들기**(비밀번호 → 친구 전용 방) · **공개 게임**(빠른 참가 / 서버 목록) · **친구 방 참가**(스팀 오버레이).
    /// 접속 절차는 <see cref="SessionStarter"/>가 맡고, 대기실로 넘기는 건 <c>RoomEntry</c>다.
    /// 서버 목록만 별도 씬(Lobby)이라 일반 SceneManager로 넘어간다 — 그 씬은 아직 네트워크가 없다.
    /// </summary>
    public class IntroController : MonoBehaviour
    {
        [SerializeField] private IntroDirector m_Director;
        [SerializeField] private SessionStarter m_Starter;
        [SerializeField] private Button m_HostButton;
        [SerializeField] private Button m_PublicButton;
        [SerializeField] private Button m_FriendButton;
        [SerializeField] private Button m_RetryButton;
        [SerializeField] private Button m_QuitButton;

        /// <summary>「공개 게임 → 서버 목록」이 여는 씬.</summary>
        [SerializeField] private string m_ServerListSceneName = "Lobby";

        // 설정(2026-09-01) — 인게임과 **같은 OptionsPopup**을 쓴다. UI 컨셉이 갈라지면
        // 같은 설정이 화면마다 다르게 보인다. 팝업이 없으면 버튼은 잠긴 채로 남는다.
        [SerializeField] private Button m_OptionsButton;

        /// <summary>초대 자동 진입이 준비 체크를 기다리는 상한(초).</summary>
        private const float AUTO_START_TIMEOUT_SECONDS = 15f;
        private const int AUTO_START_POLL_MS = 200;

        private bool m_Leaving;

        private bool Ready => m_Director == null || m_Director.CanStart;

        private bool Locked => m_Leaving || (m_Starter != null && m_Starter.Connecting.CurrentValue);

        private void Awake()
        {
            if (m_OptionsButton != null)
                m_OptionsButton.OnClickAsObservable().Subscribe(_ => OnClickOptions()).AddTo(this);

            if (m_HostButton != null)
                m_HostButton.OnClickAsObservable().Subscribe(_ => OnClickHost()).AddTo(this);

            if (m_PublicButton != null)
                m_PublicButton.OnClickAsObservable().Subscribe(_ => OnClickPublic()).AddTo(this);

            if (m_FriendButton != null)
                m_FriendButton.OnClickAsObservable().Subscribe(_ => OnClickFriend()).AddTo(this);

            if (m_RetryButton != null)
                m_RetryButton.OnClickAsObservable().Subscribe(_ => OnClickRetry()).AddTo(this);

            if (m_QuitButton != null)
                m_QuitButton.OnClickAsObservable().Subscribe(_ => OnClickQuit()).AddTo(this);
        }

        /// <summary>
        /// 초대를 받고 켜졌으면 **메뉴를 건너뛰고 바로 붙는다**(2026-08-30). 친구가 부른 사람에게
        /// 버튼을 한 번 더 누르라고 하는 화면은 초대가 반쯤 실패한 것과 같다.
        ///
        /// 준비 체크는 그대로 기다린다 — 스팀이 안 떠 있으면 초대 자체가 성립하지 않는다.
        /// </summary>
        private void Start()
        {
            // 대기실에서 돌아왔으면 커서가 잠겨 있을 수 있다 — 메뉴는 눌러야 한다.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (!Networking.SteamInvite.HasPending) return;

            GameDebug.Log("[Intro] 초대를 받고 켜졌다 — 메뉴를 건너뛴다");
            AutoJoinInvite().Forget();
        }

        private async UniTask AutoJoinInvite()
        {
            var cancel = this.GetCancellationTokenOnDestroy();
            float deadline = Time.time + AUTO_START_TIMEOUT_SECONDS;

            while (Time.time < deadline)
            {
                if (Ready) break;
                if (m_Director.HasFatalFailure) return;   // 막혔으면 사유를 보여 준 채로 선다

                await UniTask.Delay(AUTO_START_POLL_MS, cancellationToken: cancel);
            }

            if (m_Starter != null) m_Starter.TryJoinPendingInvite();
        }

        /// <summary>설정 창을 연다 — 인게임·대기실과 같은 창이다.</summary>
        private void OnClickOptions()
        {
            UI.Popup.PopupManager popups = UI.Popup.PopupManager.Instance;
            if (popups == null) return;
            popups.Open<UI.OptionsPopup>();
        }

        private bool CanAct()
        {
            if (Locked || m_Starter == null) return false;
            if (!Ready)
            {
                GameDebug.LogWarning("[Intro] 아직 준비가 안 끝났다 — 무시");
                return false;
            }
            return true;
        }

        private void OnClickHost()
        {
            if (!CanAct()) return;
            m_Starter.HostFriends();
        }

        private void OnClickFriend()
        {
            if (!CanAct()) return;
            m_Starter.JoinFriend();
        }

        private void OnClickPublic()
        {
            if (!CanAct()) return;
            ChoosePublic().Forget();
        }

        /// <summary>R.E.P.O. `PublicGameChoice` — 빠른 참가냐, 목록을 볼 거냐.</summary>
        private async UniTaskVoid ChoosePublic()
        {
            UI.Popup.PopupManager popups = UI.Popup.PopupManager.Instance;
            MessagePopup popup = popups != null ? popups.Open<MessagePopup>() : null;
            if (popup == null) return;

            popup.Setup("공개 게임", "아무 방에나 바로 들어가거나, 방 목록에서 골라 들어갑니다.", "빠른 참가", "서버 목록");
            int choice = await popup.Result;
            if (this == null || Locked) return;

            if (choice == MessagePopup.PRIMARY)
            {
                m_Starter.QuickMatch();
            }
            else if (choice == MessagePopup.SECONDARY && !string.IsNullOrEmpty(m_ServerListSceneName))
            {
                m_Leaving = true;
                SceneManager.LoadScene(m_ServerListSceneName);
            }
        }

        private void OnClickRetry()
        {
            if (m_Leaving) return;
            if (m_Director == null) return;

            GameDebug.Log("[Intro] 다시 시도");
            m_Director.Retry();
        }

        private void OnClickQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
