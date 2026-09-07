using CTC.Runtime.Player;
using NUnit.Framework;

namespace CTC.Tests
{
    public class CatFrenzyLogicTests
    {
        [Test]
        public void CanUse_NeedsLevelAndCooldown()
        {
            Assert.IsFalse(CatFrenzyLogic.CanUse(0, 0, 100, false));     // 미보유
            Assert.IsFalse(CatFrenzyLogic.CanUse(1, 101, 100, false));   // 쿨
            Assert.IsFalse(CatFrenzyLogic.CanUse(1, 0, 100, true));      // 이미 켜짐
            Assert.IsTrue(CatFrenzyLogic.CanUse(1, 100, 100, false));    // 쿨이 방금 끝났다
        }

        [Test]
        public void Multiplier_OffIsOne()
        {
            Assert.AreEqual(1f, CatFrenzyLogic.PounceMultiplier(false, 2.5f), 1e-4f);
            Assert.AreEqual(2.5f, CatFrenzyLogic.PounceMultiplier(true, 2.5f), 1e-4f);
            Assert.AreEqual(1f, CatFrenzyLogic.PounceMultiplier(true, 0f), 1e-4f);   // 데이터 사고
        }
    }
}
