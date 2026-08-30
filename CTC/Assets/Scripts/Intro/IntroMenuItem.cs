using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CTC.Runtime.Intro
{
    /// <summary>
    /// 메인 메뉴 한 줄. 커서가 올라가면 왼쪽 강조 막대와 뒤쪽 띠가 켜진다.
    ///
    /// 표시만 한다(CTC 규칙 5) — 눌렀을 때 무슨 일이 나는지는 <see cref="IntroController"/>가 안다.
    /// 잠긴 줄(아직 없는 기능)은 흐리게 두고 커서에 반응하지 않는다. 사유 문구는 붙이지 않는다 —
    /// 정식판에 남을 글자가 아니고, 켜지고 꺼지는 회색 글씨가 메인 화면을 지저분하게 만든다.
    /// </summary>
    public class IntroMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // 평소엔 가라앉아 있다가 커서가 올라간 줄만 밝아진다 — 어느 줄을 고르는 중인지가 한눈에 보인다.
        private static readonly Color LABEL_IDLE = new Color(0.36f, 0.40f, 0.45f);
        private static readonly Color LABEL_HOVER = new Color(0.82f, 0.85f, 0.88f);

        /// <summary>잠긴 줄은 평소보다 한 단계 더 어둡다 — 안 그러면 그냥 안 눌린 줄과 구별이 안 된다.</summary>
        private static readonly Color LABEL_LOCKED = new Color(0.24f, 0.27f, 0.31f);

        [SerializeField] private TMP_Text m_Label;
        [SerializeField] private Image m_AccentBar;
        [SerializeField] private Image m_HighlightBar;
        /// <summary>
        /// 씬 빌더가 굽는 시점에 정하는 값이라 **직렬화해야 한다** — 평범한 필드로 두면
        /// 플레이 진입 때 false로 돌아가 잠금이 풀린다(실측).
        /// </summary>
        [SerializeField] private bool m_Locked;

        private bool m_Hover;

        /// <summary>잠긴 줄은 눌러도 아무 일이 없다 — 버튼도 같이 꺼 준다.</summary>
        public bool Locked
        {
            get => m_Locked;
            set
            {
                m_Locked = value;
                if (m_Locked) m_Hover = false;

                var button = GetComponent<Button>();
                if (button != null) button.interactable = !m_Locked;

                Render();
            }
        }

        private void Awake() => Render();

        public void OnPointerEnter(PointerEventData _eventData)
        {
            if (m_Locked) return;
            m_Hover = true;
            Render();
        }

        public void OnPointerExit(PointerEventData _eventData)
        {
            m_Hover = false;
            Render();
        }

        private void Render()
        {
            bool lit = m_Hover && !m_Locked;

            if (m_Label != null)
            {
                m_Label.color = m_Locked ? LABEL_LOCKED : lit ? LABEL_HOVER : LABEL_IDLE;
            }

            if (m_AccentBar != null) m_AccentBar.gameObject.SetActive(lit);
            if (m_HighlightBar != null) m_HighlightBar.gameObject.SetActive(lit);
        }
    }
}
