using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 벽감 자리 고르기. 여기가 무너지면 직업권 두 장이 상자 벽을 다 차지해
    /// **치즈 벽이 0개**가 된다 — 기본 맵에서 실제로 일어났다(2026-08-12, qa_v38 6/12).
    /// </summary>
    public class AlcoveSpotLogicTests
    {
        // ---- 기본 맵(Layout_Sketch01)의 실제 지형 — 벽감이 3개로 줄었던 그 판이다 ----

        private static List<RoomRect> SketchRooms() => new()
        {
            new RoomRect { Id = "R3", Center = new Vector2(0f, 1.5f), Size = new Vector2(7f, 9f) },
            new RoomRect { Id = "R5", Center = new Vector2(-10.5f, -10.5f), Size = new Vector2(7f, 8f) },
            new RoomRect { Id = "R7", Center = new Vector2(0f, 12f), Size = new Vector2(5f, 4f) },
            new RoomRect { Id = "R4", Center = new Vector2(11f, 2f), Size = new Vector2(5.5f, 7f) },
            new RoomRect { Id = "R2", Center = new Vector2(-12.5f, 3f), Size = new Vector2(4f, 6f) },
            new RoomRect { Id = "R1", Center = new Vector2(10.5f, 12f), Size = new Vector2(4.5f, 5.5f) },
        };

        private static List<CorridorSeg> SketchCorridors()
        {
            var ends = new (float, float, float, float)[]
            {
                (-3.5f, 2.5f, -10.5f, 2.5f), (3.5f, 2.5f, 8.25f, 2.5f), (0f, 6f, 0f, 10f),
                (2.5f, 12f, 8.25f, 12f), (11f, 9.25f, 11f, 5.5f), (-11f, -6.5f, -12.5f, -6.5f),
                (-12.5f, -6.5f, -12.5f, 0f), (1.7f, -3f, 1.7f, -12f), (1.7f, -7f, -3f, -7f),
                (-3f, -7f, -3f, -12.5f), (-3f, -12.5f, -4f, -12.5f), (1.7f, -8f, 4f, -8f),
                (1.7f, -12f, 3f, -12f), (3f, -12f, 3f, -13.5f), (-4f, -12.5f, -7f, -12.5f),
                (10f, -1.5f, 10f, -13f), (10f, -8f, 7f, -8f), (10f, -5f, 13f, -5f),
                (13f, -5f, 13f, -11f), (13f, -11f, 10f, -11f), (10f, -13f, 8f, -13f),
            };
            var segs = new List<CorridorSeg>();
            foreach ((float fx, float fy, float tx, float ty) in ends)
            {
                segs.Add(new CorridorSeg
                {
                    From = new Vector2(fx, fy), To = new Vector2(tx, ty), Width = 1.6f,
                });
            }
            return segs;
        }

        private static List<CheesePlacement> SketchCheese()
        {
            var spots = new (float, float, float)[]
            {
                (-9f, -7f, 2f), (0f, -2f, 1.5f), (-2f, 4f, 1.5f), (9f, 1f, 1.5f),
                (0f, 11f, 1.5f), (-11.5f, 5f, 1.5f), (8.75f, 10f, 1.5f),
            };
            var cheese = new List<CheesePlacement>();
            foreach ((float x, float y, float r) in spots)
            {
                cheese.Add(new CheesePlacement { Pos = new Vector2(x, y), Radius = r });
            }
            return cheese;
        }

        [Test]
        public void IdealDistanceAlone_ComesUpShort_OnTheDefaultMap()
        {
            // 회귀의 뿌리 — 이상 거리만 쓰면 방 6개 중 3개만 자리가 나온다.
            // 이 전제가 바뀌면(맵이나 규칙이 바뀌어 4개 이상 나오면) 폴백 검증도 다시 봐야 한다.
            int strict = 0;
            foreach (RoomRect room in SketchRooms())
            {
                if (AlcoveSpotLogic.TryWallSpot(room, SketchCorridors(), SketchCheese(),
                        AlcoveSpotLogic.MIN_DOOR_DIST, AlcoveSpotLogic.MIN_CHEESE_DIST, out _))
                {
                    strict++;
                }
            }
            Assert.AreEqual(3, strict);
        }

        [Test]
        public void Fallback_FillsTheFloor_OnTheDefaultMap()
        {
            // 하한 4 = 강아지 1 + 직업권 2 + 치즈 벽 1. 3개면 치즈 벽이 0개가 된다.
            List<AlcoveSpotLogic.Spot> spots = AlcoveSpotLogic.CollectSpots(
                SketchRooms(), SketchCorridors(), SketchCheese(), 4);

            Assert.GreaterOrEqual(spots.Count, 4);
        }

        [Test]
        public void Fallback_StillRespectsPhysicalMinimums()
        {
            // 완화해도 물리 최소는 지킨다 — 이 아래로는 문이 막히거나 지점 원반에 겹친다.
            List<AlcoveSpotLogic.Spot> spots = AlcoveSpotLogic.CollectSpots(
                SketchRooms(), SketchCorridors(), SketchCheese(), 4);

            foreach (AlcoveSpotLogic.Spot spot in spots)
            {
                foreach (CorridorSeg seg in SketchCorridors())
                {
                    Assert.GreaterOrEqual(Vector2.Distance(seg.From, spot.Pos), AlcoveSpotLogic.FLOOR_DOOR_DIST);
                    Assert.GreaterOrEqual(Vector2.Distance(seg.To, spot.Pos), AlcoveSpotLogic.FLOOR_DOOR_DIST);
                }
                foreach (CheesePlacement cp in SketchCheese())
                {
                    Assert.GreaterOrEqual(Vector2.Distance(cp.Pos, spot.Pos), AlcoveSpotLogic.FLOOR_CHEESE_DIST);
                }
            }
        }

        [Test]
        public void RoomyMap_NeverNeedsTheFallback()
        {
            // 자리가 넉넉하면 전부 이상 거리를 지킨다 — 폴백이 멀쩡한 맵을 건드리면 안 된다.
            var rooms = new List<RoomRect>
            {
                new RoomRect { Id = "A", Center = new Vector2(-20f, 0f), Size = new Vector2(12f, 12f) },
                new RoomRect { Id = "B", Center = new Vector2(20f, 0f), Size = new Vector2(12f, 12f) },
                new RoomRect { Id = "C", Center = new Vector2(0f, 20f), Size = new Vector2(12f, 12f) },
                new RoomRect { Id = "D", Center = new Vector2(0f, -20f), Size = new Vector2(12f, 12f) },
            };
            var corridors = new List<CorridorSeg>();
            var cheese = new List<CheesePlacement>();

            List<AlcoveSpotLogic.Spot> spots = AlcoveSpotLogic.CollectSpots(rooms, corridors, cheese, 4);

            Assert.AreEqual(4, spots.Count);
            foreach (AlcoveSpotLogic.Spot spot in spots)
            {
                foreach (CheesePlacement cp in cheese)
                {
                    Assert.GreaterOrEqual(Vector2.Distance(cp.Pos, spot.Pos), AlcoveSpotLogic.MIN_CHEESE_DIST);
                }
            }
        }

        [Test]
        public void EvenTheFloorCannotInventSpots()
        {
            // 물리 최소로도 안 나오는 방은 비운다 — 자리를 지어내지 않는다.
            var rooms = new List<RoomRect>
            {
                new RoomRect { Id = "A", Center = Vector2.zero, Size = new Vector2(4f, 4f) },
            };
            var cheese = new List<CheesePlacement>
            {
                new CheesePlacement { Pos = Vector2.zero, Radius = 1.5f },   // 온 벽이 2.6m 안이다
            };

            Assert.IsEmpty(AlcoveSpotLogic.CollectSpots(rooms, new List<CorridorSeg>(), cheese, 4));
        }

        [Test]
        public void SameInput_SameSpots()
        {
            // 같은 레이아웃이면 같은 자리 — QA 재현성
            List<AlcoveSpotLogic.Spot> a = AlcoveSpotLogic.CollectSpots(
                SketchRooms(), SketchCorridors(), SketchCheese(), 4);
            List<AlcoveSpotLogic.Spot> b = AlcoveSpotLogic.CollectSpots(
                SketchRooms(), SketchCorridors(), SketchCheese(), 4);

            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Pos, b[i].Pos);
                Assert.AreEqual(a[i].YawDeg, b[i].YawDeg);
            }
        }
    }
}
