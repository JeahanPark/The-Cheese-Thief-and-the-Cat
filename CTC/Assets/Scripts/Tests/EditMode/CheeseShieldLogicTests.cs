using CTC.Runtime.Combat;
using NUnit.Framework;

namespace CTC.Tests
{
    public class CheeseShieldLogicTests
    {
        [Test]
        public void Absorb_Ratio30_Rate3_SplitsDamage()
        {
            // 100 피해 · 30% → HP 70, 치즈 30 × 3 = 90
            CheeseShieldLogic.Absorb(100, 0.3f, 3, 1000, 500, out int hp, out int cheese);
            Assert.AreEqual(70, hp);
            Assert.AreEqual(90, cheese);
        }

        [Test]
        public void Absorb_NotEnoughCheese_PaysWhatItCan_RestToHp()
        {
            // 치즈 30뿐 → 10 HP 분만 치즈로, 나머지 90은 HP
            CheeseShieldLogic.Absorb(100, 0.3f, 3, 30, 500, out int hp, out int cheese);
            Assert.AreEqual(90, hp);
            Assert.AreEqual(30, cheese);
        }

        [Test]
        public void Absorb_NoLevel_PassThrough()
        {
            CheeseShieldLogic.Absorb(50, 0f, 3, 999, 500, out int hp, out int cheese);
            Assert.AreEqual(50, hp);
            Assert.AreEqual(0, cheese);
        }

        [Test]
        public void Absorb_ZeroCheese_PassThrough()
        {
            CheeseShieldLogic.Absorb(50, 0.6f, 3, 0, 500, out int hp, out int cheese);
            Assert.AreEqual(50, hp);
            Assert.AreEqual(0, cheese);
        }

        [Test]
        public void Absorb_LethalEvenAfterAbsorb_PaysNothing()
        {
            // 남은 HP 50에 100 피해 — 30을 막아도 70이라 어차피 죽는다. 치즈를 받지 않는다.
            CheeseShieldLogic.Absorb(100, 0.3f, 3, 1000, 50, out int hp, out int cheese);
            Assert.AreEqual(100, hp);
            Assert.AreEqual(0, cheese);
        }

        [Test]
        public void Absorb_RatioOverOne_ClampsToDamage()
        {
            // 비율 1.5여도 100 피해에 100 HP분(=300치즈)까지만 받는다 — 맞지 않은 몫은 못 판다.
            CheeseShieldLogic.Absorb(100, 1.5f, 3, 10000, 500, out int hp, out int cheese);
            Assert.AreEqual(0, hp);
            Assert.AreEqual(300, cheese);
        }
    }
}
