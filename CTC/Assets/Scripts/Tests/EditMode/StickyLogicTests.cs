using CTC.Runtime.Economy;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class StickyLogicTests
    {
        [Test]
        public void SlowMultiplier_FromSlowRatio()
        {
            Assert.AreEqual(0.6f, StickyLogic.SpeedMultiplier(0.4f), 1e-4f);
            Assert.AreEqual(0.25f, StickyLogic.SpeedMultiplier(0.75f), 1e-4f);
            Assert.AreEqual(1f, StickyLogic.SpeedMultiplier(0f), 1e-4f);
        }

        [Test]
        public void Inside_UsesHorizontalRadius()
        {
            Assert.IsTrue(StickyLogic.Inside(Vector3.zero, new Vector3(2f, 3f, 0f), 2.5f));
            Assert.IsFalse(StickyLogic.Inside(Vector3.zero, new Vector3(2.6f, 0f, 0f), 2.5f));
        }

        [Test]
        public void Ready_WhenCooldownElapsed()
        {
            Assert.IsFalse(StickyLogic.Ready(10.0, 9.9));
            Assert.IsTrue(StickyLogic.Ready(10.0, 10.0));
        }
    }
}
