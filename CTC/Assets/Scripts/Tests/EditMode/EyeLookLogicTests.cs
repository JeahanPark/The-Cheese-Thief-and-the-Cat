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

        // ---------------------------------------------------------------------
        // 두 눈동자가 **같은 쪽**을 보는가 (2026-09-10)
        //
        // 실플레이에서 "오른쪽을 보면 눈동자 두개가 둘다 오른쪽으로 가야 되는데"가 나왔다.
        // 그때 이 파일에는 **두 눈동자의 자리를 함께 보는 단언이 하나도 없었다** - 그래서
        // 눈마다 따로 클램프하던 코드가 전부 통과했다. 여기서 실측 지오메트리로 직접 잰다.
        //
        // 눈 축의 출처는 `Tools/CTC/Dev/Probe Eye Geometry`가 실제 메시를 잰 결과이고,
        // 정면축은 `Tools/CTC/Dev/Bake Eye Anchors`가 프리팹에 구운 값 그대로다.
        // 머리 본의 정면은 **종마다 다르다** - 두 FBX가 다르게 리깅돼 고양이는 +z에 가깝고
        // 쥐는 +y에 가깝다. 그래서 코드에 박지 않고 모델에서 재 굽는다.
        // ---------------------------------------------------------------------

        private const float FLATTEN = 0.25f;    // BalanceInfo.EyeFlatten
        private const float PUPIL_D = 0.42f;    // BalanceInfo.EyePupilOffsetRatio
        private const float MAX_DEG = 30f;      // BalanceInfo.EyeMaxAngleDeg

        /// <summary>
        /// 한 종의 머리 본 프레임. 가로축은 좌우 대칭축이라 늘 x이고, 위는 정면에서 뽑는다
        /// (<c>cross(forward, right)</c>) - 손으로 두 번 적으면 둘이 갈린다.
        /// </summary>
        private struct Head
        {
            public string Name;
            public Vector3 Axis;      // 오른눈이 쉴 때 보는 방향
            public Vector3 Forward;   // 머리 본 로컬 정면(EyeAnchorBaker가 모델에서 재 굽는 값)

            public Vector3 Up
            {
                get { return Vector3.Cross(Forward.normalized, Vector3.right).normalized; }
            }
        }

        private static readonly Head CAT = new Head
        {
            Name = "고양이",
            Axis = new Vector3(0.500f, 0.476f, 0.724f),
            Forward = new Vector3(0f, 0.3070406f, 0.9516964f),
        };

        /// <summary>쥐는 좌우 눈이 126° 벌어진 **옆눈**이라 조건이 훨씬 가혹하다.</summary>
        private static readonly Head MOUSE = new Head
        {
            Name = "쥐",
            Axis = new Vector3(0.892f, 0.396f, 0.216f),
            Forward = new Vector3(0f, 0.87630075f, 0.48176444f),
        };

        private static readonly Head[] HEADS = { CAT, MOUSE };

        /// <summary>머리 본 로컬 조준 - 정면에서 좌우(x축) yaw · 위아래 pitch만큼 튼 방향.</summary>
        private static Vector3 Aim(Head _head, float _yawDeg, float _pitchDeg)
        {
            float yaw = _yawDeg * Mathf.Deg2Rad;
            float pitch = _pitchDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(pitch);
            return (_head.Forward.normalized * (c * Mathf.Cos(yaw)) +
                    Vector3.right * (c * Mathf.Sin(yaw)) +
                    _head.Up.normalized * Mathf.Sin(pitch)).normalized;
        }

        /// <summary>그 조준에서 두 눈동자가 앉는 자리(머리 본 로컬, 흰자 반지름 단위).</summary>
        private static void Pupils(Head _head, float _yawDeg, float _pitchDeg,
                                   out Vector3 _right, out Vector3 _left)
        {
            Vector3 restR = _head.Axis.normalized;
            Vector3 restL = EyeLookLogic.Mirror(restR);
            Vector3 pivot = EyeLookLogic.GazePivot(_head.Forward, restR, restL);
            Quaternion swing = EyeLookLogic.GazeSwing(pivot, Aim(_head, _yawDeg, _pitchDeg), MAX_DEG);
            _right = EyeLookLogic.PupilOffset(restR, swing * restR, FLATTEN, PUPIL_D);
            _left = EyeLookLogic.PupilOffset(restL, swing * restL, FLATTEN, PUPIL_D);
        }

        /// <summary>
        /// **요구사항 1번**: 오른쪽을 보면 두 눈동자가 **둘 다** 오른쪽으로 간다.
        /// 가로(좌우 대칭축 x) 부호가 같아야 하고, 한 눈만 일하면 안 된다.
        /// 고친 뒤에도 이 단언을 되돌리면(눈마다 따로 클램프) 고양이가 0.07 비율로 떨어져 빨개진다.
        /// </summary>
        [Test]
        public void BothPupils_ShiftToTheSameSide_WhenAimTurnsRight()
        {
            foreach (Head head in HEADS)
            {
                Pupils(head, 0f, 0f, out Vector3 midR, out Vector3 midL);
                Pupils(head, 45f, 0f, out Vector3 rightR, out Vector3 rightL);

                float dR = rightR.x - midR.x;
                float dL = rightL.x - midL.x;

                Assert.Greater(dR, 0.01f, head.Name + " - 오른쪽을 보는데 오른눈 눈동자가 안 갔다");
                Assert.Greater(dL, 0.01f, head.Name + " - 오른쪽을 보는데 왼눈 눈동자가 안 갔다(또는 반대로 갔다)");

                float ratio = Mathf.Min(dR, dL) / Mathf.Max(dR, dL);
                Assert.Greater(ratio, 0.5f,
                    head.Name + " - 한 눈만 일한다: 오른눈 " + dR + " vs 왼눈 " + dL);
            }
        }

        /// <summary>왼쪽도 대칭으로 성립해야 한다 - 한쪽만 맞으면 우연이다.</summary>
        [Test]
        public void BothPupils_ShiftToTheSameSide_WhenAimTurnsLeft()
        {
            foreach (Head head in HEADS)
            {
                Pupils(head, 0f, 0f, out Vector3 midR, out Vector3 midL);
                Pupils(head, -45f, 0f, out Vector3 leftR, out Vector3 leftL);

                Assert.Less(leftR.x - midR.x, -0.01f, head.Name + " - 왼쪽인데 오른눈 눈동자가 안 갔다");
                Assert.Less(leftL.x - midL.x, -0.01f, head.Name + " - 왼쪽인데 왼눈 눈동자가 안 갔다");
            }
        }

        /// <summary>
        /// **요구사항 2번**: 정면에서 봤을 때 두 눈동자가 **같은 방향으로** 움직인다.
        /// 앞눈(고양이)은 화면 평면(가로 x · 세로 = 머리 위)에서 방향이 거의 일치해야 한다 -
        /// 눈마다 따로 클램프하던 예전 코드는 여기서 cos = 0.16(약 81° 어긋남)이었다.
        /// </summary>
        [Test]
        public void BothPupils_MoveInTheSameScreenDirection_ForFrontFacingEyes()
        {
            Pupils(CAT, 0f, 0f, out Vector3 midR, out Vector3 midL);
            Pupils(CAT, 45f, 0f, out Vector3 rightR, out Vector3 rightL);

            Vector3 up = CAT.Up.normalized;
            var dR = new Vector2(rightR.x - midR.x, Vector3.Dot(rightR - midR, up));
            var dL = new Vector2(rightL.x - midL.x, Vector3.Dot(rightL - midL, up));

            float cos = Vector2.Dot(dR.normalized, dL.normalized);
            Assert.Greater(cos, 0.5f, "두 눈동자가 화면에서 갈라진다 - cos=" + cos);
        }

        /// <summary>위아래는 갈리면 안 된다 - 세로는 좌우 대칭이라 언제나 같이 가야 한다.</summary>
        [Test]
        public void BothPupils_RiseAndFallTogether()
        {
            foreach (Head head in HEADS)
            {
                Vector3 up = head.Up.normalized;
                Pupils(head, 0f, 0f, out Vector3 midR, out Vector3 midL);
                Pupils(head, 0f, 25f, out Vector3 upR, out Vector3 upL);

                float dR = Vector3.Dot(upR - midR, up);
                float dL = Vector3.Dot(upL - midL, up);
                Assert.Greater(dR, 0.005f, head.Name + " - 위를 보는데 오른눈 눈동자가 안 올라갔다");
                Assert.Greater(dL, 0.005f, head.Name + " - 위를 보는데 왼눈 눈동자가 안 올라갔다");
                Assert.AreEqual(dR, dL, 1e-4f, head.Name + " - 두 눈동자 높이가 갈렸다");
            }
        }

        /// <summary>두 눈은 **같은 각도**만큼 돈다(요크). 한 눈만 한계에 걸리면 갈려 보인다.</summary>
        [Test]
        public void GazeSwing_TurnsBothEyesByTheSameAngle()
        {
            foreach (Head head in HEADS)
            {
                Vector3 restR = head.Axis.normalized;
                Vector3 restL = EyeLookLogic.Mirror(restR);
                Vector3 pivot = EyeLookLogic.GazePivot(head.Forward, restR, restL);

                foreach (float yaw in new[] { -80f, -30f, 0f, 30f, 80f })
                {
                    Quaternion swing = EyeLookLogic.GazeSwing(pivot, Aim(head, yaw, 0f), MAX_DEG);
                    float a = Vector3.Angle(restR, swing * restR);
                    float b = Vector3.Angle(restL, swing * restL);
                    Assert.AreEqual(a, b, 0.01f, head.Name + " yaw=" + yaw + " 두 눈이 다른 각도로 돌았다");
                    Assert.LessOrEqual(a, MAX_DEG + 0.01f, head.Name + " - 눈이 한계를 넘어 돌았다");
                }
            }
        }

        /// <summary>
        /// 정면을 조준하면 눈은 안 돈다 - 눈동자가 흰자 한가운데 앉아 좌우 여유를 다 갖는다.
        /// 예전에는 쥐가 정면에서도 이미 한계(30°)에 붙어 있어 더 돌 자리가 없었다.
        /// </summary>
        [Test]
        public void GazeSwing_AimAtHeadForward_DoesNotTurn()
        {
            foreach (Head head in HEADS)
            {
                Vector3 restR = head.Axis.normalized;
                Vector3 restL = EyeLookLogic.Mirror(restR);
                Vector3 pivot = EyeLookLogic.GazePivot(head.Forward, restR, restL);
                Quaternion swing = EyeLookLogic.GazeSwing(pivot, Aim(head, 0f, 0f), MAX_DEG);

                Assert.Less(Vector3.Angle(restR, swing * restR), 0.01f, head.Name);
                Assert.Less(Vector3.Angle(restL, swing * restL), 0.01f, head.Name);
            }
        }

        /// <summary>정면축을 안 굽혔으면 두 눈의 대칭축으로 물러선다 - 좌우 한가운데는 지켜야 한다.</summary>
        [Test]
        public void GazePivot_WithoutBakedForward_FallsBackToTheMidline()
        {
            Vector3 restR = CAT.Axis.normalized;
            Vector3 pivot = EyeLookLogic.GazePivot(Vector3.zero, restR, EyeLookLogic.Mirror(restR));
            Assert.AreEqual(0f, pivot.x, 1e-5f);
            Assert.AreEqual(1f, pivot.magnitude, 1e-4f);
        }

        /// <summary>굽힌 정면축이 있으면 그것을 쓴다 - 대칭축이 아니라.</summary>
        [Test]
        public void GazePivot_UsesTheBakedForward()
        {
            Vector3 restR = MOUSE.Axis.normalized;
            Vector3 pivot = EyeLookLogic.GazePivot(MOUSE.Forward, restR, EyeLookLogic.Mirror(restR));
            Assert.Less(Vector3.Angle(pivot, MOUSE.Forward), 0.01f);
        }

        /// <summary>
        /// 순수 함수가 **실제로 그려지는 자리**와 같은가. <see cref="EyeLook"/>는 흰자를
        /// <c>LookRotation(rest)</c>로 돌리고 <c>(1,1,flatten)</c>으로 눌러 눈동자를 자식으로 단다.
        /// 그 사슬을 그대로 재현해 대조한다 - 안 그러면 테스트가 코드와 다른 걸 재게 된다.
        /// </summary>
        [Test]
        public void PupilOffset_MatchesTheTransformChain()
        {
            Vector3 rest = CAT.Axis.normalized;
            Vector3 gaze = (Quaternion.Euler(7f, -13f, 0f) * rest).normalized;

            Quaternion rot = Quaternion.LookRotation(rest);
            Vector3 childLocal = (Quaternion.Inverse(rot) * gaze) * PUPIL_D;      // EyeLook이 넣는 값
            var scaled = new Vector3(childLocal.x, childLocal.y, childLocal.z * FLATTEN);
            Vector3 viaTransform = rot * scaled;                                   // 부모 스케일 -> 부모 회전

            Vector3 pure = EyeLookLogic.PupilOffset(rest, gaze, FLATTEN, PUPIL_D);
            Assert.Less(Vector3.Distance(viaTransform, pure), 1e-5f);
        }

        /// <summary>눈동자는 눌린 흰자 표면을 따라야 한다 - 떠도 파묻혀도 안 된다.</summary>
        [Test]
        public void PupilOffset_FollowsTheFlattenedSurface()
        {
            Vector3 rest = CAT.Axis.normalized;
            for (float yaw = -MAX_DEG; yaw <= MAX_DEG + 0.001f; yaw += 10f)
            {
                Vector3 gaze = (Quaternion.Euler(0f, yaw, 0f) * rest).normalized;
                Vector3 off = EyeLookLogic.PupilOffset(rest, gaze, FLATTEN, PUPIL_D);

                float along = Vector3.Dot(off, rest);
                float across = (off - along * rest).magnitude;
                float norm = across * across + (along / FLATTEN) * (along / FLATTEN);
                Assert.AreEqual(PUPIL_D * PUPIL_D, norm, 1e-4f, "yaw=" + yaw + " 에서 표면을 벗어났다");
            }
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
