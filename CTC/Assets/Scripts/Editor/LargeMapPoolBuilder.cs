using System.Collections.Generic;
using CTC.Runtime.Map;
using UnityEditor;
using UnityEngine;
using Pool = CTC.Runtime.Map.MapCandidatePool;

namespace CTC.Editor
{
    /// <summary>
    /// 120×120 대형 후보풀을 절차적으로 만든다(2026-08-10, 기준 구성 2:8).
    ///
    /// 기존 확장 풀(42×42)은 손으로 찍은 30×30 풀을 ×1.4로 늘린 것이라 이 크기로는 못 늘린다 —
    /// 좌표만 ×4로 키우면 방 하나가 40×36m짜리 빈 체육관이 된다. 그래서 여기서는
    /// **격자에 방을 깔고 통로를 랜덤 신장 트리로 잇는다**. 넓이는 방 개수로 채우고 방 크기는 그대로 둔다.
    ///
    /// 왜 격자인가: 숨바꼭질에서 필요한 건 '넓은 공터'가 아니라 '갈림길'이다. 7×7=49칸이면
    /// 고양이가 한 번에 볼 수 있는 범위 밖에 늘 다른 길이 있고, 신장 트리 + 여분 간선이
    /// 막다른 길과 순환로를 동시에 만든다(전부 이으면 격자라 도주가 너무 쉽다).
    ///
    /// 치즈 지점은 **등급 2벌(14지점)**이다. 7지점만 두면 인원 비례로 커진 총량이 지점당으로 몰려
    /// 낮은 등급 자리가 판이 끝날 때까지 안 마르고, 그러면 후반 압축이 사라진다.
    /// </summary>
    public static class LargeMapPoolBuilder
    {
        private const int GRID = 7;                  // 7×7 = 49칸
        private const float CELL = 17f;              // 칸 간격(m) — 방 사이 간격이 통로 길이가 된다
        private const float CORRIDOR_WIDTH = 3.0f;   // 3인칭 카메라·스프링암 여유
        private const float HALF_EXTENT = 60f;       // 120 ÷ 2
        private const float JITTER = 0.75f;          // 방 중심 흔들기 — 격자 티를 지운다
        private const float CHEESE_RADIUS = 2f;
        private const int CHEESE_SETS = 2;           // 등급 1~7을 2벌 = 14지점
        private const int FORTRESS_COUNT = 8;        // 쥐 최대 8
        private const int EXTRA_EDGE_PERCENT = 40;   // 신장 트리에 더할 여분 간선 비율
        private const int BUILD_SEED = 20260810;     // 툴 자체의 시드 — 고정이라 결과가 재현된다

        // 방 크기 후보. 칸 간격 17에서 최대 12를 써도 이웃과 3.5m 이상 벌어져 통로가 들어간다.
        private static readonly float[] WIDTHS = { 10f, 12f };
        private static readonly float[] HEIGHTS = { 9f, 11f };

