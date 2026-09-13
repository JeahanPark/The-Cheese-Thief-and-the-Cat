using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 시선 → 셰이프키 가중치(2026-09-13). 수평은 **눈마다 따로**, 수직은 두 눈 공통이다.
    ///
    /// 처음엔 팩 키가 두 눈을 거울 대칭으로 묶어 평균할 수밖에 없었고, 그래서 「옆 목표를 쫓는」
    /// 시선이 0으로 뭉개졌다(인게임 yaw +75도에서 눈마다 49.3도 어긋남). 키를 좌우로 쪼갠 뒤로는
    /// **그 자세가 눈마다 제 값을 받아야 한다** — <see cref="LookingToTheSide_EachEyeGetsItsOwnDirection"/>이
    /// 이 작업의 존재 이유를 붙들고 있다.
    /// </summary>
    public class EyeBlendShapeLogicTests
    {
        // 옆눈: 쉬는 방향이 정면에서 좌우로 60도씩 벌어져 있다(쥐·고양이 둘 다 그런 꼴이다).
        private static readonly Vector3 PIVOT = Vector3.forward;
        private static readonly Vector3 REST_R =
            new Vector3(Mathf.Sin(Mathf.Deg2Rad * 60f), 0f, Mathf.Cos(Mathf.Deg2Rad * 60f));
        private static readonly Vector3 REST_L = EyeLookLogic.Mirror(REST_R);
        private const float MAX = 30f;

        private static EyeShapeWeights At(Vector3 _gazeR, Vector3 _gazeL, float _cover = 0f)
        {
            return EyeBlendShapeLogic.Weights(REST_R, REST_L, _gazeR, _gazeL, PIVOT, MAX, _cover);
        }

        [Test]
        public void Rest_AllDirectionWeightsAreZero()
        {
            EyeShapeWeights w = At(REST_R, REST_L);
            float[] all = { w.InR, w.OutR, w.InL, w.OutL, w.Up, w.Down };
            foreach (float v in all) Assert.AreEqual(0f, v, 0.01f);
        }

        [Test]
        public void BothEyesInward_BothInAreFull()
        {
            // 두 눈이 각자 코 쪽으로 최대각만큼 — 정면의 가까운 것을 모아 보는 자세다.
            Vector3 gazeR = Quaternion.AngleAxis(-MAX, Vector3.up) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(+MAX, Vector3.up) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.AreEqual(100f, w.InR, 0.5f);
            Assert.AreEqual(100f, w.InL, 0.5f);
            Assert.AreEqual(0f, w.OutR, 0.01f);
            Assert.AreEqual(0f, w.OutL, 0.01f);
        }

        [Test]
        public void BothEyesOutward_BothOutAreFull()
        {
            Vector3 gazeR = Quaternion.AngleAxis(+MAX, Vector3.up) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(-MAX, Vector3.up) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.AreEqual(100f, w.OutR, 0.5f);
            Assert.AreEqual(100f, w.OutL, 0.5f);
            Assert.AreEqual(0f, w.InR, 0.01f);
            Assert.AreEqual(0f, w.InL, 0.01f);
        }

        /// <summary>
        /// **이 작업의 존재 이유.** 두 눈을 월드에서 **같은 쪽**으로 돌리면(옆의 목표를 쫓는다)
        /// 한 눈은 안쪽, 다른 눈은 바깥쪽이 된다. 평균하던 시절엔 이게 서로 지워져 0이 됐다.
        /// 이제는 눈마다 제 방향을 온전히 받아야 한다.
        /// </summary>
        [Test]
        public void LookingToTheSide_EachEyeGetsItsOwnDirection()
        {
            Vector3 gazeR = Quaternion.AngleAxis(-MAX, Vector3.up) * REST_R;   // 오른눈: 안쪽
            Vector3 gazeL = Quaternion.AngleAxis(-MAX, Vector3.up) * REST_L;   // 왼눈: 바깥쪽
            EyeShapeWeights w = At(gazeR, gazeL);

            Assert.AreEqual(100f, w.InR, 0.5f, "오른눈이 안쪽을 못 받았다 — 평균으로 되돌아갔나");
            Assert.AreEqual(100f, w.OutL, 0.5f, "왼눈이 바깥쪽을 못 받았다 — 평균으로 되돌아갔나");
            Assert.AreEqual(0f, w.OutR, 0.01f);
            Assert.AreEqual(0f, w.InL, 0.01f);
        }

        [Test]
        public void LookingUp_DrivesUp_NotDown()
        {
            // 축 순서가 뒤집히면 **아래를 보게 된다** — Cross(up, rest)로 돌리면 고도각이 −30도다
            // (2026-09-13 계산으로 확인). 부호가 조용히 뒤집히는 자리라 여기 적어 둔다.
            Vector3 gazeR = Quaternion.AngleAxis(MAX, Vector3.Cross(REST_R, Vector3.up)) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(MAX, Vector3.Cross(REST_L, Vector3.up)) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.Greater(w.Up, 90f);
            Assert.AreEqual(0f, w.Down, 0.01f);
        }

        [Test]
        public void BeyondMaxAngle_StaysAtHundred()
        {
            Vector3 gazeR = Quaternion.AngleAxis(-MAX * 3f, Vector3.up) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(+MAX * 3f, Vector3.up) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.AreEqual(100f, w.InR, 0.01f);
            Assert.AreEqual(100f, w.InL, 0.01f);
        }

        [Test]
        public void SameEyeSameAxis_NeverBothNonZero()
        {
            for (int i = -12; i <= 12; i++)
            {
                for (int j = -12; j <= 12; j += 4)
                {
                    Vector3 gazeR = Quaternion.AngleAxis(i * 5f, Vector3.up) * REST_R;
                    Vector3 gazeL = Quaternion.AngleAxis(j * 5f, Vector3.up) * REST_L;
                    EyeShapeWeights w = At(gazeR, gazeL);
                    Assert.IsTrue(w.InR <= 0.01f || w.OutR <= 0.01f, $"오른눈 In/Out 동시 점등 i={i} j={j}");
                    Assert.IsTrue(w.InL <= 0.01f || w.OutL <= 0.01f, $"왼눈 In/Out 동시 점등 i={i} j={j}");
                    Assert.IsTrue(w.Up <= 0.01f || w.Down <= 0.01f, $"Up/Down 동시 점등 i={i} j={j}");
                }
            }
        }

        /// <summary>
        /// 감김은 **100이 아니라 140까지** 간다 — 팩 키를 100%로 두면 두께가 31% 남아
        /// 「실눈」으로 읽힌다(2026-09-13 실측). 140에서 9%이고, 148이면 면이 뒤집힌다.
        /// </summary>
        [Test]
        public void Blink_FollowsCover_UpToOverdrivenFull()
        {
            float full = EyeBlendShapeLogic.BLINK_FULL_WEIGHT;
            Assert.AreEqual(0f, At(REST_R, REST_L, 0f).Blink, 0.01f);
            Assert.AreEqual(full * 0.5f, At(REST_R, REST_L, 0.5f).Blink, 0.01f);
            Assert.AreEqual(full, At(REST_R, REST_L, 1f).Blink, 0.01f);
        }

        /// <summary>
        /// **뒤집힘 문턱을 넘지 않는다.** 쥐·고양이 둘 다 가중치 1.48에서 눈 메시의 위아래가
        /// 교차해 노멀이 뒤집힌다 — 그 밑에 머물러야 한다는 것이 이 상수의 존재 이유다.
        /// </summary>
        [Test]
        public void BlinkFullWeight_StaysBelowTheNormalFlipThreshold()
        {
            Assert.Greater(EyeBlendShapeLogic.BLINK_FULL_WEIGHT, 100f,
                "100이면 두께가 31% 남아 실눈으로 보인다");
            Assert.Less(EyeBlendShapeLogic.BLINK_FULL_WEIGHT, 147f,
                "147 위는 눈 메시가 뒤집힌다(2026-09-13 쥐·고양이 실측)");
        }

        // ===== 쏠 때 질끈(eyes.squint, > <) — 2026-09-13 =====

        [Test]
        public void Squint_ZeroByDefault_FullAtOne()
        {
            Assert.AreEqual(0f, At(REST_R, REST_L).Squint, 0.001f, "안 쏘는데 쐐기가 들어오면 기본 얼굴이 바뀐다");
            EyeShapeWeights w = EyeBlendShapeLogic.Weights(REST_R, REST_L, REST_R, REST_L, PIVOT, MAX, 0f, 1f);
            Assert.AreEqual(EyeBlendShapeLogic.SQUINT_FULL_WEIGHT, w.Squint, 0.001f);
        }

        /// <summary>
        /// 쐐기가 다 들어오면 **시선·깜빡임 키가 0이어야 한다** — 같이 밀면 선이 토막 나고(lookOut)
        /// 덩어리로 뭉개진다(blink 140). 렌더로 확인한 것을 여기서 붙든다.
        /// </summary>
        [Test]
        public void FullSquint_SilencesGazeAndBlink()
        {
            Vector3 gazeR = Quaternion.AngleAxis(+MAX, Vector3.up) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(-MAX, Vector3.up) * Quaternion.AngleAxis(MAX, Vector3.right) * REST_L;
            EyeShapeWeights w = EyeBlendShapeLogic.Weights(REST_R, REST_L, gazeR, gazeL, PIVOT, MAX, 1f, 1f);
            float[] rest = { w.InR, w.OutR, w.InL, w.OutL, w.Up, w.Down, w.Blink };
            foreach (float v in rest) Assert.AreEqual(0f, v, 0.001f);
        }

        [Test]
        public void HalfSquint_HalvesTheRest()
        {
            Vector3 gazeR = Quaternion.AngleAxis(+MAX, Vector3.up) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(-MAX, Vector3.up) * REST_L;
            EyeShapeWeights full = EyeBlendShapeLogic.Weights(REST_R, REST_L, gazeR, gazeL, PIVOT, MAX, 1f, 0f);
            EyeShapeWeights half = EyeBlendShapeLogic.Weights(REST_R, REST_L, gazeR, gazeL, PIVOT, MAX, 1f, 0.5f);
            Assert.AreEqual(full.OutR * 0.5f, half.OutR, 0.5f);
            Assert.AreEqual(full.Blink * 0.5f, half.Blink, 0.5f);
            Assert.AreEqual(50f, half.Squint, 0.001f);
        }

        [Test]
        public void Squint_ClampedToUnityRange()
        {
            Assert.AreEqual(100f, EyeBlendShapeLogic.Weights(REST_R, REST_L, REST_R, REST_L, PIVOT, MAX, 0f, 3f).Squint, 0.001f);
            Assert.AreEqual(0f, EyeBlendShapeLogic.Weights(REST_R, REST_L, REST_R, REST_L, PIVOT, MAX, 0f, -2f).Squint, 0.001f);
        }

        [Test]
        public void Weights_AlwaysWithinUnityRange()
        {
            for (int i = -20; i <= 20; i += 2)
            {
                for (int j = -20; j <= 20; j += 2)
                {
                    Vector3 gazeR = Quaternion.AngleAxis(i * 6f, Vector3.up) * REST_R;
                    Vector3 gazeL = Quaternion.AngleAxis(j * 6f, Vector3.up) * REST_L;
                    EyeShapeWeights w = At(gazeR, gazeL, 3f);   // cover도 범위 밖으로 준다
                    // 방향 여섯은 0~100이고 **감김만 140까지** 간다 — 둘을 같은 상한으로 묶으면
                    // 감김의 외삽을 막거나 방향의 초과를 놓친다.
                    float[] dirs = { w.InR, w.OutR, w.InL, w.OutL, w.Up, w.Down };
                    foreach (float v in dirs)
                    {
                        Assert.GreaterOrEqual(v, 0f);
                        Assert.LessOrEqual(v, 100f);
                    }
                    Assert.GreaterOrEqual(w.Blink, 0f);
                    Assert.LessOrEqual(w.Blink, EyeBlendShapeLogic.BLINK_FULL_WEIGHT);
                }
            }
        }
    }
}
