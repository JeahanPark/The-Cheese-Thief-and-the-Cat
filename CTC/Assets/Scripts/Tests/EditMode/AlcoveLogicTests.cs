using CTC.Runtime.Economy;
using CTC.Runtime.Map;
using NUnit.Framework;

namespace CTC.Tests
{
    public class AlcoveLogicTests
    {
        // ---- 벽 몫 합계 ----

        [Test]
        public void TotalCheese_ScalesWithCount()
        {
            int one = AlcoveLogic.TotalCheese(1);
            Assert.Greater(one, 0);
            Assert.AreEqual(one * 3, AlcoveLogic.TotalCheese(3));
        }

        [Test]
        public void TotalCheese_NoAlcovesIsZero()
        {
            Assert.AreEqual(0, AlcoveLogic.TotalCheese(0));
            Assert.AreEqual(0, AlcoveLogic.TotalCheese(-2));
        }

        // ---- 강아지 벽감은 치즈를 안 준다 (v3.8 5번) ----

        [Test]
        public void CountCheese_SkipsDogAlcoves()
        {
            // 강아지 몫까지 떼면 그 치즈는 **아무도 못 받고** 맵 총량 4900이 샌다.
            var placements = new System.Collections.Generic.List<AlcovePlacement>
            {
                new AlcovePlacement { Payload = AlcovePayload.Cheese },
                new AlcovePlacement { Payload = AlcovePayload.Dog },
                new AlcovePlacement { Payload = AlcovePayload.Cheese },
            };

            Assert.AreEqual(2, AlcoveLogic.CountCheese(placements));
            Assert.AreEqual(AlcoveLogic.TotalCheese(2), AlcoveLogic.TotalCheese(AlcoveLogic.CountCheese(placements)));
        }

        [Test]
        public void CountCheese_NullOrEmptyIsZero()
        {
            Assert.AreEqual(0, AlcoveLogic.CountCheese(null));
            Assert.AreEqual(0, AlcoveLogic.CountCheese(new System.Collections.Generic.List<AlcovePlacement>()));
        }

        // ---- 총량 보존: 벽 몫 + 지점 몫 ≤ 맵 총량 ----

        [Test]
        public void ReservePerSpot_AlcoveShareComesOutOfMapTotal()
        {
            int alcoveTotal = AlcoveLogic.TotalCheese(3);
            int withAlcoves = CheeseSpotLogic.ReservePerSpot(7, alcoveTotal);
            int without = CheeseSpotLogic.ReservePerSpot(7, 0);

            // 벽이 가져간 만큼 지점 몫이 줄어야 한다 — 더해지면 성장 기한이 늘어진다
            Assert.Less(withAlcoves, without);
            Assert.LessOrEqual(withAlcoves * 7 + alcoveTotal, CheeseSpotLogic.TOTAL_MAP_CHEESE);
        }

        [Test]
        public void ReservePerSpot_NoAlcovesMatchesLegacyTotal()
        {
            // 벽감이 없으면 예전과 정확히 같아야 한다(회귀)
            Assert.AreEqual(CheeseSpotLogic.TOTAL_MAP_CHEESE / 7, CheeseSpotLogic.ReservePerSpot(7, 0));
        }

        [Test]
        public void ReservePerSpot_ZeroSpotsIsSafe()
        {
            Assert.AreEqual(0, CheeseSpotLogic.ReservePerSpot(0, 100));
        }

        [Test]
        public void ReservePerSpot_AlcoveShareOverTotalDoesNotGoNegative()
        {
            // 배치 실수로 벽이 총량보다 많이 가져가도 지점 매장량이 음수가 되면 안 된다
            int absurd = CheeseSpotLogic.TOTAL_MAP_CHEESE * 2;
            Assert.GreaterOrEqual(CheeseSpotLogic.ReservePerSpot(7, absurd), 0);
        }

        // ---- 픽업 분할 ----

        [Test]
        public void SplitDrop_SumsToTotal()
        {
            int[] parts = AlcoveLogic.SplitDrop(150, 3);
            Assert.AreEqual(3, parts.Length);
            Assert.AreEqual(150, Sum(parts));
        }

        [Test]
        public void SplitDrop_EvenSplitIsEqual()
        {
            int[] parts = AlcoveLogic.SplitDrop(150, 3);
            Assert.AreEqual(50, parts[0]);
            Assert.AreEqual(50, parts[1]);
            Assert.AreEqual(50, parts[2]);
        }

        [Test]
        public void SplitDrop_RemainderIsKeptNotDropped()
        {
            // 100을 3으로 나누면 33·33·34 — 합이 100이어야 총량 설계가 안 샌다
            int[] parts = AlcoveLogic.SplitDrop(100, 3);
            Assert.AreEqual(100, Sum(parts));
            foreach (int p in parts) Assert.Greater(p, 0);
        }

        [Test]
        public void SplitDrop_MoreDropsThanCheeseGivesNoZeroPiles()
        {
            // 2치즈를 5덩이로 쪼갤 수는 없다 — 빈 덩이를 바닥에 놓지 않는다
            int[] parts = AlcoveLogic.SplitDrop(2, 5);
            Assert.AreEqual(2, Sum(parts));
            foreach (int p in parts) Assert.Greater(p, 0);
        }

        [Test]
        public void SplitDrop_DegenerateInputsAreEmpty()
        {
            Assert.AreEqual(0, AlcoveLogic.SplitDrop(0, 3).Length);
            Assert.AreEqual(0, AlcoveLogic.SplitDrop(150, 0).Length);
            Assert.AreEqual(0, AlcoveLogic.SplitDrop(-5, 3).Length);
        }

        private static int Sum(int[] _values)
        {
            int total = 0;
            foreach (int v in _values) total += v;
            return total;
        }
    }
}
