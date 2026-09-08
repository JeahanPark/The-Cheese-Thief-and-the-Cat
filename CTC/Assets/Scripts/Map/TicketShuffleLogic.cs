using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 직업권 자리 셔플 (테크 v3 계획 3 Task 4, 2026-09-08 · 기획서 §7-3).
    ///
    /// 깡패권·용사권이 든 자리가 판마다 달라진다 — "어느 벽을 뚫을지"가 외운 답이 되지 않게.
    ///
    /// **내용물을 통째로 교환한다.** 항목을 새로 만들거나 지우지 않는 이유:
    /// 맵 총 치즈량(<see cref="LootBoxLogic.TotalCheese"/>)이 지점 매장량에서 **이미 떼어져 있어서**,
    /// 하나라도 늘거나 줄면 고갈 시점이 밀려 후반 압축이 무너진다(벽감과 같은 이유).
    /// 순열은 자리만 바꾸므로 총량이 정의상 불변이다.
    /// </summary>
    public static class TicketShuffleLogic
    {
        /// <summary>
        /// 0..n−1의 순열 — 같은 시드면 같은 순열이다(서버·클라가 이걸로 만난다).
        /// <c>ShopSpotLogic.Shuffled</c>와 같은 Fisher-Yates·<c>System.Random</c>이라 플랫폼이 달라도 같다.
        /// </summary>
        public static int[] Permutation(int _count, int _seed)
        {
            if (_count <= 0) return new int[0];

            var order = new int[_count];
            for (int i = 0; i < _count; i++) order[i] = i;
            if (_count == 1) return order;

            var rng = new System.Random(SafeSeed(_seed));
            for (int i = _count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        /// <summary>
        /// 벽감 자리 i에 **어느 자리의 내용물**을 놓을지(<c>order[i]</c>) — 감사 M2·M3를 함께 푼다 (2026-09-09).
        ///
        /// 전 자리를 통째로 순열하던 예전 방식이 두 가지를 깼다:
        /// <list type="number">
        /// <item>**물건이 사라졌다(M2·P0-2).** 강아지 벽감은 상자가 없어 <c>BoxContents</c>가 빈 목록인데
        /// 그것도 섞였다. 순열이 강아지 자리를 안 건드릴 확률은 1/6뿐이라, 매 판 5/6로 어떤 상자 벽이
        /// 빈 손을 받고 그 물건(치즈 250 또는 직업권 1장)은 강아지 벽으로 가 **영영 안 나왔다**.</item>
        /// <item>**직업권 둘이 붙었다(M3·P0-9).** 굽기(<c>AlcoveFactory.FillBoxContents</c>)가 둘을
        /// 서로 가장 먼 두 벽에 나눠 두는 이유는 "나란히 두면 한 사람이 둘 다 먹는다"인데,
        /// 무작위 순열이 그 배치를 흩었다.</item>
        /// </list>
        ///
        /// 그래서 규칙이 셋이다:
        /// <list type="bullet">
        /// <item>**강아지 벽감은 섞지 않는다** — 제 내용물(빈 목록)을 그대로 가진다.</item>
        /// <item>직업권 한 장을 **아무 상자 벽**에 놓고, 다른 한 장은 **그 벽에서 가장 먼 상자 벽**에 놓는다.
        /// 판마다 어느 벽인지는 달라지는데(계획 3의 의도) 둘이 붙는 일은 정의상 없다(굽기의 의도).</item>
        /// <item>나머지 상자 벽끼리는 자유롭게 섞는다 — 자리만 바꾸므로 총 치즈량이 불변이다.</item>
        /// </list>
        ///
        /// 시드가 0(로비·봇 실험실)이면 **항등**이라 구운 그대로 선다.
        /// </summary>
        public static int[] ContentOrder(IReadOnlyList<AlcovePlacement> _placements, int _seed)
        {
            int count = _placements == null ? 0 : _placements.Count;
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            if (count <= 1 || _seed == 0) return order;

            // 상자 벽만 섞는다 — 강아지 벽은 위 order[i] = i 그대로 남는다.
            var boxes = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (_placements[i].Payload == AlcovePayload.Box) boxes.Add(i);
            }
            if (boxes.Count <= 1) return order;

            var rng = new System.Random(SafeSeed(_seed));

            // 직업권이 든 자리(굽기가 정확히 둘을 넣는다). 수가 둘이 아니면 최원거리 규칙이 성립하지 않으므로
            // 상자끼리의 단순 섞기로 내려간다 — 그래도 M2(물건 증발)는 안 난다.
            var tickets = new List<int>();
            var plain = new List<int>();
            foreach (int i in boxes)
            {
                if (HasTicket(_placements[i].BoxContents)) tickets.Add(i);
                else plain.Add(i);
            }
            if (tickets.Count != 2)
            {
                AssignShuffled(order, boxes, boxes, rng);
                return order;
            }

            // 직업권이 앉을 두 자리: 아무 상자 벽 + 그 벽에서 가장 먼 상자 벽
            int anchor = boxes[rng.Next(boxes.Count)];
            int partner = FarthestFrom(anchor, boxes, _placements);
            if (rng.Next(2) == 1) (tickets[0], tickets[1]) = (tickets[1], tickets[0]);
            order[anchor] = tickets[0];
            order[partner] = tickets[1];

            // 남은 상자 벽에는 남은 내용물을 섞어 넣는다
            var restSlots = new List<int>();
            foreach (int i in boxes)
            {
                if (i != anchor && i != partner) restSlots.Add(i);
            }
            AssignShuffled(order, restSlots, plain, rng);
            return order;
        }

        /// <summary>직업권(치즈가 아닌 물건)이 든 상자인가.</summary>
        public static bool HasTicket(IReadOnlyList<LootEntry> _contents)
        {
            if (_contents == null) return false;
            for (int i = 0; i < _contents.Count; i++)
            {
                if (_contents[i].Id != LootId.Cheese) return true;
            }
            return false;
        }

        /// <summary><paramref name="_from"/>에서 가장 먼 후보. 후보가 하나뿐이면 그 자신.</summary>
        private static int FarthestFrom(int _from, List<int> _candidates, IReadOnlyList<AlcovePlacement> _placements)
        {
            int best = _from;
            float bestSqr = -1f;
            foreach (int i in _candidates)
            {
                if (i == _from) continue;
                float sqr = (_placements[i].Pos - _placements[_from].Pos).sqrMagnitude;
                if (sqr <= bestSqr) continue;
                bestSqr = sqr; best = i;
            }
            return best;
        }

        // 자리 목록에 내용물 목록을 섞어 1:1로 꽂는다 — 개수가 같아야 한다(호출자가 보장).
        private static void AssignShuffled(int[] _order, List<int> _slots, List<int> _sources, System.Random _rng)
        {
            var pool = new List<int>(_sources);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            for (int i = 0; i < _slots.Count && i < pool.Count; i++) _order[_slots[i]] = pool[i];
        }

        // System.Random은 int.MinValue를 못 받는 구현이 있다 — 시드는 해시라 실제로 나올 수 있는 값이다.
        private static int SafeSeed(int _seed) => _seed == int.MinValue ? int.MaxValue : _seed;
    }
}
