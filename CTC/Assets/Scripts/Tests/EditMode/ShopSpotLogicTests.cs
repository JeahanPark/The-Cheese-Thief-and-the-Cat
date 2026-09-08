using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class ShopSpotLogicTests
    {
        private const float RADIUS = 3f;

        /// <summary>넉넉한 방(원반이 들어가는 크기)만 깔린 격자 — 크기 규칙을 안 타는 옛 테스트용.</summary>
        private static List<RoomRect> Grid(int _n, float _step)
        {
            var list = new List<RoomRect>();
            for (int i = 0; i < _n; i++)
                for (int j = 0; j < _n; j++)
                    list.Add(Room($"R{i}_{j}", new Vector2(i * _step, j * _step), new Vector2(9f, 9f)));
            return list;
        }

        private static RoomRect Room(string _id, Vector2 _center, Vector2 _size) =>
            new RoomRect { Id = _id, Center = _center, Size = _size };

        private static List<Vector2> Centers(List<RoomRect> _rooms)
        {
            var list = new List<Vector2>();
            foreach (RoomRect r in _rooms) list.Add(r.Center);
            return list;
        }

        [Test]
        public void Pick_ReturnsRequestedCount_WhenRoomsSuffice()
        {
            List<Vector2> picked = ShopSpotLogic.Pick(Grid(7, 20f), 4, 25f, RADIUS, 1);
            Assert.AreEqual(4, picked.Count);
        }

        [Test]
        public void Pick_RespectsMinGap()
        {
            List<Vector2> picked = ShopSpotLogic.Pick(Grid(7, 20f), 4, 25f, RADIUS, 7);
            for (int a = 0; a < picked.Count; a++)
                for (int b = a + 1; b < picked.Count; b++)
                    Assert.GreaterOrEqual(Vector2.Distance(picked[a], picked[b]), 25f);
        }

        [Test]
        public void Pick_TooFewRooms_ReturnsWhatFits()
        {
            // 기본 맵(30×30, 방 6)에선 4곳이 안 들어갈 수 있다 — 조용히 줄인다, 예외 없음.
            // 2×2 격자(간격 10)는 대각선 최대 거리가 14.1이라 25 간격에는 딱 1곳만 들어간다.
            List<Vector2> picked = ShopSpotLogic.Pick(Grid(2, 10f), 4, 25f, RADIUS, 1);
            Assert.AreEqual(1, picked.Count);
        }

        [Test]
        public void Pick_SameSeed_SameResult()
        {
            CollectionAssert.AreEqual(ShopSpotLogic.Pick(Grid(7, 20f), 4, 25f, RADIUS, 42),
                                      ShopSpotLogic.Pick(Grid(7, 20f), 4, 25f, RADIUS, 42));
        }

        [Test]
        public void Pick_EmptyOrZero_IsEmpty()
        {
            Assert.IsEmpty(ShopSpotLogic.Pick(null, 4, 25f, RADIUS, 1));
            Assert.IsEmpty(ShopSpotLogic.Pick(new List<RoomRect>(), 4, 25f, RADIUS, 1));
            Assert.IsEmpty(ShopSpotLogic.Pick(Grid(3, 30f), 0, 25f, RADIUS, 1));
        }

        // ===== 방 크기 규칙 (2026-09-08) =====

        [Test]
        public void RoomHoldsShop_NeedsDiameterPlusWalls()
        {
            // 반경 3 → 지름 6, 벽 여유 1.0 ⇒ 7 m부터 들어간다
            Assert.IsTrue(ShopSpotLogic.RoomHoldsShop(new Vector2(7f, 9f), RADIUS));
            Assert.IsFalse(ShopSpotLogic.RoomHoldsShop(new Vector2(4f, 6f), RADIUS));   // 기본 맵 R2
            Assert.IsFalse(ShopSpotLogic.RoomHoldsShop(new Vector2(9f, 6.9f), RADIUS)); // 한 변만 좁아도 안 된다
        }

        [Test]
        public void Pick_PrefersRoomThatHoldsTheShop()
        {
            // 좁은 방이 목록 앞에 여럿 있어도 넉넉한 방이 먼저 뽑혀야 한다 —
            // 상점이 4×6 m 방에 앉으면 그 안에서 창고를 세울 자리가 사방으로 벽이다(2026-09-08).
            var rooms = new List<RoomRect>
            {
                Room("small0", new Vector2(0f, 0f), new Vector2(4f, 6f)),
                Room("small1", new Vector2(10f, 0f), new Vector2(5f, 5f)),
                Room("small2", new Vector2(0f, 10f), new Vector2(5.5f, 7f)),
                Room("big", new Vector2(10f, 10f), new Vector2(7f, 9f)),
            };
            for (int seed = 0; seed < 20; seed++)
            {
                List<Vector2> picked = ShopSpotLogic.Pick(rooms, 1, 25f, RADIUS, seed);
                Assert.AreEqual(new Vector2(10f, 10f), picked[0], $"seed {seed}");
            }
        }

        [Test]
        public void Pick_FallsBackToCrampedRooms_WhenNoneHoldTheShop()
        {
            // 작은 맵에는 들어가는 방이 하나도 없을 수 있다 — 그래도 상점은 서야 한다.
            var rooms = new List<RoomRect> { Room("small", new Vector2(3f, 3f), new Vector2(4f, 6f)) };
            List<Vector2> picked = ShopSpotLogic.Pick(rooms, 2, 25f, RADIUS, 1);
            Assert.AreEqual(1, picked.Count);
            Assert.AreEqual(new Vector2(3f, 3f), picked[0]);
        }

        // ===== 손 배치 =====

        [Test]
        public void Resolve_HandPlaced_UsesPlacedAsIs()
        {
            // 창고 맵(2026-09-07)은 방 목록이 비어 있고 상점 4곳이 손으로 찍혀 있다 — 간격·크기 규칙을 안 탄다
            var placed = new List<Vector2> { new Vector2(-36, -28), new Vector2(40, -24), new Vector2(-40, 36), new Vector2(36, 32) };
            List<Vector2> got = ShopSpotLogic.Resolve(true, placed, new List<RoomRect>(), 4, 25f, RADIUS, 1);
            CollectionAssert.AreEqual(placed, got);
        }

        [Test]
        public void Resolve_HandPlacedButEmpty_FallsBackToRooms()
        {
            List<Vector2> got = ShopSpotLogic.Resolve(true, new List<Vector2>(), Grid(7, 20f), 4, 25f, RADIUS, 1);
            Assert.AreEqual(4, got.Count);
        }

        [Test]
        public void Resolve_Procedural_IgnoresPlaced()
        {
            var placed = new List<Vector2> { new Vector2(1, 1) };
            List<RoomRect> rooms = Grid(7, 20f);
            List<Vector2> got = ShopSpotLogic.Resolve(false, placed, rooms, 4, 25f, RADIUS, 1);
            Assert.AreEqual(4, got.Count);
            CollectionAssert.DoesNotContain(got, new Vector2(1, 1));
            CollectionAssert.IsSubsetOf(got, Centers(rooms));
        }

        [Test]
        public void Contains_UsesRadius_IgnoresHeight()
        {
            Assert.IsTrue(ShopSpotLogic.Contains(new Vector3(0, 0, 0), new Vector3(2.9f, 5f, 0), 3f));
            Assert.IsFalse(ShopSpotLogic.Contains(new Vector3(0, 0, 0), new Vector3(3.1f, 0, 0), 3f));
        }
    }
}
