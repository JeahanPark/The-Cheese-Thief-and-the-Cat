using CTC.Runtime.UI;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>접속 대기 로딩창(2026-09-19) — 언제 가리고, 언제 걷히고, 점이 어떻게 도는가.</summary>
    public class LoadingScreenLogicTests
    {
        [NUnit.Framework.Property("CaseId", "A-28")]
        [Test]
        public void ShouldShow_UntilTheMatchStarts()
        {
            Assert.IsTrue(LoadingScreenLogic.ShouldShow(_gameStarted: false, _elapsed: 5f), "판이 안 섰으면 가린다");
            Assert.IsFalse(LoadingScreenLogic.ShouldShow(true, LoadingScreenLogic.MIN_SECONDS + 0.01f), "판이 서면 걷는다");
        }

        /// <summary>로컬처럼 전원이 한꺼번에 들어오면 판이 곧바로 선다 — 그래도 최소 시간은 보인다(한 프레임 번쩍 금지).</summary>
        [NUnit.Framework.Property("CaseId", "A-28")]
        [Test]
        public void ShouldShow_AtLeastTheMinimum_EvenIfTheMatchAlreadyStarted()
        {
            Assert.IsTrue(LoadingScreenLogic.ShouldShow(true, 0f));
            Assert.IsTrue(LoadingScreenLogic.ShouldShow(true, LoadingScreenLogic.MIN_SECONDS - 0.01f));
            Assert.Greater(LoadingScreenLogic.MIN_SECONDS, 1f, "씬 로딩(약 1초)의 검은 화면을 덮어야 한다");
        }

        [NUnit.Framework.Property("CaseId", "A-28")]
        [Test]
        public void ShouldShow_GivesUpAfterTheCap_SoTheScreenNeverFreezes()
        {
            Assert.IsTrue(LoadingScreenLogic.ShouldShow(false, LoadingScreenLogic.MAX_SECONDS - 0.1f));
            Assert.IsFalse(LoadingScreenLogic.ShouldShow(false, LoadingScreenLogic.MAX_SECONDS));
        }

        [NUnit.Framework.Property("CaseId", "A-28")]
        [Test]
        public void Fades_InQuickly_OutSoftly()
        {
            Assert.AreEqual(0f, LoadingScreenLogic.FadeInAlpha(0f), 1e-4f);
            Assert.AreEqual(1f, LoadingScreenLogic.FadeInAlpha(LoadingScreenLogic.FADE_IN_SECONDS), 1e-4f);
            Assert.AreEqual(1f, LoadingScreenLogic.FadeOutAlpha(0f), 1e-4f);
            Assert.AreEqual(0f, LoadingScreenLogic.FadeOutAlpha(LoadingScreenLogic.FADE_OUT_SECONDS), 1e-4f);
        }

        [NUnit.Framework.Property("CaseId", "A-28")]
        [Test]
        public void Dots_CycleOneTwoThree()
        {
            float step = LoadingScreenLogic.DOTS_CYCLE_SECONDS / 3f;
            Assert.AreEqual(".", LoadingScreenLogic.Dots(0f));
            Assert.AreEqual("..", LoadingScreenLogic.Dots(step + 0.01f));
            Assert.AreEqual("...", LoadingScreenLogic.Dots(2f * step + 0.01f));
            Assert.AreEqual(".", LoadingScreenLogic.Dots(LoadingScreenLogic.DOTS_CYCLE_SECONDS + 0.01f), "한 바퀴 뒤 처음으로");
        }
    }
}
