using CTC.Runtime.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CTC.Runtime.Lobby.Browser
{
    /// <summary>
    /// 방 목록의 한 줄. 표시와 커서 반응만 한다(CTC 규칙 5) — 눌렀을 때 무슨 일이 나는지는 모른다.
    /// 눌리면 자기 번호를 <see cref="Clicked"/>로 알릴 뿐이고, 그걸 받아 처리하는 건 View다.
    /// </summary>
    public class LobbyRowItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private static readonly Color BG_IDLE = UiPalette.WithAlpha(UiPalette.Slot, 0.72f);
        private static readonly Color BG_HOVER = UiPalette.WithAlpha(UiPalette.Groove, 0.9f);
        private static readonly Color BG_SELECTED = UiPalette.WithAlpha(UiPalette.Accent, 0.11f);

        private static readonly Color NAME_IDLE = UiPalette.TextOn;
        private static readonly Color NAME_SELECTED = UiPalette.AccentSoft;

        private static readonly Color STATE_OPEN = UiPalette.Good;
        private static readonly Color STATE_FULL = UiPalette.Bad;

        [SerializeField] private Image m_Background;

        /// <summary>
        /// **`m_Name`이라고 부르면 안 된다** — `UnityEngine.Object`가 이미 그 이름의 직렬화 필드를
        /// 갖고 있어서, 에디터에서 `FindProperty("m_Name")`이 내 필드가 아니라 **오브젝트 이름**을
        /// 집는다. 배선이 조용히 비고 화면엔 플레이스홀더만 남는다(2026-08-26 실측).
        /// </summary>
        [SerializeField] private TMP_Text m_RoomName;
        [SerializeField] private TMP_Text m_Host;
        [SerializeField] private TMP_Text m_Count;
        [SerializeField] private TMP_Text m_State;

        private int m_Index = -1;
        private bool m_Selected;
        private bool m_Hover;

        /// <summary>이 줄이 눌렸다 — 인자는 목록에서의 번호.</summary>
        public event Action<int> Clicked;

        public void Bind(int _index, LobbyRoomInfo _room)
        {
            m_Index = _index;

            if (m_RoomName != null) m_RoomName.text = string.IsNullOrWhiteSpace(_room.Name) ? "이름 없는 방" : _room.Name;
            if (m_Host != null) m_Host.text = _room.Host;
            if (m_Count != null) m_Count.text = _room.CountLabel;

            if (m_State != null)
            {
                m_State.text = _room.StateLabel;
                m_State.color = _room.IsFull ? STATE_FULL : STATE_OPEN;
            }

            Render();
        }

        public void SetSelected(bool _selected)
        {
            m_Selected = _selected;
            Render();
        }

        public void OnPointerEnter(PointerEventData _eventData)
        {
            m_Hover = true;
            Render();
        }

        public void OnPointerExit(PointerEventData _eventData)
        {
            m_Hover = false;
            Render();
        }

        public void OnPointerClick(PointerEventData _eventData) => Clicked?.Invoke(m_Index);

        private void OnDisable()
        {
            m_Hover = false;
            Render();
        }

        private void Render()
        {
            if (m_Background != null)
            {
                m_Background.color = m_Selected ? BG_SELECTED : m_Hover ? BG_HOVER : BG_IDLE;
            }

            if (m_RoomName != null) m_RoomName.color = m_Selected ? NAME_SELECTED : NAME_IDLE;
        }
    }
}
