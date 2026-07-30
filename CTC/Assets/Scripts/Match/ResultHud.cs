using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 매치 결과 패널: 승자 텍스트 + 호스트 전용 Restart 버튼.
    /// 표시 중 커서를 해제한다 — LobbyUI의 인게임 강제 잠금은 IsShowing으로 양보받는다.
    /// </summary>
    public class ResultHud : MonoBehaviour
    {
        /// <summary>결과 패널이 커서를 점유 중인지 — LobbyUI가 잠금 강제 전 확인.</summary>
        public static bool IsShowing { get; private set; }

        private const int RESTART_THROTTLE_MS = 400;

        [SerializeField] private MatchManager m_Match;
        [SerializeField] private GameObject m_Panel;
        [SerializeField] private TMP_Text m_Text;
        [SerializeField] private Button m_RestartButton;

        private void Awake()
        {
            // 버튼 연결은 R3 (CTC 규칙 2·4) — 재시작은 연타가 잦아 ThrottleFirst로 한 번만 통과시킨다
            m_RestartButton.OnClickAsObservable()
                .ThrottleFirst(System.TimeSpan.FromMilliseconds(RESTART_THROTTLE_MS))
                .Subscribe(_ => OnClickRestart()).AddTo(this);
            m_Panel.SetActive(false);
            IsShowing = false;
        }

        private void OnClickRestart() => m_Match.RequestRestart();

        private void OnDestroy()
        {
            IsShowing = false;
        }

        // 개편 3b(2026-07-31): Update 폴링 → 결과 스트림 구독. 결과는 판당 한 번만 바뀐다.
        private void Start()
        {
            m_Match.ResultChanged.Subscribe(OnResultChanged).AddTo(this);
        }

        private void OnResultChanged(MatchResult _result)
        {
            bool show = _result != MatchResult.None;
            if (m_Panel.activeSelf != show)
            {
                m_Panel.SetActive(show);
                IsShowing = show;
                if (show)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            if (!show) return;

            m_Text.text = _result == MatchResult.MouseWin ? "MICE WIN!" : "CAT WINS!";

            NetworkManager nm = NetworkManager.Singleton;
            m_RestartButton.gameObject.SetActive(nm != null && nm.IsHost);
        }
    }
}
