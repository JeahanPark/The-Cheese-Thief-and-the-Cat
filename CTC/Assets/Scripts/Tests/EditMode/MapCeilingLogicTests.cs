using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>맵 천장 자리(2026-09-14) — 들보가 경계를 덮고, 채광창이 달빛 자리에 뚫리고, 매달린 것이 빛을 피한다.</summary>
    public class MapCeilingLogicTests
    {
        [NUnit.Framework.Property("CaseId", "D-24")]
        [Test]
        public void Girders_CoverBothEdges_EvenlySpaced()
        {
            List<float> z = MapCeilingLogic.GirderZs(15f);
            Assert.AreEqual(-15f, z[0], 1e-4f);
            Assert.AreEqual(15f, z[z.Count - 1], 1e-4f);
            for (int i = 1; i < z.Count; i++) Assert.LessOrEqual(z[i] - z[i - 1], MapCeilingLogic.GIRDER_SPACING * 1.25f);
            Assert.Greater(MapCeilingLogic.JoistXs(15f).Count, z.Count, "장선이 들보보다 촘촘하다");
        }

        [NUnit.Framework.Property("CaseId", "D-24")]
        [Test]
        public void Cells_TileTheMap_AndSkylightOnlyNearMoon()
        {
            List<Vector2> cells = MapCeilingLogic.CellCenters(15f, out float size);
            Assert.AreEqual(30f, Mathf.Sqrt(cells.Count) * size, 1e-3f, "칸이 맵 폭을 빈틈없이 덮는다");

            var moons = new List<Vector2> { new Vector2(0f, 0f) };
            int holes = 0;
            foreach (Vector2 c in cells) if (MapCeilingLogic.IsSkylight(c, moons)) holes++;
            Assert.That(holes, Is.InRange(1, 4), "달빛 하나에 채광창은 한두 칸");
            Assert.IsFalse(MapCeilingLogic.IsSkylight(new Vector2(12f, 12f), moons));
            Assert.IsFalse(MapCeilingLogic.IsSkylight(Vector2.zero, null));
        }

        [NUnit.Framework.Property("CaseId", "D-24")]
        [Test]
        public void HangSlots_Deterministic_AvoidLights_WithinLengthRange()
        {
            var lamps = new List<Vector2> { new Vector2(0f, 0f) };
            var moons = new List<Vector2> { new Vector2(10f, 10f) };
            List<MapCeilingLogic.HangSlot> a = MapCeilingLogic.HangSlots(30f, 7, 5, lamps, moons);
            List<MapCeilingLogic.HangSlot> b = MapCeilingLogic.HangSlots(30f, 7, 5, lamps, moons);
            Assert.AreEqual(a.Count, b.Count, "같은 시드는 같은 자리");
            Assert.Greater(a.Count, 0);
            foreach (MapCeilingLogic.HangSlot s in a)
            {
                Assert.Greater((s.Pos - lamps[0]).magnitude, MapCeilingLogic.HANG_CLEAR);
                Assert.Greater((s.Pos - moons[0]).magnitude, MapCeilingLogic.HANG_CLEAR + MapCeilingLogic.SKYLIGHT_RADIUS);
                Assert.That(s.Length, Is.InRange(MapCeilingLogic.HANG_MIN, MapCeilingLogic.HANG_MAX));
                Assert.That(s.Kind, Is.InRange(0, 4));
            }
            Assert.AreEqual(0, MapCeilingLogic.HangSlots(30f, 7, 0, lamps, moons).Count, "매달 종류가 없으면 비운다");
        }

        [NUnit.Framework.Property("CaseId", "D-24")]
        [Test]
        public void Deck_IsAboveWallsAndCamera()
        {
            Assert.Greater(MapCeilingLogic.DECK_Y - MapCeilingLogic.GIRDER_DEPTH - MapCeilingLogic.HANG_MAX - MapCeilingLogic.ITEM_MAX_HEIGHT, 4.0f,
                "가장 긴 매달린 것의 바닥도 카메라 최고 높이(≈4m) 위다");
        }
    }
}
