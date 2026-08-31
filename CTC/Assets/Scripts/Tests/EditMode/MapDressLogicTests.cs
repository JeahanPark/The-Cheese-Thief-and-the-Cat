using NUnit.Framework;
using CTC.Runtime.Map;

namespace CTC.Tests
{
    public class MapDressLogicTests
    {
        [Test]
        public void TileCount_Floors_And_Never_Negative()
        {
            Assert.AreEqual(5, MapDressLogic.TileCount(10f, 2f));
            Assert.AreEqual(4, MapDressLogic.TileCount(9.5f, 2f));
            Assert.AreEqual(0, MapDressLogic.TileCount(1.2f, 2f));
            Assert.AreEqual(0, MapDressLogic.TileCount(-3f, 2f));
        }

        [Test]
        public void TileOffset_Centers_Leftover()
        {
            Assert.AreEqual(0.75f, MapDressLogic.TileOffset(9.5f, 2f, 4), 1e-5f);
            Assert.AreEqual(0f, MapDressLogic.TileOffset(10f, 2f, 5), 1e-5f);
        }

        [Test]
        public void Pick_Is_Deterministic_And_In_Range()
        {
            for (int slot = 0; slot < 50; slot++)
            {
                int a = MapDressLogic.Pick(slot, 7, 1234);
                int b = MapDressLogic.Pick(slot, 7, 1234);
                Assert.AreEqual(a, b);
                Assert.That(a, Is.InRange(0, 6));
            }
        }

        [Test]
        public void Pick_Handles_Zero_Variants()
        {
            Assert.AreEqual(0, MapDressLogic.Pick(3, 0, 99));
        }
    }
}
