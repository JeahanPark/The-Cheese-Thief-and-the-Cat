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
        public void Pick_TooFewRooms_RelaxesGapToFillCount()
        {
            // 2×2 격자(간격 10)는 대각선 최대 거리가 14.1이라 25 간격에는 1곳뿐이다.
            // 이제는 간격을 반씩 줄여 목표를 채운다(25 → 12.5 → 6.25) —
            // 상점이 1곳만 서면 「지키는 동안 나머지가 열린다」가 통째로 사라진다.
            List<Vector2> picked = ShopSpotLogic.Pick(Grid(2, 10f), 4, 25f, RADIUS, 1);
            Assert.AreEqual(4, picked.Count);
        }

        [Test]
        public void Pick_RelaxedGap_NeverGoesBelowDiscDiameter()
        {
            // 원반이 겹치면 「다른 곳」이 아니다 — 반경 3 → 바닥 6 m.
            // 간격 5의 격자에서는 목표 4를 못 채워도 6 m 밑으로 내려가지 않는다.
            List<Vector2> picked = ShopSpotLogic.Pick(Grid(2, 5f), 4, 25f, RADIUS, 1);
            for (int a = 0; a < picked.Count; a++)
                for (int b = a + 1; b < picked.Count; b++)
                    Assert.GreaterOrEqual(Vector2.Distance(picked[a], picked[b]), RADIUS * 2f);
            Assert.Less(picked.Count, 4);
        }

        [Test]
        public void Pick_SameSeed_SameResult_EvenWhenRelaxed()
        {
            CollectionAssert.AreEqual(ShopSpotLogic.Pick(Grid(2, 10f), 4, 25f, RADIUS, 9),
                                      ShopSpotLogic.Pick(Grid(2, 10f), 4, 25f, RADIUS, 9));
        }

        // ===== 개수 규칙 (계획 3 Task 2) =====

        [Test]
        public void CountFor_LargeMapGetsLargeCount()
        {
            Assert.AreEqual(4, ShopSpotLogic.CountFor(60f, 4, 2, 40f));
            Assert.AreEqual(4, ShopSpotLogic.CountFor(40f, 4, 2, 40f));   // 경계값은 대형
        }

        [Test]
        public void CountFor_SmallMapGetsSmallCount()
        {
            Assert.AreEqual(2, ShopSpotLogic.CountFor(21f, 4, 2, 40f));
            Assert.AreEqual(2, ShopSpotLogic.CountFor(15f, 4, 2, 40f));   // 기본 맵 30×30
        }

        [Test]
        public void CountFor_NeverBelowOne()
        {
            Assert.AreEqual(1, ShopSpotLogic.CountFor(15f, 4, 0, 40f));
            Assert.AreEqual(1, ShopSpotLogic.CountFor(-5f, 4, -3, 40f));
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

        // ===== 손 배치 후보 추첨 (계획 3 Task 2) =====

        /// <summary>창고 맵의 상점 후보 8곳 — 어느 둘을 잡아도 25 m 이상 떨어져 있다.</summary>
        private static List<Vector2> WarehouseCandidates() => new List<Vector2>
        {
            new Vector2(-36, -28), new Vector2(40, -24), new Vector2(-40, 36), new Vector2(32, 32),
            new Vector2(0, -6), new Vector2(0, 32), new Vector2(-40, 4), new Vector2(40, 4),
        };

        [Test]
        public void PickPlaced_MoreCandidatesThanTarget_PicksTarget()
        {
            List<Vector2> got = ShopSpotLogic.PickPlaced(WarehouseCandidates(), 4, 25f, RADIUS, 1);
            Assert.AreEqual(4, got.Count);
            CollectionAssert.IsSubsetOf(got, WarehouseCandidates());
        }

        [Test]
        public void PickPlaced_DifferentSeeds_GiveDifferentSets()
        {
            // 후보 8 중 4 = 70가지 — 시드를 바꿔도 전부 같으면 무작위가 죽은 것이다.
            var first = ShopSpotLogic.PickPlaced(WarehouseCandidates(), 4, 25f, RADIUS, 1);
            bool differs = false;
            for (int seed = 2; seed < 40 && !differs; seed++)
            {
                List<Vector2> other = ShopSpotLogic.PickPlaced(WarehouseCandidates(), 4, 25f, RADIUS, seed);
                foreach (Vector2 p in other) { if (!first.Contains(p)) { differs = true; break; } }
            }
            Assert.IsTrue(differs, "시드를 바꿔도 조합이 같다");
        }

        [Test]
        public void PickPlaced_SameSeed_SameResult()
        {
            CollectionAssert.AreEqual(ShopSpotLogic.PickPlaced(WarehouseCandidates(), 4, 25f, RADIUS, 7),
                                      ShopSpotLogic.PickPlaced(WarehouseCandidates(), 4, 25f, RADIUS, 7));
        }

        [Test]
        public void PickPlaced_KeepsMinGap()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                List<Vector2> got = ShopSpotLogic.PickPlaced(WarehouseCandidates(), 4, 25f, RADIUS, seed);
                Assert.AreEqual(4, got.Count, $"seed {seed}");
                for (int a = 0; a < got.Count; a++)
                    for (int b = a + 1; b < got.Count; b++)
                        Assert.GreaterOrEqual(Vector2.Distance(got[a], got[b]), 25f, $"seed {seed}");
            }
        }

        [Test]
        public void PickPlaced_CandidatesAtOrBelowTarget_UsesAll()
        {
            var four = new List<Vector2> { new Vector2(-36, -28), new Vector2(40, -24), new Vector2(-40, 36), new Vector2(32, 32) };
            CollectionAssert.AreEqual(four, ShopSpotLogic.PickPlaced(four, 4, 25f, RADIUS, 3));

            var two = new List<Vector2> { new Vector2(-36, -28), new Vector2(40, -24) };
            CollectionAssert.AreEqual(two, ShopSpotLogic.PickPlaced(two, 4, 25f, RADIUS, 3));
        }

        [Test]
        public void Resolve_HandPlaced_PicksFromCandidates()
        {
            List<Vector2> got = ShopSpotLogic.Resolve(true, WarehouseCandidates(), new List<RoomRect>(), 4, 25f, RADIUS, 5);
            Assert.AreEqual(4, got.Count);
            CollectionAssert.IsSubsetOf(got, WarehouseCandidates());
        }

        [Test]
        public void Contains_UsesRadius_IgnoresHeight()
        {
            Assert.IsTrue(ShopSpotLogic.Contains(new Vector3(0, 0, 0), new Vector3(2.9f, 5f, 0), 3f));
            Assert.IsFalse(ShopSpotLogic.Contains(new Vector3(0, 0, 0), new Vector3(3.1f, 0, 0), 3f));
        }
    }
}
