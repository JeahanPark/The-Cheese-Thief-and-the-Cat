using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 귀 계산(2026-09-08). 요구사항이 **뒤집히지 않는 것** · **스냅하지 않는 것** ·
    /// **잠시 유지하다 돌아오는 것** · **겹친 소리 중 가까운 쪽을 따르는 것**이라 그 넷을 잰다.
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

        [Test]
        public void Holding_WithinWindow_True()
        {
            Assert.IsTrue(EarPerkLogic.Holding(0f, 1.5f));
            Assert.IsTrue(EarPerkLogic.Holding(1.49f, 1.5f));
        }

        [Test]
        public void Holding_AfterWindow_False()
        {
            Assert.IsFalse(EarPerkLogic.Holding(1.5f, 1.5f));
            Assert.IsFalse(EarPerkLogic.Holding(9f, 1.5f));
        }

        /// <summary>한 번도 못 들었으면(음의 무한대 시각) 유지도 없다.</summary>
        [Test]
        public void Holding_NeverHeard_False()
        {
            Assert.IsFalse(EarPerkLogic.Holding(float.PositiveInfinity, 1.5f));
            Assert.IsFalse(EarPerkLogic.Holding(-1f, 1.5f));
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
