using CTC.Runtime.Economy;
using NUnit.Framework;

namespace CTC.Tests
{
    public class CheeseSpotLogicTests
    {
        // 기준 구성 2:8 · 120×120 맵(2026-08-10). 총량이 쥐 수에 비례하므로
        // 매장량을 재는 테스트는 인원을 함께 적어야 뜻이 통한다.
        private const int MICE = 8;
        private const int SPOTS = 20;

        [Test]
        public void Occupancy_FreeWhenNoEarner()
        {
            Assert.AreEqual(CheeseSpotLogic.Occupancy.Free,
                CheeseSpotLogic.OccupancyOf(CheeseSpotLogic.NO_EARNER, 3));
        }

        [Test]
        public void Occupancy_MineVsOthers()
        {
            Assert.AreEqual(CheeseSpotLogic.Occupancy.Mine, CheeseSpotLogic.OccupancyOf(3, 3));
            Assert.AreEqual(CheeseSpotLogic.Occupancy.Others, CheeseSpotLogic.OccupancyOf(5, 3));
        }

        [Test]
        public void EntryOrder_KeepsArrivalSequence()
        {
            var order = new System.Collections.Generic.List<ulong>();
            CheeseSpotLogic.UpdateEntryOrder(order, new ulong[] { 7 });
            CheeseSpotLogic.UpdateEntryOrder(order, new ulong[] { 2, 7 }); // 물리 검사 순서가 뒤집혀도
            CollectionAssert.AreEqual(new ulong[] { 7, 2 }, order);        // 먼저 온 7이 앞
        }

        [Test]
        public void EntryOrder_DropsWhoLeft()
        {
            var order = new System.Collections.Generic.List<ulong>();
            CheeseSpotLogic.UpdateEntryOrder(order, new ulong[] { 1, 2, 3 });
            CheeseSpotLogic.UpdateEntryOrder(order, new ulong[] { 3, 1 });
            CollectionAssert.AreEqual(new ulong[] { 1, 3 }, order);
        }

        [Test]
        public void EntryOrder_FirstInBecomesEarner()
        {
            var order = new System.Collections.Generic.List<ulong>();
            CheeseSpotLogic.UpdateEntryOrder(order, new ulong[] { 9 });
            CheeseSpotLogic.UpdateEntryOrder(order, new ulong[] { 4, 9 });
            Assert.AreEqual(9UL, CheeseSpotLogic.SelectEarner(CheeseSpotLogic.NO_EARNER, order));
        }

        [Test]
        public void EntryOrder_EarnerLeaves_NextInLineTakesOver()
        {
            var order = new System.Collections.Generic.List<ulong>();
            CheeseSpotLogic.UpdateEntryOrder(order, new ulong[] { 9 });
            CheeseSpotLogic.UpdateEntryOrder(order, new ulong[] { 4, 9 });
            CheeseSpotLogic.UpdateEntryOrder(order, new ulong[] { 4 }); // 9가 나감
            Assert.AreEqual(4UL, CheeseSpotLogic.SelectEarner(9, order));
        }

        [Test]
        public void Extract_TakesRequestedWhenReserveIsEnough()
        {
            Assert.AreEqual(3, CheeseSpotLogic.Extract(700, 3));
        }

        [Test]
        public void Extract_ClampsToRemaining()
        {
            Assert.AreEqual(2, CheeseSpotLogic.Extract(2, 7));
        }

        [Test]
        public void Extract_EmptyReserveGivesNothing()
        {
            Assert.AreEqual(0, CheeseSpotLogic.Extract(0, 7));
            Assert.AreEqual(0, CheeseSpotLogic.Extract(-5, 7));
        }

        [Test]
        public void Reserve_HighGradeDrainsFasterThanLow()
        {
            // 균일 매장량의 목적 — 좋은 자리부터 마르며 플레이어를 나쁜 자리로 밀어낸다.
            // 등급 비례로 주면 전부 동시에 말라 압축이 생기지 않는다.
            int reserve = CheeseSpotLogic.ReservePerSpot(SPOTS, 0, MICE);
            float highSeconds = reserve / (float)CheeseSpotLogic.MAX_GRADE;
            float lowSeconds = reserve / (float)CheeseSpotLogic.MIN_GRADE;
            Assert.Less(highSeconds, lowSeconds);
            // 7등급 자리가 80초면 마른다 — 판(720초)의 1/9이라 좋은 자리 쟁탈이 초반에 몰린다
            Assert.AreEqual(80f, highSeconds, 2f);
        }

        [Test]
        public void ReservePerSpot_KeepsMapTotalWhenSpotsIncrease()
        {
            // 맵이 넓어져 지점이 늘어도 총량이 유지돼야 고갈 시점이 그대로다.
            // (지점당 560 고정이면 28지점에서 15680이 되어 고갈이 오지 않는다)
            int total = CheeseSpotLogic.MapTotal(MICE);
            Assert.AreEqual(total / SPOTS, CheeseSpotLogic.ReservePerSpot(SPOTS, 0, MICE));
            Assert.AreEqual(total / 28, CheeseSpotLogic.ReservePerSpot(28, 0, MICE));
            Assert.AreEqual(total, CheeseSpotLogic.TotalReserve(SPOTS, 0, MICE));
            Assert.AreEqual(total, CheeseSpotLogic.TotalReserve(28, 0, MICE));
        }

        [Test]
        public void MapTotal_ScalesWithMouseCount()
        {
            // 총량을 고정하면 8인 판의 1인당 몫이 3인 판의 1/3이 돼 아무도 강화를 못 한다(2026-08-10).
            Assert.AreEqual(CheeseSpotLogic.CheesePerMouse * 6, CheeseSpotLogic.MapTotal(6));
            Assert.AreEqual(CheeseSpotLogic.CheesePerMouse * 8, CheeseSpotLogic.MapTotal(8));
            // 배선 누락으로 0이 들어와도 빈 맵이 되면 안 된다
            Assert.AreEqual(CheeseSpotLogic.CheesePerMouse, CheeseSpotLogic.MapTotal(0));
        }

