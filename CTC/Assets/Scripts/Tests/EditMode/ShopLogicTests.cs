using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests
{
    /// <summary>
    /// 카탈로그(무엇을 파는가·얼마인가)는 테크 v3에서 <see cref="TechLogic"/>·`TechInfo.csv`로 옮겼다
    /// (2026-09-07) — 그쪽 판정은 `TechLogicTests`·`TechInfoTests`에 있다.
    /// 여기 남은 건 표에 안 내리는 구조 수치(스택 상한·창고 파생값)와 무기 티어 조회뿐이다.
    /// </summary>
    public class ShopLogicTests
    {
        [Test]
        public void CanBuy_EnoughCheese_True()
        {
            Assert.IsTrue(ShopLogic.CanBuy(150, 150));
        }

        [Test]
        public void CanBuy_NotEnough_False()
        {
            Assert.IsFalse(ShopLogic.CanBuy(149, 150));
        }

        [Test]
        public void PounceDamage_StacksAdd10Each()
        {
            Assert.AreEqual(60, ShopLogic.PounceDamage(60, 0));
            Assert.AreEqual(90, ShopLogic.PounceDamage(60, 3));
        }

        [Test]
        public void WarehouseTick_DerivesFromIncomeAndChunk()
        {
            // 10개씩 초당 5 → 2초마다 1회. 연출이 너무 잦거나 뜸하지 않은 구간이어야 한다.
            Assert.AreEqual(2f, ShopLogic.WarehouseTickSeconds, 1e-4f);
            Assert.GreaterOrEqual(ShopLogic.WarehouseTickSeconds, 2f);
            Assert.LessOrEqual(ShopLogic.WarehouseTickSeconds, 12f);
        }

        [Test]
        public void WarehouseTick_KeepsTotalIncomeIntact()
        {
            // 덩어리 지급으로 바꿔도 총 수입은 초당 값 그대로여야 한다
            float perSecond = ShopLogic.WAREHOUSE_TICK_CHEESE / ShopLogic.WarehouseTickSeconds;
            Assert.AreEqual(ShopLogic.WAREHOUSE_INCOME_PER_SECOND, perSecond, 1e-4f);
        }

        [Test]
        public void CurrencyName_IsRoleSpecific() // 고양이에게 "치즈 부족"이라 하던 버그
        {
            Assert.AreEqual("장난기", ShopLogic.CurrencyNameOf(RoleId.Cat));
            Assert.AreEqual("치즈", ShopLogic.CurrencyNameOf(RoleId.Mouse));
        }

        [Test]
        public void MoveSpeed_StacksAdd02Each() // 밸런스 2026-07-26: 기본 6.4 상향 + 스택 0.2 하향
        {
            // 순수 수학이라 상한과 무관하다. 실제 상한은 3레벨(테크 v3) — 만렙 7.0이다.
            Assert.AreEqual(6.4f, ShopLogic.MoveSpeed(6.4f, 0), 1e-4f);
            Assert.AreEqual(7.0f, ShopLogic.MoveSpeed(6.4f, 3), 1e-4f);
        }

        [Test]
        public void CanAddStack_BelowCap_True()
        {
            // 상한 3 = 테크 v3 발톱·발 구르기 최대 레벨(2026-09-07)
            Assert.IsTrue(ShopLogic.CanAddStack(2));
            Assert.IsFalse(ShopLogic.CanAddStack(3));
        }

        [Test]
        public void WeaponStats_Tier0_IsPopgun()
        {
            WeaponStats s = ShopLogic.WeaponStatsForTier(0);
            Assert.AreEqual(5, s.Damage);
            Assert.AreEqual(0.8f, s.CooldownSeconds, 1e-4f);
            Assert.AreEqual(8f, s.Range, 1e-4f);
        }

        [Test]
        public void WeaponStats_Tier1_IsSlingshot()
        {
            WeaponStats s = ShopLogic.WeaponStatsForTier(1);
            Assert.AreEqual(12, s.Damage);
            Assert.AreEqual(10f, s.Range, 1e-4f);
        }

        [Test]
        public void WeaponStats_Tier2_IsMachineGun()
        {
            WeaponStats s = ShopLogic.WeaponStatsForTier(2);
            Assert.AreEqual(5, s.Damage);
            Assert.AreEqual(0.15f, s.CooldownSeconds, 1e-4f);
        }

        [Test]
        public void WeaponStats_OutOfRange_Clamps()
        {
            Assert.AreEqual(ShopLogic.WeaponStatsForTier(3).Damage, ShopLogic.WeaponStatsForTier(99).Damage);
            Assert.AreEqual(ShopLogic.WeaponStatsForTier(0).Damage, ShopLogic.WeaponStatsForTier(-1).Damage);
        }

        [Test]
        public void Balance_MaxUpgradeStillTakesTwoPouncesToDropAMouse()
        {
            // 강화를 싸게 만든 대가로 만렙 덮치기가 원샷이 되면 안 된다 —
            // 원샷은 불 전직(×2)의 값어치다(2026-08-10). 테크 v3 발톱 3레벨 기준(2026-09-07).
            int maxPounce = 60 + ShopLogic.POUNCE_DAMAGE_PER_STACK * ShopLogic.MAX_UPGRADE_STACKS;
            Assert.Less(maxPounce, 200, "쥐 100HP를 한 방에 잡으면 전직할 이유가 없다");
            Assert.Greater(maxPounce, 80, "만렙인데 강화 전과 같으면 강화가 무의미하다");
        }
    }
}
