using CTC.Runtime.Player;
using CTC.Runtime.Testing;
using NUnit.Framework;

namespace CTC.Tests
{
    /// <summary>애니메이션 검수 씬의 목록·프레임 계산(2026-09-13).</summary>
    public class AnimReviewLogicTests
    {
        [Test]
        public void ClipsFor_SelfMadeFirst_ThenShared_NoCrossRole()
        {
            var mouse = AnimReviewLogic.ClipsFor(false);
            var cat = AnimReviewLogic.ClipsFor(true);

            Assert.AreEqual(AnimationStateLogic.MouseOnly[0], mouse[0], "쥐는 자작 클립이 맨 앞이어야 한다");
            Assert.AreEqual(AnimationStateLogic.CatOnly[0], cat[0], "고양이는 자작 클립이 맨 앞이어야 한다");
            foreach (string s in AnimationStateLogic.CatOnly) CollectionAssert.DoesNotContain(mouse, s);
            foreach (string s in AnimationStateLogic.MouseOnly) CollectionAssert.DoesNotContain(cat, s);
            CollectionAssert.IsSubsetOf(AnimationStateLogic.Shared, mouse);
            CollectionAssert.IsSubsetOf(AnimationStateLogic.Shared, cat);
        }

        [Test]
        public void IsSelfMade_OnlyRoleOnlyClips()
        {
            Assert.IsTrue(AnimReviewLogic.IsSelfMade(AnimationStateLogic.Warehouse));
            Assert.IsTrue(AnimReviewLogic.IsSelfMade(AnimationStateLogic.CatSkillDark));
            Assert.IsFalse(AnimReviewLogic.IsSelfMade(AnimationStateLogic.Walk));
        }

        [Test]
        public void Frames_ElevenKeyClip()
        {
            int last = AnimReviewLogic.LastFrame(10f / 24f);
            Assert.AreEqual(10, last);
            Assert.AreEqual(0, AnimReviewLogic.FrameAt(0f, last));
            Assert.AreEqual(5, AnimReviewLogic.FrameAt(0.5f, last));
            Assert.AreEqual(10, AnimReviewLogic.FrameAt(1f, last), "끝에 멈춘 순간은 마지막 프레임");
            Assert.AreEqual(5, AnimReviewLogic.FrameAt(2.5f, last), "반복 중에도 한 바퀴 안의 프레임");
            Assert.AreEqual(0.3f, AnimReviewLogic.NormalizedOf(3, last), 1e-5f);
        }

        [Test]
        public void Wrap_BothDirections()
        {
            Assert.AreEqual(0, AnimReviewLogic.Wrap(5, 5));
            Assert.AreEqual(4, AnimReviewLogic.Wrap(-1, 5));
            Assert.AreEqual(0, AnimReviewLogic.Wrap(3, 0));
        }
    }
}
