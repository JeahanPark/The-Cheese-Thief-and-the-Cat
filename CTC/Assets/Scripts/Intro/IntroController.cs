using CTC.Runtime.Core;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CTC.Runtime.Intro
{
    /// <summary>
    /// 인트로의 입력만 받는다(CTC 규칙 5) — 버튼은 R3 구독, 핸들러 이름은 OnClick~(규칙 2·4).
    ///
    /// 여기서 넘어가는 씬은 **아직 로비가 아니다.** Lobby.unity가 서기 전까지는 Game으로 간다 —
    /// 씬 이름을 인스펙터에 남겨 두었으니 로비가 생기면 그것만 바꾸면 된다.
    /// 인트로는 네트워크가 없는 씬이라 NGO가 아니라 일반 SceneManager로 넘어간다.
    /// </summary>
    public class IntroController : MonoBehaviour
    {
        [SerializeField] private IntroDirector m_Director;
        [SerializeField] private Button m_StartButton;
        [SerializeField] private Button m_RetryButton;
        [SerializeField] private Button m_QuitButton;

        /// <summary>로비가 서면 "Lobby"로 바꾼다.</summary>
        [SerializeField] private string m_NextSceneName = "Lobby";

        // 설정(2026-09-01) — 인게임과 **같은 OptionsPopup**을 쓴다. UI 컨셉이 갈라지면
        // 같은 설정이 화면마다 다르게 보인다. 팝업이 없으면 버튼은 잠긴 채로 남는다.
        [SerializeField] private Button m_OptionsButton;

        /// <summary>초대 자동 진입이 준비 체크를 기다리는 상한(초).</summary>
        private const float AUTO_START_TIMEOUT_SECONDS = 15f;
        private const int AUTO_START_POLL_MS = 200;

        private bool m_Leaving;

        private void Awake()
        {
            if (m_OptionsButton != null)
                m_OptionsButton.OnClickAsObservable().Subscribe(_ => OnClickOptions()).AddTo(this);

            if (m_StartButton != null)
                m_StartButton.OnClickAsObservable().Subscribe(_ => OnClickStart()).AddTo(this);

            if (m_RetryButton != null)
                m_RetryButton.OnClickAsObservable().Subscribe(_ => OnClickRetry()).AddTo(this);

            if (m_QuitButton != null)
                m_QuitButton.OnClickAsObservable().Subscribe(_ => OnClickQuit()).AddTo(this);
        }

        /// <summary>
        /// 초대를 받고 켜졌으면 **메뉴를 건너뛴다**(2026-08-30). 친구가 부른 사람에게
        /// "게임 시작"을 한 번 더 누르라고 하는 화면은 초대가 반쯤 실패한 것과 같다.
        ///
        /// 준비 체크는 그대로 기다린다 — 스팀이 안 떠 있으면 초대 자체가 성립하지 않는다.
        /// </summary>
        private void Start()
        {
            if (!Networking.SteamInvite.HasPending) return;

            GameDebug.Log("[Intro] 초대를 받고 켜졌다 — 메뉴를 건너뛴다");
            AutoStart().Forget();
        }

        private async UniTask AutoStart()
        {
            var cancel = this.GetCancellationTokenOnDestroy();
            float deadline = Time.time + AUTO_START_TIMEOUT_SECONDS;

            while (Time.time < deadline)
            {
                if (m_Director == null || m_Director.CanStart) break;
                if (m_Director.HasFatalFailure) return;   // 막혔으면 사유를 보여 준 채로 선다

                await UniTask.Delay(AUTO_START_POLL_MS, cancellationToken: cancel);
            }

            OnClickStart();
        }

        /// <summary>설정 창을 연다 — 인게임·대기실과 같은 창이다.</summary>
        private void OnClickOptions()
        {
            UI.Popup.PopupManager popups = UI.Popup.PopupManager.Instance;
            if (popups == null) return;
            popups.Open<UI.OptionsPopup>();
        }

        private void OnClickStart()
        {
            if (m_Leaving) return;
            if (m_Director != null && !m_Director.CanStart)
            {
                GameDebug.LogWarning("[Intro] 아직 준비가 안 끝났다 — 시작 무시");
                return;
            }

            if (string.IsNullOrEmpty(m_NextSceneName))
            {
                GameDebug.LogError("[Intro] 다음 씬 이름이 비었다");
                return;
            }

            m_Leaving = true;
            GameDebug.Log($"[Intro] 시작 → {m_NextSceneName}");
            SceneManager.LoadScene(m_NextSceneName);
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