        [Test]
        public void EffectiveMiceCount_NeverBelowTheMapsDesignedHeadcount()
        {
            // 2026-08-10 실측 버그: 대형 맵(권장 6~8)에 쥐 1명이면 총량 1400인데
            // 벽감·상자에 이미 깔린 치즈가 2895라 지점 매장량이 통째로 0이 됐다.
            Assert.AreEqual(6, CheeseSpotLogic.EffectiveMiceCount(1, 6));
            Assert.AreEqual(8, CheeseSpotLogic.EffectiveMiceCount(8, 6));
            // 권장 인원이 안 적힌 맵이어도 0으로 나눠지면 안 된다
            Assert.AreEqual(1, CheeseSpotLogic.EffectiveMiceCount(0, 0));
        }

        [Test]
        public void MapTotal_CoversWhatTheMapAlreadyHoldsOnTheGround()
        {
            // 대형 맵 실측치: 벽감 5×250 + 상자 47×35 = 2895.
            // 설계 인원(6~8)으로 계산한 총량이 이걸 넉넉히 넘어야 지점이 살아남는다.
            const int PLACED_ON_MAP = 1250 + 1645;
            int total = CheeseSpotLogic.MapTotal(CheeseSpotLogic.EffectiveMiceCount(1, 6));
            Assert.Greater(total, PLACED_ON_MAP * 2, "맵에 깔린 치즈가 경제의 절반을 넘으면 파밍이 죽는다");
        }

        [Test]
        public void ReservePerSpot_ZeroSpotsIsSafe()
        {
            Assert.AreEqual(0, CheeseSpotLogic.ReservePerSpot(0, 0, MICE));
            Assert.AreEqual(0, CheeseSpotLogic.TotalReserve(0, 0, MICE));
        }

        [Test]
        public void TotalReserve_CoversFullTechForEveryMouse()
        {
            // 쥐 1마리 최종 빌드 = 기관총 600 + 요새 220 + 창고 150 = 970.
            // 인원이 몇이든 1인당 몫이 이걸 넘어야 고갈이 '굶기기'가 아니라 '기한'이 된다.
            const int FULL_BUILD_PER_MOUSE = 600 + 220 + 150;
            foreach (int mice in new[] { 6, 8 })
            {
                Assert.GreaterOrEqual(CheeseSpotLogic.TotalReserve(SPOTS, 0, mice),
                    FULL_BUILD_PER_MOUSE * mice, $"쥐 {mice}명");
            }
        }

        [Test]
        public void LastStand_IsSmallerThanNormalSpot()
        {
            // 부활 지점은 만회 기회지 두 번째 성장 구간이 아니다
            Assert.Less(CheeseSpotLogic.LAST_STAND_RESERVE, CheeseSpotLogic.ReservePerSpot(SPOTS, 0, MICE));
        }

        [Test]
        public void IncomePerSecond_EqualsGrade()
        {
            Assert.AreEqual(3, CheeseSpotLogic.IncomePerSecond(3));
        }

        [Test]
        public void IncomePerSecond_ClampsBelowMin()
        {
            Assert.AreEqual(1, CheeseSpotLogic.IncomePerSecond(0));
        }

        [Test]
        public void IncomePerSecond_ClampsAboveMax()
        {
            Assert.AreEqual(7, CheeseSpotLogic.IncomePerSecond(99));
        }

        [Test]
        public void SelectEarner_Empty_ReturnsNoEarner()
        {
            Assert.AreEqual(CheeseSpotLogic.NO_EARNER,
                CheeseSpotLogic.SelectEarner(CheeseSpotLogic.NO_EARNER, new ulong[0]));
        }

        [Test]
        public void SelectEarner_NoCurrent_PicksFirst()
        {
            Assert.AreEqual(5UL, CheeseSpotLogic.SelectEarner(CheeseSpotLogic.NO_EARNER, new ulong[] { 5, 7 }));
        }

        [Test]
        public void SelectEarner_CurrentStillPresent_Retained()
        {
            // 지점당 1마리 — 나중에 온 쥐(5)가 있어도 선점자(7) 유지
            Assert.AreEqual(7UL, CheeseSpotLogic.SelectEarner(7UL, new ulong[] { 5, 7 }));
        }

        [Test]
        public void SelectEarner_CurrentLeft_PicksFirstRemaining()
        {
            Assert.AreEqual(5UL, CheeseSpotLogic.SelectEarner(7UL, new ulong[] { 5 }));
        }

        [Test]
        public void Accumulate_BelowOne_NoPayout()
        {
            float rem = CheeseSpotLogic.Accumulate(0f, 1, 0.4f, out int whole);
            Assert.AreEqual(0, whole);
            Assert.AreEqual(0.4f, rem, 1e-4f);
        }

        [Test]
        public void Accumulate_CrossesOne_PaysAndKeepsRemainder()
        {
            float rem = CheeseSpotLogic.Accumulate(0.4f, 1, 0.7f, out int whole);
            Assert.AreEqual(1, whole);
            Assert.AreEqual(0.1f, rem, 1e-4f);
        }

        [Test]
        public void Accumulate_HighGrade_PaysProportionally()
        {
            // 등급 7, 1초 → 7치즈
            float rem = CheeseSpotLogic.Accumulate(0f, 7, 1f, out int whole);
            Assert.AreEqual(7, whole);
            Assert.AreEqual(0f, rem, 1e-4f);
        }
    }
}
