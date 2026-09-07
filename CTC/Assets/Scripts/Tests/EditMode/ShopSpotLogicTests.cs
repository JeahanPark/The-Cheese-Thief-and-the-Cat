using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class ShopSpotLogicTests
    {
        private static List<Vector2> Grid(int _n, float _step)
        {
            var list = new List<Vector2>();
            for (int i = 0; i < _n; i++) for (int j = 0; j < _n; j++) list.Add(new Vector2(i * _step, j * _step));
            return list;
        }

        [Test]
        public void Pick_ReturnsRequestedCount_WhenRoomsSuffice()
        {
            List<Vector2> picked = ShopSpotLogic.Pick(Grid(7, 20f), 4, 25f, 1);
            Assert.AreEqual(4, picked.Count);
        }

        [Test]
        public void Pick_RespectsMinGap()
        {
            List<Vector2> picked = ShopSpotLogic.Pick(Grid(7, 20f), 4, 25f, 7);
            for (int a = 0; a < picked.Count; a++)
                for (int b = a + 1; b < picked.Count; b++)
                    Assert.GreaterOrEqual(Vector2.Distance(picked[a], picked[b]), 25f);
        }

        [Test]
        public void Pick_TooFewRooms_ReturnsWhatFits()
        {
            // 기본 맵(30×30, 방 6)에선 4곳이 안 들어갈 수 있다 — 조용히 줄인다, 예외 없음.
            // 2×2 격자(간격 10)는 대각선 최대 거리가 14.1이라 25 간격에는 딱 1곳만 들어간다.
            List<Vector2> picked = ShopSpotLogic.Pick(Grid(2, 10f), 4, 25f, 1);
            Assert.AreEqual(1, picked.Count);
        }

        [Test]
        public void Pick_SameSeed_SameResult()
        {
            CollectionAssert.AreEqual(ShopSpotLogic.Pick(Grid(7, 20f), 4, 25f, 42),
                                      ShopSpotLogic.Pick(Grid(7, 20f), 4, 25f, 42));
        }

        [Test]
        public void Pick_EmptyOrZero_IsEmpty()
        {
            Assert.IsEmpty(ShopSpotLogic.Pick(null, 4, 25f, 1));
            Assert.IsEmpty(ShopSpotLogic.Pick(new List<Vector2>(), 4, 25f, 1));
            Assert.IsEmpty(ShopSpotLogic.Pick(Grid(3, 30f), 0, 25f, 1));
        }

        [Test]
        public void Resolve_HandPlaced_UsesPlacedAsIs()
        {
            // 창고 맵(2026-09-07)은 방 목록이 비어 있고 상점 4곳이 손으로 찍혀 있다 — 간격 규칙을 안 탄다
            var placed = new List<Vector2> { new Vector2(-36, -28), new Vector2(40, -24), new Vector2(-40, 36), new Vector2(36, 32) };
            List<Vector2> got = ShopSpotLogic.Resolve(true, placed, new List<Vector2>(), 4, 25f, 1);
            CollectionAssert.AreEqual(placed, got);
        }

        [Test]
        public void Resolve_HandPlacedButEmpty_FallsBackToRooms()
        {
            List<Vector2> got = ShopSpotLogic.Resolve(true, new List<Vector2>(), Grid(7, 20f), 4, 25f, 1);
            Assert.AreEqual(4, got.Count);
        }

        [Test]
        public void Resolve_Procedural_IgnoresPlaced()
        {
            var placed = new List<Vector2> { new Vector2(1, 1) };
            List<Vector2> got = ShopSpotLogic.Resolve(false, placed, Grid(7, 20f), 4, 25f, 1);
            Assert.AreEqual(4, got.Count);
            CollectionAssert.DoesNotContain(got, new Vector2(1, 1));
        }

        [Test]
        public void Contains_UsesRadius_IgnoresHeight()
        {
            Assert.IsTrue(ShopSpotLogic.Contains(new Vector3(0, 0, 0), new Vector3(2.9f, 5f, 0), 3f));
            Assert.IsFalse(ShopSpotLogic.Contains(new Vector3(0, 0, 0), new Vector3(3.1f, 0, 0), 3f));
        }
    }
}
