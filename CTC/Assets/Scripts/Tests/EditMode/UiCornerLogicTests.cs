using CTC.Runtime.UI;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>UI 둥근 모서리(2026-09-23) — 어느 칸을 둥글리나 · 얼마나.</summary>
    public class UiCornerLogicTests
    {
        private const float CW = 1920f;
        private const float CH = 1080f;

        [Test]
        public void ShouldRound_PlainColorPanelsAndButtons()
        {
            Assert.IsTrue(UiCornerLogic.ShouldRound(false, true, 1f, 480f, 56f, CW, CH), "버튼");
            Assert.IsTrue(UiCornerLogic.ShouldRound(false, true, 0.9f, 900f, 700f, CW, CH), "창");
        }

        [Test]
        public void ShouldRound_LeavesIntendedShapesAlone()
        {
            Assert.IsFalse(UiCornerLogic.ShouldRound(true, true, 1f, 64f, 64f, CW, CH), "스프라이트가 있는 아이콘");
            Assert.IsFalse(UiCornerLogic.ShouldRound(false, false, 1f, 200f, 20f, CW, CH), "Filled 게이지");
            Assert.IsFalse(UiCornerLogic.ShouldRound(false, true, 0f, 300f, 300f, CW, CH), "클릭만 받는 투명 판");
            Assert.IsFalse(UiCornerLogic.ShouldRound(false, true, 1f, 480f, 3f, CW, CH), "위쪽 노란 줄 — 알약이 된다");
            Assert.IsFalse(UiCornerLogic.ShouldRound(false, true, 0.6f, CW, CH, CW, CH), "화면 전체 베일 — 모서리가 비친다");
        }

        [Test]
        public void CornerPx_GrowsWithSize_NeverPastHalfTheShortSide()
        {
            Assert.AreEqual(UiCornerLogic.CORNER_SMALL_PX, UiCornerLogic.CornerPx(30f, 30f));
            Assert.AreEqual(UiCornerLogic.CORNER_MEDIUM_PX, UiCornerLogic.CornerPx(480f, 56f));
            Assert.AreEqual(UiCornerLogic.CORNER_LARGE_PX, UiCornerLogic.CornerPx(900f, 700f));
            Assert.AreEqual(4f, UiCornerLogic.CornerPx(200f, 8f), 1e-4f, "짧은 변 8px면 반지름 4가 한계(작은 5보다 먼저)");
        }

        [Test]
        public void Multiplier_TurnsSpriteBorderIntoWantedRadius()
        {
            // 화면 모서리 = 테두리 ÷ 배수
            float mul = UiCornerLogic.PixelsPerUnitMultiplier(8f);
            Assert.AreEqual(8f, UiCornerLogic.SPRITE_RADIUS_PX / mul, 1e-4f);
            Assert.AreEqual(1f, UiCornerLogic.PixelsPerUnitMultiplier(0f), "0이면 나누지 않는다");
        }
    }
}
