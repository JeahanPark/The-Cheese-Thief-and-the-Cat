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
        public void Find_Fortress_MatchesOriginalBunker()
        {
            Assert.AreEqual(220, ShopLogic.Find(ShopItemId.Fortress).Cost);
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
        public void RoleCatalogIndices_MouseSixInCatalogOrder()
        {
            var idx = ShopLogic.RoleCatalogIndices(RoleId.Mouse);
            Assert.AreEqual(6, idx.Count);
            Assert.AreEqual(ShopItemId.Slingshot, ShopLogic.CATALOG[idx[0]].Id);
            Assert.AreEqual(ShopItemId.Fortress, ShopLogic.CATALOG[idx[4]].Id);
            Assert.AreEqual(ShopItemId.Warehouse, ShopLogic.CATALOG[idx[5]].Id);
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
            float payback = ShopLogic.Find(ShopItemId.Warehouse).Cost
                            / (float)ShopLogic.WAREHOUSE_INCOME_PER_SECOND;
            Assert.AreEqual(75f, payback, 1e-4f);
            Assert.Less(payback, CTC.Runtime.Match.MatchFlowLogic.MATCH_DURATION_SECONDS * 0.5f);
        }

        [Test]
        public void WarehouseTick_DerivesFromIncomeAndChunk()
        {
            // 10개씩 초당 2 → 5초마다 1회. 연출이 너무 잦거나 뜸하지 않은 구간이어야 한다.
            Assert.AreEqual(5f, ShopLogic.WarehouseTickSeconds, 1e-4f);
            Assert.GreaterOrEqual(ShopLogic.WarehouseTickSeconds, 2f);
            Assert.LessOrEqual(ShopLogic.WarehouseTickSeconds, 10f);
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
        public void IsPlaceable_OnlyStructures()
        {
            Assert.IsTrue(ShopLogic.IsPlaceable(ShopItemId.Fortress));
            Assert.IsTrue(ShopLogic.IsPlaceable(ShopItemId.Decoy));
            Assert.IsTrue(ShopLogic.IsPlaceable(ShopItemId.Warehouse));
            Assert.IsFalse(ShopLogic.IsPlaceable(ShopItemId.Slingshot));
            Assert.IsFalse(ShopLogic.IsPlaceable(ShopItemId.CatSpeed));
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
    }
}
