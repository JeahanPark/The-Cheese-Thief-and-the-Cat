using CTC.Runtime.Economy;
using CTC.Runtime.Map;
using NUnit.Framework;

namespace CTC.Tests
{
    public class AlcoveLogicTests
    {
        // 기준 구성 2:8 · 120×120 맵(2026-08-10) — 치즈 총량이 쥐 수에 비례한다.
        private const int MICE = 8;
        private const int SPOTS = 20;

        // ---- 벽 뒤 상자의 치즈 합계 ----

        private static AlcovePlacement Box(int _cheese) => new AlcovePlacement
        {
            Payload = AlcovePayload.Box,
            BoxContents = new System.Collections.Generic.List<LootEntry>
            {
                new LootEntry { Id = LootId.Cheese, Amount = _cheese },
            },
        };

        private static AlcovePlacement Ticket() => new AlcovePlacement
        {
            Payload = AlcovePayload.Box,
            BoxContents = new System.Collections.Generic.List<LootEntry>
            {
                new LootEntry { Id = LootId.GangTicket, Amount = 1 },
            },
        };

        [Test]
        public void TotalCheese_SumsWhatTheBoxesActuallyHold()
        {
            // 벽감 수 × 고정액이 아니다(2026-08-11) — 벽은 보상을 품지 않고 뒤에 상자가 있을 뿐이라,
            // 무엇이 얼마나 들었는지는 자리마다 다르다.
            var placements = new System.Collections.Generic.List<AlcovePlacement>
            {
                Box(250), Box(250), Ticket(),
                new AlcovePlacement { Payload = AlcovePayload.Dog },
            };
            Assert.AreEqual(500, AlcoveLogic.TotalCheese(placements));
        }

        [Test]
        public void TotalCheese_TicketWallsTakeNoCheese()
        {
            // 직업권만 든 벽이 치즈를 떼 가면, 아무도 못 받는 치즈가 총량에서 샌다
            Assert.AreEqual(0, AlcoveLogic.TotalCheese(
                new System.Collections.Generic.List<AlcovePlacement> { Ticket() }));
        }

        [Test]
        public void TotalCheese_DogWallHoldsNothing()
        {
            // 강아지 벽은 상자가 없다 — 몫을 떼면 그 치즈는 아무도 못 받는다
            Assert.AreEqual(0, AlcoveLogic.TotalCheese(
                new System.Collections.Generic.List<AlcovePlacement>
                { new AlcovePlacement { Payload = AlcovePayload.Dog } }));
            Assert.AreEqual(0, AlcoveLogic.TotalCheese(null));
        }

        [Test]
        public void CountBoxAlcoves_SkipsDogWalls()
        {
            var placements = new System.Collections.Generic.List<AlcovePlacement>
            {
                Box(250),
                new AlcovePlacement { Payload = AlcovePayload.Dog },
                Ticket(),
            };
            Assert.AreEqual(2, AlcoveLogic.CountBoxAlcoves(placements));
            Assert.AreEqual(0, AlcoveLogic.CountBoxAlcoves(null));
        }

        // ---- 총량 보존: 벽 몫 + 지점 몫 ≤ 맵 총량 ----

        [Test]
        public void ReservePerSpot_AlcoveShareComesOutOfMapTotal()
        {
            int alcoveTotal = AlcoveLogic.TotalCheese(
                new System.Collections.Generic.List<AlcovePlacement> { Box(250), Box(250), Ticket() });
            int withAlcoves = CheeseSpotLogic.ReservePerSpot(SPOTS, alcoveTotal, MICE);
            int without = CheeseSpotLogic.ReservePerSpot(SPOTS, 0, MICE);

            // 벽이 가져간 만큼 지점 몫이 줄어야 한다 — 더해지면 성장 기한이 늘어진다
            Assert.Less(withAlcoves, without);
            Assert.LessOrEqual(withAlcoves * SPOTS + alcoveTotal, CheeseSpotLogic.MapTotal(MICE));
        }

        [Test]
        public void ReservePerSpot_NoAlcovesMatchesLegacyTotal()
        {
            // 벽감이 없으면 총량을 지점 수로 나눈 값 그대로여야 한다(회귀)
            Assert.AreEqual(CheeseSpotLogic.MapTotal(MICE) / SPOTS,
                CheeseSpotLogic.ReservePerSpot(SPOTS, 0, MICE));
        }

        [Test]
        public void ReservePerSpot_ZeroSpotsIsSafe()
        {
            Assert.AreEqual(0, CheeseSpotLogic.ReservePerSpot(0, 100, MICE));
        }

        [Test]
        public void ReservePerSpot_AlcoveShareOverTotalDoesNotGoNegative()
        {
            // 배치 실수로 벽이 총량보다 많이 가져가도 지점 매장량이 음수가 되면 안 된다
            int absurd = CheeseSpotLogic.MapTotal(MICE) * 2;
            Assert.GreaterOrEqual(CheeseSpotLogic.ReservePerSpot(SPOTS, absurd, MICE), 0);
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
