using CTC.Runtime.Economy;
using NUnit.Framework;

namespace CTC.Tests
{
    public class CheeseSpotLogicTests
    {
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
            float highSeconds = CheeseSpotLogic.SPOT_RESERVE / (float)CheeseSpotLogic.MAX_GRADE;
            float lowSeconds = CheeseSpotLogic.SPOT_RESERVE / (float)CheeseSpotLogic.MIN_GRADE;
            Assert.Less(highSeconds, lowSeconds);
            Assert.AreEqual(100f, highSeconds, 1f);
        }

        [Test]
        public void TotalReserve_CoversFullTechForFourMice()
        {
            // 쥐 1마리 최종 빌드 = 기관총 600 + 요새 220 + 창고 150 = 970.
            // 맵 총량이 4마리분(3880)보다 넉넉해야 고갈이 '굶기기'가 아니라 '기한'이 된다.
            const int FULL_BUILD_PER_MOUSE = 600 + 220 + 150;
            int total = CheeseSpotLogic.TotalReserve(7);
            Assert.GreaterOrEqual(total, FULL_BUILD_PER_MOUSE * 4);
        }

        [Test]
        public void LastStand_IsSmallerThanNormalSpot()
        {
            // 부활 지점은 만회 기회지 두 번째 성장 구간이 아니다
            Assert.Less(CheeseSpotLogic.LAST_STAND_RESERVE, CheeseSpotLogic.SPOT_RESERVE);
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
