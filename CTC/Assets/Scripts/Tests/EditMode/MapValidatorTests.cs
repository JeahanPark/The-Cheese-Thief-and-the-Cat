using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class MapValidatorTests
    {
        private static RoomRect Room(string id, float cx, float cz, float sx, float sz) =>
            new RoomRect { Id = id, Center = new Vector2(cx, cz), Size = new Vector2(sx, sz) };

        private static CorridorSeg Seg(float ax, float az, float bx, float bz, float w = 1.6f) =>
            new CorridorSeg { From = new Vector2(ax, az), To = new Vector2(bx, bz), Width = w };

        // R3(중앙)·R5(남서)만 있고 통로 1개로 이은 최소 유효 맵
        private static (List<RoomRect>, List<CorridorSeg>) MinimalConnected()
        {
            var rooms = new List<RoomRect> { Room("R3", 0, 1.5f, 7, 9), Room("R5", -10.5f, -10.5f, 7, 8) };
            var corr = new List<CorridorSeg> { Seg(0f, -3f, -10.5f, -6.5f) };
            return (rooms, corr);
        }

        [Test]
        public void V4_OverlappingRooms_Fails()
        {
            var rooms = new List<RoomRect> { Room("A", 0, 0, 5, 5), Room("B", 2, 2, 5, 5) };
            Assert.IsFalse(MapValidator.NoRoomOverlap(rooms));
        }

        [Test]
        public void V4_SeparatedRooms_Passes()
        {
            var rooms = new List<RoomRect> { Room("A", 0, 0, 5, 5), Room("B", 10, 10, 5, 5) };
            Assert.IsTrue(MapValidator.NoRoomOverlap(rooms));
        }

        [Test]
        public void V3_AllRoomsReachable_Passes()
        {
            var (rooms, corr) = MinimalConnected();
            Assert.IsTrue(MapValidator.AllConnected(rooms, corr, "R3"));
        }

        [Test]
        public void V3_OrphanRoom_Fails()
        {
            var rooms = new List<RoomRect> { Room("R3", 0, 1.5f, 7, 9), Room("R5", -10.5f, -10.5f, 7, 8) };
            var corr = new List<CorridorSeg>(); // 통로 없음 → R5 고아
            Assert.IsFalse(MapValidator.AllConnected(rooms, corr, "R3"));
        }

        [Test]
        public void V6_NarrowCorridor_Fails()
        {
            var corr = new List<CorridorSeg> { Seg(0, 0, 5, 0, 1.2f) };
            Assert.IsFalse(MapValidator.CorridorsWideEnough(corr, 1.6f));
        }

        [Test]
        public void V11_CheeseGradesComplete_Passes()
        {
            var cheese = new List<CheesePlacement>();
            for (int g = 1; g <= 7; g++)
                cheese.Add(new CheesePlacement { Pos = new Vector2(g, 0), Grade = g, Radius = 2f });
            Assert.IsTrue(MapValidator.CheeseGradesComplete(cheese));
        }

        [Test]
        public void V11_MissingGrade_Fails()
        {
            var cheese = new List<CheesePlacement>();
            for (int g = 1; g <= 6; g++) // 7 누락
                cheese.Add(new CheesePlacement { Pos = new Vector2(g, 0), Grade = g, Radius = 2f });
            Assert.IsFalse(MapValidator.CheeseGradesComplete(cheese));
        }

        [Test]
        public void V9_FortressCheeseTooClose_Fails()
        {
            var forts = new List<FortressPlacement> { new() { Pos = new Vector2(0, 0), YawDeg = 0 } };
            var cheese = new List<CheesePlacement> { new() { Pos = new Vector2(2f, 0), Grade = 1, Radius = 2f } };
            Assert.IsFalse(MapValidator.FortressCheeseSpacing(forts, cheese, 3.7f));
        }

        [Test]
        public void V9_FortressCheeseFarEnough_Passes()
        {
            var forts = new List<FortressPlacement> { new() { Pos = new Vector2(0, 0), YawDeg = 0 } };
            var cheese = new List<CheesePlacement> { new() { Pos = new Vector2(4f, 0), Grade = 1, Radius = 2f } };
            Assert.IsTrue(MapValidator.FortressCheeseSpacing(forts, cheese, 3.7f));
        }
    }
}
