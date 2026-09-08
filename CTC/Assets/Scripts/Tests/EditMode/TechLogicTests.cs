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
        public void IsActive_FortressWarehouseStickyDashFrenzy_Only()
        {
            Assert.IsTrue(TechLogic.IsActive(TechId.Fortress));
            Assert.IsTrue(TechLogic.IsActive(TechId.Dash));
            Assert.IsTrue(TechLogic.IsActive(TechId.Sticky));
            Assert.IsTrue(TechLogic.IsActive(TechId.Warehouse));  // 조준 설치 — 밖에서 숫자키로 놓는다
            Assert.IsTrue(TechLogic.IsActive(TechId.CatFrenzy));  // 고양이 첫 액티브 테크 — 숫자키로 켠다
            Assert.IsFalse(TechLogic.IsActive(TechId.Gun));
            Assert.IsFalse(TechLogic.IsActive(TechId.CatClaw));
            Assert.IsFalse(TechLogic.IsActive(TechId.CatHide));   // 산 순간 끝 — 누를 게 없다
            Assert.IsFalse(TechLogic.IsActive(TechId.CatHunger));
            Assert.IsFalse(TechLogic.IsActive(TechId.CatEar));
        }

        [Test]
        public void Frenzy_WithFire_CannotOneShotLv3Fortress()
        {
            // 광분(테크)과 불(전직)은 이제 **곱하지 않는다** — 큰 쪽만 쓴다(계획 3 Task 5).
            // 전직 문턱이 300으로 내려와 둘을 같이 켜는 일이 흔해졌고, 곱이면 ×5.0이라
            // 쥐가 돈을 다 쓴 방벽이 고양이 클릭 한 번에 사라진다.
            // 못 박을 것은 둘이다: ① 합성이 max다 ② 그래서 요새 Lv3을 한 방에 못 부순다.
            int maxPounce = ShopLogic.PounceDamage(
                CTC.Runtime.Info.InfoManager.Instance.PlayerOf(RoleId.Cat).PounceDamage,
                ShopLogic.MAX_UPGRADE_STACKS);
            float frenzy = TechLogic.ParamOf(TechId.CatFrenzy, TechLogic.MaxLevel(TechId.CatFrenzy), 1);
            float fire = CTC.Runtime.Player.CatClassLogic.FireDamageMultiplier;
            Assert.Greater(frenzy, 1f, "광분 만렙 배율이 1보다 커야 이 테스트가 뜻을 가진다");

            float combined = CTC.Runtime.Player.CatClassLogic.PounceMultiplier(frenzy, fire);
            Assert.AreEqual(UnityEngine.Mathf.Max(frenzy, fire), combined, 1e-4f,
                "광분·불을 곱하면 흡수(기획서 §5)가 아니다");

            int burst = UnityEngine.Mathf.RoundToInt(maxPounce * combined);
            int fortressLv3 = UnityEngine.Mathf.RoundToInt(
                TechLogic.ParamOf(TechId.Fortress, TechLogic.MaxLevel(TechId.Fortress), 1));

            Assert.Less(burst, fortressLv3,
                $"한 방 {burst} ≥ 요새 Lv3 HP {fortressLv3} — 값을 고쳐야 한다(테스트가 아니라)");
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

        [Test]
        public void CatAdvancement_ReachableWithinOneMatch()
        {
            // 고양이 수입은 덮치기 하나뿐이다 — 쥐(100HP)를 잡으면 100/나눗값 + 킬 보너스.
            // 전직이 그 수입으로 판 안에 안 닿으면 4종 전직이 통째로 죽은 콘텐츠가 된다
            // (2026-08-10 실측: 비용 1750 = 16킬, 720초·120×120에선 불가능).
            //
            // 계획 3 Task 5부터 문턱은 **누적 획득 장난기 하나**다(전에는 강화 두 줄 만렙 = 실효 780).
            // 무엇을 사든 상관없으므로 재는 것도 테크 가격이 아니라 CSV 문턱 그 자체다.
            // 이 판정이 없으면 문턱이나 킬 보너스를 만질 때마다 조용히 다시 막히거나 너무 헐거워진다.
            int perCatch = 100 / FunLogic.PLAYER_DAMAGE_DIVISOR + FunLogic.KILL_BONUS;
            int threshold = CTC.Runtime.Player.CatClassLogic.MischiefThreshold;

            Assert.LessOrEqual(threshold, perCatch * 10,
                $"전직 문턱 {threshold} — 열 번 잡아도 못 열면 없는 기능이다");
            // 반대로 너무 싸면 초반에 열려 남은 판이 일방적이 된다. 벽·구조물 수입까지 있으니
            // "두 번 잡으면 전직"은 너무 이르다 — 지금 값 300은 잡기 ~3번이다.
            Assert.Greater(threshold, perCatch * 2,
                $"전직 문턱 {threshold} — 두 번 만에 열리면 후반 카드가 아니다");
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

        // ===== 액티브 전용 키 (2026-09-09 감사 P0-1) =====

        /// <summary>
        /// 쥐는 「밖」이 있어 숫자키로 쓴다 — 전용 키가 필요 없다.
        /// 고양이는 상점 면제라 핫바가 늘 구매 모드고, 그 역할의 액티브만 전용 키를 쓴다.
        /// </summary>
        [Test]
        public void NeedsActiveKey_OnlyForShopExemptRoleActives()
        {
            Assert.IsTrue(TechLogic.NeedsActiveKey(RoleId.Cat, TechId.CatFrenzy));

            Assert.IsFalse(TechLogic.NeedsActiveKey(RoleId.Mouse, TechId.Fortress));
            Assert.IsFalse(TechLogic.NeedsActiveKey(RoleId.Mouse, TechId.Dash));
            Assert.IsFalse(TechLogic.NeedsActiveKey(RoleId.Mouse, TechId.Sticky));

            // 패시브는 누를 것이 없다 — 역할과 무관하다.
            Assert.IsFalse(TechLogic.NeedsActiveKey(RoleId.Cat, TechId.CatClaw));
            Assert.IsFalse(TechLogic.NeedsActiveKey(RoleId.Cat, TechId.CatEar));
        }

        /// <summary>
        /// **고양이 액티브는 지금 하나뿐이다.** 전용 키가 Space 한 개라 「맨 왼쪽 액티브 칸」으로
        /// 고르는 규칙(<c>ShopHotbar.PressActiveKey</c>)이 성립한다.
        /// 둘째가 생기면 이 테스트가 깨진다 — 그때 키를 나눌 방법을 먼저 정해야 한다.
        /// </summary>
        [Test]
        public void ShopExemptRole_HasExactlyOneActive_SoOneKeyIsEnough()
        {
            var actives = new List<TechId>();
            foreach (TechId id in System.Enum.GetValues(typeof(TechId)))
            {
                if (id == TechId.None || !TechLogic.IsActive(id)) continue;
                TechRow row = TechLogic.RowOf(id, 1);
                if (!row.IsValid || TechLogic.NeedsShopSpot(row.Role)) continue;
                actives.Add(id);
            }

            CollectionAssert.AreEqual(new[] { TechId.CatFrenzy }, actives);
        }
    }
}
