using CTC.Runtime.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 참가자 보드의 한 칸. 표시만 한다(규칙 5) — 누가 방장인지 판정하지 않고 받은 대로 그린다.
    ///
    /// 빈 자리도 이 컴포넌트가 그린다. 자리를 아예 숨기면 인원이 들쭉날쭉해 보여서,
    /// 정원만큼 칸을 깔아 두고 "빈 자리"로 흐리게 남긴다.
    /// </summary>
    public class RoomMemberRow : MonoBehaviour
    {
        private static readonly Color BG_FILLED = UiPalette.Slot;
        private static readonly Color BG_EMPTY = UiPalette.WithAlpha(UiPalette.Panel, 0.45f);

        private static readonly Color NAME_FILLED = UiPalette.TextOn;
        private static readonly Color NAME_EMPTY = UiPalette.WithAlpha(UiPalette.TextDim, 0.55f);
        private static readonly Color NAME_SELF = UiPalette.AccentSoft;

        [SerializeField] private Image m_Background;

        /// <summary>`m_Name`으로 부르면 안 된다 — <see cref="UnityEngine.Object"/>가 그 이름을 이미 쓴다.</summary>
        [SerializeField] private TMP_Text m_MemberName;
        [SerializeField] private TMP_Text m_HostBadge;

        public void BindEmpty()
        {
            if (m_Background != null) m_Background.color = BG_EMPTY;
            if (m_MemberName != null)
            {
                m_MemberName.text = "빈 자리";
                m_MemberName.color = NAME_EMPTY;
            }
            if (m_HostBadge != null) m_HostBadge.gameObject.SetActive(false);
        }

        public void Bind(string _name, bool _isHost, bool _isSelf)
        {
            if (m_Background != null) m_Background.color = BG_FILLED;
            if (m_MemberName != null)
            {
                m_MemberName.text = _isSelf ? $"{_name} (나)" : _name;
                m_MemberName.color = _isSelf ? NAME_SELF : NAME_FILLED;
            }
            if (m_HostBadge != null) m_HostBadge.gameObject.SetActive(_isHost);
        }
    }
}
