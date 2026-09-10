using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 조준 방향의 양자화·포장(2026-09-10). 요구사항은 셋이다 —
    /// **왕복이 허용 오차 안**, **경계각(±180°·수직)에서 안 튄다**, **2바이트에 든다**.
    /// 네트워크도 트랜스폼도 안 쓰는 순수 함수만 본다.
    /// </summary>
    public class GazeAimLogicTests
    {
        /// <summary>yaw 반칸(0.703°) + pitch 반칸(0.354°)이 낼 수 있는 최대 각오차보다 살짝 위.</summary>
        private const float TOLERANCE_DEG = 0.9f;

        [Test]
        public void Forward_PacksToDefaultValue()
        {
            Assert.AreEqual(GazeAimLogic.FORWARD, GazeAimLogic.Pack(Vector3.forward));
        }

        /// <summary>NetworkVariable 기본값(0)이 곧 정면이어야 스폰 첫 프레임에 눈이 안 튄다.</summary>
        [Test]
        public void DefaultValue_UnpacksToForward()
        {
            Assert.Less(Vector3.Angle(GazeAimLogic.Unpack(GazeAimLogic.FORWARD), Vector3.forward), 1e-3f);
        }

        [Test]
        public void RoundTrip_AcrossWholeSphere_StaysWithinTolerance()
        {
            for (float yaw = -180f; yaw < 180f; yaw += 7f)
            {
                for (float pitch = -89f; pitch <= 89f; pitch += 7f)
                {
                    Vector3 dir = GazeAimLogic.Direction(yaw, pitch);
                    Vector3 back = GazeAimLogic.Unpack(GazeAimLogic.Pack(dir));
                    Assert.LessOrEqual(Vector3.Angle(dir, back), TOLERANCE_DEG,
                        $"yaw={yaw} pitch={pitch}");
                }
            }
        }

        [Test]
        public void RoundTrip_AlwaysUnitLength()
        {
            for (float yaw = -180f; yaw < 180f; yaw += 13f)
            {
                Vector3 back = GazeAimLogic.Unpack(GazeAimLogic.Pack(GazeAimLogic.Direction(yaw, 40f)));
                Assert.AreEqual(1f, back.magnitude, 1e-4f);
            }
        }

        /// <summary>길이가 1이 아닌 방향(카메라 forward는 늘 1이지만 속도 벡터는 아니다)도 같아야 한다.</summary>
        [Test]
        public void Pack_IgnoresMagnitude()
        {
            Vector3 dir = GazeAimLogic.Direction(37f, -21f);
            Assert.AreEqual(GazeAimLogic.Pack(dir), GazeAimLogic.Pack(dir * 9.3f));
        }

        [Test]
        public void Pack_ZeroVector_IsForward()
        {
            Assert.AreEqual(GazeAimLogic.FORWARD, GazeAimLogic.Pack(Vector3.zero));
        }

        // ── 경계각 ──────────────────────────────────────────────────────────

        /// <summary>±180°는 같은 방향이다 — 다른 칸에 떨어지면 뒤를 볼 때마다 눈이 튄다.</summary>
        [Test]
        public void Yaw_PlusAndMinus180_AreTheSameCell()
        {
            Assert.AreEqual(GazeAimLogic.QuantizeYaw(180f), GazeAimLogic.QuantizeYaw(-180f));
        }

        /// <summary>360°를 감는다 — 뒤를 도는 순간 반대편으로 날아가면 안 된다.</summary>
        [Test]
        public void Yaw_WrapsAroundTheSeam()
        {
            Vector3 a = GazeAimLogic.Unpack(GazeAimLogic.PackAngles(179.6f, 0f));
            Vector3 b = GazeAimLogic.Unpack(GazeAimLogic.PackAngles(-179.6f, 0f));
            Assert.LessOrEqual(Vector3.Angle(a, b), GazeAimLogic.YAW_STEP_DEG + 0.01f);
        }

        /// <summary>1° 씩 한 바퀴 — 이웃 칸 사이 간격이 한 칸을 절대 안 넘는다(=튀는 지점이 없다).</summary>
        [Test]
        public void Yaw_FullSweep_NeverJumps()
        {
            Vector3 prev = GazeAimLogic.Unpack(GazeAimLogic.PackAngles(-180f, 0f));
            for (float yaw = -179f; yaw <= 180f; yaw += 1f)
            {
                Vector3 now = GazeAimLogic.Unpack(GazeAimLogic.PackAngles(yaw, 0f));
                Assert.LessOrEqual(Vector3.Angle(prev, now), GazeAimLogic.YAW_STEP_DEG + 0.01f,
                    $"yaw={yaw}에서 튀었다");
                prev = now;
            }
        }

        /// <summary>위아래는 **감지 않고 자른다** — 수직을 넘겨 뒤집히면 눈이 뒤통수를 본다.</summary>
        [Test]
        public void Pitch_BeyondVertical_ClampsInsteadOfWrapping()
        {
            Assert.AreEqual(GazeAimLogic.QuantizePitch(90f), GazeAimLogic.QuantizePitch(150f));
            Assert.AreEqual(GazeAimLogic.QuantizePitch(-90f), GazeAimLogic.QuantizePitch(-150f));
        }

        [Test]
        public void Pitch_StraightUpAndDown_SurviveRoundTrip()
        {
            Assert.Less(Vector3.Angle(GazeAimLogic.Unpack(GazeAimLogic.Pack(Vector3.up)),
                                      Vector3.up), TOLERANCE_DEG);
            Assert.Less(Vector3.Angle(GazeAimLogic.Unpack(GazeAimLogic.Pack(Vector3.down)),
                                      Vector3.down), TOLERANCE_DEG);
        }

        /// <summary>수직에서는 yaw가 뜻이 없다 — 어떤 yaw로 접어도 같은 방향이 나와야 한다.</summary>
        [Test]
        public void Pitch_AtPole_YawDoesNotMatter()
        {
            Vector3 a = GazeAimLogic.Unpack(GazeAimLogic.PackAngles(0f, 90f));
            Vector3 b = GazeAimLogic.Unpack(GazeAimLogic.PackAngles(140f, 90f));
            Assert.LessOrEqual(Vector3.Angle(a, b), 0.01f);
        }

        // ── 방어 ────────────────────────────────────────────────────────────

        [Test]
        public void Quantize_NaN_IsForward()
        {
            Assert.AreEqual(0, GazeAimLogic.QuantizeYaw(float.NaN));
            Assert.AreEqual(0, GazeAimLogic.QuantizePitch(float.NaN));
        }

        [Test]
        public void Quantize_Infinity_IsForward()
        {
            Assert.AreEqual(0, GazeAimLogic.QuantizeYaw(float.PositiveInfinity));
        }

        /// <summary>칸 번호가 sbyte 범위를 벗어나면 상·하위 바이트가 서로를 먹는다.</summary>
        [Test]
        public void Quantize_StaysInsideSignedByte()
        {
            for (float yaw = -720f; yaw <= 720f; yaw += 3f)
            {
                int q = GazeAimLogic.QuantizeYaw(yaw);
                Assert.GreaterOrEqual(q, -128);
                Assert.LessOrEqual(q, 127);
            }
            Assert.GreaterOrEqual(GazeAimLogic.QuantizePitch(-999f), -127);
            Assert.LessOrEqual(GazeAimLogic.QuantizePitch(999f), 127);
        }

        /// <summary>yaw와 pitch가 서로 안 샌다 — 한 바이트씩 나눠 쓰는 포장의 유일한 위험.</summary>
        [Test]
        public void PackedBytes_DoNotBleedIntoEachOther()
        {
            for (int yaw = -128; yaw <= 127; yaw += 17)
            {
                for (int pitch = -127; pitch <= 127; pitch += 19)
                {
                    short packed = GazeAimLogic.PackAngles(
                        yaw * GazeAimLogic.YAW_STEP_DEG, pitch * GazeAimLogic.PITCH_STEP_DEG);
                    GazeAimLogic.UnpackAngles(packed, out float y, out float p);
                    Assert.AreEqual(yaw, Mathf.RoundToInt(y / GazeAimLogic.YAW_STEP_DEG));
                    Assert.AreEqual(pitch, Mathf.RoundToInt(p / GazeAimLogic.PITCH_STEP_DEG));
                }
            }
        }

        /// <summary>가만히 있으면 0바이트다 — 같은 방향이 같은 값을 내야 「바뀔 때만 보낸다」가 성립한다.</summary>
        [Test]
        public void Pack_IsDeterministic()
        {
            Vector3 dir = GazeAimLogic.Direction(63.4f, -12.7f);
            Assert.AreEqual(GazeAimLogic.Pack(dir), GazeAimLogic.Pack(dir));
        }

        /// <summary>실제로 쓰는 손잡이(눈 최대각 30°) 안에서 한 칸이 1.5°를 안 넘는다.</summary>
        [Test]
        public void StepSize_IsFineEnoughForEyes()
        {
            Assert.Less(GazeAimLogic.YAW_STEP_DEG, 1.5f);
            Assert.Less(GazeAimLogic.PITCH_STEP_DEG, 1.5f);
        }
    }
}
