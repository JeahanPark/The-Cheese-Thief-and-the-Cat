using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 순수 결정적 맵 생성기. SO에 의존하지 않는 DTO(PoolData)를 받아 LayoutResult를 만든다 —
    /// EditMode 테스트가 SO 없이 호출, 에디터 툴이 SO를 DTO로 변환해 호출.
    /// 검증(MapValidator) 실패 시 다음 시드로 재시도(최대 MAX_RETRY).
    /// </summary>
    public static class MapGenerator
    {
        private const int MAX_RETRY = 200;
        private const int MIN_ROOMS = 5;
        private const int MAX_ROOMS = 6;

        public enum Tag { Outer, Middle, Center }

        public readonly struct RoomOption
        {
            public readonly string Id; public readonly Vector2 Center, Size;
            public readonly Tag Tag; public readonly bool Always;
            public RoomOption(string id, Vector2 c, Vector2 s, Tag t, bool a)
            { Id = id; Center = c; Size = s; Tag = t; Always = a; }
        }
        public readonly struct CorridorOption
        {
            public readonly string Id, A, B; public readonly Vector2[] Waypoints; public readonly float Width;
            public CorridorOption(string id, string a, string b, Vector2[] wp, float w)
            { Id = id; A = a; B = b; Waypoints = wp; Width = w; }
        }
        public readonly struct CheeseSlotOption
        {
            public readonly string RoomId; public readonly Vector2 Pos;
            public readonly int MinGrade, MaxGrade, FixedGrade; public readonly bool Fixed; public readonly float Radius;
            public CheeseSlotOption(string room, Vector2 p, int min, int max, bool fix, int fg, float r)
            { RoomId = room; Pos = p; MinGrade = min; MaxGrade = max; Fixed = fix; FixedGrade = fg; Radius = r; }
        }
        public readonly struct FortressSlotOption
        {
            public readonly string RoomId; public readonly Vector2 Pos; public readonly float Yaw; public readonly bool Always;
            public FortressSlotOption(string room, Vector2 p, float yaw, bool always)
            { RoomId = room; Pos = p; Yaw = yaw; Always = always; }
        }

        public class PoolData
        {
            public readonly List<RoomOption> Rooms;
            public readonly List<CorridorOption> Corridors;
            public readonly List<MazePreset> Mazes;
            public readonly List<CheeseSlotOption> Cheese;
            public readonly List<FortressSlotOption> Fortress;
            public PoolData(List<RoomOption> r, List<CorridorOption> c, List<MazePreset> m,
                List<CheeseSlotOption> ch, List<FortressSlotOption> f)
            { Rooms = r; Corridors = c; Mazes = m; Cheese = ch; Fortress = f; }
        }

        public class LayoutResult
        {
            public int Seed;
            public List<RoomRect> Rooms = new();
            public List<CorridorSeg> Corridors = new();
            public List<CheesePlacement> Cheese = new();
            public List<FortressPlacement> Fortresses = new();
        }

        /// <summary>시드로 유효 맵 생성. 재시도 소진 시 null.</summary>
        public static LayoutResult Generate(PoolData pool, int seed)
        {
            for (int attempt = 0; attempt < MAX_RETRY; attempt++)
            {
                var rng = new System.Random(seed + attempt * 7919);
                LayoutResult r = TryBuild(pool, rng, seed);
                if (r != null && Validate(r)) return r;
            }
            return null;
        }

        private static LayoutResult TryBuild(PoolData pool, System.Random rng, int seed)
        {
            // 1. 방 선택
            var chosen = pool.Rooms.Where(r => r.Always).ToList();
            var rest = pool.Rooms.Where(r => !r.Always).OrderBy(_ => rng.Next()).ToList();
            int target = MIN_ROOMS + rng.Next(0, MAX_ROOMS - MIN_ROOMS + 1);
            foreach (var r in rest)
            {
                if (chosen.Count >= target) break;
                chosen.Add(r);
            }
            var chosenIds = chosen.Select(r => r.Id).ToHashSet();

            var result = new LayoutResult { Seed = seed };
            result.Rooms = chosen.Select(r => new RoomRect { Id = r.Id, Center = r.Center, Size = r.Size }).ToList();

            // 2. 통로 — 양 끝 방이 모두 선택됐으면 채택
            foreach (var c in pool.Corridors)
            {
                if (!chosenIds.Contains(c.A) || !chosenIds.Contains(c.B)) continue;
                for (int i = 0; i < c.Waypoints.Length - 1; i++)
                    result.Corridors.Add(new CorridorSeg { From = c.Waypoints[i], To = c.Waypoints[i + 1], Width = c.Width });
            }

            // 3. 미로 — 배타 방 미선택 시 채택 (남부 슬롯)
            foreach (var m in pool.Mazes)
            {
                if (!string.IsNullOrEmpty(m.ExclusiveRoomId) && chosenIds.Contains(m.ExclusiveRoomId)) continue;
                if (m.Segments != null) result.Corridors.AddRange(m.Segments);
            }

            // 4. 치즈 배정
            if (!AssignCheese(pool, chosenIds, result)) return null;

            // 5. 요새 배정
            if (!AssignFortresses(pool, chosenIds, result)) return null;

            return result;
        }

        // G1 고정 → G7·G6(중앙/미로) → G2~G5(외곽·중간 밴드). 등급 완전 소진 실패 시 null.
        private static bool AssignCheese(PoolData pool, HashSet<string> chosenIds, LayoutResult result)
        {
            var avail = pool.Cheese.Where(s => chosenIds.Contains(s.RoomId)).ToList();
            var used = new HashSet<int>();
            var placed = new List<CheesePlacement>();

            // 고정 슬롯
            foreach (var s in avail.Where(s => s.Fixed))
            {
                placed.Add(new CheesePlacement { Pos = s.Pos, Grade = s.FixedGrade, Radius = s.Radius });
                used.Add(s.FixedGrade);
            }
            // 나머지 등급 7..2 내림차순으로, 밴드 맞는 슬롯에서 배정(고등급 우선 = 중앙/미로 심부)
            var free = avail.Where(s => !s.Fixed).ToList();
            var usedSlots = new HashSet<int>();
            for (int g = 7; g >= 1; g--)
            {
                if (used.Contains(g)) continue;
                int pick = -1;
                for (int i = 0; i < free.Count; i++)
                {
                    if (usedSlots.Contains(i)) continue;
                    if (g >= free[i].MinGrade && g <= free[i].MaxGrade) { pick = i; break; }
                }
                if (pick < 0) return false; // 이 등급 채울 슬롯 없음 → 폐기
                usedSlots.Add(pick);
                placed.Add(new CheesePlacement { Pos = free[pick].Pos, Grade = g, Radius = free[pick].Radius });
                used.Add(g);
            }
            result.Cheese = placed;
            return used.Count == 7;
        }

        // F0 고정 첫째 + 선택 외곽/중간 방에 라운드로빈으로 총 6개(부족분은 남은 슬롯).
        private static bool AssignFortresses(PoolData pool, HashSet<string> chosenIds, LayoutResult result)
        {
            var avail = pool.Fortress.Where(s => chosenIds.Contains(s.RoomId)).ToList();
            var placed = new List<FortressPlacement>();
            foreach (var s in avail.Where(s => s.Always))
                placed.Add(new FortressPlacement { Pos = s.Pos, YawDeg = s.Yaw });
            foreach (var s in avail.Where(s => !s.Always))
            {
                if (placed.Count >= 6) break;
                placed.Add(new FortressPlacement { Pos = s.Pos, YawDeg = s.Yaw });
            }
            result.Fortresses = placed;
            return placed.Count >= 1; // 최소 F0. 쥐 수 초과분은 런타임에서 요새 공유(SpawnPosition % 개수)
        }

        private static bool Validate(LayoutResult r) =>
            MapValidator.NoRoomOverlap(r.Rooms)
            && MapValidator.AllConnected(r.Rooms, r.Corridors, "R3")
            && MapValidator.CorridorsWideEnough(r.Corridors, MapValidator.MIN_CORRIDOR_WIDTH)
            && MapValidator.CheeseGradesComplete(r.Cheese)
            && MapValidator.FortressCheeseSpacing(r.Fortresses, r.Cheese, MapValidator.MIN_FORTRESS_CHEESE_DIST);
    }
}
