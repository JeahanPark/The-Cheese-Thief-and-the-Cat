using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 귀 계산. 요구사항이 **뒤집히지 않는 것** · **스냅하지 않는 것** ·
    /// **딜레이 뒤에 빠르게 섰다 천천히 풀리는 것**(2026-09-10 걸음 단위 재설계) ·
    /// **겹친 소리 중 가까운 쪽을 따르는 것**이라 그 넷을 잰다.
    /// 트랜스폼·시간을 안 쓰는 순수 함수만 본다.
    /// </summary>
    public class EarPerkLogicTests
    {
        private static readonly Vector3 FORWARD = new Vector3(0f, 0f, 1f);

        [Test]
        public void TurnDegrees_SoundOnRight_TurnsPositive()
        {
            float turn = EarPerkLogic.TurnDegrees(FORWARD, new Vector3(1f, 0f, 1f), 60f);
            Assert.AreEqual(45f, turn, 0.01f);
        }

        [Test]
        public void TurnDegrees_SoundOnLeft_TurnsNegative()
        {
            float turn = EarPerkLogic.TurnDegrees(FORWARD, new Vector3(-1f, 0f, 1f), 60f);
            Assert.AreEqual(-45f, turn, 0.01f);
        }

        /// <summary>뒤에서 나는 소리에 귀가 통째로 넘어가면 머리를 뚫는다 — 최대 각에서 멈춰야 한다.</summary>
        [Test]
        public void TurnDegrees_SoundBehind_ClampsToMax()
        {
            float right = EarPerkLogic.TurnDegrees(FORWARD, new Vector3(0.3f, 0f, -1f), 55f);
            float left = EarPerkLogic.TurnDegrees(FORWARD, new Vector3(-0.3f, 0f, -1f), 55f);
            Assert.AreEqual(55f, right, 0.01f);
            Assert.AreEqual(-55f, left, 0.01f);
        }

        /// <summary>정확히 뒤(축이 없는 경우)에도 최대 각을 넘지 않는다.</summary>
        [Test]
        public void TurnDegrees_ExactlyBehind_StaysWithinMax()
        {
            float turn = EarPerkLogic.TurnDegrees(FORWARD, -FORWARD, 55f);
            Assert.LessOrEqual(Mathf.Abs(turn), 55f + 0.01f);
        }

        /// <summary>높이 차이는 무시한다 — 들리는지 판정도 수평 거리다.</summary>
        [Test]
        public void TurnDegrees_IgnoresHeight()
        {
            float flat = EarPerkLogic.TurnDegrees(FORWARD, new Vector3(1f, 0f, 1f), 60f);
            float high = EarPerkLogic.TurnDegrees(FORWARD, new Vector3(1f, 9f, 1f), 60f);
            Assert.AreEqual(flat, high, 0.01f);
        }

        [Test]
        public void TurnDegrees_ZeroDirection_DoesNotTurn()
        {
            Assert.AreEqual(0f, EarPerkLogic.TurnDegrees(FORWARD, Vector3.zero, 60f), 0.01f);
            Assert.AreEqual(0f, EarPerkLogic.TurnDegrees(Vector3.zero, FORWARD, 60f), 0.01f);
        }

        [Test]
        public void TurnDegrees_ZeroMax_NeverTurns()
        {
            Assert.AreEqual(0f, EarPerkLogic.TurnDegrees(FORWARD, new Vector3(1f, 0f, 0f), 0f), 0.01f);
        }

        /// <summary>한 프레임에 목표까지 다 가면 스냅이다 — 일부만 가야 한다.</summary>
        [Test]
        public void Follow_OneFrame_DoesNotSnap()
        {
            float next = EarPerkLogic.Follow(0f, 50f, 8f, 1f / 60f);
            Assert.Greater(next, 0f);
            Assert.Less(next, 50f);
        }

        /// <summary>프레임률이 달라도 같은 시간이면 같은 자리 — 지수 감쇠의 요구사항.</summary>
        [Test]
        public void Follow_FrameRateIndependent()
        {
            float slow = EarPerkLogic.Follow(0f, 50f, 8f, 0.1f);

            float fast = 0f;
            for (int i = 0; i < 10; i++) fast = EarPerkLogic.Follow(fast, 50f, 8f, 0.01f);

            Assert.AreEqual(slow, fast, 0.5f);
        }

        [Test]
        public void Follow_ZeroSpeedOrTime_HoldsStill()
        {
            Assert.AreEqual(12f, EarPerkLogic.Follow(12f, 50f, 0f, 0.1f), 1e-4f);
            Assert.AreEqual(12f, EarPerkLogic.Follow(12f, 50f, 8f, 0f), 1e-4f);
        }

        /// <summary>딜레이 동안은 **아직 안 선다** — 듣자마자 튀면 기계 같다.</summary>
        [Test]
        public void PerkEnvelope_DuringDelay_StaysAtStart()
        {
            Assert.AreEqual(0f, EarPerkLogic.PerkEnvelope(0f, 0f, 0.07f, 0.08f, 0.45f), 1e-4f);
            Assert.AreEqual(0f, EarPerkLogic.PerkEnvelope(0.069f, 0f, 0.07f, 0.08f, 0.45f), 1e-4f);
        }

        /// <summary>딜레이가 끝나면 빠르게 1까지 오른다.</summary>
        [Test]
        public void PerkEnvelope_RisesToOne()
        {
            float mid = EarPerkLogic.PerkEnvelope(0.07f + 0.04f, 0f, 0.07f, 0.08f, 0.45f);
            Assert.Greater(mid, 0.2f);
            Assert.Less(mid, 0.8f);
            Assert.AreEqual(1f, EarPerkLogic.PerkEnvelope(0.07f + 0.08f, 0f, 0.07f, 0.08f, 0.45f), 1e-3f);
        }

        /// <summary>다 풀리면 정확히 0 — 「안 들리면 안 움직인다」가 여기서 끝난다.</summary>
        [Test]
        public void PerkEnvelope_AfterFall_IsExactlyZero()
        {
            Assert.AreEqual(0f, EarPerkLogic.PerkEnvelope(0.07f + 0.08f + 0.45f, 0f, 0.07f, 0.08f, 0.45f), 1e-6f);
            Assert.AreEqual(0f, EarPerkLogic.PerkEnvelope(99f, 1f, 0.07f, 0.08f, 0.45f), 1e-6f);
        }

        /// <summary>풀리는 쪽이 서는 쪽보다 느려야 동물 같다 — 같은 진행률에서 아직 더 높다.</summary>
        [Test]
        public void PerkEnvelope_FallsSlowerThanItRises()
        {
            const float delay = 0.07f, rise = 0.08f, fall = 0.45f;
            float halfUp = EarPerkLogic.PerkEnvelope(delay + rise * 0.5f, 0f, delay, rise, fall);
            float sameTimeAfterPeak = EarPerkLogic.PerkEnvelope(delay + rise + rise * 0.5f, 0f, delay, rise, fall);
            Assert.Greater(sameTimeAfterPeak, halfUp);
        }

        /// <summary>
        /// 풀리는 도중에 또 들으면 **0으로 뚝 떨어졌다 오르지 않는다** — 그 자리에서 이어 오른다.
        /// 이게 없으면 뛰는 상대 앞에서 귀가 덜덜 떤다.
        /// </summary>
        [Test]
        public void PerkEnvelope_RetriggerContinuesFromCurrent()
        {
            float at = EarPerkLogic.PerkEnvelope(0.001f, 0.6f, 0.07f, 0.08f, 0.45f);
            Assert.AreEqual(0.6f, at, 1e-4f);
            Assert.Greater(EarPerkLogic.PerkEnvelope(0.07f + 0.02f, 0.6f, 0.07f, 0.08f, 0.45f), 0.6f);
        }

        /// <summary>값은 언제나 0~1 안이다 — 시작값이 범위를 벗어나도.</summary>
        [Test]
        public void PerkEnvelope_StaysInRange()
        {
            for (float t = 0f; t < 1f; t += 0.017f)
            {
                float v = EarPerkLogic.PerkEnvelope(t, 3f, 0.07f, 0.08f, 0.45f);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
        }

        /// <summary>정점을 **넘어설 때만** 한 번 센다 — 이미 서 있는 동안엔 안 센다.</summary>
        [Test]
        public void CrossedPeak_CountsRisingEdgeOnly()
        {
            Assert.IsTrue(EarPerkLogic.CrossedPeak(0.2f, 0.7f, 0.6f));
            Assert.IsFalse(EarPerkLogic.CrossedPeak(0.7f, 0.9f, 0.6f));
            Assert.IsFalse(EarPerkLogic.CrossedPeak(0.9f, 0.3f, 0.6f));
            Assert.IsFalse(EarPerkLogic.CrossedPeak(0f, 0f, 0.6f));
        }

        [Test]
        public void Closer_NearerSoundWins()
        {
            Assert.IsTrue(EarPerkLogic.Closer(3f, 8f));
            Assert.IsFalse(EarPerkLogic.Closer(8f, 3f));
            Assert.IsFalse(EarPerkLogic.Closer(3f, 3f));
        }

        /// <summary>소리 쪽 귀는 다 돌고, 반대쪽 귀는 덜 돈다 — 뻣뻣함과 머리 가로지름을 같이 막는다.</summary>
        [Test]
        public void SideScale_NearEarTurnsFully_FarEarLess()
        {
            // 소리가 오른쪽(+) → 오른귀(+x)가 가까운 쪽
            Assert.AreEqual(1f, EarPerkLogic.SideScale(30f, 0.12f, 0.45f), 1e-4f);
            Assert.AreEqual(0.45f, EarPerkLogic.SideScale(30f, -0.12f, 0.45f), 1e-4f);
            // 소리가 왼쪽(−) → 왼귀(−x)가 가까운 쪽
            Assert.AreEqual(1f, EarPerkLogic.SideScale(-30f, -0.12f, 0.45f), 1e-4f);
            Assert.AreEqual(0.45f, EarPerkLogic.SideScale(-30f, 0.12f, 0.45f), 1e-4f);
        }

        /// <summary>안 돌 때는 좌우를 나눌 이유가 없다 — 0에 비율을 곱해도 0이라 값은 1로 둔다.</summary>
        [Test]
        public void SideScale_NoTurn_IsOne()
        {
            Assert.AreEqual(1f, EarPerkLogic.SideScale(0f, 0.12f, 0.45f), 1e-4f);
            Assert.AreEqual(1f, EarPerkLogic.SideScale(0f, -0.12f, 0.45f), 1e-4f);
        }

        /// <summary>비율은 0~1로 막는다 — 먼 귀가 가까운 귀보다 더 돌면 안 된다.</summary>
        [Test]
        public void SideScale_RatioIsClamped()
        {
            Assert.AreEqual(1f, EarPerkLogic.SideScale(30f, -0.12f, 3f), 1e-4f);
            Assert.AreEqual(0f, EarPerkLogic.SideScale(30f, -0.12f, -2f), 1e-4f);
        }
    }
}
