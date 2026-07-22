using CTC.Runtime.Economy;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    public class FunLogicTests
    {
        [Test]
        public void AccruedFun_DamageOnly_OneToOne()
        {
            Assert.AreEqual(60, FunLogic.AccruedFun(60, false));
        }

        [Test]
        public void AccruedFun_PlayerKill_AddsBonus20()
        {
            Assert.AreEqual(60 + 20, FunLogic.AccruedFun(60, true));
        }

        [Test]
        public void AccruedFun_ZeroDamage_NoAccrual()
        {
            Assert.AreEqual(0, FunLogic.AccruedFun(0, false));
        }

        [Test]
        public void AccruedFun_NegativeDamage_ClampedToZero()
        {
            Assert.AreEqual(0, FunLogic.AccruedFun(-5, false));
        }

        [Test]
        public void AccruedFun_StructureKill_NoBonus() // 요새·미끼 파괴: killedPlayer=false로 호출됨
        {
            Assert.AreEqual(40, FunLogic.AccruedFun(40, false));
        }
    }
}
