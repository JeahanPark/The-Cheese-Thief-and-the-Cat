using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class MovementInputTests
    {
        [Test]
        public void ZeroInput_ReturnsZeroDirection()
        {
            Assert.AreEqual(Vector3.zero, MovementInput.ToDirection(0f, 0f));
        }

        [Test]
        public void ForwardInput_MapsToPositiveZ()
        {
            var dir = MovementInput.ToDirection(0f, 1f);
            Assert.AreEqual(new Vector3(0f, 0f, 1f), dir);
        }

        [Test]
        public void DiagonalInput_IsClampedToUnitLength()
        {
            var dir = MovementInput.ToDirection(1f, 1f);
            Assert.AreEqual(1f, dir.magnitude, 1e-4f);
        }

        [Test]
        public void PartialInput_BelowUnit_IsNotScaledUp()
        {
            var dir = MovementInput.ToDirection(0.5f, 0f);
            Assert.AreEqual(new Vector3(0.5f, 0f, 0f), dir);
        }
    }
}
