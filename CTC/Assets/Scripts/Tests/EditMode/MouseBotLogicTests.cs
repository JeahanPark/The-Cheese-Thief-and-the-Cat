using System.Collections.Generic;
using CTC.Runtime.Bots;
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
        [Test]
        public void NextPurchase_GrowthBuildBuysIncomeBeforeWeapons()
        {
            // 수급·성장 빌드: 대시(도주) → 창고(수입) → 요새(생존)
            Assert.AreEqual(CTC.Runtime.Economy.ShopItemId.Dash,
                MouseBotLogic.NextPurchase(1000, false, false, false, false));
            Assert.AreEqual(CTC.Runtime.Economy.ShopItemId.Warehouse,
                MouseBotLogic.NextPurchase(1000, true, false, false, false));
            Assert.AreEqual(CTC.Runtime.Economy.ShopItemId.Fortress,
                MouseBotLogic.NextPurchase(1000, true, false, true, false));

            // **새총은 안 산다**(2026-08-14). 봇은 맞서 쏘지 않는다 — 안 쓸 총에 치즈를
            // 태우면 요새 살 돈이 줄어 경제 측정이 왜곡된다.
            Assert.AreEqual(CTC.Runtime.Economy.ShopItemId.None,
                MouseBotLogic.NextPurchase(1000, true, false, true, true));

            Assert.AreEqual(CTC.Runtime.Economy.ShopItemId.None,
                MouseBotLogic.NextPurchase(0, false, false, false, false));
            Assert.AreEqual(CTC.Runtime.Economy.ShopItemId.None,
                MouseBotLogic.NextPurchase(9999, true, true, true, true));
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
    }
}