        [MenuItem("Tools/CTC/Build Large Map Pool")]
        public static void Build()
        {
            var rng = new System.Random(BUILD_SEED);

            // ---- 1. 방 ----
            var rooms = new List<Pool.RoomCandidate>();
            var centers = new Vector2[GRID, GRID];
            var sizes = new Vector2[GRID, GRID];
            var ids = new string[GRID, GRID];

            var fortressCells = FortressCells();

            for (int j = 0; j < GRID; j++)
            {
                for (int i = 0; i < GRID; i++)
                {
                    int index = j * GRID + i;
                    string id = $"R{index}";
                    // 요새 방은 크기를 고정한다 — 스폰 자리와 치즈 이격을 계산으로 보장하려면 변수를 줄여야 한다
                    bool isFortressRoom = fortressCells.Contains((i, j));
                    var size = isFortressRoom
                        ? new Vector2(12f, 11f)
                        : new Vector2(WIDTHS[rng.Next(WIDTHS.Length)], HEIGHTS[rng.Next(HEIGHTS.Length)]);

                    var center = new Vector2(
                        (i - (GRID - 1) * 0.5f) * CELL + Jitter(rng),
                        (j - (GRID - 1) * 0.5f) * CELL + Jitter(rng));

                    // 중앙 칸은 고양이 스폰 자리(0,0)다 — 흔들면 벽 안에서 시작할 수 있다
                    if (i == GRID / 2 && j == GRID / 2) center = Vector2.zero;

                    centers[i, j] = center;
                    sizes[i, j] = size;
                    ids[i, j] = id;

                    rooms.Add(new Pool.RoomCandidate
                    {
                        Id = id,
                        Center = center,
                        Size = size,
                        Tag = TagOf(i, j),
                        AlwaysInclude = true,   // 격자는 통째로 쓴다 — 빼면 통로가 끊긴다
                    });
                }
            }

            // ---- 2. 통로: 랜덤 신장 트리 + 여분 간선 ----
            var corridors = BuildCorridors(rng, centers, sizes, ids);

            // ---- 3. 치즈 슬롯 ----
            var cheese = BuildCheeseSlots(rng, centers, sizes, ids, fortressCells);

            // ---- 4. 요새 슬롯 ----
            var fortress = new List<Pool.FortressSlot>();
            foreach ((int i, int j) in fortressCells)
            {
                Vector2 c = centers[i, j];
                // 입구가 맵 안쪽을 보게 한다 — 밖을 보면 쥐가 경계벽에 끼인 채로 시작한다
                float yaw = Mathf.Atan2(-c.x, -c.y) * Mathf.Rad2Deg;
                fortress.Add(new Pool.FortressSlot
                {
                    RoomId = ids[i, j],
                    Pos = c,
                    YawDeg = yaw,
                    AlwaysInclude = fortress.Count == 0,   // 첫 자리는 F0 — 반드시 들어간다
                });
            }

            // ---- 5. 저장 ----
            var pool = AssetDatabase.LoadAssetAtPath<Pool>(MapGeneratorWindow.LargePoolPath);
            if (pool == null)
            {
                pool = ScriptableObject.CreateInstance<Pool>();
                AssetDatabase.CreateAsset(pool, MapGeneratorWindow.LargePoolPath);
            }
            pool.Set(rooms, corridors, new List<MazePreset>(), cheese, fortress);
            int roomCount = GRID * GRID;
            // 권장 쥐 6~8 — 1인당 면적이 2400㎡(6인)~1800㎡(8인)로, 기준 맵 1v3의 300㎡보다 훨씬 희박하다.
            // 그래서 고양이가 둘이고 발소리·파밍 소리를 함께 키웠다.
            pool.SetShape(roomCount, roomCount, HALF_EXTENT, "대형 맵 (120×120)", 6, 8,
                CHEESE_SETS, FORTRESS_COUNT);
            EditorUtility.SetDirty(pool);
            AssetDatabase.SaveAssets();

            Debug.Log($"[LargeMapPool] 생성 — 방 {rooms.Count}·통로 {corridors.Count}(폭 {CORRIDOR_WIDTH})" +
                      $"·치즈 슬롯 {cheese.Count}(등급 {CHEESE_SETS}벌 = {7 * CHEESE_SETS}지점)" +
                      $"·요새 {fortress.Count}·반경 {HALF_EXTENT} → {MapGeneratorWindow.LargePoolPath}");
        }

        private static float Jitter(System.Random _rng) => (float)(_rng.NextDouble() * 2.0 - 1.0) * JITTER;

        // 바깥 테두리를 한 바퀴 돌며 8칸을 고르게 집는다 — 쥐 8명이 맵 전체에 흩어져 시작한다.
        private static HashSet<(int, int)> FortressCells()
        {
            var ring = new List<(int, int)>();
            for (int i = 0; i < GRID; i++) ring.Add((i, 0));
            for (int j = 1; j < GRID; j++) ring.Add((GRID - 1, j));
            for (int i = GRID - 2; i >= 0; i--) ring.Add((i, GRID - 1));
            for (int j = GRID - 2; j >= 1; j--) ring.Add((0, j));

            var picked = new HashSet<(int, int)>();
            for (int k = 0; k < FORTRESS_COUNT; k++)
                picked.Add(ring[k * ring.Count / FORTRESS_COUNT]);
            return picked;
        }

        // 중앙 3×3은 Center(고등급), 그 밖 5×5 테두리는 Middle, 가장 바깥은 Outer.
        private static Pool.RoomTag TagOf(int _i, int _j)
        {
            int ring = Mathf.Max(Mathf.Abs(_i - GRID / 2), Mathf.Abs(_j - GRID / 2));
            if (ring <= 1) return Pool.RoomTag.Center;
            return ring == 2 ? Pool.RoomTag.Middle : Pool.RoomTag.Outer;
        }

