using System.Collections.Generic;
using System.Linq;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class MapGeneratorTests
    {
        // 테스트용 최소 풀 — R3·R5(필수) + 외곽 R1·R2·R6 + 미로A. 후보풀 명세 좌표 축약.
        private static MapGenerator.PoolData BuildTestPool()
        {
            var rooms = new List<MapGenerator.RoomOption>
            {
                new("R3", new Vector2(0, 1.5f), new Vector2(7, 9), MapGenerator.Tag.Center, true),
                new("R5", new Vector2(-10.5f, -10.5f), new Vector2(7, 8), MapGenerator.Tag.Outer, true),
                new("R1", new Vector2(10.5f, 12f), new Vector2(4.5f, 5.5f), MapGenerator.Tag.Outer, false),
                new("R2", new Vector2(-12.5f, 3f), new Vector2(4, 6), MapGenerator.Tag.Outer, false),
                new("R6", new Vector2(-11f, 11f), new Vector2(5, 5), MapGenerator.Tag.Outer, false),
            };
            var corr = new List<MapGenerator.CorridorOption>
            {
                new("C1", "R3", "R2", new[] { new Vector2(-3.5f, 2.5f), new Vector2(-10.5f, 2.5f) }, 1.6f),
                new("C5", "R2", "R6", new[] { new Vector2(-12.5f, 6f), new Vector2(-11f, 8.5f) }, 1.6f),
                new("C10", "R5", "R2", new[] { new Vector2(-11f, -6.5f), new Vector2(-12.5f, 0f) }, 1.6f),
                new("C6b", "R6", "R1", new[] { new Vector2(-8.5f, 12f), new Vector2(8.25f, 12f) }, 1.6f),
            };
            // 치즈/요새 슬롯은 §4·§5 축약 — 등급 완전 소진 가능하게 구성
            var cheese = new List<MapGenerator.CheeseSlotOption>
            {
                new("R5", new Vector2(-9, -7), 1, 1, true, 1, 2f),   // G1 고정
                new("R2", new Vector2(-11.5f, 5), 2, 3, false, 0, 2f),
                new("R6", new Vector2(-9, 9.5f), 2, 3, false, 0, 2f),
                new("R1", new Vector2(8.75f, 10), 3, 4, false, 0, 2f),
                new("R3", new Vector2(-2, 4), 6, 6, false, 0, 2f),
                new("R3", new Vector2(0, -2), 7, 7, false, 0, 2f),
                new("R2", new Vector2(-13, 5.5f), 4, 5, false, 0, 2f), // F5(-13,1.5)에서 4.0m — V9 이격
                new("R6", new Vector2(-12, 12), 4, 5, false, 0, 2f),
            };
            var fort = new List<MapGenerator.FortressSlotOption>
            {
                new("R5", new Vector2(-11, -12), 0f, true),
                new("R2", new Vector2(-13, 1.5f), 90f, false),
                new("R6", new Vector2(-12, 12), 180f, false),
                new("R1", new Vector2(11.5f, 13), 180f, false),
            };
            return new MapGenerator.PoolData(rooms, corr, new List<MazePreset>(), cheese, fort);
        }

        [Test]
        public void Generate_SameSeed_Deterministic()
        {
            var pool = BuildTestPool();
            var a = MapGenerator.Generate(pool, 12345);
            var b = MapGenerator.Generate(pool, 12345);
            Assert.IsNotNull(a);
            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
            for (int i = 0; i < a.Rooms.Count; i++)
                Assert.AreEqual(a.Rooms[i].Id, b.Rooms[i].Id);
        }

        /// <summary>
        /// 결정성의 짝(케이스 E-31) — 씨앗이 결과에 안 닿아도 위 테스트는 통과한다.
        /// 이 풀은 방이 전부 채택되므로 씨앗이 바꾸는 것은 선택 방의 **순서**다.
        /// </summary>
        [NUnit.Framework.Property("CaseId", "E-31")]
        [Test]
        public void Generate_DifferentSeeds_DifferSomewhere()
        {
            var pool = BuildTestPool();
            string first = RoomOrder(MapGenerator.Generate(pool, 1));
            bool differs = false;
            for (int seed = 2; seed <= 40 && !differs; seed++)
                differs = RoomOrder(MapGenerator.Generate(pool, seed)) != first;
            Assert.IsTrue(differs, "씨앗을 40개 굴려도 생성 결과가 같다 — 씨앗이 결과에 안 닿는다");
        }

        private static string RoomOrder(MapGenerator.LayoutResult _layout)
        {
            return _layout == null ? "null" : string.Join(",", _layout.Rooms.Select(r => r.Id));
        }

        [Test]
        public void Generate_AlwaysIncludesRequiredRooms()
        {
            var pool = BuildTestPool();
            var layout = MapGenerator.Generate(pool, 777);
            Assert.IsNotNull(layout);
            var ids = layout.Rooms.Select(r => r.Id).ToHashSet();
            Assert.IsTrue(ids.Contains("R3"));
            Assert.IsTrue(ids.Contains("R5"));
        }

        [Test]
        public void Generate_G1_FixedAtAnchor()
        {
            var pool = BuildTestPool();
            var layout = MapGenerator.Generate(pool, 42);
            Assert.IsNotNull(layout);
            var g1 = layout.Cheese.First(c => c.Grade == 1);
            Assert.Less(Vector2.Distance(g1.Pos, new Vector2(-9, -7)), 0.05f);
        }

        [Test]
        public void Generate_CheeseGradesComplete()
        {
            var pool = BuildTestPool();
            var layout = MapGenerator.Generate(pool, 99);
            Assert.IsNotNull(layout);
            Assert.IsTrue(MapValidator.CheeseGradesComplete(layout.Cheese));
        }

        [Test]
        public void Generate_100Seeds_HighSuccessRate()
        {
            var pool = BuildTestPool();
            int ok = 0;
            for (int s = 0; s < 100; s++)
                if (MapGenerator.Generate(pool, s) != null) ok++;
            Assert.GreaterOrEqual(ok, 90, $"생성 성공 {ok}/100 (기준 90)");
        }
    }
}
