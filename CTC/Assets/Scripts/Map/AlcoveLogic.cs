namespace CTC.Runtime.Map
{
    /// <summary>
    /// 벽감(부술 수 있는 벽)의 치즈 배분 규칙. MonoBehaviour와 분리된 순수 함수(EditMode TDD).
    ///
    /// **벽 몫은 맵 총량 4900 안에서 뗀다.** 그냥 더하면 성장 기한(고갈)이 늘어져
    /// 후반 압축이 무너진다 — 총량을 지점 수로 나누는 구조가 고갈 시점을 고정하기 때문이다.
    /// 여기서는 "벽이 가져갈 총액"만 계산하고, 빼는 일은 <see cref="Economy.CheeseSpotLogic"/>이 한다.
    /// 치즈 지점은 벽감의 존재를 몰라도 된다.
    /// </summary>
    public static class AlcoveLogic
    {
        // 수치 원본은 CSV(BalanceInfo.csv)
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;

        /// <summary>벽감 하나가 품는 치즈. 창고 1채 값 — 달려올 가치가 있어야 경쟁이 생긴다.</summary>
        public static int CheesePerAlcove => Balance.AlcoveCheese;

        /// <summary>벽감이 쏟아 놓는 덩이 수. 하나면 부순 쥐가 독식해 경쟁이 안 생긴다.</summary>
        public static int DropCount => Balance.AlcoveDropCount;

        /// <summary>벽감 HP — 딱총 19초 · 새총 8초 · 기관총 3.6초 · 덮치기 5초.</summary>
        public static int AlcoveHp => Balance.AlcoveHp;

        /// <summary>맵의 벽감 전부가 가져가는 치즈 총액.</summary>
        public static int TotalCheese(int _alcoveCount)
        {
            return _alcoveCount <= 0 ? 0 : _alcoveCount * CheesePerAlcove;
        }

        /// <summary>
        /// 벽감 하나의 치즈를 바닥 덩이로 쪼갠다. **합계는 원본과 같아야 한다** —
        /// 나머지를 버리면 그만큼 맵 총량이 새어 고갈 시점이 밀린다.
        /// 나누어떨어지지 않으면 앞쪽 덩이부터 1씩 더 얹는다.
        /// 치즈보다 덩이가 많으면 덩이 수를 줄인다 — 빈 덩이를 바닥에 놓지 않는다.
        /// </summary>
        public static int[] SplitDrop(int _total, int _count)
        {
            if (_total <= 0 || _count <= 0) return System.Array.Empty<int>();

            int piles = _count > _total ? _total : _count;
            var parts = new int[piles];
            int each = _total / piles;
            int remainder = _total - each * piles;

            for (int i = 0; i < piles; i++)
            {
                parts[i] = each + (i < remainder ? 1 : 0);
            }
            return parts;
        }
    }
}
