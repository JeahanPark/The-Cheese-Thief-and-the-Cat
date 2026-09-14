using CTC.Runtime.Player;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>깡패 변신 시간표(2026-09-14) — 소품이 앞발이 닿는 순간에 나타나고, 끝나면 전부 또렷하다.</summary>
    public class GangTransformLogicTests
    {
        private const float EPS = 1e-4f;

        [Test]
        public void Order_BrowsThenClubThenHat_AllBeforeEnd()
        {
            Assert.Less(GangTransformLogic.BROWS_AT, GangTransformLogic.CLUB_AT);
            Assert.Less(GangTransformLogic.CLUB_AT, GangTransformLogic.HAT_AT);
            Assert.Less(GangTransformLogic.HAT_AT + GangTransformLogic.FADE_SECONDS, GangTransformLogic.SECONDS, "모자가 다 차오른 뒤에 끝나야 한다");
        }

        [Test]
        public void PropAlpha_ZeroBefore_RampsOverFade_OneAfter()
        {
            float at = GangTransformLogic.CLUB_AT;
            Assert.AreEqual(0f, GangTransformLogic.ClubAlpha(0f), EPS);
            Assert.AreEqual(0f, GangTransformLogic.ClubAlpha(at - 0.01f), EPS);
            Assert.AreEqual(0.5f, GangTransformLogic.ClubAlpha(at + GangTransformLogic.FADE_SECONDS * 0.5f), EPS);
            Assert.AreEqual(1f, GangTransformLogic.ClubAlpha(at + GangTransformLogic.FADE_SECONDS + 0.01f), EPS);
            Assert.AreEqual(0f, GangTransformLogic.HatAlpha(at + GangTransformLogic.FADE_SECONDS), EPS, "몽둥이가 다 나올 때 모자는 아직이다");
        }

        [Test]
        public void Brows_OnWhenStandingUp_AndDoneAtEnd()
        {
            Assert.IsFalse(GangTransformLogic.BrowsOn(0f));
            Assert.IsTrue(GangTransformLogic.BrowsOn(GangTransformLogic.BROWS_AT));
            Assert.IsFalse(GangTransformLogic.Done(GangTransformLogic.SECONDS - 0.01f));
            Assert.IsTrue(GangTransformLogic.Done(GangTransformLogic.SECONDS));
            CollectionAssert.Contains(AnimationStateLogic.MouseOnly, AnimationStateLogic.GangTransform);
        }
    }
}
