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

        // System.Random은 int.MinValue를 못 받는 구현이 있다 — 시드는 해시라 실제로 나올 수 있는 값이다.
        private static int SafeSeed(int _seed) => _seed == int.MinValue ? int.MaxValue : _seed;
    }
}
