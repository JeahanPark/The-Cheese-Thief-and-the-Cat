using CTC.Runtime.UI;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>스턴 테두리(2026-09-19) — 맞은 사람 화면 가장자리가 즉시 하얗게 서고, 풀리면 빠진다. 가운데는 비어 있다.</summary>
    public class StunVignetteLogicTests
    {
        [NUnit.Framework.Property("CaseId", "B-37")]
        [Test]
        public void RisesAtOnce_AndFallsAfter()
        {
            Assert.AreEqual(0f, StunVignetteLogic.WhileStunned(0f), 1e-4f);
            Assert.AreEqual(StunVignetteLogic.PEAK, StunVignetteLogic.WhileStunned(StunVignetteLogic.RISE_SECONDS), 1e-4f);
            Assert.Less(StunVignetteLogic.RISE_SECONDS, 0.1f, "「맞았다」가 즉시 읽혀야 한다");
            Assert.AreEqual(StunVignetteLogic.PEAK, StunVignetteLogic.AfterStun(StunVignetteLogic.PEAK, 0f), 1e-4f);
            Assert.AreEqual(0f, StunVignetteLogic.AfterStun(StunVignetteLogic.PEAK, StunVignetteLogic.FALL_SECONDS), 1e-4f);
        }

        [NUnit.Framework.Property("CaseId", "B-37")]
        [Test]
        public void Edge_LeavesTheCenterClear()
        {
            Assert.AreEqual(0f, StunVignetteLogic.EdgeAlpha(0f), 1e-4f, "화면 가운데는 비어 있다 — 조준을 가리지 않는다");
            Assert.AreEqual(0f, StunVignetteLogic.EdgeAlpha(0.5f), 1e-4f);
            Assert.AreEqual(1f, StunVignetteLogic.EdgeAlpha(1f), 1e-4f, "가장자리가 가장 진하다");
            Assert.Less(StunVignetteLogic.EdgeAlpha(0.7f), StunVignetteLogic.EdgeAlpha(0.9f));
        }
    }
}
