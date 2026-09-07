using NUnit.Framework;
using CTC.Runtime.Map;

namespace CTC.Tests
{
    public class MapCatalogLogicTests
    {
        private static readonly float[] Extents = { 15f, 21f, 60f, 60f };   // 기본·확장·창고·대형(격자, 개발용)
        private static readonly bool[] DevOnly = { false, false, false, true };

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
            Assert.IsTrue(MapCatalogLogic.IsSelectable(Extents[0], _devOnly: false, _devBuild: true));
            Assert.IsTrue(MapCatalogLogic.IsSelectable(Extents[3], _devOnly: true, _devBuild: true));
        }

        [Test]
        public void Release_Shows_Only_Official()
        {
            Assert.IsFalse(MapCatalogLogic.IsSelectable(Extents[0], _devOnly: false, _devBuild: false));
            Assert.IsFalse(MapCatalogLogic.IsSelectable(Extents[1], _devOnly: false, _devBuild: false));
            Assert.IsTrue(MapCatalogLogic.IsSelectable(Extents[2], _devOnly: false, _devBuild: false));
        }

        /// <summary>격자 대형 맵은 크기는 정식이지만 개발 전용으로 강등됐다(2026-09-07) — 배포에선 안 보인다.</summary>
        [Test]
        public void Release_Hides_DevOnly_Even_If_Large()
        {
            Assert.IsFalse(MapCatalogLogic.IsSelectable(60f, _devOnly: true, _devBuild: false));
        }

        [Test]
        public void Default_Index_Is_First_Official_Not_DevOnly()
        {
            Assert.AreEqual(2, MapCatalogLogic.DefaultIndex(Extents, DevOnly));
            Assert.AreEqual(3, MapCatalogLogic.DefaultIndex(Extents, new[] { false, false, true, false }));
        }

        [Test]
        public void Default_Index_Falls_Back_To_Zero_When_Nothing_Official()
        {
            Assert.AreEqual(0, MapCatalogLogic.DefaultIndex(new[] { 15f, 21f }, new[] { false, false }));
        }
    }
}
