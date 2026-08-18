using CTC.Runtime.Economy;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    public class FunLogicTests
    {
        [Test]
        public void AccruedFun_PlayerDamage_HalfRate() // v3.6: 1:1 → 0.5:1
        {
            Assert.AreEqual(30, FunLogic.AccruedFun(60, false, false));
        }

        [Test]
        public void AccruedFun_PlayerKill_AddsBonus30()
        {
            Assert.AreEqual(30 + FunLogic.KILL_BONUS, FunLogic.AccruedFun(60, true, false));
        }

        [Test]
        public void AccruedFun_ZeroDamage_NoAccrual()
        {
            Assert.AreEqual(0, FunLogic.AccruedFun(0, false, false));
        }

        [Test]
        public void AccruedFun_NegativeDamage_ClampedToZero()
        {
            Assert.AreEqual(0, FunLogic.AccruedFun(-5, false, false));
        }

        [Test]
        public void AccruedFun_Structure_QuarterRate()
        {
            // 밸런스 수치(요새 HP)를 여기 박지 않는다 — 그 값은 CSV라 바뀐다(2026-08-18에 180→480).
            // 여기서 못 박을 것은 **규칙**이다: 구조물 피해는 나눗셈만 하고 킬 보너스가 없다.
            Assert.AreEqual(ShopLogic.FORTRESS_HP / FunLogic.STRUCTURE_DAMAGE_DIVISOR,
                FunLogic.AccruedFun(ShopLogic.FORTRESS_HP, false, true));
        }

        [Test]
        public void AccruedFun_Structure_IgnoresKillBonus() // 킬 보너스는 플레이어 전용
        {
            Assert.AreEqual(ShopLogic.WAREHOUSE_MAX_HP / 4, FunLogic.AccruedFun(ShopLogic.WAREHOUSE_MAX_HP, true, true));
        }

        [Test]
        public void AccruedFun_StructureIsCheaperThanPlayer() // 벽 때리기가 사냥보다 낫지 않아야 한다
        {
            Assert.Less(FunLogic.AccruedFun(100, false, true), FunLogic.AccruedFun(100, false, false));
        }

        [Test]
        public void AccruedFun_OddDamage_RoundsDown() // 정수 유지 — 잔돈은 버린다
        {
            Assert.AreEqual(2, FunLogic.AccruedFun(5, false, false));
        }
    }
}
