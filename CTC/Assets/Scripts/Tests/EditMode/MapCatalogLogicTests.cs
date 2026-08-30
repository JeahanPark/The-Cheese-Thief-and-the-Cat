using NUnit.Framework;
using CTC.Runtime.Map;

namespace CTC.Tests
{
    public class MapCatalogLogicTests
    {
        private static readonly float[] Extents = { 15f, 21f, 60f };   // 기본·확장·대형

        [Test]
        public void Official_Means_Large_Or_Bigger()
        {
            Assert.IsFalse(MapCatalogLogic.IsOfficial(15f));
            Assert.IsFalse(MapCatalogLogic.IsOfficial(21f));
            Assert.IsTrue(MapCatalogLogic.IsOfficial(60f));
        }

        [Test]
        public void Dev_Build_Shows_Everything()
        {
            Assert.IsTrue(MapCatalogLogic.IsSelectable(Extents[0], _devBuild: true));
            Assert.IsTrue(MapCatalogLogic.IsSelectable(Extents[2], _devBuild: true));
        }

        [Test]
        public void Release_Shows_Only_Official()
        {
            Assert.IsFalse(MapCatalogLogic.IsSelectable(Extents[0], _devBuild: false));
            Assert.IsFalse(MapCatalogLogic.IsSelectable(Extents[1], _devBuild: false));
            Assert.IsTrue(MapCatalogLogic.IsSelectable(Extents[2], _devBuild: false));
        }

        [Test]
        public void Default_Index_Is_First_Official()
        {
            Assert.AreEqual(2, MapCatalogLogic.DefaultIndex(Extents));
        }

        [Test]
        public void Default_Index_Falls_Back_To_Zero_When_Nothing_Official()
        {
            Assert.AreEqual(0, MapCatalogLogic.DefaultIndex(new[] { 15f, 21f }));
        }
    }
}
