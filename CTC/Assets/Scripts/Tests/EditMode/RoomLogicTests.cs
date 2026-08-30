using CTC.Runtime.Room;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class RoomLogicTests
    {
        [Test]
        public void CanStart_HostWithTwo_True()
        {
            Assert.IsTrue(RoomLogic.CanStart(2, true));
        }

        [Test]
        public void CanStart_HostAlone_False()
        {
            Assert.IsFalse(RoomLogic.CanStart(1, true));
        }

        [Test]
        public void CanStart_GuestEvenWhenFull_False()
        {
            Assert.IsFalse(RoomLogic.CanStart(RoomLogic.MAX_PLAYERS, false));
        }

        [Test]
        public void StartHint_Guest_TellsToWait()
        {
            StringAssert.Contains("방장", RoomLogic.StartHint(4, false));
        }

        [Test]
        public void StartHint_HostTooFew_TellsMinimum()
        {
            StringAssert.Contains(RoomLogic.MIN_PLAYERS.ToString(), RoomLogic.StartHint(1, true));
        }

        [Test]
        public void DisplayName_Blank_FallsBackToSlotNumber()
        {
            Assert.AreEqual("플레이어 3", RoomLogic.DisplayName("   ", 2));
        }

        [Test]
        public void DisplayName_Trimmed()
        {
            Assert.AreEqual("야옹이", RoomLogic.DisplayName("  야옹이 ", 0));
        }

        [Test]
        public void CountLabel_ShowsCapacity()
        {
            Assert.AreEqual($"3 / {RoomLogic.MAX_PLAYERS}", RoomLogic.CountLabel(3));
        }

        /// <summary>자리는 슬롯 번호로 고정이다 — 인원이 늘어도 이미 선 사람이 안 밀린다.</summary>
        [Test]
        public void StandPosition_SameSlotSameSpot()
        {
            Vector3 a = RoomLogic.StandPosition(3, RoomLogic.MAX_PLAYERS, 5f);
            Vector3 b = RoomLogic.StandPosition(3, RoomLogic.MAX_PLAYERS, 5f);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void StandPosition_OnCircleOfRadius()
        {
            Vector3 p = RoomLogic.StandPosition(4, 10, 5f);
            Assert.AreEqual(5f, new Vector2(p.x, p.z).magnitude, 0.001f);
            Assert.AreEqual(0f, p.y, 0.001f);
        }

        [Test]
        public void StandPosition_DifferentSlotsDoNotOverlap()
        {
            Vector3 a = RoomLogic.StandPosition(0, 10, 5f);
            Vector3 b = RoomLogic.StandPosition(1, 10, 5f);
            Assert.Greater(Vector3.Distance(a, b), 1f);
        }

        [Test]
        public void StandPosition_ZeroCount_IsOrigin()
        {
            Assert.AreEqual(Vector3.zero, RoomLogic.StandPosition(0, 0, 5f));
        }

        /// <summary>서 있는 자리에서 원 중심을 본다 — 바깥쪽 반대편(180도)이다.</summary>
        [Test]
        public void StandYaw_FacesInward()
        {
            Assert.AreEqual(180f, RoomLogic.StandYaw(0, 10), 0.001f);
        }
    }
}
