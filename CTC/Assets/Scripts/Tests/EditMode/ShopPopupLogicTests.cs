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

        // ───────────── 목록 + 상세(2026-09-19 시안 B) ─────────────

        private static ShopPopupCard Card(TechId _id, int _cheese, IReadOnlyList<TechId> _soldOut = null,
            params (TechId id, int level)[] _owned)
        {
            return ShopPopupLogic.CardOf(Owned(_owned), _id, RoleId.Mouse, _cheese, 5, _soldOut, false);
        }

        [NUnit.Framework.Property("CaseId", "C-41")]
        [Test]
        public void ListOrder_GroupsByAxis_KeepsCatalogOrderWithin()
        {
            IReadOnlyList<TechId> catalog = TechLogic.Catalog(RoleId.Mouse);
            List<TechId> order = ShopPopupLogic.ListOrder(catalog);

            CollectionAssert.AreEquivalent(catalog, order, "빠지거나 겹치는 기술이 없다");
            var position = new List<TechId>(catalog);
            for (int i = 1; i < order.Count; i++)
            {
                TechAxis before = TechLogic.RowOf(order[i - 1], 1).Axis;
                TechAxis now = TechLogic.RowOf(order[i], 1).Axis;
                Assert.LessOrEqual((int)before, (int)now, "축 순서로 묶인다 — 한 축이 두 번 나뉘면 머리글이 두 번 뜬다");
                if (before == now)
                    Assert.Less(position.IndexOf(order[i - 1]), position.IndexOf(order[i]), "같은 축 안에서는 CSV 순서 그대로");
            }
        }

        [NUnit.Framework.Property("CaseId", "C-41")]
        [Test]
        public void AxisLabel_EveryAxisHasItsOwnName()
        {
            var seen = new HashSet<string>();
            foreach (TechAxis axis in System.Enum.GetValues(typeof(TechAxis)))
            {
                string label = ShopPopupLogic.AxisLabel(axis);
                Assert.IsNotEmpty(label, axis.ToString());
                Assert.IsTrue(seen.Add(label), "축 이름이 겹친다: " + label);
            }
        }

        [NUnit.Framework.Property("CaseId", "C-41")]
        [Test]
        public void Steps_OwnedThenNextThenLocked()
        {
            ShopPopupCard card = Card(TechId.Warehouse, 5000, null, (TechId.Warehouse, 1));

            Assert.AreEqual(ShopStep.Owned, ShopPopupLogic.StepOf(card, 1));
            Assert.AreEqual(ShopStep.Next, ShopPopupLogic.StepOf(card, 2));
            Assert.AreEqual(ShopStep.Locked, ShopPopupLogic.StepOf(card, 3));
            Assert.AreEqual("Lv 1/3", ShopPopupLogic.LevelText(card), "예전 ○○○ 점 대신 글자로");
        }

        [NUnit.Framework.Property("CaseId", "C-41")]
        [Test]
        public void Steps_NextIsBlockedWhenShort_AndLockedWhenSoldOut()
        {
            ShopPopupCard shortCard = Card(TechId.Gun, 100, null, (TechId.Gun, 1));
            Assert.AreEqual(ShopStep.NextBlocked, ShopPopupLogic.StepOf(shortCard, 2), "다음 단계지만 치즈가 모자라다");

            ShopPopupCard sold = Card(TechId.Pepper, 5000, new List<TechId> { TechId.Pepper });
            Assert.AreEqual(ShopStep.Locked, ShopPopupLogic.StepOf(sold, 1), "품절이면 다음 단계도 잠긴 것으로 보인다");
            Assert.AreEqual("품절", ShopPopupLogic.RowPrice(sold, RoleId.Mouse));
        }

        [NUnit.Framework.Property("CaseId", "C-41")]
        [Test]
        public void Button_SaysBuyOrUpgrade_WithNextLabelAndPrice()
        {
            ShopPopupCard fresh = Card(TechId.Fortress, 5000);
            string buyText = ShopPopupLogic.ButtonText(fresh, RoleId.Mouse, 5);
            StringAssert.Contains("산다", buyText);
            StringAssert.Contains(fresh.Cost.ToString(), buyText);
            Assert.AreEqual(ShopButtonTone.Buy, ShopPopupLogic.ButtonTone(fresh));
            Assert.AreEqual("새로 사면 칸 1개를 쓴다", ShopPopupLogic.NoteText(fresh, RoleId.Mouse));

            ShopPopupCard upgrade = Card(TechId.Fortress, 5000, null, (TechId.Fortress, 1));
            string upText = ShopPopupLogic.ButtonText(upgrade, RoleId.Mouse, 5);
            StringAssert.Contains("키운다", upText);
            StringAssert.Contains(TechLogic.RowOf(TechId.Fortress, 2).Label, upText, "다음 단계 이름을 쓴다");
            Assert.AreEqual("강화는 칸을 안 쓴다", ShopPopupLogic.NoteText(upgrade, RoleId.Mouse));
        }

        [NUnit.Framework.Property("CaseId", "C-41")]
        [Test]
        public void Button_TellsWhyNot_ShortSoldOutMaxed()
        {
            ShopPopupCard poor = Card(TechId.Dash, 40);
            Assert.AreEqual(ShopButtonTone.Short, ShopPopupLogic.ButtonTone(poor));
            StringAssert.Contains((poor.Cost - 40) + " 모자람", ShopPopupLogic.ButtonText(poor, RoleId.Mouse, 5));

            ShopPopupCard sold = Card(TechId.Dash, 5000, new List<TechId> { TechId.Dash });
            Assert.AreEqual(ShopButtonTone.Blocked, ShopPopupLogic.ButtonTone(sold));
            Assert.AreEqual("오늘은 품절", ShopPopupLogic.ButtonText(sold, RoleId.Mouse, 5));

            ShopPopupCard maxed = Card(TechId.Dash, 5000, null, (TechId.Dash, 3));
            Assert.AreEqual(ShopButtonTone.Maxed, ShopPopupLogic.ButtonTone(maxed));
            Assert.AreEqual("만렙", ShopPopupLogic.RowPrice(maxed, RoleId.Mouse));
            StringAssert.StartsWith("만렙", ShopPopupLogic.ButtonText(maxed, RoleId.Mouse, 5));
        }
    }
}
