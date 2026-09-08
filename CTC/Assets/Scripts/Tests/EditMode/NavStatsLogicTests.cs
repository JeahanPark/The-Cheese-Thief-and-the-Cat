using CTC.Runtime.Ai;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 길찾기 계측의 **표본 규칙**(2026-09-09 감사 T6 / 케이스 E-14).
    ///
    /// 잡는 회귀: **포기가 지표를 좋게 만드는 것.** 예전에는 길을 못 찾아 코너가 0이면
    /// 우회율에 1.00(만점)을 적었고, 목표 미달에는 0m(정확히 도착)를 적었다 —
    /// 길찾기가 나빠질수록 두 지표가 다 좋아졌다.
    /// </summary>
    public class NavStatsLogicTests
    {
        [Test]
        public void GiveUp_IsNotADetourSample()
        {
            Assert.IsFalse(NavStatsLogic.IsDetourSample(0, 12f));
        }

        [Test]
        public void GiveUp_DetourIsNotPerfectScore()
        {
            float detour = NavStatsLogic.Detour(0, 0f, 12f);

            Assert.AreEqual(NavStatsLogic.NO_DETOUR, detour, 0.001f);
            Assert.AreNotEqual(1f, detour);
        }

        [Test]
        public void TinyHop_IsNotADetourSample()
        {
            // 반 미터도 안 되는 경로를 1.00으로 세면 평균이 만점 쪽으로 끌린다.
            Assert.IsFalse(NavStatsLogic.IsDetourSample(3, 0.2f));
            Assert.AreEqual(NavStatsLogic.NO_DETOUR, NavStatsLogic.Detour(3, 0.3f, 0.2f), 0.001f);
        }

        [Test]
        public void RealPath_IsMeasured()
        {
            // 14m를 걸어 10m 떨어진 곳에 닿았다 → 1.4
            Assert.IsTrue(NavStatsLogic.IsDetourSample(4, 10f));
            Assert.AreEqual(1.4f, NavStatsLogic.Detour(4, 14f, 10f), 0.001f);
        }

        [Test]
        public void StraightPath_ScoresOne()
        {
            Assert.AreEqual(1f, NavStatsLogic.Detour(2, 10f, 10f), 0.001f);
        }

        [Test]
        public void GiveUp_ShortfallIsTheWholeDistance()
        {
            // 목표까지 33m인데 길을 못 찾았다 → 33m 미달이다. 0이 아니다.
            Assert.AreEqual(33f, NavStatsLogic.Shortfall(0, true, 0f, 33f), 0.001f);
            Assert.IsTrue(NavStatsLogic.IsShortfallSample(0, true));
        }

        [Test]
        public void SuccessfulPath_ShortfallIsTheResidual()
        {
            Assert.AreEqual(1.5f, NavStatsLogic.Shortfall(5, false, 1.5f, 40f), 0.001f);
            Assert.IsTrue(NavStatsLogic.IsShortfallSample(5, false));
        }

        [Test]
        public void AlreadyThere_IsNotAShortfallSample()
        {
            // 코너 0 · 포기 아님 = 「이미 그 자리」다. 표본으로 세면 0m가 평균을 끌어내린다.
            Assert.IsFalse(NavStatsLogic.IsShortfallSample(0, false));
        }

        [Test]
        public void GiveUpsCannotImproveTheAverage()
        {
            // 실제 계획 셋(1.2·1.4·1.6)에 포기 열 번을 섞어도 평균이 안 변한다.
            float sum = 0f;
            int samples = 0;
            foreach (float d in new[] { 1.2f, 1.4f, 1.6f })
            {
                if (!NavStatsLogic.IsDetourSample(3, 10f)) continue;
                sum += d;
                samples++;
            }
            for (int i = 0; i < 10; i++)
            {
                if (!NavStatsLogic.IsDetourSample(0, 25f)) continue;
                sum += NavStatsLogic.Detour(0, 0f, 25f);
                samples++;
            }

            Assert.AreEqual(3, samples, "포기가 표본으로 새어 들어갔다");
            Assert.AreEqual(1.4f, sum / samples, 0.001f);
        }
    }
}
