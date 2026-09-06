using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>방 모듈 조립 규칙(2026-09-07) — 문 변 판정·발자국·모듈 선택.</summary>
    public class RoomModuleLogicTests
    {
        private static RoomRect Room(float _cx, float _cy, float _w, float _h) =>
            new RoomRect { Id = "R", Center = new Vector2(_cx, _cy), Size = new Vector2(_w, _h) };

        private static CorridorSeg Seg(float _x1, float _y1, float _x2, float _y2, float _w = 3f) =>
            new CorridorSeg { From = new Vector2(_x1, _y1), To = new Vector2(_x2, _y2), Width = _w };

        /// <summary>변 한가운데에 복도 끝점이 닿으면 그 변이 문이다. 순서 N·S·E·W.</summary>
        [Test]
        public void DoorSides_MarksSidesTouchedByCorridorEnds()
        {
            RoomRect room = Room(0f, 0f, 12f, 11f);
            var corridors = new List<CorridorSeg>
            {
                Seg(6f, 0f, 11f, 0f),      // E 변에서 나감
                Seg(0f, -5.5f, 0f, -12f),  // S 변에서 나감
            };
            bool[] sides = RoomModuleLogic.DoorSides(room, corridors);
            CollectionAssert.AreEqual(new[] { false, true, true, false }, sides);
        }

        [Test]
        public void DoorSides_IgnoresCorridorsOfOtherRooms()
        {
            RoomRect room = Room(0f, 0f, 12f, 11f);
            var corridors = new List<CorridorSeg> { Seg(30f, 0f, 40f, 0f) };
            CollectionAssert.AreEqual(new[] { false, false, false, false }, RoomModuleLogic.DoorSides(room, corridors));
        }

        [Test]
        public void FootprintKey_BigAndSmall()
        {
            Assert.AreEqual(RoomModuleLogic.FOOTPRINT_BIG, RoomModuleLogic.FootprintKey(new Vector2(12f, 11f)));
            Assert.AreEqual(RoomModuleLogic.FOOTPRINT_SMALL, RoomModuleLogic.FootprintKey(new Vector2(10f, 9f)));
            Assert.AreEqual("", RoomModuleLogic.FootprintKey(new Vector2(7f, 7f)), "모르는 크기는 빈 키 — 큐브로 후퇴");
        }

        [Test]
        public void PickModule_DeterministicAndInRange()
        {
            var ids = new List<string> { "A", "B", "C" };
            string first = RoomModuleLogic.PickModule(ids, 5, 777);
            Assert.AreEqual(first, RoomModuleLogic.PickModule(ids, 5, 777));
            CollectionAssert.Contains(ids, first);
            Assert.AreEqual("", RoomModuleLogic.PickModule(new List<string>(), 5, 777));
        }

        [Test]
        public void SideYaw_FacesOutward()
        {
            Assert.AreEqual(0f, RoomModuleLogic.SideYaw(0));     // N → +z
            Assert.AreEqual(180f, RoomModuleLogic.SideYaw(1));   // S
            Assert.AreEqual(90f, RoomModuleLogic.SideYaw(2));    // E → +x
            Assert.AreEqual(270f, RoomModuleLogic.SideYaw(3));   // W
        }
    }
}
