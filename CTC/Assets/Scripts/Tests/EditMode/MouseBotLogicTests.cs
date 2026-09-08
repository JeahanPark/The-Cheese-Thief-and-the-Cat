using System.Collections.Generic;
using CTC.Runtime.Bots;
using CTC.Runtime.Economy;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 쥐 봇의 판단 규칙. **영리해지면 안 된다** — 봇이 사람보다 잘하면 그 승률은
    /// 밸런스가 아니라 봇의 실력을 잰 것이 된다.
    /// </summary>
    public class MouseBotLogicTests
    {
        // 인자가 여덟 개라 부르는 자리마다 위치를 세게 되면 테스트가 먼저 거짓말을 한다 —
        // 이름 있는 인자로 감싸 두고, 각 테스트는 자기가 재는 것만 바꾼다.
        private static TechId Next(
            int cheese, bool dash = false, bool gun = false, bool warehouse = false,
            bool fortress = false, bool shield = false, bool pouch = false, int owned = 0,
            IReadOnlyList<TechId> soldOut = null)
            => MouseBotLogic.NextPurchase(cheese, dash, gun, warehouse, fortress, shield, pouch, owned, soldOut);

        [Test]
        public void NextPurchase_SkipsSoldOut()
        {
            // 오늘 품절인 줄을 모르면 봇이 매 틱 거절당하는 죽은 구매를 반복한다(계획 3 Task 3).
            // 대시가 품절이면 그다음 줄(요새)로 내려간다 — 멈추지 않는다.
            Assert.AreEqual(TechId.Fortress, Next(1000, soldOut: new[] { TechId.Dash }));
        }

        [Test]
        public void NextPurchase_Dash_First()
        {
            int cost = TechLogic.RowOf(TechId.Dash, 1).Cost;
            Assert.AreEqual(TechId.Dash, Next(cost));
        }

        [Test]
        public void NextPurchase_Fortress_BeforeWarehouse()
        {
            // 넉넉히 줘서 대시·요새·창고 값 전부 감당할 수 있는 상태로 — 그래도 대시부터 있으면 요새다
            Assert.AreEqual(TechId.Fortress, Next(1000, dash: true));
        }

        [Test]
        public void NextPurchase_WarehouseReserve_IncludesPlaceCosts()
        {
            // 창고 Lv1 값 + **창고 설치비** + 요새 설치비를 모두 남겨야 창고다(Task 8 갱신, 2026-09-07) —
            // 테크 v3부터 창고도 요새처럼 Lv1(권한) 따로 · 설치마다 WarehousePlaceCost 따로 낸다.
            // 방패·주머니는 이미 산 것으로 둔다 — 안 그러면 창고를 못 사는 돈으로 그쪽을 사서
            // 이 테스트가 재려는 "창고 유보"가 아니라 다음 항목을 재게 된다.
            var balance = CTC.Runtime.Info.InfoManager.Instance.Balance;
            int need = TechLogic.RowOf(TechId.Warehouse, 1).Cost
                       + balance.WarehousePlaceCost
                       + balance.FortressPlaceCost;
            Assert.AreEqual(TechId.None,
                Next(need - 1, dash: true, fortress: true, shield: true, pouch: true));
            Assert.AreEqual(TechId.Warehouse,
                Next(need, dash: true, fortress: true, shield: true, pouch: true));
        }

        [Test]
        public void NextPurchase_NeverBuysGun()
        {
            // 2026-08-14 결정 — 봇은 맞서 쏘지 않는다. 총이 없어도(hasGun:false), 돈이 넘쳐도 안 산다.
            Assert.AreEqual(TechId.None,
                Next(99999, dash: true, warehouse: true, fortress: true, shield: true, pouch: true));
        }

        [Test]
        public void NextPurchase_AllBought_None()
        {
            Assert.AreEqual(TechId.None,
                Next(9999, dash: true, gun: true, warehouse: true, fortress: true,
                     shield: true, pouch: true));
        }

        // ---- 테크 v3 계획 2b — 치즈 방패 · 큰 주머니 · 칸 상한 ----

        [Test]
        public void NextPurchase_ShieldThenPouch_AfterTheStaples()
        {
            // 대시·요새·창고를 갖춘 뒤의 순서: 치즈 방패 → 큰 주머니. 둘 다 패시브라 봇이
            // 흉내 낼 판단이 없다(액티브를 주면 재는 대상이 밸런스가 아니라 봇이 된다).
            Assert.AreEqual(TechId.CheeseShield,
                Next(9999, dash: true, warehouse: true, fortress: true, owned: 3));
            Assert.AreEqual(TechId.BigPouch,
                Next(9999, dash: true, warehouse: true, fortress: true, shield: true, owned: 4));
        }

        [Test]
        public void NextPurchase_ShieldKeepsTheEmergencyWallMoney()
        {
            // 방패 값만 있고 요새 설치비가 안 남으면 안 산다 — 지갑을 털면 그 직후 쫓길 때
            // 발밑에 벽을 못 세운다(창고와 같은 규칙).
            var balance = CTC.Runtime.Info.InfoManager.Instance.Balance;
            int need = TechLogic.RowOf(TechId.CheeseShield, 1).Cost + balance.FortressPlaceCost;
            Assert.AreEqual(TechId.None,
                Next(need - 1, dash: true, warehouse: true, fortress: true, owned: 3));
            Assert.AreEqual(TechId.CheeseShield,
                Next(need, dash: true, warehouse: true, fortress: true, owned: 3));
        }

        [Test]
        public void NextPurchase_StopsAtTheSlotCap()
        {
            // 칸이 다 차면(TechSlotCap) 새 테크는 못 산다 — 봇은 레벨을 올리지 않고 새것만 사므로
            // 돈이 아무리 많아도 여기서 멈춰야 한다. 안 막으면 매 틱 서버에 거절당한다.
            int cap = CTC.Runtime.Info.InfoManager.Instance.Balance.TechSlotCap;
            Assert.AreEqual(TechId.None, Next(99999, owned: cap));
            Assert.AreEqual(TechId.None, Next(99999, owned: cap + 1));

            // 한 칸 남으면 그대로 산다
            Assert.AreEqual(TechId.Dash, Next(99999, owned: cap - 1));
        }

        [Test]
        public void RefugeSpent_RejectsAWallAlreadyBeingBroken()
        {
            // 고양이가 벽을 뚫을 수 있게 된 뒤로 반쯤 깎인 요새는 은신처가 아니다 —
            // 그 뒤로 뛰어들면 도망이 아니라 그 자리에서 죽는다(2026-08-18).
            Assert.IsFalse(MouseBotLogic.IsRefugeSpent(480, 480));
            Assert.IsFalse(MouseBotLogic.IsRefugeSpent(300, 480));
            Assert.IsTrue(MouseBotLogic.IsRefugeSpent(200, 480));
            Assert.IsFalse(MouseBotLogic.IsRefugeSpent(10, 0), "최대 HP를 모르면 판단하지 않는다");
        }

        [Test]
        public void ChooseSpot_BalancesGradeAgainstDistance()
        {
            // 가까운 것만 고르면 봇들이 스폰 근처에 뭉치고, 등급만 보면 전원이 한가운데로 몰린다.
            var positions = new List<Vector3> { new Vector3(2f, 0f, 0f), new Vector3(40f, 0f, 0f) };
            var grades = new List<int> { 1, 7 };
            var usable = new List<bool> { true, true };

            // 가까운 1등급(1/2)이 먼 7등급(7/40)보다 낫다
            Assert.AreEqual(0, MouseBotLogic.ChooseSpot(positions, grades, usable, Vector3.zero));

            // 거리가 비슷하면 등급이 이긴다
            positions[1] = new Vector3(3f, 0f, 0f);
            Assert.AreEqual(1, MouseBotLogic.ChooseSpot(positions, grades, usable, Vector3.zero));
        }

        [Test]
        public void ChooseSpot_SkipsUnusable()
        {
            // 마르거나 남이 쓰는 자리는 라벨에 그렇게 적혀 있다 — 사람도 가서 보면 안다
            var positions = new List<Vector3> { new Vector3(1f, 0f, 0f), new Vector3(20f, 0f, 0f) };
            var grades = new List<int> { 7, 1 };
            var usable = new List<bool> { false, true };

            Assert.AreEqual(1, MouseBotLogic.ChooseSpot(positions, grades, usable, Vector3.zero));
        }

        [Test]
        public void ChooseSpot_NothingUsableIsMinusOne()
        {
            var positions = new List<Vector3> { Vector3.zero };
            Assert.AreEqual(-1, MouseBotLogic.ChooseSpot(
                positions, new List<int> { 3 }, new List<bool> { false }, Vector3.zero));
            Assert.AreEqual(-1, MouseBotLogic.ChooseSpot(
                new List<Vector3>(), null, null, Vector3.zero));
        }

        [Test]
        public void FleeDirection_PointsAway()
        {
            Vector3 dir = MouseBotLogic.FleeDirection(new Vector3(5f, 0f, 0f), Vector3.zero);
            Assert.Greater(dir.x, 0.9f);

            // 겹쳐 있어도 영벡터를 돌려주면 안 된다 — 그러면 제자리에서 잡힌다
            Assert.AreEqual(1f, MouseBotLogic.FleeDirection(Vector3.zero, Vector3.zero).magnitude, 0.01f);
        }

        // ---- 도주 목표 — 위협에게 안 보이는 자리를 고른다 ----

        [Test]
        public void FleeGoal_StraightAway_WhenAlreadyHidden()
        {
            // 정반대가 숨는 자리면 그리로 — 덜 꺾을수록 위협에서 멀어진다
            Vector3 goal = MouseBotLogic.FleeGoal(
                new Vector3(5f, 0f, 0f), Vector3.zero, 18f, (_t, _g) => true);
            Assert.AreEqual(23f, goal.x, 0.01f);
            Assert.AreEqual(0f, goal.z, 0.01f);
        }

        [Test]
        public void FleeGoal_TurnsToTheHiddenSide()
        {
            // 정반대는 보이고 옆은 숨는다(차폐벽 뒤) — 꺾어서 시선을 끊어야 한다
            Vector3 self = new Vector3(5f, 0f, 0f);
            Vector3 straight = self + Vector3.right * 18f;

            Vector3 goal = MouseBotLogic.FleeGoal(
                self, Vector3.zero, 18f,
                (_t, _g) => Vector3.Distance(_g, straight) > 1f);   // 정반대만 노출

            Assert.Greater(Vector3.Distance(goal, straight), 1f, "보이는 정반대를 골랐다");
            Assert.Greater(Vector3.Distance(goal, self), 17f, "도주 거리가 줄었다");
        }

        [Test]
        public void FleeGoal_FallsBackToStraight_WhenNowhereHides()
        {
            // 숨을 곳이 아예 없으면 정반대 — 거리라도 벌어야 한다
            Vector3 goal = MouseBotLogic.FleeGoal(
                new Vector3(5f, 0f, 0f), Vector3.zero, 18f, (_t, _g) => false);
            Assert.AreEqual(23f, goal.x, 0.01f);

            // 델리게이트가 없어도 죽지 않는다
            Assert.AreEqual(23f, MouseBotLogic.FleeGoal(
                new Vector3(5f, 0f, 0f), Vector3.zero, 18f, null).x, 0.01f);
        }

        // ---- 요새 피난 — 이길 수 있는 경주만 뛴다 ----

        [Test]
        public void Refuge_PicksTheNearFort_WhenTheRaceIsWinnable()
        {
            // 요새 10m·고양이는 요새에서 30m — 1.3배 빨라도 못 따라온다
            var forts = new List<Vector3> { new Vector3(10f, 0f, 0f) };
            Assert.AreEqual(0, MouseBotLogic.ChooseRefuge(
                forts, Vector3.zero, new Vector3(-20f, 0f, 0f)));
        }

        [Test]
        public void Refuge_SkipsFortBehindTheCat()
        {
            // 위협 뒤편의 요새로 뛰는 건 자살이다 — 경주 계산이 이걸 걸러야 한다
            var forts = new List<Vector3> { new Vector3(10f, 0f, 0f) };
            Assert.AreEqual(-1, MouseBotLogic.ChooseRefuge(
                forts, Vector3.zero, new Vector3(8f, 0f, 0f)));
        }

        [Test]
        public void Refuge_IgnoresFarForts()
        {
            // 반경 밖 요새는 뛰어가다 잡힌다
            var forts = new List<Vector3> { new Vector3(MouseBotLogic.FORT_REFUGE_RADIUS + 5f, 0f, 0f) };
            Assert.AreEqual(-1, MouseBotLogic.ChooseRefuge(
                forts, Vector3.zero, new Vector3(-50f, 0f, 0f)));
            Assert.AreEqual(-1, MouseBotLogic.ChooseRefuge(
                new List<Vector3>(), Vector3.zero, new Vector3(-50f, 0f, 0f)));
        }

        [Test]
        public void Refuge_PrefersTheNearestWinnableFort()
        {
            var forts = new List<Vector3>
            {
                new Vector3(18f, 0f, 0f),    // 이길 수 있지만 멀다
                new Vector3(6f, 0f, 6f),     // 가깝고 이긴다 — 이걸 골라야 한다
            };
            Assert.AreEqual(1, MouseBotLogic.ChooseRefuge(
                forts, Vector3.zero, new Vector3(-40f, 0f, 0f)));
        }

        // ===== 상점으로 걸어간다 (계획 3 Task 6) =====

        [Test]
        public void WantsShopping_NothingToBuy_False()
        {
            // 살 것이 없으면 안 간다 — 빈손으로 가면 봇이 상점 주위를 맴돌고,
            // 그러면 쟴 승률이 밸런스가 아니라 봇의 길찾기 실력이 된다.
            Assert.IsFalse(MouseBotLogic.WantsShopping(TechId.None, 999, 0, 5f));
        }

        [Test]
        public void WantsShopping_NotEnoughCheese_False()
        {
            // 돈이 모자라면 가서 기다리지 않는다 — 파밍이 먼저다.
            Assert.IsFalse(MouseBotLogic.WantsShopping(TechId.Dash, 40, 50, 5f));
        }

        [Test]
        public void WantsShopping_CloseAndAffordable_True()
        {
            Assert.IsTrue(MouseBotLogic.WantsShopping(TechId.Dash, 50, 50, 5f));
        }

        [Test]
        public void WantsShopping_TooFar_False()
        {
            // 상한 밖의 상점을 향해 맵을 가로지르면 그 판을 통째로 걷기만 한다.
            Assert.IsFalse(MouseBotLogic.WantsShopping(
                TechId.Dash, 999, 50, MouseBotLogic.SHOP_TRAVEL_MAX + 0.1f));
        }

        [Test]
        public void WantsShopping_Boundaries_AreInclusive()
        {
            // 경계값 — 딜 맞는 돈과 정확히 상한 거리는 간다.
            Assert.IsTrue(MouseBotLogic.WantsShopping(
                TechId.Dash, 50, 50, MouseBotLogic.SHOP_TRAVEL_MAX));
        }
    }
}
