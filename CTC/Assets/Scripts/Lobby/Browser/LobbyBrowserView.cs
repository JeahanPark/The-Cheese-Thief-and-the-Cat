using System.Collections.Generic;
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
    /// 줄은 씬에 미리 깔아 둔 고정 개수(한 페이지)를 쓰고 남는 건 끈다. 넘치면 페이지로 넘긴다(2026-09-14,
    /// R.E.P.O. 서버 목록). 접속 중에는 버튼을 잠그고 「연결 중…」을 띄운다.
    /// </summary>
    public class LobbyBrowserView : MonoBehaviour
    {
        [SerializeField] private LobbyBrowser m_Browser;
        [SerializeField] private LobbyRowItem[] m_Rows;

        [Header("목록 머리")]
        [SerializeField] private TMP_Text m_CountLabel;
        [SerializeField] private TMP_Text m_EmptyLabel;
        [SerializeField] private TMP_Text m_PageLabel;
        [SerializeField] private GameObject m_PrevButton;
        [SerializeField] private GameObject m_NextButton;

        [Header("접속")]
        [SerializeField] private FrontDoor.SessionStarter m_Starter;
        [SerializeField] private CanvasGroup m_InputGroup;
        [SerializeField] private GameObject m_ConnectingLabel;

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

            if (m_Starter != null)
                m_Starter.Connecting.Subscribe(RenderConnecting).AddTo(this);
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

            IReadOnlyList<int> filtered = m_Browser.Filtered;
            int total = filtered.Count;
            int slots = m_Rows != null ? m_Rows.Length : 0;
            int start = ServerListLogic.PageStart(m_Browser.Page, total, slots > 0 ? slots : ServerListLogic.PAGE_SIZE);

            for (int i = 0; i < slots; i++)
            {
                LobbyRowItem row = m_Rows[i];
                if (row == null) continue;

                int at = start + i;
                bool used = at < total;
                row.gameObject.SetActive(used);
                if (!used) continue;

                int roomIndex = filtered[at];
                row.Bind(roomIndex, m_Browser.Rooms[roomIndex]);
                row.SetSelected(roomIndex == m_Browser.SelectedIndex);
            }

            if (m_CountLabel != null)
            {
                int all = m_Browser.Rooms.Count;
                m_CountLabel.text = m_Browser.Busy ? "불러오는 중…"
                    : total == all ? $"{all}개"
                    : $"{total}개 / 전체 {all}개";
            }

            int pages = ServerListLogic.PageCount(total, slots > 0 ? slots : ServerListLogic.PAGE_SIZE);
            if (m_PageLabel != null) m_PageLabel.text = $"{m_Browser.Page + 1} / {pages}";
            if (m_PrevButton != null) m_PrevButton.SetActive(m_Browser.Page > 0);
            if (m_NextButton != null) m_NextButton.SetActive(m_Browser.Page < pages - 1);

            // 방이 없는 이유가 셋이다 — 정말 없거나, 검색에 안 걸리거나, 스팀이 없거나. 구별해 준다.
            if (m_EmptyLabel != null)
            {
                bool empty = total == 0 && !m_Browser.Busy;
                m_EmptyLabel.gameObject.SetActive(empty);
                if (empty)
                {
                    m_EmptyLabel.text = !m_Browser.SteamReady && m_Browser.Rooms.Count == 0
                        ? "스팀에 연결되지 않아 방을 불러올 수 없습니다."
                        : m_Browser.Rooms.Count > 0
                            ? "검색에 맞는 방이 없습니다."
                            : "열린 방이 없습니다. 직접 만들어 보세요.";
                }
            }

            RenderDetail();
        }

        private void RenderConnecting(bool _connecting)
        {
            if (m_InputGroup != null) m_InputGroup.interactable = !_connecting;
            if (m_ConnectingLabel != null) m_ConnectingLabel.SetActive(_connecting);
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
