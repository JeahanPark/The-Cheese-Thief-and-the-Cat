using System.Collections.Generic;
using CTC.Runtime.Map;
using UnityEditor;
using UnityEngine;
using Pool = CTC.Runtime.Map.MapCandidatePool;

namespace CTC.Editor
{
    /// <summary>
    /// 후보풀 명세(docs/.../2026-07-16-map-candidate-pool.md §1~5)를 SO 에셋으로 굽는다.
    /// F1b·F4는 요새 풋프린트가 방을 넘던 명세값을 정정(Plan 11 §정정표).
    /// Tools > CTC > Build Map Candidate Pool
    /// </summary>
    public static class MapCandidatePoolBuilder
    {
        private const string PoolPath = "Assets/Prefabs/Maps/MapCandidatePool.asset";

        [MenuItem("Tools/CTC/Build Map Candidate Pool")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Maps"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Maps");

            var rooms = new List<Pool.RoomCandidate>
            {
                Room("R3", 0, 1.5f, 7, 9, Pool.RoomTag.Center, true),
                Room("R5", -10.5f, -10.5f, 7, 8, Pool.RoomTag.Outer, true),
                Room("R1", 10.5f, 12f, 4.5f, 5.5f, Pool.RoomTag.Outer, false),
                Room("R2", -12.5f, 3f, 4, 6, Pool.RoomTag.Outer, false),
                Room("R4", 11f, 2f, 5.5f, 7, Pool.RoomTag.Middle, false),
                Room("R6", -11f, 11f, 5, 5, Pool.RoomTag.Outer, false),
                Room("R7", 0, 12f, 5, 4, Pool.RoomTag.Middle, false),
                Room("R8", 11f, -10f, 5, 6, Pool.RoomTag.Outer, false),
                Room("R9", 0, -11f, 5, 5, Pool.RoomTag.Outer, false),
            };

            var corr = new List<Pool.CorridorCandidate>
            {
                Corr("C1", "R3", "R2", 1.6f, V(-3.5f, 2.5f), V(-10.5f, 2.5f)),
                Corr("C2", "R3", "R4", 1.6f, V(3.5f, 2.5f), V(8.25f, 2.5f)),
                Corr("C3", "R3", "R7", 1.6f, V(0, 6), V(0, 10)),
                Corr("C5", "R2", "R6", 1.6f, V(-12.5f, 6), V(-13.3f, 7.5f), V(-11, 8.5f)),
                Corr("C6a", "R6", "R7", 1.6f, V(-8.5f, 12), V(-2.5f, 12)),
                Corr("C6b", "R7", "R1", 1.6f, V(2.5f, 12), V(8.25f, 12)),
                Corr("C6c", "R6", "R1", 1.6f, V(-8.5f, 13), V(8.25f, 13)),
                Corr("C7", "R1", "R4", 1.6f, V(11, 9.25f), V(11, 5.5f)),
                Corr("C10", "R5", "R2", 1.6f, V(-11, -6.5f), V(-12.5f, -6.5f), V(-12.5f, 0)),
            };

            var mazes = new List<MazePreset>
            {
                MazeA(), MazeB(),
            };

            var cheese = new List<Pool.CheeseSlot>
            {
                Cheese("R5", -9, -7, 1, 1, true, 1, 2f),           // G1 고정
                Cheese("R2", -11.5f, 5, 2, 3, false, 0, 1.5f),
                Cheese("R6", -9, 9.5f, 2, 3, false, 0, 1.5f),
                Cheese("R1", 8.75f, 10, 2, 3, false, 0, 1.5f),
                Cheese("R8", 9, -8, 2, 3, false, 0, 1.5f),
                Cheese("R9", 1.8f, -9, 3, 4, false, 0, 1.5f),
                Cheese("R4", 9, 1, 4, 5, false, 0, 1.5f),          // F(R4 12.5,4.5)에서 4.95m — V9 이격
                Cheese("R7", 0, 11, 4, 5, false, 0, 1.5f),
                Cheese("R3", -2, 4, 6, 6, false, 0, 1.5f),
                Cheese("R3", 0, -2, 7, 7, false, 0, 1.5f),
            };

            var fort = new List<Pool.FortressSlot>
            {
                Fort("R5", -11, -12, 0f, true),      // F0 고정
                Fort("R8", 12, -11, 0f, false),
                Fort("R6", -12, 12, 180f, false),
                Fort("R1", 11.5f, 13, 180f, false),
                Fort("R9", 0, -12.3f, 0f, false),    // 정정: -12.5 → -12.3
                Fort("R2", -13, 1.5f, 90f, false),
                Fort("R4", 12.5f, 4.5f, 270f, false),// 정정: 13 → 12.5
            };

            var pool = AssetDatabase.LoadAssetAtPath<Pool>(PoolPath);
            if (pool == null) { pool = ScriptableObject.CreateInstance<Pool>(); AssetDatabase.CreateAsset(pool, PoolPath); }
            pool.Set(rooms, corr, mazes, cheese, fort);
            EditorUtility.SetDirty(pool);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MapCandidatePoolBuilder] 후보풀 저장 — 방 {rooms.Count}·통로 {corr.Count}·미로 {mazes.Count}·치즈 {cheese.Count}·요새 {fort.Count}");
        }

        private static Vector2 V(float _x, float _z) => new Vector2(_x, _z);

        private static Pool.RoomCandidate Room(string _id, float _cx, float _cz, float _sx, float _sz, Pool.RoomTag _tag, bool _always) =>
            new() { Id = _id, Center = V(_cx, _cz), Size = V(_sx, _sz), Tag = _tag, AlwaysInclude = _always };

        private static Pool.CorridorCandidate Corr(string _id, string _a, string _b, float _w, params Vector2[] _wp) =>
            new() { Id = _id, RoomA = _a, RoomB = _b, Width = _w, Waypoints = new List<Vector2>(_wp) };

        private static Pool.CheeseSlot Cheese(string _room, float _x, float _z, int _min, int _max, bool _fix, int _fg, float _r) =>
            new() { RoomId = _room, Pos = V(_x, _z), MinGrade = _min, MaxGrade = _max, Fixed = _fix, FixedGrade = _fg, Radius = _r };

        private static Pool.FortressSlot Fort(string _room, float _x, float _z, float _yaw, bool _always) =>
            new() { RoomId = _room, Pos = V(_x, _z), YawDeg = _yaw, AlwaysInclude = _always };

        private static CorridorSeg Seg(float _ax, float _az, float _bx, float _bz) =>
            new() { From = V(_ax, _az), To = V(_bx, _bz), Width = 1.6f };

        // 미로-A (남중앙) — 명세 §3
        private static MazePreset MazeA() => new()
        {
            Id = "MazeA", ExclusiveRoomId = "R9",
            Segments = new List<CorridorSeg>
            {
                Seg(1.7f, -4, 1.7f, -12), Seg(1.7f, -7, -3, -7), Seg(-3, -7, -3, -12.5f),
                Seg(-3, -12.5f, -4, -12.5f), Seg(1.7f, -8, 4, -8), Seg(1.7f, -12, 3, -12), Seg(3, -12, 3, -13.5f),
            },
        };

        // 미로-B (남동) — 명세 §3
        private static MazePreset MazeB() => new()
        {
            Id = "MazeB", ExclusiveRoomId = "R8",
            Segments = new List<CorridorSeg>
            {
                Seg(10, -2, 10, -13), Seg(10, -8, 7, -8), Seg(10, -5, 13, -5),
                Seg(13, -5, 13, -11), Seg(13, -11, 10, -11), Seg(10, -13, 8, -13),
            },
        };
    }
}
