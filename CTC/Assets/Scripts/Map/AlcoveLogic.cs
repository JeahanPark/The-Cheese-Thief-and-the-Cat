namespace CTC.Runtime.Map
{
    /// <summary>
    /// 벽감(부술 수 있는 벽) 뒤에 있는 것의 치즈 결산. MonoBehaviour와 분리된 순수 함수(EditMode TDD).
    ///
    /// **벽 몫은 맵 총량 안에서 뗀다.** 그냥 더하면 성장 기한(고갈)이 늘어져
    /// 후반 압축이 무너진다 — 총량을 지점 수로 나누는 구조가 고갈 시점을 고정하기 때문이다.
    /// 여기서는 "벽이 가져갈 총액"만 계산하고, 빼는 일은 <see cref="Economy.CheeseSpotLogic"/>이 한다.
    /// 치즈 지점은 벽감의 존재를 몰라도 된다.
    /// </summary>
    public static class AlcoveLogic
    {
        // 수치 원본은 CSV(BalanceInfo.csv)
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;

        /// <summary>벽 뒤 상자에 넣을 치즈 기본값. 창고 1채 값 — 달려올 가치가 있어야 경쟁이 생긴다.</summary>
        public static int CheesePerAlcove => Balance.AlcoveCheese;

        /// <summary>벽감 HP — 딱총 19초 · 새총 8초 · 기관총 3.6초 · 덮치기 5초.</summary>
        public static int AlcoveHp => Balance.AlcoveHp;

        /// <summary>
        /// 벽 뒤 상자들이 품은 치즈 합계 (2026-08-11).
        ///
        /// 예전엔 벽감 수 × 고정액이었다. 지금은 벽이 보상을 품지 않고 **뒤에 상자가 있을 뿐**이라,
        /// 무엇이 얼마나 들었는지는 자리마다 다르다 — 직업권만 든 벽은 치즈를 0 가져간다.
        /// 세는 자리는 여기 하나여야 한다. 안 그러면 아무도 못 받는 치즈가 총량에서 새어 나간다.
        /// </summary>
        public static int TotalCheese(System.Collections.Generic.IReadOnlyList<AlcovePlacement> _placements)
        {
            if (_placements == null) return 0;

            int sum = 0;
            for (int i = 0; i < _placements.Count; i++)
            {
                if (_placements[i].Payload != AlcovePayload.Box) continue;
                sum += LootBoxLogic.CheeseIn(_placements[i].BoxContents);
            }
            return sum;
        }

        /// <summary>상자를 막고 있는 벽 수 — 강아지 벽은 빼고 센다(QA·표시용).</summary>
        public static int CountBoxAlcoves(System.Collections.Generic.IReadOnlyList<AlcovePlacement> _placements)
        {
            if (_placements == null) return 0;

            int count = 0;
            for (int i = 0; i < _placements.Count; i++)
            {
                if (_placements[i].Payload == AlcovePayload.Box) count++;
            }
            return count;
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
