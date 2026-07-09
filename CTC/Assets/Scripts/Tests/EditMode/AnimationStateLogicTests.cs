using CTC.Runtime.Player;
using NUnit.Framework;

namespace CTC.Tests
{
    public class AnimationStateLogicTests
    {
        [Test]
        public void BelowWalkThreshold_ReturnsIdle()
        {
            Assert.AreEqual("Idle_A", AnimationStateLogic.FromSpeed(0.05f, 0.1f, 3.5f));
        }

        [Test]
        public void BetweenThresholds_ReturnsWalk()
        {
            Assert.AreEqual("Walk", AnimationStateLogic.FromSpeed(1.5f, 0.1f, 3.5f));
        }

        [Test]
        public void AtOrAboveRunThreshold_ReturnsRun()
        {
            Assert.AreEqual("Run", AnimationStateLogic.FromSpeed(3.5f, 0.1f, 3.5f));
        }

        [Test]
        public void ExactlyWalkThreshold_ReturnsWalk()
        {
            Assert.AreEqual("Walk", AnimationStateLogic.FromSpeed(0.1f, 0.1f, 3.5f));
        }
    }
}
