using System.Collections.Generic;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 고양이 추첨 규칙 — 순수 함수(EditMode TDD). **뽑는 것은 서버다**, 여기는 규칙만 안다.
    ///
    /// 난수를 안에서 만들지 않고 `_roll`로 받는다. 그래야 테스트가 "3번째가 뽑히면"을
    /// 직접 쓸 수 있고, 서버가 `Random.Range`를 어디서 부르든 규칙은 하나로 남는다.
    /// </summary>
    public static class CatDrawLogic
    {
        /// <summary>직전 고양이를 빼고도 후보가 남으려면 이 인원은 돼야 한다.</summary>
        public const int REPEAT_GUARD_MIN_PLAYERS = 3;

        /// <summary>
        /// 후보 목록을 만든다 — 직전 판의 고양이는 뺀다(3명 이상일 때).
        /// 2명뿐이면 빼는 순간 남는 사람이 하나라 "무작위"가 아니게 되므로 그대로 둔다.
        /// </summary>
        public static List<ulong> Candidates(IReadOnlyList<ulong> _members, ulong _previousCat)
        {
            var list = new List<ulong>(_members.Count);
            bool guard = _members.Count >= REPEAT_GUARD_MIN_PLAYERS;

            for (int i = 0; i < _members.Count; i++)
            {
                if (guard && _members[i] == _previousCat) continue;
                list.Add(_members[i]);
            }

            // 전원이 직전 고양이일 수는 없지만, 목록이 비면 뽑기가 통째로 죽는다 — 되돌린다.
            if (list.Count == 0) list.AddRange(_members);
            return list;
        }

        /// <summary>
        /// 고양이를 하나 고른다. `_roll`은 0 이상의 아무 정수 — 후보 수로 접어서 쓴다.
        /// 참가자가 없으면 <paramref name="_fallback"/>(보통 서버 자신)을 준다.
        /// </summary>
        public static ulong Pick(IReadOnlyList<ulong> _members, ulong _previousCat, int _roll, ulong _fallback)
        {
            if (_members == null || _members.Count == 0) return _fallback;

            List<ulong> candidates = Candidates(_members, _previousCat);
            int index = _roll % candidates.Count;
            if (index < 0) index += candidates.Count;
            return candidates[index];
        }

        /// <summary>뽑힌 고양이를 빼고 전원을 쥐로 — 인게임에 넘길 역할표.</summary>
        public static Dictionary<ulong, Lobby.RoleId> RoleTable(IReadOnlyList<ulong> _members, ulong _cat)
        {
            var table = new Dictionary<ulong, Lobby.RoleId>(_members.Count);
            for (int i = 0; i < _members.Count; i++)
            {
                table[_members[i]] = _members[i] == _cat ? Lobby.RoleId.Cat : Lobby.RoleId.Mouse;
            }
            return table;
        }
    }
}
