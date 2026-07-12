using CTC.Runtime.Economy;
using NUnit.Framework;

namespace CTC.Tests
{
    public class SurvivalLogicTests
    {
        [Test]
        public void UpkeepSpend_CappedAtBalance()
        {
            Assert.AreEqual(10, SurvivalLogic.UpkeepSpend(30));
            Assert.AreEqual(7, SurvivalLogic.UpkeepSpend(7));
            Assert.AreEqual(0, SurvivalLogic.UpkeepSpend(0));
        }

        [Test]
        public void StarvationDamage_OnlyWhenShort()
        {
            Assert.AreEqual(0, SurvivalLogic.StarvationDamage(10));
            Assert.AreEqual(10, SurvivalLogic.StarvationDamage(9));
            Assert.AreEqual(10, SurvivalLogic.StarvationDamage(0));
        }
    }
}
