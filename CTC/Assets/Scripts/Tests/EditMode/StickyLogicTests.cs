using CTC.Runtime.Economy;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class StickyLogicTests
    {
        [Test]
        public void SlowMultiplier_FromSlowRatio()
        {
            Assert.AreEqual(0.6f, StickyLogic.SpeedMultiplier(0.4f), 1e-4f);
            Assert.AreEqual(0.25f, StickyLogic.SpeedMultiplier(0.75f), 1e-4f);
            Assert.AreEqual(1f, StickyLogic.SpeedMultiplier(0f), 1e-4f);
        }

        [Test]
        public void Inside_UsesHorizontalRadius()
        {
            Assert.IsTrue(StickyLogic.Inside(Vector3.zero, new Vector3(2f, 3f, 0f), 2.5f));
            Assert.IsFalse(StickyLogic.Inside(Vector3.zero, new Vector3(2.6f, 0f, 0f), 2.5f));
        }

        [Test]
        public void Ready_WhenCooldownElapsed()
        {
            Assert.IsFalse(StickyLogic.Ready(10.0, 9.9));
            Assert.IsTrue(StickyLogic.Ready(10.0, 10.0));
        }

        [Test]
        public void MinMultiplier_NoSources_IsOne() // 아무도 안 걸었으면 제 속도
        {
            Assert.AreEqual(1f, StickyLogic.MinMultiplier(new float[0]), 1e-4f);
            Assert.AreEqual(1f, StickyLogic.MinMultiplier(null), 1e-4f);
        }

        [Test]
        public void MinMultiplier_OverlappingPatches_StrongestWins() // 겹쳐 밟으면 더 센 쪽
        {
            Assert.AreEqual(0.25f, StickyLogic.MinMultiplier(new[] { 0.6f, 0.25f }), 1e-4f);
            Assert.AreEqual(0.25f, StickyLogic.MinMultiplier(new[] { 0.25f, 0.6f }), 1e-4f);
        }

        [Test]
        public void MinMultiplier_WeakerPatchExpires_SurvivorStillSlows() // 하나가 꺼져도 남은 게 이어진다
        {
            // 예전 버그: 나간 장판이 1을 덮어써서 아직 밟고 있는 장판 위에서 제 속도로 달렸다.
            Assert.AreEqual(0.25f, StickyLogic.MinMultiplier(new[] { 0.25f }), 1e-4f);
        }
    }
}
