using System.Collections.Generic;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>상점 팝업(2026-09-19) — 카드 한 장의 상태와 창이 열리고 닫히는 조건.</summary>
    public class ShopPopupLogicTests
    {
        private static List<TechEntry> Owned(params (TechId id, int level)[] _entries)
        {
            var list = new List<TechEntry>();
            foreach ((TechId id, int level) in _entries) list.Add(new TechEntry(id, level));
            return list;
        }

        [NUnit.Framework.Property("CaseId", "C-33")]
        [Test]
        public void Card_Buyable_WhenAffordableAndSlotLeft()
        {
            ShopPopupCard card = ShopPopupLogic.CardOf(
                Owned(), TechId.Dash, RoleId.Mouse, _cheese: 500, _slotCap: 5, _soldOut: null, _clubActive: false);

            Assert.AreEqual(TechDeny.None, card.Deny);
            Assert.IsTrue(card.CanBuy);
            Assert.AreEqual(1, card.NextLevel);
            Assert.Greater(card.Cost, 0);
            Assert.AreEqual(0, card.Short);
        }

        [NUnit.Framework.Property("CaseId", "C-33")]
        [Test]
        public void Card_NotEnough_TellsHowMuchIsMissing()
        {
            ShopPopupCard card = ShopPopupLogic.CardOf(
                Owned(), TechId.Dash, RoleId.Mouse, _cheese: 40, _slotCap: 5, _soldOut: null, _clubActive: false);

            Assert.AreEqual(TechDeny.NotEnough, card.Deny);
            Assert.IsFalse(card.CanBuy);
            Assert.AreEqual(card.Cost - 40, card.Short, "모자란 양을 그대로 문구에 쓴다");
        }

        [NUnit.Framework.Property("CaseId", "C-33")]
        [Test]
        public void Card_SlotsFull_OnlyBlocksNewTechs()
        {
            List<TechEntry> owned = Owned(
                (TechId.Gun, 1), (TechId.Dash, 1), (TechId.Fortress, 1),
                (TechId.Warehouse, 1), (TechId.Sticky, 1));

            ShopPopupCard fresh = ShopPopupLogic.CardOf(
                owned, TechId.FastFeet, RoleId.Mouse, 9999, 5, null, false);
            ShopPopupCard upgrade = ShopPopupLogic.CardOf(
                owned, TechId.Dash, RoleId.Mouse, 9999, 5, null, false);

            Assert.AreEqual(TechDeny.SlotsFull, fresh.Deny);
            Assert.AreEqual(TechDeny.None, upgrade.Deny, "가진 것을 키우는 건 칸을 새로 안 쓴다");
        }

        [NUnit.Framework.Property("CaseId", "C-33")]
        [Test]
        public void Card_SoldOut_AndMaxLevel_AreTheirOwnReasons()
        {
            var soldOut = new List<TechId> { TechId.Sticky };

            Assert.AreEqual(TechDeny.SoldOut, ShopPopupLogic
                .CardOf(Owned(), TechId.Sticky, RoleId.Mouse, 9999, 5, soldOut, false).Deny);

            ShopPopupCard maxed = ShopPopupLogic
                .CardOf(Owned((TechId.Dash, 3)), TechId.Dash, RoleId.Mouse, 9999, 5, null, false);
            Assert.AreEqual(TechDeny.MaxLevel, maxed.Deny);
            Assert.AreEqual(0, maxed.NextLevel, "만렙이면 다음 레벨이 없다 — View가 「만렙」을 그린다");
            Assert.AreEqual(3, maxed.OwnedLevel);
        }

        [NUnit.Framework.Property("CaseId", "C-33")]
        [Test]
        public void Card_GangCannotBuyGunTech()
        {
            ShopPopupCard gun = ShopPopupLogic.CardOf(
                Owned(), TechId.Gun, RoleId.Mouse, 9999, 5, null, _clubActive: true);
            ShopPopupCard dash = ShopPopupLogic.CardOf(
                Owned(), TechId.Dash, RoleId.Mouse, 9999, 5, null, _clubActive: true);

            Assert.AreEqual(TechDeny.GangNoGun, gun.Deny);
            Assert.AreEqual(TechDeny.None, dash.Deny, "깡패도 대시는 산다");
        }

        [NUnit.Framework.Property("CaseId", "C-33")]
        [Test]
        public void Card_HeroCannotBuyGunPepperOrDash()
        {
            foreach (TechId id in new[] { TechId.Gun, TechId.Pepper, TechId.Dash })
            {
                Assert.AreEqual(TechDeny.HeroNoGun, ShopPopupLogic
                    .CardOf(Owned(), id, RoleId.Mouse, 9999, 5, null, false, _swordActive: true).Deny, id + "는 용사가 못 산다");
            }
            Assert.AreEqual(TechDeny.None, ShopPopupLogic
                .CardOf(Owned(), TechId.Fortress, RoleId.Mouse, 9999, 5, null, false, _swordActive: true).Deny, "요새는 산다");
        }

        [NUnit.Framework.Property("CaseId", "C-34")]
        [Test]
        public void CanOpen_NeedsShopSpotAndAliveAndFreeHands()
        {
            Assert.IsTrue(ShopPopupLogic.CanOpen(
                _role: RoleId.Mouse, _inShop: true, _dead: false, _locked: false, _typing: false));
            Assert.IsFalse(ShopPopupLogic.CanOpen(RoleId.Mouse, false, false, false, false), "원 밖");
            Assert.IsFalse(ShopPopupLogic.CanOpen(RoleId.Mouse, true, true, false, false), "죽었다");
            Assert.IsFalse(ShopPopupLogic.CanOpen(RoleId.Mouse, true, false, true, false), "깡패 변신 중");
            Assert.IsFalse(ShopPopupLogic.CanOpen(RoleId.Mouse, true, false, false, true), "채팅 중");
            Assert.IsFalse(ShopPopupLogic.CanOpen(RoleId.Cat, true, false, false, false), "고양이는 핫바로 산다");
        }

        [NUnit.Framework.Property("CaseId", "C-34")]
        [Test]
        public void ShouldClose_OnDamageDeathEndOrLeavingTheCircle()
        {
            Assert.IsTrue(ShopPopupLogic.ShouldClose(
                _inShop: true, _dead: false, _hurt: true, _locked: false, _matchOver: false), "맞았다");
            Assert.IsTrue(ShopPopupLogic.ShouldClose(true, true, false, false, false), "죽었다");
            Assert.IsTrue(ShopPopupLogic.ShouldClose(false, false, false, false, false), "원 밖으로 밀려났다");
            Assert.IsTrue(ShopPopupLogic.ShouldClose(true, false, false, true, false), "깡패 변신");
            Assert.IsTrue(ShopPopupLogic.ShouldClose(true, false, false, false, true), "판이 끝났다");
            Assert.IsFalse(ShopPopupLogic.ShouldClose(true, false, false, false, false), "평온하면 안 닫는다");
        }
    }
}
