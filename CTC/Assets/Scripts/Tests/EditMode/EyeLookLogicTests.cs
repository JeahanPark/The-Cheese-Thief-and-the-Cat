using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 눈동자 계산(2026-09-08). 눈이 **뒤집히지 않는 것**과 **스냅하지 않는 것**이 요구사항이라
    /// 그 둘을 각도로 잰다. 트랜스폼·시간을 안 쓰는 순수 함수만 본다.
    /// </summary>
    public class EyeLookLogicTests
    {
        private static readonly Vector3 REST = new Vector3(1f, 0f, 0f);

        [Test]
        public void ClampGaze_WithinLimit_LooksAtAim()
        {
            Vector3 aim = Quaternion.Euler(0f, 10f, 0f) * REST;
            Vector3 gaze = EyeLookLogic.ClampGaze(REST, aim, 30f);
            Assert.Less(Vector3.Angle(gaze, aim), 0.01f);
        }

        [Test]
        public void ClampGaze_BeyondLimit_StopsAtMaxAngle()
        {
            Vector3 aim = Quaternion.Euler(0f, 120f, 0f) * REST;
            Vector3 gaze = EyeLookLogic.ClampGaze(REST, aim, 30f);
            Assert.AreEqual(30f, Vector3.Angle(REST, gaze), 0.01f);
        }

        /// <summary>정반대를 봐도 눈이 뒤집히면 안 된다 — 회전축이 없는 경우.</summary>
        [Test]
        public void ClampGaze_OppositeAim_NeverFlips()
        {
            Vector3 gaze = EyeLookLogic.ClampGaze(REST, -REST, 30f);
            Assert.LessOrEqual(Vector3.Angle(REST, gaze), 30f + 0.01f);
        }

        [Test]
        public void ClampGaze_ZeroAim_KeepsRest()
        {
            Vector3 gaze = EyeLookLogic.ClampGaze(REST, Vector3.zero, 30f);
            Assert.Less(Vector3.Angle(REST, gaze), 0.01f);
        }

        [Test]
        public void ClampGaze_ZeroMaxAngle_NeverMoves()
        {
            Vector3 aim = Quaternion.Euler(0f, 45f, 0f) * REST;
            Assert.Less(Vector3.Angle(REST, EyeLookLogic.ClampGaze(REST, aim, 0f)), 0.01f);
        }

        [Test]
        public void ClampGaze_ResultIsUnitLength()
        {
            Vector3 aim = Quaternion.Euler(20f, 45f, 0f) * REST * 7f;
            Assert.AreEqual(1f, EyeLookLogic.ClampGaze(REST * 3f, aim, 30f).magnitude, 1e-4f);
        }

        /// <summary>한 프레임에 목표까지 다 가면 스냅이다 — 일부만 가야 한다.</summary>
        [Test]
        public void Follow_OneFrame_MovesPartway()
        {
            Vector3 target = Quaternion.Euler(0f, 30f, 0f) * REST;
            Vector3 next = EyeLookLogic.Follow(REST, target, 10f, 1f / 60f);

            float moved = Vector3.Angle(REST, next);
            Assert.Greater(moved, 0.5f, "전혀 안 움직이면 죽은 눈이다");
            Assert.Less(moved, 25f, "한 프레임에 다 가면 스냅이다");
        }

        [Test]
        public void Follow_ManyFrames_ConvergesToTarget()
        {
            Vector3 target = Quaternion.Euler(0f, 30f, 0f) * REST;
            Vector3 cur = REST;
            for (int i = 0; i < 300; i++) cur = EyeLookLogic.Follow(cur, target, 10f, 1f / 60f);
            Assert.Less(Vector3.Angle(cur, target), 0.5f);
        }

        [Test]
        public void Follow_ZeroDeltaTime_HoldsStill()
        {
            Vector3 target = Quaternion.Euler(0f, 30f, 0f) * REST;
            Assert.Less(Vector3.Angle(REST, EyeLookLogic.Follow(REST, target, 10f, 0f)), 0.01f);
        }

        /// <summary>프레임률이 달라도 같은 시간에 같은 만큼 간다(지수 감쇠).</summary>
        [Test]
        public void Follow_IsFrameRateIndependent()
        {
            Vector3 target = Quaternion.Euler(0f, 60f, 0f) * REST;

            Vector3 slow = REST;
            for (int i = 0; i < 30; i++) slow = EyeLookLogic.Follow(slow, target, 8f, 1f / 30f);

            Vector3 fast = REST;
            for (int i = 0; i < 120; i++) fast = EyeLookLogic.Follow(fast, target, 8f, 1f / 120f);

            Assert.AreEqual(Vector3.Angle(REST, slow), Vector3.Angle(REST, fast), 1f);
        }

        [Test]
        public void BlinkCover_ClosedAtMiddle_OpenAtEdges()
        {
            Assert.AreEqual(0f, EyeLookLogic.BlinkCover(0f, 0.2f), 1e-4f);
            Assert.AreEqual(1f, EyeLookLogic.BlinkCover(0.1f, 0.2f), 1e-4f);
            Assert.AreEqual(0f, EyeLookLogic.BlinkCover(0.2f, 0.2f), 1e-4f);
            Assert.AreEqual(0f, EyeLookLogic.BlinkCover(5f, 0.2f), 1e-4f);
        }

        [Test]
        public void BlinkCover_ZeroDuration_NeverCloses()
        {
            Assert.AreEqual(0f, EyeLookLogic.BlinkCover(0.05f, 0f), 1e-4f);
        }

        [Test]
        public void NextBlinkDelay_StaysInRange()
        {
            Assert.AreEqual(2f, EyeLookLogic.NextBlinkDelay(2f, 7f, 0f), 1e-4f);
            Assert.AreEqual(7f, EyeLookLogic.NextBlinkDelay(2f, 7f, 1f), 1e-4f);
            Assert.AreEqual(4.5f, EyeLookLogic.NextBlinkDelay(2f, 7f, 0.5f), 1e-4f);
        }

        /// <summary>CSV에 min/max가 뒤집혀 들어와도 음수 대기가 나오면 안 된다.</summary>
        [Test]
        public void NextBlinkDelay_SwappedBounds_StillPositive()
        {
            float delay = EyeLookLogic.NextBlinkDelay(7f, 2f, 0.25f);
            Assert.GreaterOrEqual(delay, 2f);
            Assert.LessOrEqual(delay, 7f);
        }

        [Test]
        public void Mirror_FlipsOnlyX()
        {
            Vector3 m = EyeLookLogic.Mirror(new Vector3(0.9f, 0.4f, 0.2f));
            Assert.AreEqual(-0.9f, m.x, 1e-5f);
            Assert.AreEqual(0.4f, m.y, 1e-5f);
            Assert.AreEqual(0.2f, m.z, 1e-5f);
        }

        /// <summary>두 눈이 같은 조준을 봐도 각자 자기 쉬는 방향에서 클램프된다(안쪽으로 뒤집히지 않게).</summary>
        [Test]
        public void BothEyes_ClampAroundOwnRest()
        {
            Vector3 restR = new Vector3(0.892f, 0.396f, 0.216f).normalized;
            Vector3 restL = EyeLookLogic.Mirror(restR);
            Vector3 aim = Vector3.forward;

            Assert.AreEqual(30f, Vector3.Angle(restR, EyeLookLogic.ClampGaze(restR, aim, 30f)), 0.01f);
            Assert.AreEqual(30f, Vector3.Angle(restL, EyeLookLogic.ClampGaze(restL, aim, 30f)), 0.01f);
        }
    }
}
