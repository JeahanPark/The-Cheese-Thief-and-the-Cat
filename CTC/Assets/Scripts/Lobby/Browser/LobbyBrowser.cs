using System.Collections.Generic;
using CTC.Runtime.Core;
using CTC.Runtime.Networking;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using SteamLobby = Steamworks.Data.Lobby;

namespace CTC.Runtime.Lobby.Browser
{
    /// <summary>
    /// 서버 목록의 Model(규칙 5·6) — 방 목록을 물어다 주고 검색·페이지·선택을 든다. 화면은 스팀을 모르고 이 스트림만 구독한다.
    ///
    /// **에디터에는 스팀이 없다.** 그래서 에디터에서는 가짜 목록을 내준다 — 안 그러면
    /// 화면을 한 줄도 못 그려 보고 개발이 막힌다. 가짜는 에디터에서만 나온다.
    ///
    /// 새로고침은 **명시 호출만** 한다. 프레임마다 스팀을 찌르면 로그아웃 순간에 터진다는 게
    /// 이 프로젝트가 이미 겪은 사고다(ConnectionUI, 2026-08-26).
    ///
    /// 2026-09-14: 접속(참가·만들기)은 <c>SessionStarter</c>로 옮겼다 — 메인 화면도 같은 절차를 쓴다.
    /// 검색·페이지는 R.E.P.O. 서버 목록을 따랐다(<see cref="ServerListLogic"/>).
    /// </summary>
    public class LobbyBrowser : MonoBehaviour
    {
        private readonly List<LobbyRoomInfo> m_Rooms = new List<LobbyRoomInfo>();
        private readonly List<int> m_Filtered = new List<int>();
        private readonly Subject<Unit> m_Changed = new Subject<Unit>();

        private SteamLobby[] m_Raw = System.Array.Empty<SteamLobby>();
        private SteamLobbyManager m_Steam;
        private bool m_Busy;
        private int m_Selected = -1;
        private int m_Page;
        private string m_Search = "";

        /// <summary>목록이나 상태가 바뀔 때마다 울린다.</summary>
        public Observable<Unit> Changed => m_Changed;

        public IReadOnlyList<LobbyRoomInfo> Rooms => m_Rooms;

        /// <summary>검색을 통과한 방의 번호(<see cref="Rooms"/> 기준).</summary>
        public IReadOnlyList<int> Filtered => m_Filtered;

        /// <summary>불러오는 중인가 — 화면이 "새로고침" 버튼을 잠그는 데 쓴다.</summary>
        public bool Busy => m_Busy;

        /// <summary>스팀이 붙어 있는가. 아니면 화면이 안내를 띄운다.</summary>
        public bool SteamReady => SteamBootstrap.Ready;

        /// <summary>마지막 새로고침에서 **다른 프로토콜 버전**이라 숨긴 방 수 — 빌드가 어긋난 두 사람에게 「왜 비었나」를 알려 준다(2026-09-20).</summary>
        public int HiddenByVersion => m_Steam != null ? m_Steam.HiddenByVersion : 0;

        public int Page => m_Page;

        public int PageCount => ServerListLogic.PageCount(m_Filtered.Count);

        public string Search => m_Search;

        /// <summary>
        /// 지금 고른 방(<see cref="Rooms"/> 기준 번호). 없으면 -1.
        /// 선택은 화면이 아니라 여기 있다 — 참여 버튼(Controller)과 상세 패널(View)이
        /// 같은 값을 봐야 하는데, 둘 중 하나가 들면 나머지가 그걸 캐물어야 한다.
        /// </summary>
        public int SelectedIndex => m_Selected;

        public bool HasSelection => m_Selected >= 0 && m_Selected < m_Rooms.Count;

        public LobbyRoomInfo Selected => HasSelection ? m_Rooms[m_Selected] : default;

        public void Select(int _index)
        {
            int next = _index >= 0 && _index < m_Rooms.Count ? _index : -1;
            if (next == m_Selected) return;

            m_Selected = next;
            m_Changed.OnNext(Unit.Default);
        }

        public void SetSearch(string _query)
        {
            string query = _query ?? "";
            if (query == m_Search) return;

            m_Search = query;
            m_Page = 0;
            ApplyFilter();
        }

        public void TurnPage(int _delta)
        {
            int next = ServerListLogic.ClampPage(m_Page + _delta, m_Filtered.Count);
            if (next == m_Page) return;

            m_Page = next;
            m_Changed.OnNext(Unit.Default);
        }

