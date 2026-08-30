using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.Intro
{
    /// <summary>
    /// 인트로 화면 표시만 한다(CTC 규칙 5) — 판정은 전부 <see cref="IntroDirector"/>와
    /// <see cref="IntroBootLogic"/>에 있고, 여기서는 아무것도 결정하지 않는다.
    /// 갱신은 R3 구독이다(규칙 3) — Update에서 폴링하지 않는다.
    /// </summary>
    public class IntroView : MonoBehaviour
    {
        [SerializeField] private IntroDirector m_Director;

        /// <summary>"게임 시작" 줄 — 준비가 안 끝나면 잠가 둔다.</summary>
        [SerializeField] private IntroMenuItem m_StartItem;
        [SerializeField] private GameObject m_ErrorPanel;
        [SerializeField] private TMP_Text m_ErrorText;

        private void Start()
        {
            if (m_Director == null)
            {
                Core.GameDebug.LogError("[IntroView] Director가 안 붙었다 — 화면이 안 산다");
                return;
            }

            m_Director.Changed.Subscribe(_ => Render()).AddTo(this);
            Render();
        }

        private void Render()
        {
            if (m_Director == null) return;

            bool fatal = m_Director.HasFatalFailure;
            bool canStart = m_Director.CanStart;

            if (m_StartItem != null) m_StartItem.Locked = !canStart;

            if (m_ErrorPanel != null) m_ErrorPanel.SetActive(fatal);

            // 무엇이 왜 막혔는지 한 줄로 — 항목 이름이 없으면 "스팀이 실행되지 않음"만 떠서 맥락이 없다.
            if (m_ErrorText != null && fatal)
            {
                int i = IntroBootLogic.FirstFatalIndex(m_Director.States);
                string label = i >= 0 ? IntroBootLogic.LabelOf(IntroBootLogic.STEPS[i]) : "";
                string reason = m_Director.FatalReason;

                m_ErrorText.text = string.IsNullOrEmpty(label)
                    ? "준비를 마치지 못했습니다."
                    : string.IsNullOrEmpty(reason) ? label : $"{label} — {reason}";
            }
        }
    }
}
