using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 시선 → 셰이프키 가중치(2026-09-13). 팩 셰이프키는 **두 눈이 거울 대칭으로 묶여** 있어
    /// 좌우 비대칭 시선을 표현할 수 없다 — 그 성질을 코드가 실제로 그렇게 다루는지까지 잰다.
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
            Assert.AreEqual(0f, w.LookIn, 0.01f);
            Assert.AreEqual(0f, w.LookOut, 0.01f);
            Assert.AreEqual(0f, w.LookUp, 0.01f);
            Assert.AreEqual(0f, w.LookDown, 0.01f);
        }

        [Test]
        public void FullInward_LookInIsFull_LookOutIsZero()
        {
            // 두 눈이 각자 코 쪽으로 최대각만큼 — 정면을 모아 보는 자세다.
            Vector3 gazeR = Quaternion.AngleAxis(-MAX, Vector3.up) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(+MAX, Vector3.up) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.AreEqual(100f, w.LookIn, 0.5f);
            Assert.AreEqual(0f, w.LookOut, 0.01f);
        }

        [Test]
        public void FullOutward_LookOutIsFull_LookInIsZero()
        {
            Vector3 gazeR = Quaternion.AngleAxis(+MAX, Vector3.up) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(-MAX, Vector3.up) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.AreEqual(100f, w.LookOut, 0.5f);
            Assert.AreEqual(0f, w.LookIn, 0.01f);
        }

        [Test]
        public void LookingUp_DrivesLookUp_NotLookDown()
        {
            // 축 순서가 뒤집히면 **아래를 보게 된다** — Cross(up, rest)로 돌리면 고도각이 −30도다
            // (2026-09-13 계산으로 확인). 부호가 조용히 뒤집히는 자리라 여기 적어 둔다.
            Vector3 gazeR = Quaternion.AngleAxis(MAX, Vector3.Cross(REST_R, Vector3.up)) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(MAX, Vector3.Cross(REST_L, Vector3.up)) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.Greater(w.LookUp, 90f);
            Assert.AreEqual(0f, w.LookDown, 0.01f);
        }

        [Test]
        public void BeyondMaxAngle_StaysAtHundred()
        {
            Vector3 gazeR = Quaternion.AngleAxis(-MAX * 3f, Vector3.up) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(+MAX * 3f, Vector3.up) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.AreEqual(100f, w.LookIn, 0.01f);
        }

        [Test]
        public void AsymmetricGaze_CancelsToNearZero()
        {
            // 오른눈은 안쪽, 왼눈은 바깥쪽 — 「옆의 목표를 쫓는」 자세다.
            // 셰이프키로는 표현할 수 없으므로 **평균이 0에 가까워야** 한다(설계 §2).
            Vector3 gazeR = Quaternion.AngleAxis(-MAX, Vector3.up) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(-MAX, Vector3.up) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.Less(w.LookIn, 1f);
            Assert.Less(w.LookOut, 1f);
        }

        [Test]
        public void SameAxisPair_NeverBothNonZero()
        {
            for (int i = -12; i <= 12; i++)
            {
                Vector3 gazeR = Quaternion.AngleAxis(i * 5f, Vector3.up) * REST_R;
                Vector3 gazeL = Quaternion.AngleAxis(-i * 5f, Vector3.up) * REST_L;
                EyeShapeWeights w = At(gazeR, gazeL);
                Assert.IsTrue(w.LookIn <= 0.01f || w.LookOut <= 0.01f, "In/Out 동시 점등 i=" + i);
                Assert.IsTrue(w.LookUp <= 0.01f || w.LookDown <= 0.01f, "Up/Down 동시 점등 i=" + i);
            }
        }

        [Test]
        public void Blink_FollowsCover()
        {
            Assert.AreEqual(0f, At(REST_R, REST_L, 0f).Blink, 0.01f);
            Assert.AreEqual(50f, At(REST_R, REST_L, 0.5f).Blink, 0.01f);
            Assert.AreEqual(100f, At(REST_R, REST_L, 1f).Blink, 0.01f);
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
                    float[] all = { w.LookIn, w.LookOut, w.LookUp, w.LookDown, w.Blink };
                    foreach (float v in all)
                    {
                        Assert.GreaterOrEqual(v, 0f);
                        Assert.LessOrEqual(v, 100f);
                    }
                }
            }
        }
    }
}
