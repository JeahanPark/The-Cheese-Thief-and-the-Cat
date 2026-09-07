using CTC.Runtime.Economy;
using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void StructureFun_BaseLevelFortress_SameAsAccruedFun() // Lv1은 그대로다
        {
            int hp = ShopLogic.FORTRESS_HP;
            Assert.AreEqual(FunLogic.AccruedFun(hp, false, true), FunLogic.StructureFun(hp, hp, hp));
        }

        [Test]
        public void StructureFun_Lv3Fortress_EarnsSameAsLv1() // 요새를 올려도 고양이 수입은 그대로
        {
            int baseHp = ShopLogic.FORTRESS_HP;
            int lv3Hp = Mathf.RoundToInt(TechLogic.ParamOf(TechId.Fortress, 3, 1));
            Assert.Greater(lv3Hp, baseHp, "Lv3 요새 HP가 기본보다 커야 이 테스트가 뜻을 가진다");
            Assert.AreEqual(FunLogic.StructureFun(baseHp, baseHp, baseHp),
                FunLogic.StructureFun(lv3Hp, lv3Hp, baseHp));
        }

        [Test]
        public void StructureFun_NonFortress_Unchanged() // 레벨 없는 구조물은 기본=최대라 그대로다
        {
            int hp = ShopLogic.WAREHOUSE_MAX_HP;
            Assert.AreEqual(FunLogic.AccruedFun(hp, false, true), FunLogic.StructureFun(hp, hp, hp));
        }
    }
}
