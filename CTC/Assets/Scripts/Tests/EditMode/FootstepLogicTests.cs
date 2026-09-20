using CTC.Runtime.Audio;
using CTC.Runtime.Info;
using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 발소리가 어디까지 닿는가. **120×120 맵에서 고양이가 이길 수 있는지가 여기 달려 있다** —
    /// 잠행이 완전 무음이면 구석에 웅크린 쥐는 단서가 0이고, 그러면 "버티면 쥐 승"이 자동 승리가 된다.
    /// 그 위험을 알고도 **2026-09-20 사용자 결정으로 살금살금은 다시 무음**이다(CSV 잠행 반경 0) —
    /// 판이 쥐 쪽으로 기울면 CSV만 되돌린다. 함수들은 값만 따르고, 결정은 Balance_ 테스트가 못박는다.
    /// </summary>
    public class FootstepLogicTests
    {
        [Test]
        public void AudibleRange_SilentWhenStanding()
        {
            Assert.AreEqual(0f, FootstepLogic.AudibleRange(false, false, 18f, 4f));
            Assert.AreEqual(0f, FootstepLogic.AudibleRange(false, true, 18f, 4f));
        }

        [Test]
        public void AudibleRange_SneakUsesSneakRange()
        {
            // 함수는 잠행 반경을 그대로 쓴다 — 무음 여부는 CSV 값(지금 0)이 정한다(Balance_SneakIsSilentForBothRoles)
            float sneak = FootstepLogic.AudibleRange(true, true, 18f, 4f);
            Assert.AreEqual(4f, sneak);
            Assert.Less(sneak, FootstepLogic.AudibleRange(true, false, 18f, 4f));
        }

        [Test]
        public void AudibleRange_NegativeRangeIsClampedNotFlipped()
        {
            Assert.AreEqual(0f, FootstepLogic.AudibleRange(true, true, 18f, -4f));
        }

        [Test]
        public void IsMoving_IgnoresJitterBelowThreshold()
        {
            Assert.IsFalse(FootstepLogic.IsMoving(0.4f, 0.5f));
            Assert.IsTrue(FootstepLogic.IsMoving(0.5f, 0.5f));
        }

        [Test]
        public void Advance_StepsOncePerStride_CarriesRemainder()
        {
            float acc = FootstepLogic.Advance(0f, 0.5f, 1.8f, out bool step);
            Assert.IsFalse(step);
            Assert.AreEqual(0.5f, acc, 1e-4f);

            acc = FootstepLogic.Advance(acc, 1.5f, 1.8f, out step);   // 2.0 ≥ 1.8 → 한 걸음, 0.2 남김
            Assert.IsTrue(step);
            Assert.AreEqual(0.2f, acc, 1e-4f);
        }

        [Test]
        public void Advance_SlowWalkTakesLongerBetweenSteps()
        {
            // 같은 시간(1초)에 살금(0.45×)은 걸음이 절반도 안 난다 — 박자가 속도를 따라간다
            int Steps(float speed)
            {
                int n = 0; float acc = 0f;
                for (int i = 0; i < 60; i++)
                {
                    acc = FootstepLogic.Advance(acc, speed / 60f, 1.8f, out bool s);
                    if (s) n++;
                }
                return n;
            }
            Assert.AreEqual(3, Steps(6f));           // 6m/s ÷ 1.8m ≈ 3.3 → 3걸음
            Assert.AreEqual(1, Steps(6f * 0.45f));   // 2.7m/s → 1걸음
        }

        [Test]
        public void StrideOf_ReproducesLoopCadenceAtBaseSpeed()
        {
            // 쥐 루프는 1.48초에 5걸음(3.4/s), 이속 6 → 보폭 1.78m
            Assert.AreEqual(6f / 3.4f, FootstepLogic.StrideOf(6f, 3.4f), 1e-4f);
            Assert.AreEqual(0f, FootstepLogic.StrideOf(6f, 0f));
        }

        [Test]
        public void Balance_MouseIsHeardFartherThanCat()
        {
            // 예전엔 반대였다(쥐 8 · 고양이 15) — 쥐가 항상 먼저 알아 넓은 맵에서 회피가 공짜였다.
            // 2:8 기준으로 뒤집는다: 고양이의 주 단서가 발소리여야 한다.
            PlayerInfo cat = InfoManager.Instance.PlayerOf(RoleId.Cat);
            PlayerInfo mouse = InfoManager.Instance.PlayerOf(RoleId.Mouse);

            Assert.Greater(mouse.FootstepRangeMeters, cat.FootstepRangeMeters);
            // 그래도 고양이 발소리는 들려야 한다 — 도주가 성립할 최소한의 예고
            Assert.Greater(cat.FootstepRangeMeters, 0f);
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "B-41")]
        public void Balance_SneakIsSilentForBothRoles()
        {
            // 2026-09-20 사용자 결정: 살금살금은 완전 무음(반경 0). 되돌리면 이 테스트도 같이 고친다.
            foreach (RoleId role in new[] { RoleId.Cat, RoleId.Mouse })
            {
                PlayerInfo info = InfoManager.Instance.PlayerOf(role);
                Assert.AreEqual(0f, info.SneakFootstepRangeMeters, $"{role} 잠행 반경");
                Assert.IsFalse(FootstepLogic.MakesSound(
                    FootstepLogic.AudibleRange(true, true, info.FootstepRangeMeters, info.SneakFootstepRangeMeters)),
                    $"{role} 살금살금 걸음은 소리가 안 나야 한다");
                Assert.IsTrue(FootstepLogic.MakesSound(
                    FootstepLogic.AudibleRange(true, false, info.FootstepRangeMeters, info.SneakFootstepRangeMeters)),
                    $"{role} 보통 걸음은 소리가 나야 한다");
            }
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "B-41")]
        public void MakesSound_OnlyWithPositiveRange()
        {
            Assert.IsFalse(FootstepLogic.MakesSound(0f));
            Assert.IsFalse(FootstepLogic.MakesSound(-1f));
            Assert.IsTrue(FootstepLogic.MakesSound(0.5f));
        }
    }
}
