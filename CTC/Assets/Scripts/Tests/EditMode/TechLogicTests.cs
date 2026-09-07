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
            // 계획 2a에서 쥐 테크가 10종이 됐다 — 순서는 CSV 등장 순(기존 5종 뒤에 신규 5종).
            CollectionAssert.AreEqual(
                new[]
                {
                    TechId.Gun, TechId.Pepper, TechId.Fortress, TechId.Warehouse, TechId.Dash,
                    TechId.CheeseShield, TechId.BigPouch, TechId.HiddenWarehouse, TechId.Sticky, TechId.FastFeet,
                }, c);
        }

        [Test]
        public void Catalog_Cat_IsClawStompThenNewFour()
        {
            IReadOnlyList<TechId> c = TechLogic.Catalog(RoleId.Cat);
            // 계획 2b에서 고양이도 6종이 됐다 — 순서는 CSV 등장 순(기존 2종 뒤에 신규 4종).
            // 칸은 5뿐이라 6종 중 하나는 포기해야 한다(기획서 §5 의도).
            CollectionAssert.AreEqual(
                new[]
                {
                    TechId.CatClaw, TechId.CatStomp,
                    TechId.CatHide, TechId.CatHunger, TechId.CatFrenzy, TechId.CatEar,
                }, c);
        }

        [Test]
        public void MaxLevel_MouseTechs3_Unknown0()
        {
            // 계획 2a: 쥐 테크는 전부 3레벨까지 열렸다
            Assert.AreEqual(3, TechLogic.MaxLevel(TechId.Gun));
            Assert.AreEqual(3, TechLogic.MaxLevel(TechId.Dash));
            Assert.AreEqual(3, TechLogic.MaxLevel(TechId.CheeseShield));
            Assert.AreEqual(3, TechLogic.MaxLevel(TechId.CatHide));  // 계획 2b에서 행이 들어왔다
            Assert.AreEqual(0, TechLogic.MaxLevel(TechId.None));     // 없는 테크는 0
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
            Assert.IsFalse(TechLogic.NextRow(Owned((TechId.Gun, 3)), TechId.Gun).IsValid);
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
                TechLogic.CanBuy(Owned((TechId.Gun, 3)), TechId.Gun, RoleId.Mouse, 9999, 5));
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
            CollectionAssert.AreEqual(
                new[]
                {
                    TechId.Pepper, TechId.Fortress, TechId.Warehouse,
                    TechId.CheeseShield, TechId.BigPouch, TechId.HiddenWarehouse, TechId.Sticky, TechId.FastFeet,
                }, c);
        }

        [Test]
        public void Candidates_AllOwned_IsEmpty()
        {
            // 고양이가 6종을 다 샀으면 후보가 없다 — 핫바가 이 빈 목록에 기대 채울 칸이 없다고 판단한다.
            // (칸 상한 5는 CanBuy가 보는 규칙이고 여기는 "안 산 것 고르기"만 본다.)
            IReadOnlyList<TechId> c = TechLogic.Candidates(RoleId.Cat, Owned(
                (TechId.CatClaw, 1), (TechId.CatStomp, 1), (TechId.CatHide, 1),
                (TechId.CatHunger, 1), (TechId.CatFrenzy, 1), (TechId.CatEar, 1)));
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

        /// <summary>테크 하나를 1레벨부터 만렙까지 다 올리는 데 드는 총액.</summary>
        private static int TotalCost(TechId _id)
        {
            int sum = 0;
            for (int lv = 1; lv <= TechLogic.MaxLevel(_id); lv++) sum += TechLogic.RowOf(_id, lv).Cost;
            return sum;
        }

        [Test]
        public void CatAdvancement_ReachableWithinOneMatch()
        {
            // 고양이 수입은 덮치기 하나뿐이다 — 쥐(100HP)를 잡으면 100/나눗값 + 킬 보너스.
            // 전직(강화 두 줄 만렙)이 그 수입으로 판 안에 안 닿으면 4종 전직이 통째로
            // 죽은 콘텐츠가 된다(2026-08-10 실측: 비용 1750 = 16킬, 720초·120×120에선 불가능).
            //
            // 이 판정이 없으면 강화 가격이나 킬 보너스를 만질 때마다 조용히 다시 막힌다.
            // 테크 v3에서는 "만렙 = 레벨을 1..N 다 사는 값"이라 표에서 합산한다(현재 780).
            // 계획 3에서 전직 조건이 300으로 바뀌면 이 판정을 다시 본다.
            int perCatch = 100 / FunLogic.PLAYER_DAMAGE_DIVISOR + FunLogic.KILL_BONUS;
            int advanceCost = TotalCost(TechId.CatClaw) + TotalCost(TechId.CatStomp);

            Assert.LessOrEqual(advanceCost, perCatch * 10,
                $"전직 {advanceCost} — 열 번 잡아도 못 열면 없는 기능이다");
            // 반대로 너무 싸면 초반에 열려 남은 판이 일방적이 된다
            Assert.Greater(advanceCost, perCatch * 5,
                $"전직 {advanceCost} — 다섯 번 만에 열리면 후반 카드가 아니다");
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
            // 표에 없는 테크는 "만렙"이 아니라 "모름"이어야 UI 문구가 거짓이 안 된다.
            // 계획 2b로 고양이 4종에 행이 생겨서, 이제 표에 없는 것은 None뿐이다.
            Assert.AreEqual(TechDeny.Unknown,
                TechLogic.CanBuy(Owned(), TechId.None, RoleId.Cat, 9999, 5));
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
