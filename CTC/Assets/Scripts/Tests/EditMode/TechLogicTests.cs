// Assets/Scripts/Tests/EditMode/TechLogicTests.cs
using System.Collections.Generic;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests
{
    public class TechLogicTests
    {
        private static List<TechEntry> Owned(params (TechId, int)[] _e)
        {
            var list = new List<TechEntry>();
            foreach (var (id, lv) in _e) list.Add(new TechEntry(id, lv));
            return list;
        }

        [Test]
        public void Catalog_Mouse_IsCsvOrderDistinct()
        {
            IReadOnlyList<TechId> c = TechLogic.Catalog(RoleId.Mouse);
            CollectionAssert.AreEqual(
                new[] { TechId.Gun, TechId.Pepper, TechId.Fortress, TechId.Warehouse, TechId.Dash }, c);
        }

        [Test]
        public void MaxLevel_Gun2_Dash1_Unknown0()
        {
            Assert.AreEqual(2, TechLogic.MaxLevel(TechId.Gun));
            Assert.AreEqual(1, TechLogic.MaxLevel(TechId.Dash));
            Assert.AreEqual(0, TechLogic.MaxLevel(TechId.CheeseShield)); // 계획 2 전엔 행이 없다
        }

        [Test]
        public void LevelOf_NotOwned_IsZero()
        {
            Assert.AreEqual(0, TechLogic.LevelOf(Owned(), TechId.Gun));
            Assert.AreEqual(2, TechLogic.LevelOf(Owned((TechId.Gun, 2)), TechId.Gun));
        }

        [Test]
        public void NextRow_NotOwned_IsLevel1()
        {
            TechRow row = TechLogic.NextRow(Owned(), TechId.Gun);
            Assert.AreEqual(1, row.Level);
            Assert.AreEqual(150, row.Cost);
        }

        [Test]
        public void NextRow_AtMax_IsEmpty()
        {
            Assert.IsFalse(TechLogic.NextRow(Owned((TechId.Gun, 2)), TechId.Gun).IsValid);
        }

        [Test]
        public void CanBuy_NewTech_WithinCap_Ok()
        {
            Assert.AreEqual(TechDeny.None,
                TechLogic.CanBuy(Owned(), TechId.Gun, RoleId.Mouse, 150, 5));
        }

        [Test]
        public void CanBuy_NewTech_SlotsFull_Denied()
        {
            var owned = Owned((TechId.Gun, 1), (TechId.Pepper, 1), (TechId.Fortress, 1),
                              (TechId.Warehouse, 1), (TechId.Dash, 1));
            // 5칸이 찼다 — 6번째 테크는 못 산다(기획 규칙 1). 계획 2에서 테크가 늘면 의미가 생긴다.
            Assert.AreEqual(TechDeny.SlotsFull,
                TechLogic.CanBuy(owned, TechId.CheeseShield, RoleId.Mouse, 9999, 5));
        }

        [Test]
        public void CanBuy_Upgrade_WhenSlotsFull_StillOk()
        {
            var owned = Owned((TechId.Gun, 1), (TechId.Pepper, 1), (TechId.Fortress, 1),
                              (TechId.Warehouse, 1), (TechId.Dash, 1));
            Assert.AreEqual(TechDeny.None,
                TechLogic.CanBuy(owned, TechId.Gun, RoleId.Mouse, 600, 5));
        }

        [Test]
        public void CanBuy_AtMaxLevel_Denied()
        {
            Assert.AreEqual(TechDeny.MaxLevel,
                TechLogic.CanBuy(Owned((TechId.Gun, 2)), TechId.Gun, RoleId.Mouse, 9999, 5));
        }

        [Test]
        public void CanBuy_WrongRole_Denied()
        {
            Assert.AreEqual(TechDeny.WrongRole,
                TechLogic.CanBuy(Owned(), TechId.CatClaw, RoleId.Mouse, 9999, 5));
        }

        [Test]
        public void CanBuy_Poor_Denied()
        {
            Assert.AreEqual(TechDeny.NotEnough,
                TechLogic.CanBuy(Owned(), TechId.Gun, RoleId.Mouse, 149, 5));
        }

        [Test]
        public void CanBuy_Unknown_Denied()
        {
            Assert.AreEqual(TechDeny.Unknown,
                TechLogic.CanBuy(Owned(), TechId.None, RoleId.Mouse, 9999, 5));
        }

        [Test]
        public void IsActive_FortressStickyDash_Only()
        {
            Assert.IsTrue(TechLogic.IsActive(TechId.Fortress));
            Assert.IsTrue(TechLogic.IsActive(TechId.Dash));
            Assert.IsTrue(TechLogic.IsActive(TechId.Sticky));
            Assert.IsTrue(TechLogic.IsActive(TechId.Warehouse)); // 조준 설치 — 밖에서 숫자키로 놓는다
            Assert.IsFalse(TechLogic.IsActive(TechId.Gun));
            Assert.IsFalse(TechLogic.IsActive(TechId.CatClaw));
        }

        [Test]
        public void Candidates_ExcludesOwned_KeepsOrder()
        {
            IReadOnlyList<TechId> c = TechLogic.Candidates(RoleId.Mouse, Owned((TechId.Gun, 1), (TechId.Dash, 1)));
            CollectionAssert.AreEqual(new[] { TechId.Pepper, TechId.Fortress, TechId.Warehouse }, c);
        }

        [Test]
        public void NeedsShopSpot_MouseOnly()
        {
            // 기획 규칙 3·5 — 쥐는 상점 안에서만, 고양이는 핫바에서 바로
            Assert.IsTrue(TechLogic.NeedsShopSpot(RoleId.Mouse));
            Assert.IsFalse(TechLogic.NeedsShopSpot(RoleId.Cat));
        }

        [Test]
        public void CanBuy_NoneWhenMaxLevelZero_IsUnknownNotMaxLevel()
        {
            // 표에 없는 테크(계획 2 전 CheeseShield)는 "만렙"이 아니라 "모름"이어야 UI 문구가 거짓이 안 된다
            Assert.AreEqual(TechDeny.Unknown,
                TechLogic.CanBuy(Owned(), TechId.CheeseShield, RoleId.Mouse, 9999, 5));
        }
    }
}
