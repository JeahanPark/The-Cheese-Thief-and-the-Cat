using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 치즈 요새의 벽을 통째로 하나의 메시로 굽는다 — Tools > CTC > Build Fortress Mesh.
    ///
    /// **왜 벽 8개가 아니라 메시 1개인가.** 프리팹의 벽은 박스 8개인데, 그건 콜라이더로서
    /// 밸런스(쥐만 통과하는 문 1.0×0.75)를 들고 있는 치수다. 그 박스마다 치즈 구멍을 파면
    /// 비균등 스케일에 눌려 구멍이 타원으로 늘어나고, 이음매마다 각진 틈이 보인다(치즈에서 겪었다).
    /// 그래서 **콜라이더는 박스 그대로 두고, 보이는 것만 실제 치수의 메시 하나로 바꾼다.**
    ///
    /// 형상은 CSG 네 줄이다 — 바깥 상자에서 안쪽을 파내 벽을 만들고(지붕 없음),
    /// 앞뒤에 쥐 전용 문을 도려낸 뒤, 바깥·안쪽 면에 치즈 구멍을 판다.
    /// 치수는 <see cref="SetupNetworkingScene"/>의 요새 상수와 **같아야 한다** — 어긋나면
    /// 에러 없이 그림과 몸이 따로 논다.
    /// </summary>
    public static class FortressMeshBuilder
    {
        // ── 요새 치수 (StructurePrefabFactory의 FORTRESS_* 와 같은 값) ────
        private const float WALL_HEIGHT = 1.2f;
        private const float HALF = 1.2f;                 // 풋프린트 2.4×2.4
        private const float DOOR_WIDTH = 1.0f;
        private const float DOOR_HEIGHT = 0.75f;
        private const float WALL_THICKNESS = 0.2f;

        // ── 형태 ────────────────────────────────────────────────────────
        /// <summary>바깥 모서리 둥글림. 0이면 칼같이 각져 이 게임 톤이 아니다.</summary>
        private const float OUTER_ROUND = 0.07f;

        /// <summary>안쪽을 파낼 때 남기는 필렛. 벽 두께(0.2)의 4분의 1을 넘기면 벽이 날처럼 얇아진다.</summary>
        private const float INNER_FILLET = 0.055f;

        /// <summary>문 입구의 필렛. 여기만 조금 더 둥근 이유는 몸이 실제로 지나가는 자리라서다.</summary>
        private const float DOOR_FILLET = 0.06f;

        // ── 구멍 ────────────────────────────────────────────────────────
        private const int HOLES_PER_OUTER_FACE = 6;
        private const int HOLES_PER_INNER_FACE = 3;
        private const float HOLE_RADIUS_MIN = 0.060f;
        private const float HOLE_RADIUS_MAX = 0.095f;

        /// <summary>반지름 대비 파는 깊이. 벽이 0.2뿐이라 이 값을 올리면 구멍이 벽을 **뚫는다**.</summary>
        private const float HOLE_DEPTH_RATIO = 0.55f;

        private const float HOLE_EDGE_MARGIN = 0.10f;
        private const float HOLE_GAP = 0.05f;
        private const int HOLE_SEED = 20260830;
        private const int HOLE_TRY_LIMIT = 600;

        // ── 격자 ────────────────────────────────────────────────────────
        /// <summary>
        /// 격자 칸. 구멍 깊이(≈0.05)가 한 칸은 되어야 파인 게 읽힌다. 줄이면 매끈해지는 대신
        /// 삼각형이 제곱으로 늘어난다 — 요새는 한 판에 여럿 서는 물건이라 여기서 멈췄다.
        /// </summary>
        private const float CELL_SIZE = 0.055f;

        private const float GRID_PADDING = 0.10f;
        private const int RELAX_ITERATIONS = 6;
        private const float RELAX_STRENGTH = 0.5f;
        private const int NORMAL_SMOOTH_ITERATIONS = 3;

        /// <summary>
        /// 요새 벽은 창고(밝은 치즈)와 **한눈에 갈라져야 한다** — 하나는 숨는 곳이고 하나는 돈줄이다.
        /// 그래서 같은 치즈 계열이되 묵은 색으로 낮춰 잡았다(#C0862F).
        /// </summary>
        private static readonly Color WALL_COLOR = new Color(0.753f, 0.525f, 0.184f);

        private const float WALL_SMOOTHNESS = 0.12f;

        [MenuItem("Tools/CTC/Build Fortress Mesh")]
        public static void Build()
        {
            Vector4[] holes = MakeHoles();

            var min = new Vector3(-HALF - GRID_PADDING, -GRID_PADDING, -HALF - GRID_PADDING);
            var max = new Vector3(HALF + GRID_PADDING, WALL_HEIGHT + GRID_PADDING, HALF + GRID_PADDING);

            Mesh built = SdfMesher.Build("Mesh_CheeseFortress", _p => SampleField(_p, holes), min, max,
                CELL_SIZE, RELAX_ITERATIONS, RELAX_STRENGTH, NORMAL_SMOOTH_ITERATIONS);

            Mesh mesh = SdfMesher.SaveMesh(built, PropAssets.FORTRESS_MESH);
            SdfMesher.SaveMaterial(PropAssets.FORTRESS_MATERIAL, "M_CheeseWall", WALL_COLOR, WALL_SMOOTHNESS);
            SdfMesher.Commit(PropAssets.FORTRESS_MESH);

            Debug.Log($"[FortressMeshBuilder] 완료 → {PropAssets.FORTRESS_MESH} " +
                      $"(구멍 {holes.Length}개 / 정점 {mesh.vertexCount} / 삼각형 {mesh.triangles.Length / 3})");
        }

        /// <summary>
        /// 요새 거리장. 음수가 안쪽이다.
        /// 바깥 상자 → 안쪽을 파내 벽만 남김(위가 뚫려 지붕이 없다) → 앞뒤 문 → 치즈 구멍.
        /// </summary>
        private static float SampleField(Vector3 _p, Vector4[] _holes)
        {
            // 바깥 껍데기. 바닥(y=0)에서 시작해 벽 높이까지.
            float d = SdfMesher.RoundBox(_p, new Vector3(0f, WALL_HEIGHT * 0.5f, 0f),
                new Vector3(HALF, WALL_HEIGHT * 0.5f, HALF), OUTER_ROUND);

            // 안마당을 판다. 위아래로 넉넉히 넘겨야 지붕과 바닥이 남지 않는다.
            float inner = SdfMesher.Box(_p, new Vector3(0f, WALL_HEIGHT, 0f),
                new Vector3(HALF - WALL_THICKNESS, WALL_HEIGHT, HALF - WALL_THICKNESS));
            d = SdfMesher.SmoothSubtract(d, inner, INNER_FILLET);

            // 쥐 전용 문 — 앞뒤 양면(한쪽 문 캠핑을 반대편 탈출로 카운터). 아래로 넘겨 파야
            // 문턱이 남지 않는다.
            float doorHalfY = (DOOR_HEIGHT + WALL_THICKNESS) * 0.5f;
            float doorCenterY = DOOR_HEIGHT - doorHalfY;
            float doorZ = HALF - WALL_THICKNESS * 0.5f;
            var doorHalf = new Vector3(DOOR_WIDTH * 0.5f, doorHalfY, WALL_THICKNESS);

            float front = SdfMesher.Box(_p, new Vector3(0f, doorCenterY, doorZ), doorHalf);
            float back = SdfMesher.Box(_p, new Vector3(0f, doorCenterY, -doorZ), doorHalf);
            d = SdfMesher.SmoothSubtract(d, front, DOOR_FILLET);
            d = SdfMesher.SmoothSubtract(d, back, DOOR_FILLET);

            for (int i = 0; i < _holes.Length; i++)
            {
                float dx = _p.x - _holes[i].x;
                float dy = _p.y - _holes[i].y;
                float dz = _p.z - _holes[i].z;
                float sphere = Mathf.Sqrt(dx * dx + dy * dy + dz * dz) - _holes[i].w;
                if (-sphere > d) d = -sphere;   // 차집합 — 구가 지나간 자리를 판다
            }
            return d;
        }

        // ── 구멍 배치 ───────────────────────────────────────────────────

        /// <summary>
        /// 벽면마다 구멍을 흩는다. xyz = 빼낼 구의 중심, w = 반지름.
        /// 중심을 표면 **바깥으로** (r - 깊이)만큼 띄워 얕게 판다 — 깊이가 벽 두께를 넘으면 뚫린다.
        /// </summary>
        private static Vector4[] MakeHoles()
        {
            var rng = new System.Random(HOLE_SEED);
            var holes = new List<Vector4>();

            float inner = HALF - WALL_THICKNESS;

            // 바깥 네 면 — 문이 있는 ±Z 면은 문 자리를 피한다
            ScatterOnFace(holes, rng, _alongX: false, _plane: HALF, _sign: 1f,
                _alongMax: HALF, _hasDoor: false, _count: HOLES_PER_OUTER_FACE);
            ScatterOnFace(holes, rng, _alongX: false, _plane: -HALF, _sign: -1f,
                _alongMax: HALF, _hasDoor: false, _count: HOLES_PER_OUTER_FACE);
            ScatterOnFace(holes, rng, _alongX: true, _plane: HALF, _sign: 1f,
                _alongMax: HALF, _hasDoor: true, _count: HOLES_PER_OUTER_FACE);
            ScatterOnFace(holes, rng, _alongX: true, _plane: -HALF, _sign: -1f,
                _alongMax: HALF, _hasDoor: true, _count: HOLES_PER_OUTER_FACE);

            // 안쪽 네 면 — 쥐가 안에서 보는 벽이다. 바깥보다 적게 판다(벽이 얇아 보이면 안 된다)
            ScatterOnFace(holes, rng, _alongX: false, _plane: inner, _sign: -1f,
                _alongMax: inner, _hasDoor: false, _count: HOLES_PER_INNER_FACE);
            ScatterOnFace(holes, rng, _alongX: false, _plane: -inner, _sign: 1f,
                _alongMax: inner, _hasDoor: false, _count: HOLES_PER_INNER_FACE);
            ScatterOnFace(holes, rng, _alongX: true, _plane: inner, _sign: -1f,
                _alongMax: inner, _hasDoor: true, _count: HOLES_PER_INNER_FACE);
            ScatterOnFace(holes, rng, _alongX: true, _plane: -inner, _sign: 1f,
                _alongMax: inner, _hasDoor: true, _count: HOLES_PER_INNER_FACE);

            return holes.ToArray();
        }

        /// <summary>
        /// 축에 정렬된 벽면 하나에 구멍을 흩는다.
        /// <paramref name="_alongX"/>가 참이면 면의 법선이 Z축(즉 문이 있는 앞뒤 벽)이다.
        /// <paramref name="_sign"/>은 면이 향하는 바깥 방향이다 — 구를 이쪽으로 띄운다.
        /// </summary>
        private static void ScatterOnFace(List<Vector4> _holes, System.Random _rng,
            bool _alongX, float _plane, float _sign, float _alongMax, bool _hasDoor, int _count)
        {
            var placed = new List<Vector3>();   // x = 가로 위치, y = 높이, z = 자국 반지름

            int tries = 0;
            while (placed.Count < _count && tries++ < HOLE_TRY_LIMIT)
            {
                float radius = Mathf.Lerp(HOLE_RADIUS_MIN, HOLE_RADIUS_MAX, (float)_rng.NextDouble());
                float rim = RimRadius(radius);
                float margin = rim + HOLE_EDGE_MARGIN;

                float alongLimit = _alongMax - margin;
                float highLimit = WALL_HEIGHT - margin;
                if (alongLimit <= 0f || highLimit <= margin) continue;

                float along = Mathf.Lerp(-alongLimit, alongLimit, (float)_rng.NextDouble());
                float high = Mathf.Lerp(margin, highLimit, (float)_rng.NextDouble());

                // 문 자리에는 벽이 없다 — 파면 허공에 자국이 남거나 문틀이 이빠진다
                if (_hasDoor && Mathf.Abs(along) < DOOR_WIDTH * 0.5f + margin && high < DOOR_HEIGHT + margin)
                {
                    continue;
                }

                bool clash = false;
                for (int i = 0; i < placed.Count; i++)
                {
                    float da = along - placed[i].x, dh = high - placed[i].y;
                    if (Mathf.Sqrt(da * da + dh * dh) < rim + placed[i].z + HOLE_GAP) { clash = true; break; }
                }
                if (clash) continue;

                placed.Add(new Vector3(along, high, rim));

                float offset = _plane + _sign * (radius - radius * HOLE_DEPTH_RATIO);
                _holes.Add(_alongX
                    ? new Vector4(along, high, offset, radius)
                    : new Vector4(offset, high, along, radius));
            }
        }

        /// <summary>구 중심을 표면 밖에 두면 남는 자국의 반지름 — 구 반지름에 비례한다.</summary>
        private static float RimRadius(float _radius)
        {
            float depth = _radius * HOLE_DEPTH_RATIO;
            return Mathf.Sqrt(Mathf.Max(2f * _radius * depth - depth * depth, 0f));
        }
    }
}
