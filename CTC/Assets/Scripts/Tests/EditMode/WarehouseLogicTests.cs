using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>창고 평면도(2026-09-07) — 선반 열을 틈으로 자르는 순수 규칙.</summary>
    public class WarehouseLogicTests
    {
        [Test]
        public void SplitByGaps_CutsRowIntoSegments()
        {
            var gaps = new List<Vector2> { new Vector2(-12f, -9f), new Vector2(6f, 9f) };
            List<Vector2> segs = WarehouseLogic.SplitByGaps(-18f, 18f, gaps);
            CollectionAssert.AreEqual(
                new[] { new Vector2(-18f, -12f), new Vector2(-9f, 6f), new Vector2(9f, 18f) }, segs);
        }

        [Test]
        public void SplitByGaps_NoGaps_OneSegment()
        {
            List<Vector2> segs = WarehouseLogic.SplitByGaps(0f, 10f, new List<Vector2>());
            Assert.AreEqual(1, segs.Count);
            Assert.AreEqual(new Vector2(0f, 10f), segs[0]);
        }

        [Test]
        public void SplitByGaps_UnsortedGaps_StillOrdered()
        {
            var gaps = new List<Vector2> { new Vector2(6f, 9f), new Vector2(-12f, -9f) };
            List<Vector2> segs = WarehouseLogic.SplitByGaps(-18f, 18f, gaps);
            Assert.AreEqual(3, segs.Count);
            Assert.AreEqual(new Vector2(-18f, -12f), segs[0]);
        }

        [Test]
        public void SplitByGaps_DropsSlivers()
        {
            var gaps = new List<Vector2> { new Vector2(0.1f, 3f) };
            List<Vector2> segs = WarehouseLogic.SplitByGaps(0f, 10f, gaps);
            Assert.AreEqual(1, segs.Count, "0.1m 조각은 버린다");
            Assert.AreEqual(new Vector2(3f, 10f), segs[0]);
        }

        /// <summary>가로 열(z 고정)과 세로 열(x 고정)을 박스로 — 두께는 열 축과 직각.</summary>
        [Test]
        public void ShelfBox_HorizontalAndVertical()
        {
            PropPlacement h = WarehouseLogic.ShelfBox("S", new Vector2(-18f, 15f), new Vector2(18f, 15f), new Vector2(-9f, 6f), 1f);
            Assert.AreEqual(new Vector2(-1.5f, 15f), h.Pos);
            Assert.AreEqual(new Vector2(15f, 1f), h.Size);

            PropPlacement v = WarehouseLogic.ShelfBox("S", new Vector2(-52f, 20f), new Vector2(-52f, 56f), new Vector2(37f, 56f), 1f);
            Assert.AreEqual(new Vector2(-52f, 46.5f), v.Pos);
            Assert.AreEqual(new Vector2(1f, 19f), v.Size);
        }

        /// <summary>벽 안쪽 여유 — 프롭이 경계벽에 파고들면 실패.</summary>
        [Test]
        public void InsideBounds_RejectsWallOverlap()
        {
            Assert.IsTrue(WarehouseLogic.InsideBounds(new Vector2(50f, 50f), new Vector2(10f, 4f), 60f, 0.25f));
            Assert.IsFalse(WarehouseLogic.InsideBounds(new Vector2(56f, 0f), new Vector2(10f, 4f), 60f, 0.25f));
        }
    }
}
