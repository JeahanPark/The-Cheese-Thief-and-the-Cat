using CTC.Runtime.UI;
using CTC.Runtime.Core;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 우측 맵 선택 (MVC의 View) — 고른 맵을 칠하고, 방장이 아니면 잠근다.
    ///
    /// **비방장에게도 보여 준다.** 못 고르는 것과 못 보는 것은 다르다 — 어떤 맵에서 하는지는
    /// 들어온 사람 전원이 알아야 한다(인게임 로비와 같은 규칙).
    /// </summary>
    public class RoomMapView : MonoBehaviour
    {
        private static readonly Color PICKED = UiPalette.Accent;
        private static readonly Color IDLE = UiPalette.Slot;

        private static readonly Color LABEL_PICKED = UiPalette.TextOnAccent;
        private static readonly Color LABEL_IDLE = UiPalette.TextOn;

        [SerializeField] private RoomSession m_Session;
        [SerializeField] private RoomMapCatalog m_Catalog;
        [SerializeField] private Button[] m_Buttons;
        [SerializeField] private TMP_Text m_BriefLabel;

        private void Start()
        {
            if (m_Session == null || m_Catalog == null)
            {
                GameDebug.LogWarning("[Room] 맵 선택에 세션·목록이 안 붙어 있다");
                return;
            }

            m_Session.ViewChanged.Subscribe(_ => Render()).AddTo(this);
        }

        private void Render()
        {
            if (m_Buttons == null || m_Session == null || m_Catalog == null) return;

            int picked = m_Session.MapIndex;
            bool isHost = m_Session.IsHost && !m_Session.Starting;

            for (int i = 0; i < m_Buttons.Length; i++)
            {
                Button button = m_Buttons[i];
                if (button == null) continue;

                // 정식 맵만 노출(2026-08-31) — 작은 맵은 개발 빌드에서만 버튼이 선다.
                bool exists = i < m_Catalog.Count && m_Catalog.IsSelectableAt(i);
                if (button.gameObject.activeSelf != exists) button.gameObject.SetActive(exists);
                if (!exists) continue;

                bool mine = i == picked;
                button.interactable = isHost && !mine;

                var image = button.GetComponent<Image>();
                if (image != null) image.color = mine ? PICKED : IDLE;

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = m_Catalog.ShortNameAt(i);
                    label.color = mine ? LABEL_PICKED : LABEL_IDLE;
                }
            }

            if (m_BriefLabel != null) m_BriefLabel.text = m_Catalog.BriefAt(picked);
        }
    }
}
