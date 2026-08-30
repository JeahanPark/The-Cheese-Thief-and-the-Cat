using CTC.Runtime.Core;
using R3;
using TMPro;
using UnityEngine;

namespace CTC.Runtime.Lobby.Browser
{
    /// <summary>
    /// 방 목록 화면 표시만 한다(CTC 규칙 5). 판정도 스팀 호출도 없다 —
    /// <see cref="LobbyBrowser"/>가 울릴 때마다 지금 상태를 그대로 옮겨 그린다(규칙 3).
    ///
    /// 줄은 씬에 미리 깔아 둔 고정 개수를 쓰고 남는 건 끈다. 방이 그보다 많으면 "+N"으로만 알린다 —
    /// 스크롤은 아직 없다.
    /// </summary>
    public class LobbyBrowserView : MonoBehaviour
    {
        [SerializeField] private LobbyBrowser m_Browser;
        [SerializeField] private LobbyRowItem[] m_Rows;

        [Header("목록 머리")]
        [SerializeField] private TMP_Text m_CountLabel;
        [SerializeField] private TMP_Text m_EmptyLabel;

        [Header("상세")]
        [SerializeField] private GameObject m_DetailPanel;
        [SerializeField] private TMP_Text m_DetailName;
        [SerializeField] private TMP_Text m_DetailHost;
        [SerializeField] private TMP_Text m_DetailCount;
        [SerializeField] private GameObject m_JoinButton;

        private void Start()
        {
            if (m_Browser == null)
            {
                GameDebug.LogError("[LobbyBrowserView] Browser가 안 붙었다 — 화면이 안 산다");
                return;
            }

            if (m_Rows != null)
            {
                for (int i = 0; i < m_Rows.Length; i++)
                {
                    if (m_Rows[i] != null) m_Rows[i].Clicked += OnRowClicked;
                }
            }

            m_Browser.Changed.Subscribe(_ => Render()).AddTo(this);
            Render();
        }

        private void OnDestroy()
        {
            if (m_Rows == null) return;
            for (int i = 0; i < m_Rows.Length; i++)
            {
                if (m_Rows[i] != null) m_Rows[i].Clicked -= OnRowClicked;
            }
        }

        private void OnRowClicked(int _index)
        {
            if (m_Browser != null) m_Browser.Select(_index);
        }

        private void Render()
        {
            if (m_Browser == null) return;

            int total = m_Browser.Rooms.Count;
            int slots = m_Rows != null ? m_Rows.Length : 0;
            int shown = total < slots ? total : slots;

            for (int i = 0; i < slots; i++)
            {
                LobbyRowItem row = m_Rows[i];
                if (row == null) continue;

                bool used = i < shown;
                row.gameObject.SetActive(used);
                if (!used) continue;

                row.Bind(i, m_Browser.Rooms[i]);
                row.SetSelected(i == m_Browser.SelectedIndex);
            }

            if (m_CountLabel != null)
            {
                m_CountLabel.text = m_Browser.Busy ? "불러오는 중…"
                    : total > slots ? $"{total}개 (+{total - slots})"
                    : $"{total}개";
            }

            // 방이 없는 이유가 둘이다 — 정말 없거나, 스팀이 없거나. 둘을 구별해 준다.
            if (m_EmptyLabel != null)
            {
                bool empty = total == 0 && !m_Browser.Busy;
                m_EmptyLabel.gameObject.SetActive(empty);
                if (empty)
                {
                    m_EmptyLabel.text = m_Browser.SteamReady
                        ? "열린 방이 없습니다. 직접 만들어 보세요."
                        : "스팀에 연결되지 않아 방을 불러올 수 없습니다.";
                }
            }

            RenderDetail();
        }

        private void RenderDetail()
        {
            bool has = m_Browser.HasSelection;

            if (m_DetailPanel != null) m_DetailPanel.SetActive(has);
            if (m_JoinButton != null) m_JoinButton.SetActive(has && !m_Browser.Selected.IsFull);
            if (!has) return;

            LobbyRoomInfo room = m_Browser.Selected;
            if (m_DetailName != null) m_DetailName.text = string.IsNullOrWhiteSpace(room.Name) ? "이름 없는 방" : room.Name;
            if (m_DetailHost != null) m_DetailHost.text = room.Host;
            if (m_DetailCount != null) m_DetailCount.text = room.CountLabel;
        }
    }
}
