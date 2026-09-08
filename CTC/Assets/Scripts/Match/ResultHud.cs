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

        // 시리즈 여부를 보려고 잡는다 — MatchManager와 같은 GO에 동승한다(배선 추가 없음).
        private MatchSeries m_Series;

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
            if (m_Match != null) m_Series = m_Match.GetComponent<MatchSeries>();
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

            m_Text.text = LabelOf(_result);

            // **시리즈 중에는 재시작 버튼을 숨긴다.** 다음 판은 `MatchSeries`가 알아서 여는데
            // 버튼까지 있으면 사람이 눌러 판이 두 번 초기화된다(2026-09-09 감사 A).
            // 시리즈가 곧 한 경기라 사람이 누를 자리가 없다는 것이 그 클래스의 전제다.
            NetworkManager nm = NetworkManager.Singleton;
            bool seriesRunning = m_Series != null && m_Series.Active;
            m_RestartButton.gameObject.SetActive(nm != null && nm.IsHost && !seriesRunning);
        }

        /// <summary>
        /// 결과 문구. **삼항으로 쓰지 않는다** — 예전엔 "쥐 승이 아니면 고양이 승"이라
        /// 깡패 팀이 이겨도 화면에 "CAT WINS!"가 떴다(2026-08-10 실측). 승자가 셋인 게임이다.
        /// </summary>
        public static string LabelOf(MatchResult _result)
        {
            switch (_result)
            {
                case MatchResult.MouseWin: return "MICE WIN!";
                case MatchResult.CatWin: return "CAT WINS!";
                case MatchResult.GangWin: return "GANG WINS!";
                default: return "";
            }
        }
    }
}
