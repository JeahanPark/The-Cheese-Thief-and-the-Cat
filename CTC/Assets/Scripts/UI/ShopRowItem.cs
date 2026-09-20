using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 상점 목록의 한 줄(2026-09-19 시안 B). 표시와 커서 반응만 한다(CTC 규칙 5) — 로비 방 목록(<c>LobbyRowItem</c>)과 같은 규약.
    /// **마우스를 올리면 그 줄이 골라지고**(<see cref="Hovered"/>) 오른쪽 상세가 바뀐다. **누르면 산다**(<see cref="Clicked"/>) —
    /// 무엇을 하는지는 모른다, 제 번호만 알린다.
    /// </summary>
    public class ShopRowItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        private static readonly Color BG_IDLE = UiPalette.WithAlpha(UiPalette.Slot, 0.72f);
        private static readonly Color BG_SELECTED = UiPalette.WithAlpha(UiPalette.Accent, 0.11f);

        /// <summary>품절 줄 — 흐리게(시안의 불투명도 0.6).</summary>
        private const float DIM_ALPHA = 0.6f;

        [SerializeField] private Image m_Background;
        [SerializeField] private CanvasGroup m_Group;

        /// <summary>**`m_Name`이라고 부르면 안 된다**(CLAUDE.md 함정 1) — 오브젝트 이름을 집는다.</summary>
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private Image[] m_Bars;
        [SerializeField] private TMP_Text m_Price;

        private int m_Index = -1;
        private bool m_Selected;
        private bool m_Dim;

        /// <summary>커서가 이 줄에 올라왔다 — 인자는 목록에서의 번호.</summary>
        public event Action<int> Hovered;

        /// <summary>이 줄이 눌렸다 — 인자는 목록에서의 번호.</summary>
        public event Action<int> Clicked;

        /// <summary>한 줄을 그린다. 막대는 가진 단계만큼 초록(보유)이다.</summary>
        public void Bind(int _index, string _title, int _ownedLevel, string _price, Color _priceColor, bool _dim)
        {
            m_Index = _index;
            m_Dim = _dim;
            if (m_Title != null) m_Title.text = _title;
            if (m_Price != null)
            {
                m_Price.text = _price;
                m_Price.color = _priceColor;
            }
            for (int i = 0; i < m_Bars.Length; i++)
            {
                if (m_Bars[i] != null) m_Bars[i].color = i < _ownedLevel ? UiPalette.Good : UiPalette.Groove;
            }
            Render();
        }

        public void SetSelected(bool _selected)
        {
            if (m_Selected == _selected) return;
            m_Selected = _selected;
            Render();
        }

        public void OnPointerEnter(PointerEventData _eventData) => Hovered?.Invoke(m_Index);

        public void OnPointerClick(PointerEventData _eventData) => Clicked?.Invoke(m_Index);

        /// <summary>QA: 커서를 올린 것과 같은 길.</summary>
        public void DevHover() => Hovered?.Invoke(m_Index);

        /// <summary>QA: 누른 것과 같은 길 — 지름길을 만들면 배선 누락을 못 잡는다.</summary>
        public void DevClick() => Clicked?.Invoke(m_Index);

        private void Render()
        {
            if (m_Background != null) m_Background.color = m_Selected ? BG_SELECTED : BG_IDLE;
            if (m_Title != null) m_Title.color = m_Selected ? UiPalette.AccentSoft : m_Dim ? UiPalette.TextDim : UiPalette.TextOn;
            if (m_Group != null) m_Group.alpha = m_Dim ? DIM_ALPHA : 1f;
        }
    }
}