        /// <summary>고른 방의 스팀 핸들. 가짜 목록(에디터)이거나 목록이 낡았으면 null.</summary>
        public SteamLobby? SelectedLobby
        {
            get
            {
                if (!HasSelection) return null;
                int raw = m_Rooms[m_Selected].Index;
                return raw >= 0 && raw < m_Raw.Length ? m_Raw[raw] : (SteamLobby?)null;
            }
        }

        private void Awake()
        {
            m_Steam = FindAnyObjectByType<SteamLobbyManager>();
            if (m_Steam == null) GameDebug.LogWarning("[LobbyBrowser] SteamLobbyManager가 씬에 없다");
        }

        private void Start() => Refresh();

        private void OnDestroy() => m_Changed.Dispose();

        public void Refresh()
        {
            if (m_Busy) return;

#if UNITY_EDITOR
            FillFakeRooms();
#else
            RefreshAsync().Forget();
#endif
        }

        private async UniTaskVoid RefreshAsync()
        {
            if (m_Steam == null || !SteamBootstrap.Ready)
            {
                OnLobbies(System.Array.Empty<SteamLobby>());
                return;
            }

            m_Busy = true;
            m_Changed.OnNext(Unit.Default);
            SteamLobby[] found = await m_Steam.RefreshLobbies();
            if (this == null) return;   // 기다리는 사이 씬이 넘어갔다
            OnLobbies(found);
        }

        private void OnLobbies(SteamLobby[] _found)
        {
            m_Busy = false;
            m_Raw = _found ?? System.Array.Empty<SteamLobby>();

            m_Rooms.Clear();
            for (int i = 0; i < m_Raw.Length; i++)
            {
                SteamLobby lobby = m_Raw[i];
                // 방장 이름은 방 데이터에서 — 안 들어간 방의 Owner는 스팀이 안 준다(멤버여야 읽힌다, 2026-09-20).
                m_Rooms.Add(new LobbyRoomInfo(
                    lobby.GetData(SteamLobbyLogic.NAME_KEY),
                    SteamLobbyLogic.HostLabel(lobby.GetData(SteamLobbyLogic.HOST_KEY), lobby.Owner.Name),
                    lobby.MemberCount,
                    lobby.MaxMembers,
                    i));
            }

            m_Selected = -1;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            m_Filtered.Clear();
            m_Filtered.AddRange(ServerListLogic.Filter(m_Rooms, m_Search));
            m_Page = ServerListLogic.ClampPage(m_Page, m_Filtered.Count);
            if (m_Selected >= 0 && !m_Filtered.Contains(m_Selected)) m_Selected = -1;
            m_Changed.OnNext(Unit.Default);
        }

#if UNITY_EDITOR
        /// <summary>에디터 전용 가짜 목록 — 화면 배치·검색·페이지를 눈으로 확인하기 위한 것이다(두 페이지가 되게 11개).</summary>
        private void FillFakeRooms()
        {
            m_Raw = System.Array.Empty<SteamLobby>();
            m_Rooms.Clear();
            int max = SteamLobbyLogic.MAX_PLAYERS;
            m_Rooms.Add(new LobbyRoomInfo("치즈 창고 털러 갈 사람", "야옹이대장", 6, max, -1));
            m_Rooms.Add(new LobbyRoomInfo("초보만 · 욕 ㄴㄴ", "생쥐삼형제", 4, max, -1));
            m_Rooms.Add(new LobbyRoomInfo("빡겜 풀방만", "치즈러버", 10, max, -1));
            m_Rooms.Add(new LobbyRoomInfo("한 판만 더", "숨은쥐", 7, max, -1));
            m_Rooms.Add(new LobbyRoomInfo("아무나 오세요", "냥냥펀치", 2, max, -1));
            m_Rooms.Add(new LobbyRoomInfo("새벽 치즈 파티", "올빼미", 3, max, -1));
            m_Rooms.Add(new LobbyRoomInfo("고양이만 할 사람", "냥이", 5, max, -1));
            m_Rooms.Add(new LobbyRoomInfo("음성 필수", "쥐돌이", 8, max, -1));
            m_Rooms.Add(new LobbyRoomInfo("Cheese Heist", "Remy", 1, max, -1));
            m_Rooms.Add(new LobbyRoomInfo("느긋하게", "치즈장인", 9, max, -1));
            m_Rooms.Add(new LobbyRoomInfo("친구 구해요", "혼자쥐", 1, max, -1));
            m_Selected = -1;
            ApplyFilter();
        }
#endif
    }
}
