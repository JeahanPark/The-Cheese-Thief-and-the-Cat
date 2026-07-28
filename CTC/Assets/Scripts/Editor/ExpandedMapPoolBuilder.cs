using System.Collections.Generic;
using System.Linq;
using CTC.Runtime.Map;
using UnityEditor;
using UnityEngine;
using Pool = CTC.Runtime.Map.MapCandidatePool;

namespace CTC.Editor
{
    /// <summary>
    /// 기본 후보풀을 스케일해 확장 후보풀을 만든다(2026-07-28, 맵 2배).
    ///
    /// 좌표만 ×2로 키우면 방 하나가 14×18m가 돼 텅 빈 체육관이 되고 이동 시간만 2배가 된다.
    /// 그래서 좌표는 ×1.4만 키우고(면적 2배 = 42×42) 남는 공간은 **방 개수**로 채운다.
    /// 복도 폭은 배율이 아니라 명시 상향 — 1.6m는 3인칭 카메라 기준 업계 권장(3m)의 절반이다.
    /// </summary>
    public static class ExpandedMapPoolBuilder
    {
        private const string SourcePoolPath = "Assets/Prefabs/Maps/MapCandidatePool.asset";

        private const float COORD_SCALE = 1.4f;      // 면적 2배 (30×30 → 42×42)
        private const float CORRIDOR_WIDTH = 3.0f;   // 3인칭 카메라·스프링암 여유
        private const float HALF_EXTENT = 21f;       // 42 ÷ 2
        // 방 9개를 전부 쓴다 — 미로는 R8·R9와 배타라 자동으로 빠지고, 그 자리를 방과 복도가 채운다.
        private const int MIN_ROOMS = 9;
        private const int MAX_ROOMS = 9;

        [MenuItem("Tools/CTC/Build Expanded Map Pool")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<Pool>(SourcePoolPath);
            if (source == null)
            {
                Debug.LogError($"[ExpandedMapPool] 원본 풀 없음: {SourcePoolPath}");
                return;
            }

            var rooms = source.Rooms.Select(r => new Pool.RoomCandidate
            {
                Id = r.Id,
                Center = r.Center * COORD_SCALE,
                Size = r.Size * COORD_SCALE,
                Tag = r.Tag,
                AlwaysInclude = r.AlwaysInclude,
            }).ToList();

            var corridors = source.Corridors.Select(c => new Pool.CorridorCandidate
            {
                Id = c.Id,
                RoomA = c.RoomA,
                RoomB = c.RoomB,
                Waypoints = c.Waypoints.Select(w => w * COORD_SCALE).ToList(),
                Width = CORRIDOR_WIDTH,
            }).ToList();
            corridors.AddRange(SouthCorridors());

            var mazes = source.Mazes.Select(ScaleMaze).ToList();

            var cheese = source.CheeseSlots.Select(c => new Pool.CheeseSlot
            {
                RoomId = c.RoomId,
                Pos = c.Pos * COORD_SCALE,
                MinGrade = c.MinGrade,
                MaxGrade = c.MaxGrade,
                Fixed = c.Fixed,
                FixedGrade = c.FixedGrade,
                Radius = c.Radius, // 반경은 안 키운다 — 파밍 자리는 여전히 '한 지점'이어야 한다
            }).ToList();

            var fortress = source.FortressSlots.Select(f => new Pool.FortressSlot
            {
                RoomId = f.RoomId,
                Pos = f.Pos * COORD_SCALE,
                YawDeg = f.YawDeg,
                AlwaysInclude = f.AlwaysInclude,
            }).ToList();

            var pool = AssetDatabase.LoadAssetAtPath<Pool>(MapGeneratorWindow.ExpandedPoolPath);
            if (pool == null)
            {
                pool = ScriptableObject.CreateInstance<Pool>();
                AssetDatabase.CreateAsset(pool, MapGeneratorWindow.ExpandedPoolPath);
            }
            pool.Set(rooms, corridors, mazes, cheese, fortress);
            pool.SetShape(MIN_ROOMS, MAX_ROOMS, HALF_EXTENT, "확장 맵 (42×42)");
            EditorUtility.SetDirty(pool);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ExpandedMapPool] 생성 — 방 {rooms.Count}(채택 {MIN_ROOMS}~{MAX_ROOMS})" +
                      $"·통로 {corridors.Count}(폭 {CORRIDOR_WIDTH})·치즈 {cheese.Count}·요새 {fortress.Count}" +
                      $"·반경 {HALF_EXTENT} → {MapGeneratorWindow.ExpandedPoolPath}");
        }

        /// <summary>
        /// 남부 방(R8·R9)을 잇는 신규 복도. 원본 풀에는 이 둘로 가는 복도가 아예 없어서
        /// 미로로만 닿는 '배타 슬롯'이었다(MazeA↔R9, MazeB↔R8). 방 9개를 전부 쓰려면
        /// 미로가 빠지므로 그 연결을 복도가 대신해야 한다.
        ///
        /// 좌표는 원본 좌표계로 적고 스케일을 함께 곱한다 — 위 방·치즈와 같은 배율이라
        /// 방 경계 접점이 정확히 맞는다.
        /// </summary>
        private static List<Pool.CorridorCandidate> SouthCorridors()
        {
            return new List<Pool.CorridorCandidate>
            {
                // R3 남쪽 변(z=-3) → R9 북쪽 변(z=-8.5)
                Corridor("CS1", "R3", "R9", new Vector2(0f, -3f), new Vector2(0f, -8.5f)),
                // R5 동쪽 변(x=-7) → R9 서쪽 변(x=-2.5)
                Corridor("CS2", "R5", "R9", new Vector2(-7f, -10.5f), new Vector2(-2.5f, -10.5f)),
                // R9 동쪽 변(x=2.5) → R8 서쪽 변(x=8.5)
                Corridor("CS3", "R9", "R8", new Vector2(2.5f, -10.5f), new Vector2(8.5f, -10.5f)),
                // R4 남쪽 변(z=-1.5) → R8 북쪽 변(z=-7)
                Corridor("CS4", "R4", "R8", new Vector2(11f, -1.5f), new Vector2(11f, -7f)),
            };
        }

        private static Pool.CorridorCandidate Corridor(string id, string a, string b, Vector2 from, Vector2 to)
        {
            return new Pool.CorridorCandidate
            {
                Id = id,
                RoomA = a,
                RoomB = b,
                Waypoints = new List<Vector2> { from * COORD_SCALE, to * COORD_SCALE },
                Width = CORRIDOR_WIDTH,
            };
        }

        // 미로는 세그먼트 목록 — 좌표를 스케일하고 폭만 통로 기준으로 올린다.
        private static MazePreset ScaleMaze(MazePreset _source)
        {
            var scaled = new MazePreset
            {
                Id = _source.Id,
                ExclusiveRoomId = _source.ExclusiveRoomId,
                Segments = new List<CorridorSeg>(),
            };
            if (_source.Segments == null) return scaled;

            foreach (CorridorSeg s in _source.Segments)
            {
                scaled.Segments.Add(new CorridorSeg
                {
                    From = s.From * COORD_SCALE,
                    To = s.To * COORD_SCALE,
                    Width = CORRIDOR_WIDTH,
                });
            }
            return scaled;
        }
    }
}
