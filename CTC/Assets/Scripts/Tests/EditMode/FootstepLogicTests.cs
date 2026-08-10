using CTC.Runtime.Audio;
using CTC.Runtime.Info;
using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 발소리가 어디까지 닿는가. **120×120 맵에서 고양이가 이길 수 있는지가 여기 달려 있다** —
    /// 잠행이 완전 무음이면 구석에 웅크린 쥐는 단서가 0이고, 그러면 "버티면 쥐 승"이 자동 승리가 된다.
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
        public void AudibleRange_SneakIsQuietButNotSilent()
        {
            // 0이 되면 예전(완전 무음)으로 돌아간 것이다 — 이 판정이 이 파일의 존재 이유다
            float sneak = FootstepLogic.AudibleRange(true, true, 18f, 4f);
            Assert.Greater(sneak, 0f);
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
        public void Balance_SneakRangeIsSetForBothRoles()
        {
            foreach (RoleId role in new[] { RoleId.Cat, RoleId.Mouse })
            {
                PlayerInfo info = InfoManager.Instance.PlayerOf(role);
                Assert.Greater(info.SneakFootstepRangeMeters, 0f, $"{role} 잠행 반경이 0이면 완전 무음이다");
                Assert.Less(info.SneakFootstepRangeMeters, info.FootstepRangeMeters, $"{role}");
            }
        }
    }
}
