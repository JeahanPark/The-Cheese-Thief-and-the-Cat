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
        public void Catalog_Cat_IsClawThenStomp()
        {
            IReadOnlyList<TechId> c = TechLogic.Catalog(RoleId.Cat);
            CollectionAssert.AreEqual(new[] { TechId.CatClaw, TechId.CatStomp }, c);
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
        public void IsActive_FortressWarehouseStickyDash_Only()
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
        public void Candidates_AllOwned_IsEmpty()
        {
            // 고양이가 발톱·발구르기를 다 샀으면 후보가 없다 — 핫바가 이 빈 목록에 기대 채울 칸이 없다고 판단한다
            IReadOnlyList<TechId> c = TechLogic.Candidates(RoleId.Cat, Owned((TechId.CatClaw, 1), (TechId.CatStomp, 1)));
            CollectionAssert.IsEmpty(c);
        }

        [Test]
        public void RowOf_Level0_IsInvalid()
        {
            Assert.IsFalse(TechLogic.RowOf(TechId.Gun, 0).IsValid);
        }

        [Test]
        public void NeedsShopSpot_MouseOnly()
        {
            // 기획 규칙 3·5 — 쥐는 상점 안에서만, 고양이는 핫바에서 바로
            Assert.IsTrue(TechLogic.NeedsShopSpot(RoleId.Mouse));
            Assert.IsFalse(TechLogic.NeedsShopSpot(RoleId.Cat));
        }

        [Test]
        public void Warehouse_FirstBuildCostsSameAsBefore()
        {
            // 설치권 50 + 설치비 100 = 150 — 옛 창고 값 그대로. 재설치만 100이 붙는다(째기 응징).
            int b = CTC.Runtime.Info.InfoManager.Instance.Balance.WarehousePlaceCost;
            Assert.AreEqual(150, TechLogic.RowOf(TechId.Warehouse, 1).Cost + b);
            float payback = (TechLogic.RowOf(TechId.Warehouse, 1).Cost + b) / (float)ShopLogic.WAREHOUSE_INCOME_PER_SECOND;
            Assert.Less(payback, CTC.Runtime.Match.MatchFlowLogic.MATCH_DURATION_SECONDS * 0.5f);
        }

        [Test]
        public void Fortress_PlaceCost_IsCheapInstantShield()
        {
            // 즉발 방패 — 쫓기는 순간 살 수 있는 값이어야 한다(2026-08-15). 설치권 100은 따로.
            Assert.AreEqual(80, CTC.Runtime.Info.InfoManager.Instance.Balance.FortressPlaceCost);
            Assert.AreEqual(100, TechLogic.RowOf(TechId.Fortress, 1).Cost);
        }

        [Test]
        public void CatStackCap_MatchesTechMaxLevel()
        {
            // 두 상한이 갈리면 HUD가 "3/5"처럼 거짓말한다
            Assert.AreEqual(TechLogic.MaxLevel(TechId.CatClaw), ShopLogic.MAX_UPGRADE_STACKS);
            Assert.AreEqual(TechLogic.MaxLevel(TechId.CatStomp), ShopLogic.MAX_UPGRADE_STACKS);
        }

        [Test]
        public void CanBuy_NoneWhenMaxLevelZero_IsUnknownNotMaxLevel()
        {
            // 표에 없는 테크(계획 2 전 CheeseShield)는 "만렙"이 아니라 "모름"이어야 UI 문구가 거짓이 안 된다
            Assert.AreEqual(TechDeny.Unknown,
                TechLogic.CanBuy(Owned(), TechId.CheeseShield, RoleId.Mouse, 9999, 5));
        }

        [Test]
        public void UseDenyText_NotEnough_NamesCurrencyAndShortfall()
        {
            // "왜 못 쓰나"를 판정한 쪽이 말한다 — View가 TechId로 추측하면 거짓말이 된다
            Assert.AreEqual("치즈 부족 · 40 모자람",
                TechLogic.UseDenyText(UseDeny.NotEnough, RoleId.Mouse, 40));
            Assert.AreEqual("장난기 부족 · 5 모자람",
                TechLogic.UseDenyText(UseDeny.NotEnough, RoleId.Cat, 5));
        }

        [Test]
        public void UseDenyText_SituationalReasonsAreDistinct()
        {
            Assert.AreEqual("요새 안에서는 못 세운다 — 밖으로",
                TechLogic.UseDenyText(UseDeny.InsideFortress, RoleId.Mouse, 0));
            Assert.AreEqual("창고는 하나까지",
                TechLogic.UseDenyText(UseDeny.LimitReached, RoleId.Mouse, 0));
            Assert.AreEqual("패시브 — 늘 켜져 있다",
                TechLogic.UseDenyText(UseDeny.NotActive, RoleId.Mouse, 0));
        }

        [Test]
        public void UseDenyText_UnknownFallsBackWithoutLying()
        {
            Assert.AreEqual("아직 없다", TechLogic.UseDenyText(UseDeny.NotOwned, RoleId.Mouse, 0));
            Assert.AreEqual("지금은 못 쓴다", TechLogic.UseDenyText(UseDeny.Unavailable, RoleId.Mouse, 0));
        }
    }
}
