using System.Collections.Generic;

namespace CTC.Runtime.Lobby.Browser
{
    /// <summary>
    /// 서버 목록의 검색·페이지·빠른 참가 고르기 — 순수 함수(EditMode TDD, 2026-09-14).
    ///
    /// 스팀 방 검색은 값이 **완전히 같을 때만** 거른다. 그래서 이름 검색은 받아 온 목록을 여기서 거른다.
    /// </summary>
    public static class ServerListLogic
    {
        /// <summary>R.E.P.O.와 같은 한 페이지 줄 수.</summary>
        public const int PAGE_SIZE = 8;

        /// <summary>검색어를 띄어쓰기로 나눠 **전부** 들어간 방만(방 이름·방장, 대소문자 무시). 빈 검색어는 전부.</summary>
        public static List<int> Filter(IReadOnlyList<LobbyRoomInfo> _rooms, string _query)
        {
            var result = new List<int>();
            if (_rooms == null) return result;

            string[] terms = string.IsNullOrWhiteSpace(_query)
                ? System.Array.Empty<string>()
                : _query.Trim().ToLowerInvariant().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < _rooms.Count; i++)
            {
                string hay = ((_rooms[i].Name ?? "") + " " + (_rooms[i].Host ?? "")).ToLowerInvariant();
                bool all = true;
                foreach (string term in terms)
                {
                    if (hay.Contains(term)) continue;
                    all = false;
                    break;
                }
                if (all) result.Add(i);
            }
            return result;
        }

        /// <summary>페이지 수 — 비어 있어도 1(「1 / 1」로 보인다).</summary>
        public static int PageCount(int _count, int _pageSize = PAGE_SIZE)
        {
            if (_pageSize <= 0 || _count <= 0) return 1;
            return (_count + _pageSize - 1) / _pageSize;
        }

        public static int ClampPage(int _page, int _count, int _pageSize = PAGE_SIZE)
        {
            int last = PageCount(_count, _pageSize) - 1;
            return _page < 0 ? 0 : _page > last ? last : _page;
        }

        /// <summary>그 페이지의 첫 줄이 전체 목록에서 몇 번째인가.</summary>
        public static int PageStart(int _page, int _count, int _pageSize = PAGE_SIZE)
        {
            return ClampPage(_page, _count, _pageSize) * _pageSize;
        }

        /// <summary>
        /// 빠른 참가가 들어갈 방 — **빈자리가 있는 방 중 가장 찬 방**(R.E.P.O. `MatchmakingMode.FillRoom`).
        /// 한 판이 빨리 차야 기다리는 사람이 줄어든다. 같으면 앞의 것. 없으면 -1(새로 만든다).
        /// </summary>
        public static int PickQuickMatch(IReadOnlyList<LobbyRoomInfo> _rooms)
        {
            int best = -1;
            if (_rooms == null) return best;

            for (int i = 0; i < _rooms.Count; i++)
            {
                if (_rooms[i].IsFull) continue;
                if (best < 0 || _rooms[i].MemberCount > _rooms[best].MemberCount) best = i;
            }
            return best;
        }
    }
}