        // 격자 간선(가로·세로 이웃)을 랜덤 신장 트리로 고르고, 남은 간선의 일부를 더한다.
        // 전부 이으면 어디서든 네 방향이 열려 추격이 성립하지 않고, 트리만 쓰면 막다른 길뿐이라
        // 한 번 걸리면 끝이다 — 그 사이가 필요하다.
        private static List<Pool.CorridorCandidate> BuildCorridors(
            System.Random _rng, Vector2[,] _centers, Vector2[,] _sizes, string[,] _ids)
        {
            var edges = new List<(int ai, int aj, int bi, int bj)>();
            for (int j = 0; j < GRID; j++)
                for (int i = 0; i < GRID; i++)
                {
                    if (i + 1 < GRID) edges.Add((i, j, i + 1, j));
                    if (j + 1 < GRID) edges.Add((i, j, i, j + 1));
                }
            Shuffle(_rng, edges);

            var parent = new int[GRID * GRID];
            for (int k = 0; k < parent.Length; k++) parent[k] = k;

            var chosen = new List<(int, int, int, int)>();
            var leftover = new List<(int, int, int, int)>();
            foreach (var e in edges)
            {
                int a = e.aj * GRID + e.ai;
                int b = e.bj * GRID + e.bi;
                if (Find(parent, a) == Find(parent, b)) { leftover.Add(e); continue; }
                parent[Find(parent, a)] = Find(parent, b);
                chosen.Add(e);
            }
            int extra = leftover.Count * EXTRA_EDGE_PERCENT / 100;
            for (int k = 0; k < extra; k++) chosen.Add(leftover[k]);

            var corridors = new List<Pool.CorridorCandidate>();
            foreach ((int ai, int aj, int bi, int bj) in chosen)
            {
                Vector2 ca = _centers[ai, aj], cb = _centers[bi, bj];
                Vector2 sa = _sizes[ai, aj], sb = _sizes[bi, bj];
                Vector2 from, to;

                if (aj == bj)   // 가로 이웃 — x축으로 뻗는다
                {
                    float y = OverlapMid(ca.y, sa.y, cb.y, sb.y);
                    from = new Vector2(ca.x + sa.x * 0.5f, y);
                    to = new Vector2(cb.x - sb.x * 0.5f, y);
                }
                else            // 세로 이웃 — z축으로 뻗는다
                {
                    float x = OverlapMid(ca.x, sa.x, cb.x, sb.x);
                    from = new Vector2(x, ca.y + sa.y * 0.5f);
                    to = new Vector2(x, cb.y - sb.y * 0.5f);
                }

                corridors.Add(new Pool.CorridorCandidate
                {
                    Id = $"C{_ids[ai, aj]}_{_ids[bi, bj]}",
                    RoomA = _ids[ai, aj],
                    RoomB = _ids[bi, bj],
                    Waypoints = new List<Vector2> { from, to },
                    Width = CORRIDOR_WIDTH,
                });
            }
            return corridors;
        }

        // 두 방이 공유하는 축 구간의 한가운데 — 여기로 뚫어야 통로가 양쪽 벽 안에 들어간다.
        private static float OverlapMid(float _ca, float _sa, float _cb, float _sb)
        {
            float lo = Mathf.Max(_ca - _sa * 0.5f, _cb - _sb * 0.5f);
            float hi = Mathf.Min(_ca + _sa * 0.5f, _cb + _sb * 0.5f);
            return (lo + hi) * 0.5f;
        }

        /// <summary>
        /// 방마다 치즈 자리 2개(대각 반대쪽). **요새 방은 건너뛴다** — 요새-치즈 이격(3.7m)을
        /// 계산으로 보장하는 가장 확실한 방법이 "같은 방에 안 두는 것"이다.
        ///
        /// 목록 순서가 중요하다: 생성기는 밴드가 맞는 **첫** 슬롯을 집으므로, 같은 방의 두 자리가
        /// 나란히 있으면 2벌짜리 등급이 한 방에 겹쳐 박힌다. 그래서 자리 번호별로 방을 한 바퀴 돌며
        /// (매번 다른 순서로) 늘어놓는다.
        /// </summary>
        private static List<Pool.CheeseSlot> BuildCheeseSlots(
            System.Random _rng, Vector2[,] _centers, Vector2[,] _sizes, string[,] _ids,
            HashSet<(int, int)> _fortressCells)
        {
            var slots = new List<Pool.CheeseSlot>();

            for (int slotIndex = 0; slotIndex < 2; slotIndex++)
            {
                var cells = new List<(int, int)>();
                for (int j = 0; j < GRID; j++)
                    for (int i = 0; i < GRID; i++)
                    {
                        if (_fortressCells.Contains((i, j))) continue;
                        cells.Add((i, j));
                    }
                Shuffle(_rng, cells);

                foreach ((int i, int j) in cells)
                {
                    Vector2 c = _centers[i, j];
                    Vector2 s = _sizes[i, j];
                    float sign = slotIndex == 0 ? 1f : -1f;
                    var pos = new Vector2(c.x + sign * s.x * 0.25f, c.y - sign * s.y * 0.25f);

                    (int min, int max) = BandOf(TagOf(i, j));
                    slots.Add(new Pool.CheeseSlot
                    {
                        RoomId = _ids[i, j],
                        Pos = pos,
                        MinGrade = min,
                        MaxGrade = max,
                        Fixed = false,
                        FixedGrade = 0,
                        Radius = CHEESE_RADIUS,
                    });
                }
            }
            return slots;
        }

        // 심부일수록 고등급 — 좋은 자리를 가지려면 맵 한가운데로 들어와야 한다.
        private static (int min, int max) BandOf(Pool.RoomTag _tag)
        {
            if (_tag == Pool.RoomTag.Center) return (6, 7);
            return _tag == Pool.RoomTag.Middle ? (3, 5) : (1, 3);
        }

        private static void Shuffle<T>(System.Random _rng, List<T> _list)
        {
            for (int i = _list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_list[i], _list[j]) = (_list[j], _list[i]);
            }
        }

        private static int Find(int[] _parent, int _x)
        {
            while (_parent[_x] != _x) { _parent[_x] = _parent[_parent[_x]]; _x = _parent[_x]; }
            return _x;
        }
    }
}
