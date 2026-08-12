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
            // 수급·성장 빌드: 대시(도주) → 창고(수입) → 요새(생존) → 새총(싸움)
            Assert.AreEqual(CTC.Runtime.Economy.ShopItemId.Dash,
                MouseBotLogic.NextPurchase(1000, false, false, false, false));
            Assert.AreEqual(CTC.Runtime.Economy.ShopItemId.Warehouse,
                MouseBotLogic.NextPurchase(1000, true, false, false, false));
            Assert.AreEqual(CTC.Runtime.Economy.ShopItemId.Fortress,
                MouseBotLogic.NextPurchase(1000, true, false, true, false));

            // **새총은 맨 뒤다.** 앞에 두면 쥐 여덟이 도망치며 쏴 고양이를 97초에 죽인다(2026-08-12).
            Assert.AreEqual(CTC.Runtime.Economy.ShopItemId.Slingshot,
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
    }
}
