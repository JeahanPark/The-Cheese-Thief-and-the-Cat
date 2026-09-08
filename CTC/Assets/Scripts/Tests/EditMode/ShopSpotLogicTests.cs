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

        // ===== 원반 안을 본다 (2026-09-08) =====
        //
        // 왜 있나: `RoomHoldsShop`은 방 **크기만** 봤다. 넓은 방 한가운데 엄폐물이 서 있으면
        // 원반이 통째로 막히는데 아무도 안 봤고, 기본 맵의 상점 하나가 실제로 그렇게 앉아
        // (-10.5,-10.5) 요새·창고 둘 다 설치가 거절됐다. 「좌표가 차단물 안에 박힌다」 다섯 번째.
        //
        // 굽기 검사(MapPointAudit)로는 못 잡는다 — 상점 자리는 **판마다 시드로** 뽑아 굽기를 안 지난다.
        // 그래서 판정이 여기(자리를 고르는 곳)에 있고, 차단물 질의는 밖에서 주입한다.

        /// <summary>축 정렬 박스들을 차단물로 보는 질의 — 런타임의 격자(NavGrid) 자리에 들어간다.</summary>
        private static System.Func<Vector2, bool> BlockedBy(params Rect[] _boxes)
        {
            return _p =>
            {
                foreach (Rect box in _boxes) { if (box.Contains(_p)) return false; }
                return true;
            };
        }

        /// <summary>한 점을 중심으로 한 정사각 차단물.</summary>
        private static Rect Box(Vector2 _center, float _side) =>
            new Rect(_center.x - _side * 0.5f, _center.y - _side * 0.5f, _side, _side);

        /// <summary>
        /// **밀어내기로도 못 빠져나오는** 벽 한 변(m). 최대 밀어내기(1.5×3=4.5 m)보다 반폭이 커야
        /// 「이 자리는 못 쓴다」를 시험할 수 있다 — 작게 잡으면 `NudgeToOpen`이 옆으로 피해 버려
        /// 테스트가 재려던 것이 사라진다.
        /// </summary>
        private const float WALL = 12f;

        private static ShopSpotLogic.SpotCheck Check(System.Func<Vector2, bool> _isFree) =>
            new ShopSpotLogic.SpotCheck { IsFree = _isFree };

        [Test]
        public void ProbeRingRadius_LeavesRoomForThePlacement()
        {
            // 원반 3 m 안에 요새(반치수 1.2)가 통째로 들어가는 가장 바깥 고리는 1.8 m다.
            Assert.AreEqual(1.8f, ShopSpotLogic.ProbeRingRadius(3f, 1.2f), 1e-4f);
            // 설치물이 원반보다 크면 중심 한 점만 본다.
            Assert.AreEqual(0f, ShopSpotLogic.ProbeRingRadius(1f, 1.2f), 1e-4f);
        }

        [Test]
        public void ProbePoints_IsCenterPlusRing()
        {
            var probes = new List<Vector2>();
            ShopSpotLogic.ProbePoints(new Vector2(5f, -5f), RADIUS, 1.2f, probes);

            Assert.AreEqual(ShopSpotLogic.DISC_PROBE_RING + 1, probes.Count);
            Assert.AreEqual(new Vector2(5f, -5f), probes[0], "첫 탐침은 중심이다");
            for (int i = 1; i < probes.Count; i++)
            {
                Assert.AreEqual(1.8f, Vector2.Distance(probes[i], new Vector2(5f, -5f)), 1e-3f);
            }
        }

        [Test]
        public void FreeProbeCount_NoQuery_CountsEveryProbe()
        {
            Assert.AreEqual(ShopSpotLogic.DISC_PROBE_RING + 1,
                ShopSpotLogic.FreeProbeCount(Vector2.zero, RADIUS, null));
            Assert.AreEqual(ShopSpotLogic.DISC_PROBE_RING + 1,
                ShopSpotLogic.FreeProbeCount(Vector2.zero, RADIUS, new ShopSpotLogic.SpotCheck()));
        }

        [Test]
        public void FreeProbeCount_BlockerAtCenterEatsProbes()
        {
            // 원반 한가운데 4 m짜리 상자 — 중심과 둘레 대부분을 먹는다.
            ShopSpotLogic.SpotCheck check = Check(BlockedBy(Box(Vector2.zero, 4f)));
            int free = ShopSpotLogic.FreeProbeCount(Vector2.zero, RADIUS, check);
            Assert.Less(free, ShopSpotLogic.DISC_PROBE_RING + 1, $"열린 자리 {free}");
            Assert.IsFalse(ShopSpotLogic.CenterFree(Vector2.zero, check), "한가운데가 막혔다");
        }

        [Test]
        public void DiscUsable_NullCheck_IsAlwaysTrue()
        {
            // 검사를 안 주면 예전 그대로 — 이 기본값이 있어야 기존 호출부·테스트가 안 깨진다.
            Assert.IsTrue(ShopSpotLogic.DiscUsable(Vector2.zero, RADIUS, null));
        }

        [Test]
        public void DiscUsable_RejectsDiscWithBlockerInside()
        {
            Assert.IsFalse(ShopSpotLogic.DiscUsable(Vector2.zero, RADIUS,
                Check(BlockedBy(Box(Vector2.zero, 4f)))));
        }

        [Test]
        public void DiscUsable_AcceptsDiscWhoseBlockerIsOutside()
        {
            // 차단물이 원반 밖(12 m 떨어진 곳)이면 아무 상관이 없다.
            Assert.IsTrue(ShopSpotLogic.DiscUsable(Vector2.zero, RADIUS,
                Check(BlockedBy(Box(new Vector2(12f, 12f), 4f)))));
        }

        [Test]
        public void Pick_PrefersOpenRoomOverBlockedRoom()
        {
            // 두 방 다 넉넉하다(9×9) — 크기만 보던 규칙으로는 구분이 안 된다.
            // 막힌 쪽이 뽑히면 그 판의 상점은 요새·창고를 못 세운다.
            var rooms = new List<RoomRect>
            {
                Room("Blocked", new Vector2(0f, 0f), new Vector2(9f, 9f)),
                Room("Open", new Vector2(60f, 0f), new Vector2(9f, 9f)),
            };
            ShopSpotLogic.SpotCheck check = Check(BlockedBy(Box(Vector2.zero, WALL)));

            for (int seed = 0; seed < 20; seed++)
            {
                List<Vector2> got = ShopSpotLogic.Pick(rooms, 1, 25f, RADIUS, seed, check);
                Assert.AreEqual(1, got.Count, $"seed {seed}");
                Assert.AreEqual(new Vector2(60f, 0f), got[0], $"seed {seed} — 막힌 방이 뽑혔다");
            }
        }

        [Test]
        public void Pick_EveryRoomBlocked_StillPlacesShops()
        {
            // 후보를 다 걸러 상점이 0곳이 되면 안 된다 — 막힌 자리라도 서는 편이 낫다.
            var rooms = new List<RoomRect>
            {
                Room("A", new Vector2(0f, 0f), new Vector2(9f, 9f)),
                Room("B", new Vector2(60f, 0f), new Vector2(9f, 9f)),
            };
            ShopSpotLogic.SpotCheck check =
                Check(BlockedBy(Box(Vector2.zero, WALL), Box(new Vector2(60f, 0f), WALL)));

            List<Vector2> got = ShopSpotLogic.Pick(rooms, 2, 25f, RADIUS, 1, check);
            Assert.AreEqual(2, got.Count, "전부 막혀도 개수는 채운다");
        }

        [Test]
        public void PickPlaced_PrefersOpenCandidates()
        {
            // 손 배치 후보 8곳 중 넷이 막혔다면 오늘 서는 넷은 열린 쪽이어야 한다.
            List<Vector2> all = WarehouseCandidates();
            var boxes = new List<Rect>();
            for (int i = 0; i < 4; i++) boxes.Add(Box(all[i], WALL));
            ShopSpotLogic.SpotCheck check = Check(BlockedBy(boxes.ToArray()));

            // 기대값을 `DiscUsable`로 만들지 않는다 — 그러면 판정을 지웠을 때 테스트도 같이 무너져
            // 늘 초록이 된다(뮤테이션이 안 잡힌다). 열린 넷을 손으로 적는다.
            List<Vector2> openOnes = all.GetRange(4, 4);

            for (int seed = 0; seed < 20; seed++)
            {
                List<Vector2> got = ShopSpotLogic.PickPlaced(all, 4, 25f, RADIUS, seed, check);
                Assert.AreEqual(4, got.Count, $"seed {seed}");
                foreach (Vector2 p in got)
                {
                    Assert.Contains(p, openOnes, $"seed {seed} — 막힌 후보 {p}가 뽑혔다");
                }
            }
        }

        [Test]
        public void PickPlaced_FewCandidates_PutsOpenOnesFirst()
        {
            // 후보가 목표 이하라 전부 쓰더라도 **순서**는 열린 것부터다(그 순서가 스폰 순서다).
            var two = new List<Vector2> { new Vector2(0f, 0f), new Vector2(60f, 0f) };
            ShopSpotLogic.SpotCheck check = Check(BlockedBy(Box(Vector2.zero, WALL)));

            List<Vector2> got = ShopSpotLogic.PickPlaced(two, 4, 25f, RADIUS, 3, check);
            CollectionAssert.AreEqual(new List<Vector2> { new Vector2(60f, 0f), new Vector2(0f, 0f) }, got);
        }

        // ===== 고양이 스폰을 삼키지 않는다 (같은 층의 형제 결함) =====

        /// <summary>고양이가 서는 자리 — `PlayerSpawner.SpawnPosition`이 주는 중앙 라인.</summary>
        private static ShopSpotLogic.SpotCheck CatSpawnCheck() => new ShopSpotLogic.SpotCheck
        {
            KeepAway = CTC.Runtime.Lobby.PlayerSpawner.CatSpawnPoints(),
            KeepAwayMargin = 1f,
        };

        [Test]
        public void CatSpawnPoints_MatchTheSpawnFormula()
        {
            // 공식을 베끼지 않는다 — 여기서 어긋나면 상점만 옛 자리를 피하고 에러가 안 난다.
            List<Vector2> points = CTC.Runtime.Lobby.PlayerSpawner.CatSpawnPoints(2);
            Assert.AreEqual(2, points.Count);
            Assert.AreEqual(new Vector2(0f, 0f), points[0]);
            Assert.AreEqual(new Vector2(2f, 0f), points[1]);
        }

        [Test]
        public void SwallowsKeepAway_SeesTheCatSpawnInsideTheDisc()
        {
            Assert.IsTrue(ShopSpotLogic.SwallowsKeepAway(new Vector2(1f, 0f), RADIUS, CatSpawnCheck()));
            Assert.IsFalse(ShopSpotLogic.SwallowsKeepAway(new Vector2(40f, 0f), RADIUS, CatSpawnCheck()));
        }

        [Test]
        public void Pick_NeverSitsOnTheCatSpawn()
        {
            // 상점이 고양이 스폰 위에 앉으면 판이 열리자마자 고양이가 상점 한복판에 서서
            // 쥐가 사러 갈 수가 없다 — 자리를 고르는 쪽에서 뺀다.
            var rooms = new List<RoomRect>
            {
                Room("OnSpawn", new Vector2(0f, 0f), new Vector2(9f, 9f)),
                Room("Away", new Vector2(60f, 0f), new Vector2(9f, 9f)),
            };

            // 「그 방을 안 쓴다」가 아니라 **「스폰을 안 삼킨다」**를 잰다 — 밀어내기가 생긴 뒤로는
            // 스폰 위의 방도 옆으로 비켜서 쓸 수 있고, 그건 막을 이유가 없다.
            for (int seed = 0; seed < 20; seed++)
            {
                List<Vector2> got = ShopSpotLogic.Pick(rooms, 1, 25f, RADIUS, seed, CatSpawnCheck());
                Assert.AreEqual(1, got.Count, $"seed {seed}");
                Assert.IsFalse(ShopSpotLogic.SwallowsKeepAway(got[0], RADIUS, CatSpawnCheck()),
                    $"seed {seed} — 고양이 스폰 위에 앉았다: {got[0]}");
            }
        }

        [Test]
        public void Pick_OnlyCatSpawnRoom_StillPlacesAShop()
        {
            var rooms = new List<RoomRect> { Room("OnSpawn", new Vector2(0f, 0f), new Vector2(9f, 9f)) };
            List<Vector2> got = ShopSpotLogic.Pick(rooms, 1, 25f, RADIUS, 1, CatSpawnCheck());
            Assert.AreEqual(1, got.Count, "방이 하나뿐이어도 상점은 선다");
        }

        // ===== 막힌 자리를 옆으로 민다 =====
        //
        // 왜 거르기만으로 부족한가: 기본 맵은 방이 여섯뿐이고 2026-09-08 실측에서 방 중심이
        // 둘 다 나빴다((-10.5,-10.5) 한가운데가 엄폐물 · (10.5,12) 둘레가 전부 벽).
        // 전부 떨어뜨려도 후퇴 규칙이 그중 하나를 세우므로 **고쳐지는 게 없다**.

        [Test]
        public void NudgeToOpen_LeavesAGoodSpotAlone()
        {
            ShopSpotLogic.SpotCheck check = Check(BlockedBy(Box(new Vector2(40f, 40f), WALL)));
            Assert.AreEqual(Vector2.zero, ShopSpotLogic.NudgeToOpen(Vector2.zero, RADIUS, check));
        }

        [Test]
        public void NudgeToOpen_MovesOffABlockedCenter()
        {
            // 한가운데 4 m 상자 — 1.5 m 밖으로 나가면 상자 밖이다.
            ShopSpotLogic.SpotCheck check = Check(BlockedBy(Box(Vector2.zero, 4f)));
            Vector2 moved = ShopSpotLogic.NudgeToOpen(Vector2.zero, RADIUS, check);

            Assert.AreNotEqual(Vector2.zero, moved, "막힌 자리에서 안 움직였다");
            Assert.IsTrue(ShopSpotLogic.CenterFree(moved, check), "옮긴 자리도 막혀 있다");
            Assert.LessOrEqual(moved.magnitude,
                ShopSpotLogic.NUDGE_STEP_METERS * ShopSpotLogic.NUDGE_STEPS + 1e-3f,
                "너무 멀리 갔다 — 상점이 딴 방으로 넘어간다");
        }

        [Test]
        public void NudgeToOpen_GivesUpWhenEverythingNearbyIsWall()
        {
            // 밀어내기 사거리(4.5 m)보다 반폭이 큰 벽이면 옮길 데가 없다 — 원래 자리를 돌려준다.
            ShopSpotLogic.SpotCheck check = Check(BlockedBy(Box(Vector2.zero, WALL)));
            Assert.AreEqual(Vector2.zero, ShopSpotLogic.NudgeToOpen(Vector2.zero, RADIUS, check));
        }

        [Test]
        public void NudgeToOpen_NeverLandsOnTheCatSpawn()
        {
            // 스폰 위의 방은 옆으로 비켜서 쓴다 — 다만 비킨 자리도 스폰을 삼키면 안 된다.
            Vector2 moved = ShopSpotLogic.NudgeToOpen(Vector2.zero, RADIUS, CatSpawnCheck());
            Assert.IsFalse(ShopSpotLogic.SwallowsKeepAway(moved, RADIUS, CatSpawnCheck()),
                $"밀어낸 자리 {moved}가 아직 고양이 스폰을 문다");
        }

        [Test]
        public void NudgeToOpen_SameInputSameAnswer()
        {
            ShopSpotLogic.SpotCheck check = Check(BlockedBy(Box(Vector2.zero, 4f)));
            Assert.AreEqual(ShopSpotLogic.NudgeToOpen(Vector2.zero, RADIUS, check),
                            ShopSpotLogic.NudgeToOpen(Vector2.zero, RADIUS, check));
        }
    }
}
