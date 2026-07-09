using CTC.Runtime.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class CameraOrbitLogicTests
    {
        [Test]
        public void ClampPitch_WithinRange_Unchanged()
        {
            Assert.AreEqual(20f, CameraOrbitLogic.ClampPitch(20f, -30f, 70f));
        }

        [Test]
        public void ClampPitch_BelowMin_ClampsToMin()
        {
            Assert.AreEqual(-30f, CameraOrbitLogic.ClampPitch(-45f, -30f, 70f));
        }

        [Test]
        public void ClampPitch_AboveMax_ClampsToMax()
        {
            Assert.AreEqual(70f, CameraOrbitLogic.ClampPitch(90f, -30f, 70f));
        }

        [Test]
        public void WrapYaw_WithinRange_Unchanged()
        {
            Assert.AreEqual(90f, CameraOrbitLogic.WrapYaw(90f), 1e-4f);
        }

        [Test]
        public void WrapYaw_Over180_WrapsToNegative()
        {
            Assert.AreEqual(-170f, CameraOrbitLogic.WrapYaw(190f), 1e-4f);
        }

        [Test]
        public void WrapYaw_UnderMinus180_WrapsToPositive()
        {
            Assert.AreEqual(170f, CameraOrbitLogic.WrapYaw(-190f), 1e-4f);
        }

        [Test]
        public void Step_MouseRight_IncreasesYaw()
        {
            Vector2 next = CameraOrbitLogic.Step(new Vector2(0f, 20f), new Vector2(10f, 0f), 0.5f, -30f, 70f);
            Assert.AreEqual(5f, next.x, 1e-4f);
            Assert.AreEqual(20f, next.y, 1e-4f);
        }

        [Test]
        public void Step_MouseUp_DecreasesPitch_NonInverted()
        {
            // 마우스 위(+delta.y) = 시선 위 = 카메라가 내려감(pitch 감소)
            Vector2 next = CameraOrbitLogic.Step(new Vector2(0f, 20f), new Vector2(0f, 10f), 0.5f, -30f, 70f);
            Assert.AreEqual(15f, next.y, 1e-4f);
        }

        [Test]
        public void Step_PitchIsClamped()
        {
            Vector2 next = CameraOrbitLogic.Step(new Vector2(0f, -25f), new Vector2(0f, 100f), 0.5f, -30f, 70f);
            Assert.AreEqual(-30f, next.y, 1e-4f);
        }
    }
}
