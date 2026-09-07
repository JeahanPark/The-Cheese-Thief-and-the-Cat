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
            CheeseShieldLogic.Absorb(100, 0.3f, 3, 1000, out int hp, out int cheese);
            Assert.AreEqual(70, hp);
            Assert.AreEqual(90, cheese);
        }

        [Test]
        public void Absorb_NotEnoughCheese_PaysWhatItCan_RestToHp()
        {
            // 치즈 30뿐 → 10 HP 분만 치즈로, 나머지 90은 HP
            CheeseShieldLogic.Absorb(100, 0.3f, 3, 30, out int hp, out int cheese);
            Assert.AreEqual(90, hp);
            Assert.AreEqual(30, cheese);
        }

        [Test]
        public void Absorb_NoLevel_PassThrough()
        {
            CheeseShieldLogic.Absorb(50, 0f, 3, 999, out int hp, out int cheese);
            Assert.AreEqual(50, hp);
            Assert.AreEqual(0, cheese);
        }

        [Test]
        public void Absorb_ZeroCheese_PassThrough()
        {
            CheeseShieldLogic.Absorb(50, 0.6f, 3, 0, out int hp, out int cheese);
            Assert.AreEqual(50, hp);
            Assert.AreEqual(0, cheese);
        }
    }
}
