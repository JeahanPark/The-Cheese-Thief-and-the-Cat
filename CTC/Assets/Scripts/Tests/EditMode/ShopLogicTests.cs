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
        public void Find_Fortress_MatchesOriginalBunker()
        {
            Assert.AreEqual(220, ShopLogic.Find(ShopItemId.Fortress).Cost);
        }

        [Test]
        public void Find_CatItems_AreCatRole()
        {
            Assert.AreEqual(RoleId.Cat, ShopLogic.Find(ShopItemId.CatAttack).Role);
            Assert.AreEqual(RoleId.Cat, ShopLogic.Find(ShopItemId.CatSpeed).Role);
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
        public void MoveSpeed_StacksAdd04Each()
        {
            Assert.AreEqual(4.5f, ShopLogic.MoveSpeed(4.5f, 0), 1e-4f);
            Assert.AreEqual(6.5f, ShopLogic.MoveSpeed(4.5f, 5), 1e-4f);
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
