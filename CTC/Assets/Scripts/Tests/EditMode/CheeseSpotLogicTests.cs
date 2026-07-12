using CTC.Runtime.Economy;
using NUnit.Framework;

namespace CTC.Tests
{
    public class CheeseSpotLogicTests
    {
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
