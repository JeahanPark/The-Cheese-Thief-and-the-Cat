using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests
{
    public class ShopLogicTests
    {
        [Test]
        public void Find_Slingshot_HasOriginalCost()
        {
            ShopItem item = ShopLogic.Find(ShopItemId.Slingshot);
            Assert.AreEqual(150, item.Cost);
            Assert.AreEqual(RoleId.Mouse, item.Role);
        }

        [Test]
        public void Find_CatItems_AreCatRole()
        {
            Assert.AreEqual(RoleId.Cat, ShopLogic.Find(ShopItemId.CatAttack).Role);
            Assert.AreEqual(RoleId.Cat, ShopLogic.Find(ShopItemId.CatSpeed).Role);
        }

        [Test]
        public void Find_Fortress_IsCheapInstantShield()
        {
            // 즉발 3방짜리 방패(2026-08-15) — 쫓기는 순간 살 수 있는 값이어야 한다
            Assert.AreEqual(80, ShopLogic.Find(ShopItemId.Fortress).Cost);
            Assert.AreEqual(RoleId.Mouse, ShopLogic.Find(ShopItemId.Fortress).Role);
        }

        [Test]
        public void Find_Unknown_ReturnsNone()
        {
            Assert.AreEqual(ShopItemId.None, ShopLogic.Find((ShopItemId)999).Id);
        }

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
        public void RoleCatalogIndices_MouseFiveInCatalogOrder()
        {
            // 미끼 삭제(2026-08-15) — 쥐 5칸: 새총·기관총·대시·요새·창고
            var idx = ShopLogic.RoleCatalogIndices(RoleId.Mouse);
            Assert.AreEqual(5, idx.Count);
            Assert.AreEqual(ShopItemId.Slingshot, ShopLogic.CATALOG[idx[0]].Id);
            Assert.AreEqual(ShopItemId.Fortress, ShopLogic.CATALOG[idx[3]].Id);
            Assert.AreEqual(ShopItemId.Warehouse, ShopLogic.CATALOG[idx[4]].Id);
        }

        [Test]
        public void RoleCatalogIndices_FitsHotbar()
        {
            // 카탈로그가 슬롯보다 길어지면 조용히 잘린다 — 늘릴 때 HOTBAR_SLOTS도 같이 올려야 한다
            Assert.LessOrEqual(ShopLogic.RoleCatalogIndices(RoleId.Mouse).Count, ShopLogic.HOTBAR_SLOTS);
            Assert.LessOrEqual(ShopLogic.RoleCatalogIndices(RoleId.Cat).Count, ShopLogic.HOTBAR_SLOTS);
        }

        [Test]
        public void Warehouse_IsMouseItemCosting150()
        {
            ShopItem item = ShopLogic.Find(ShopItemId.Warehouse);
            Assert.AreEqual(RoleId.Mouse, item.Role);
            Assert.AreEqual(150, item.Cost);
        }

        [Test]
        public void Warehouse_PaysBackWithinMatch()
        {
            // 회수 = 가격 / 초당 생산. 판 길이(600s)보다 충분히 짧아야 살 이유가 있다.
            // v3.6: 2/s(75s)는 "150을 들일 값이 아니다"는 피드백을 받아 5/s(30s)로 올렸다.
            float payback = ShopLogic.Find(ShopItemId.Warehouse).Cost
                            / (float)ShopLogic.WAREHOUSE_INCOME_PER_SECOND;
            Assert.AreEqual(30f, payback, 1e-4f);
            Assert.Less(payback, CTC.Runtime.Match.MatchFlowLogic.MATCH_DURATION_SECONDS * 0.5f);
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
        public void RoleCatalogIndices_CatTwo()
        {
            var idx = ShopLogic.RoleCatalogIndices(RoleId.Cat);
            Assert.AreEqual(2, idx.Count);
            Assert.AreEqual(ShopItemId.CatAttack, ShopLogic.CATALOG[idx[0]].Id);
            Assert.AreEqual(ShopItemId.CatSpeed, ShopLogic.CATALOG[idx[1]].Id);
        }

        [Test]
        public void IsPlaceable_OnlyWarehouse()
        {
            Assert.IsTrue(ShopLogic.IsPlaceable(ShopItemId.Warehouse));
            // 요새는 2026-08-15부터 즉시 설치(내 위치 중심) — 쫓기는 중에 고스트 조준을 요구할 수 없다
            Assert.IsFalse(ShopLogic.IsPlaceable(ShopItemId.Fortress));
            Assert.IsFalse(ShopLogic.IsPlaceable(ShopItemId.Slingshot));
            Assert.IsFalse(ShopLogic.IsPlaceable(ShopItemId.CatSpeed));
        }

        [Test]
        public void CurrencyName_IsRoleSpecific() // 고양이에게 "치즈 부족"이라 하던 버그
        {
            Assert.AreEqual("장난기", ShopLogic.CurrencyNameOf(RoleId.Cat));
            Assert.AreEqual("치즈", ShopLogic.CurrencyNameOf(RoleId.Mouse));
        }

        [Test]
        public void MoveSpeed_StacksAdd02Each() // 밸런스 2026-07-26: 기본 6.4 상향 + 스택 0.2 하향 (만렙 7.4)
        {
            Assert.AreEqual(6.4f, ShopLogic.MoveSpeed(6.4f, 0), 1e-4f);
            Assert.AreEqual(7.4f, ShopLogic.MoveSpeed(6.4f, 5), 1e-4f);
        }

        [Test]
        public void CanAddStack_BelowCap_True()
        {
            Assert.IsTrue(ShopLogic.CanAddStack(4));
            Assert.IsFalse(ShopLogic.CanAddStack(5));
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
            Assert.AreEqual(ShopLogic.WeaponStatsForTier(2).Damage, ShopLogic.WeaponStatsForTier(99).Damage);
            Assert.AreEqual(ShopLogic.WeaponStatsForTier(0).Damage, ShopLogic.WeaponStatsForTier(-1).Damage);
        }

        [Test]
        public void Balance_CatAdvancementIsReachableWithinOneMatch()
        {
            // 고양이 수입은 덮치기 하나뿐이다 — 쥐(100HP)를 잡으면 100/나눗값 + 킬 보너스.
            // 전직(강화 두 줄 만렙)이 그 수입으로 판 안에 안 닿으면 4종 전직이 통째로
            // 죽은 콘텐츠가 된다(2026-08-10 실측: 비용 1750 = 16킬, 720초·120×120에선 불가능).
            //
            // 이 판정이 없으면 강화 가격이나 킬 보너스를 만질 때마다 조용히 다시 막힌다.
            int perCatch = 100 / FunLogic.PLAYER_DAMAGE_DIVISOR + FunLogic.KILL_BONUS;
            int advanceCost = (ShopLogic.Find(ShopItemId.CatAttack).Cost
                               + ShopLogic.Find(ShopItemId.CatSpeed).Cost) * ShopLogic.MAX_UPGRADE_STACKS;

            Assert.LessOrEqual(advanceCost, perCatch * 10,
                $"전직 {advanceCost} — 열 번 잡아도 못 열면 없는 기능이다");
            // 반대로 너무 싸면 초반에 열려 남은 판이 일방적이 된다
            Assert.Greater(advanceCost, perCatch * 5,
                $"전직 {advanceCost} — 다섯 번 만에 열리면 후반 카드가 아니다");
        }

        [Test]
        public void Balance_MaxUpgradeStillTakesTwoPouncesToDropAMouse()
        {
            // 강화를 싸게 만든 대가로 만렙 덮치기가 원샷이 되면 안 된다 —
            // 원샷은 불 전직(×2)의 값어치다(2026-08-10).
            int maxPounce = 60 + ShopLogic.POUNCE_DAMAGE_PER_STACK * ShopLogic.MAX_UPGRADE_STACKS;
            Assert.Less(maxPounce, 200, "쥐 100HP를 한 방에 잡으면 전직할 이유가 없다");
            Assert.Greater(maxPounce, 100, "만렙인데 세 방이면 강화가 무의미하다");
        }
    }
}
