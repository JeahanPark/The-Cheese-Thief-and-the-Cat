using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CTC.Editor
{
    /// <summary>
    /// 만화식 치즈 쐐기를 코드로 굽는다 — Tools > CTC > Build Cheese Mesh.
    ///
    /// 왜 거리장(SDF) + 서피스 넷인가.
    /// 이 톤이 요구하는 건 둘이다 — (1) 모서리가 둥글게 깎인 통통한 쐐기,
    /// (2) 표면이 **안으로 파인** 구멍. 면을 손으로 짜고 표면에 구를 얹는 방식은
    /// 구멍이 아니라 혹으로 보였다(실측). 거리장에서 쐐기를 만들고 구를 **빼면**
    /// 둥근 모서리와 오목한 구멍이 둘 다 형상 자체로 나온다.
    /// 등치면 추출은 조회표가 없는 naive surface nets라 코드가 짧고 결과가 부드럽다.
    ///
    /// 다시 실행하면 같은 에셋에 덮어쓴다(멱등) — GUID를 유지하므로 프리팹 참조가 안 끊긴다.
    /// </summary>
    public static class CheeseMeshBuilder
    {
        private const string MODEL_FOLDER = "Assets/Models/Cheese";
        private const string MESH_PATH = MODEL_FOLDER + "/Mesh_CheeseWedge.asset";
        private const string MATERIAL_FOLDER = "Assets/Materials/Props";
        private const string MATERIAL_PATH = MATERIAL_FOLDER + "/M_Cheese.mat";
        private const string PREFAB_FOLDER = "Assets/Prefabs/Props";
        private const string PREFAB_PATH = PREFAB_FOLDER + "/CheeseWedge.prefab";

        // ── 쐐기 모양 ────────────────────────────────────────────────────
        // 삼각 단면은 XY 평면(카메라를 보는 넓은 면), 두께는 Z로 뽑는다.
        // 아래 좌표는 **둥글리기 전의 심(core)** 이다 — 실제 외곽선은 CORNER_ROUND만큼 더 나간다.

        private const float HALF_DEPTH = 0.20f;

        /// <summary>모서리 둥글림. 각진 삼각기둥은 이 게임 톤이 아니다 — 0이면 칼같이 각진다.</summary>
        private const float CORNER_ROUND = 0.085f;

        /// <summary>구운 뒤 X 길이를 이 값에 맞춰 균등 배율한다(다른 축은 비율 유지).</summary>
        private const float TARGET_LENGTH = 1f;

        // ── 구멍 ────────────────────────────────────────────────────────
        private const int HOLES_PER_BROAD_FACE = 5;
        /// <summary>
        /// 빗면 구멍은 0이다. 넓은 면 구멍과 붙어 빗면을 따라 긴 홈으로 이어졌고,
        /// 구멍이 아니라 **깨진 자국**으로 보였다(실측). 참고 그림도 구멍은 넓은 면에만 있다.
        /// </summary>
        private const int HOLES_ON_SLOPE = 0;
        private const float HOLE_RADIUS_MIN = 0.055f;
        private const float HOLE_RADIUS_MAX = 0.080f;

        /// <summary>반지름 대비 파는 깊이. 1이면 반구, 참고 그림의 구멍은 얕고 가장자리가 부드럽다.</summary>
        private const float HOLE_DEPTH_RATIO = 0.65f;

        /// <summary>구멍 자국을 외곽선에서 이만큼 떨어뜨린다 — 실루엣이 통째로 이빠지지 않게.</summary>
        private const float HOLE_EDGE_MARGIN = 0.035f;

        private const float HOLE_GAP = 0.022f;
        private const int HOLE_SEED = 20260830;
        private const int HOLE_TRY_LIMIT = 2000;

        // ── 격자 ────────────────────────────────────────────────────────
        /// <summary>격자 칸 크기. 구멍 깊이(≈0.04)가 세 칸은 되어야 파인 게 눈에 읽힌다 — 구멍을 줄이면 여기도 같이 줄인다.</summary>
        private const float CELL_SIZE = 0.015f;

        private const float GRID_PADDING = 0.05f;

        /// <summary>계단 자국을 펴는 횟수. 늘리면 매끈해지고 대신 뾰족한 끝이 조금씩 무뎌진다.</summary>
        private const int RELAX_ITERATIONS = 8;

        private const float RELAX_STRENGTH = 0.55f;

        /// <summary>
        /// 법선을 이웃끼리 몇 번 섞을지. 둥근 모서리가 격자 몇 칸밖에 안 되면 평평한 면과 만나는 자리에서
        /// 밝기가 급히 꺾이고, 그 경계가 격자를 따라가며 **톱니**로 보인다(실측). 섞으면 그 경계가 풀린다.
        /// </summary>
        private const int NORMAL_SMOOTH_ITERATIONS = 3;

        private const float CHEESE_METALLIC = 0f;

        /// <summary>참고 그림의 치즈는 하이라이트가 거의 없는 무광 점토다 — 높이면 플라스틱이 된다.</summary>
        private const float CHEESE_SMOOTHNESS = 0.14f;

        private static readonly Vector2 TRI_A = new Vector2(-0.37f, 0.06f);   // 왼쪽 아래
        private static readonly Vector2 TRI_B = new Vector2(0.37f, 0.06f);    // 오른쪽 뾰족한 끝
        private static readonly Vector2 TRI_C = new Vector2(-0.37f, 0.52f);   // 왼쪽 위

        /// <summary>
        /// 로비 배경(T_LobbyBackdrop.png)의 치즈에서 뽑되 **램프 바로 아래 밝은 자리가 아니라 중간 밝기**를 기준으로 잡았다.
        /// #EFC24A. 인트로 스포트라이트가 세서(intensity 320) 재질은 화면에서 이보다 밝게 나온다 — 그만큼 낮춰 둔 값이다.
        /// </summary>
        private static readonly Color CHEESE_COLOR = new Color(0.937f, 0.761f, 0.290f);

        // 셀의 여덟 꼭짓점을 잇는 열두 변. 비트: 1=x, 2=y, 4=z.
        private static readonly int[] EDGE_A = { 0, 1, 2, 0, 4, 5, 6, 4, 0, 1, 3, 2 };
        private static readonly int[] EDGE_B = { 1, 3, 3, 2, 5, 7, 7, 6, 4, 5, 7, 6 };

        [MenuItem("Tools/CTC/Build Cheese Mesh")]
        public static void Build()
        {
            EnsureFolder("Assets", "Models");
            EnsureFolder("Assets/Models", "Cheese");
            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets/Materials", "Props");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Props");

            Vector4[] holes = MakeHoles();
            Mesh mesh = SaveMesh(BuildWedgeMesh(holes));
            Material material = SaveMaterial();
            SavePrefab(mesh, material);

            AssetDatabase.SaveAssets();
            // 내용만 갈아 끼우면 디스크는 새 것인데 메모리·GPU에는 옛 메시가 남는다(실측 —
            // 정점 수가 바뀌었는데 렌더 결과가 바이트까지 같았다). 강제로 다시 들여온다.
            AssetDatabase.ImportAsset(MESH_PATH, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            Debug.Log($"[CheeseMeshBuilder] 완료 → {PREFAB_PATH} " +
                      $"(구멍 {holes.Length}개 / 정점 {mesh.vertexCount} / 삼각형 {mesh.triangles.Length / 3})");
        }

        // ── 거리장 ──────────────────────────────────────────────────────

        /// <summary>
        /// 쐐기에서 구멍을 뺀 거리장. 음수가 안쪽이다.
        /// 삼각 단면을 Z로 뽑고 모서리를 둥글린 뒤, 구를 빼서 오목한 딤플을 만든다.
        /// </summary>
        private static float SampleField(Vector3 _p, Vector4[] _holes)
        {
            float tri = SdTriangle(new Vector2(_p.x, _p.y));
            float slab = Mathf.Abs(_p.z) - HALF_DEPTH;

            // 단면 거리와 두께 거리를 합쳐 기둥으로 — 바깥은 유클리드, 안쪽은 최대값.
            float ox = Mathf.Max(tri, 0f);
            float oz = Mathf.Max(slab, 0f);
            float d = Mathf.Min(Mathf.Max(tri, slab), 0f) + Mathf.Sqrt(ox * ox + oz * oz);
            d -= CORNER_ROUND;

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

        /// <summary>삼각형까지의 부호 있는 거리(2D). 변 셋을 각각 재고 감김 방향으로 안팎을 가른다.</summary>
        private static float SdTriangle(Vector2 _p)
        {
            Vector2 e0 = TRI_B - TRI_A, e1 = TRI_C - TRI_B, e2 = TRI_A - TRI_C;
            Vector2 v0 = _p - TRI_A, v1 = _p - TRI_B, v2 = _p - TRI_C;

            Vector2 q0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 q1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 q2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));

            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);

            float d = Mathf.Min(Mathf.Min(q0.sqrMagnitude, q1.sqrMagnitude), q2.sqrMagnitude);
            float w = Mathf.Min(Mathf.Min(s * (v0.x * e0.y - v0.y * e0.x),
                                          s * (v1.x * e1.y - v1.y * e1.x)),
                                          s * (v2.x * e2.y - v2.y * e2.x));
            return -Mathf.Sqrt(d) * Mathf.Sign(w);
        }

        private static Vector3 FieldNormal(Vector3 _p, Vector4[] _holes)
        {
            const float H = 0.0025f;
            float x = SampleField(new Vector3(_p.x + H, _p.y, _p.z), _holes)
                    - SampleField(new Vector3(_p.x - H, _p.y, _p.z), _holes);
            float y = SampleField(new Vector3(_p.x, _p.y + H, _p.z), _holes)
                    - SampleField(new Vector3(_p.x, _p.y - H, _p.z), _holes);
            float z = SampleField(new Vector3(_p.x, _p.y, _p.z + H), _holes)
                    - SampleField(new Vector3(_p.x, _p.y, _p.z - H), _holes);

            var n = new Vector3(x, y, z);
            return n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
        }

        // ── 구멍 배치 ───────────────────────────────────────────────────

        /// <summary>
        /// 구멍은 넓은 삼각면 양쪽과 빗면에만 판다. 바닥·뒷면은 카메라에 안 보여 정점만 먹는다.
        /// xyz = 빼낼 구의 중심, w = 반지름. 중심을 표면 **바깥으로** (r - 깊이)만큼 띄워 얕게 판다.
        /// </summary>
        private static Vector4[] MakeHoles()
        {
            var rng = new System.Random(HOLE_SEED);
            var holes = new List<Vector4>();

            AddBroadFaceHoles(holes, rng, 1f);
            AddBroadFaceHoles(holes, rng, -1f);
            AddSlopeHoles(holes, rng);
            return holes.ToArray();
        }

        /// <summary>구 중심을 표면 밖에 두면 남는 자국의 반지름 — 구 반지름에 비례한다.</summary>
        private static float RimRadius(float _radius)
        {
            float depth = _radius * HOLE_DEPTH_RATIO;
            return Mathf.Sqrt(Mathf.Max(2f * _radius * depth - depth * depth, 0f));
        }

        private static void AddBroadFaceHoles(List<Vector4> _holes, System.Random _rng, float _sign)
        {
            float faceZ = HALF_DEPTH + CORNER_ROUND;
            var placed = new List<Vector3>();   // xy = 중심, z = 자국 반지름

            int tries = 0;
            while (placed.Count < HOLES_PER_BROAD_FACE && tries++ < HOLE_TRY_LIMIT)
            {
                float radius = Mathf.Lerp(HOLE_RADIUS_MIN, HOLE_RADIUS_MAX, (float)_rng.NextDouble());
                float rim = RimRadius(radius);

                float x = Mathf.Lerp(TRI_A.x, TRI_B.x, (float)_rng.NextDouble());
                float y = Mathf.Lerp(TRI_A.y, TRI_C.y, (float)_rng.NextDouble());

                // 외곽선(심 삼각형 + 둥글림)을 자국이 넘으면 실루엣이 이빠진다.
                if (SdTriangle(new Vector2(x, y)) > CORNER_ROUND - rim - HOLE_EDGE_MARGIN) continue;

                bool clash = false;
                for (int i = 0; i < placed.Count; i++)
                {
                    float dx = x - placed[i].x, dy = y - placed[i].y;
                    if (Mathf.Sqrt(dx * dx + dy * dy) < rim + placed[i].z + HOLE_GAP) { clash = true; break; }
                }
                if (clash) continue;

                placed.Add(new Vector3(x, y, rim));
                _holes.Add(new Vector4(x, y, _sign * (faceZ + radius - radius * HOLE_DEPTH_RATIO), radius));
            }
        }

        private static void AddSlopeHoles(List<Vector4> _holes, System.Random _rng)
        {
            Vector2 along = TRI_C - TRI_B;
            float length = along.magnitude;
            Vector2 dir = along / length;
            Vector2 normal = new Vector2(dir.y, -dir.x);            // 빗면 바깥쪽
            if (Vector2.Dot(normal, TRI_B - TRI_A) < 0f) normal = -normal;

            var placed = new List<Vector3>();   // x = 빗면 위 거리, y = z좌표, z = 자국 반지름

            int tries = 0;
            while (placed.Count < HOLES_ON_SLOPE && tries++ < HOLE_TRY_LIMIT)
            {
                float radius = Mathf.Lerp(HOLE_RADIUS_MIN, HOLE_RADIUS_MAX, (float)_rng.NextDouble());
                float rim = RimRadius(radius);
                float half = HALF_DEPTH - rim;
                if (half <= 0f) continue;

                float t = Mathf.Lerp(rim + HOLE_EDGE_MARGIN, length - rim - HOLE_EDGE_MARGIN, (float)_rng.NextDouble());
                float z = Mathf.Lerp(-half, half, (float)_rng.NextDouble());

                bool clash = false;
                for (int i = 0; i < placed.Count; i++)
                {
                    float dt = t - placed[i].x, dz = z - placed[i].y;
                    if (Mathf.Sqrt(dt * dt + dz * dz) < rim + placed[i].z + HOLE_GAP) { clash = true; break; }
                }
                if (clash) continue;

                placed.Add(new Vector3(t, z, rim));
                Vector2 onEdge = TRI_B + dir * t + normal * (CORNER_ROUND + radius - radius * HOLE_DEPTH_RATIO);
                _holes.Add(new Vector4(onEdge.x, onEdge.y, z, radius));
            }
        }

        // ── 등치면 추출 (naive surface nets) ────────────────────────────

        private static Mesh BuildWedgeMesh(Vector4[] _holes)
        {
            float reach = CORNER_ROUND + GRID_PADDING;
            var min = new Vector3(Mathf.Min(TRI_A.x, TRI_B.x) - reach,
                                  Mathf.Min(TRI_A.y, TRI_C.y) - reach,
                                  -(HALF_DEPTH + reach));
            var max = new Vector3(Mathf.Max(TRI_A.x, TRI_B.x) + reach,
                                  Mathf.Max(TRI_A.y, TRI_C.y) + reach,
                                  HALF_DEPTH + reach);

            int nx = Mathf.CeilToInt((max.x - min.x) / CELL_SIZE);
            int ny = Mathf.CeilToInt((max.y - min.y) / CELL_SIZE);
            int nz = Mathf.CeilToInt((max.z - min.z) / CELL_SIZE);

            int sx = nx + 1, sy = ny + 1, sz = nz + 1;
            var field = new float[sx * sy * sz];
            for (int z = 0; z < sz; z++)
            {
                for (int y = 0; y < sy; y++)
                {
                    for (int x = 0; x < sx; x++)
                    {
                        var p = new Vector3(min.x + x * CELL_SIZE, min.y + y * CELL_SIZE, min.z + z * CELL_SIZE);
                        field[(z * sy + y) * sx + x] = SampleField(p, _holes);
                    }
                }
            }

            var cellVertex = new int[nx * ny * nz];
            for (int i = 0; i < cellVertex.Length; i++) cellVertex[i] = -1;

            var vertices = new List<Vector3>();

            // 셀 안에서 부호가 바뀌는 변들의 교차점을 평균해 정점 하나를 놓는다.
            for (int z = 0; z < nz; z++)
            {
                for (int y = 0; y < ny; y++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        Vector3 sum = Vector3.zero;
                        int crossings = 0;

                        for (int e = 0; e < 12; e++)
                        {
                            int a = EDGE_A[e], b = EDGE_B[e];
                            int ax = x + (a & 1), ay = y + ((a >> 1) & 1), az = z + ((a >> 2) & 1);
                            int bx = x + (b & 1), by = y + ((b >> 1) & 1), bz = z + ((b >> 2) & 1);

                            float fa = field[(az * sy + ay) * sx + ax];
                            float fb = field[(bz * sy + by) * sx + bx];
                            if ((fa < 0f) == (fb < 0f)) continue;

                            float t = fa / (fa - fb);
                            sum += new Vector3(
                                min.x + (ax + (bx - ax) * t) * CELL_SIZE,
                                min.y + (ay + (by - ay) * t) * CELL_SIZE,
                                min.z + (az + (bz - az) * t) * CELL_SIZE);
                            crossings++;
                        }

                        if (crossings == 0) continue;
                        cellVertex[(z * ny + y) * nx + x] = vertices.Count;
                        vertices.Add(sum / crossings);
                    }
                }
            }

            var triangles = new List<int>();
            for (int z = 0; z < nz; z++)
            {
                for (int y = 0; y < ny; y++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        bool inside = field[(z * sy + y) * sx + x] < 0f;

                        if (y > 0 && z > 0 && inside != (field[(z * sy + y) * sx + x + 1] < 0f))
                        {
                            AddQuad(triangles, cellVertex, nx, ny,
                                x, y - 1, z - 1, x, y - 1, z, x, y, z, x, y, z - 1);
                        }
                        if (x > 0 && z > 0 && inside != (field[(z * sy + y + 1) * sx + x] < 0f))
                        {
                            AddQuad(triangles, cellVertex, nx, ny,
                                x - 1, y, z - 1, x, y, z - 1, x, y, z, x - 1, y, z);
                        }
                        if (x > 0 && y > 0 && inside != (field[((z + 1) * sy + y) * sx + x] < 0f))
                        {
                            AddQuad(triangles, cellVertex, nx, ny,
                                x - 1, y - 1, z, x, y - 1, z, x, y, z, x - 1, y, z);
                        }
                    }
                }
            }

            var verts = vertices.ToArray();
            Relax(verts, triangles, _holes);

            var normals = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++) normals[i] = FieldNormal(verts[i], _holes);
            SmoothNormals(normals, triangles);

            // 감김 방향은 축마다 뒤집힐 수 있다. 거리장 기울기가 곧 바깥이므로 그걸 정답 삼아
            // 삼각형마다 맞춘다 — 추측하지 않고 매번 검사한다.
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 a = verts[triangles[i]], b = verts[triangles[i + 1]], c = verts[triangles[i + 2]];
                Vector3 face = Vector3.Cross(b - a, c - a);
                Vector3 outward = normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]];
                if (Vector3.Dot(face, outward) < 0f)
                {
                    int swap = triangles[i + 1];
                    triangles[i + 1] = triangles[i + 2];
                    triangles[i + 2] = swap;
                }
            }

            Normalize(verts);

            // **박스 투영(미터 단위) — `SdfMesher.PlanarUvs`와 같은 규약이다.**
            // 전에는 길이로 나눈 0~1이었는데, 쐐기와 창고가 `M_Cheese` 한 장을 나눠 쓰면서
            // 문제가 됐다: 같은 머티리얼인데 한쪽은 0~1이고 한쪽은 미터라 타일 배율을
            // 어느 한쪽에 맞추면 반대쪽이 깨진다(2026-08-30 실측 — 쐐기만 단색으로 보였다).
            // 규약을 미터로 통일해 두면 배율 하나로 둘 다 맞는다.
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                Vector3 n = normals[i];
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);

                if (ax >= ay && ax >= az) uvs[i] = new Vector2(v.z, v.y);
                else if (ay >= az) uvs[i] = new Vector2(v.x, v.z);
                else uvs[i] = new Vector2(v.x, v.y);
            }

            var mesh = new Mesh { name = "Mesh_CheeseWedge", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>
        /// 서피스 넷은 격자에 붙은 정점을 그대로 쓰기 때문에 비스듬한 면과 구멍 테두리에 **계단 자국**이 남는다
        /// (실측: 빗면과 구멍 가장자리가 톱니로 보였다). 이웃 쪽으로 살짝 당겨 접선 방향으로 펴고,
        /// 매번 거리장 위로 되돌려 놓는다 — 그래야 부드러워지면서 형태가 뭉개지지 않는다.
        /// </summary>
        private static void Relax(Vector3[] _verts, List<int> _triangles, Vector4[] _holes)
        {
            var neighborSum = new Vector3[_verts.Length];
            var neighborCount = new int[_verts.Length];

            for (int pass = 0; pass < RELAX_ITERATIONS; pass++)
            {
                for (int i = 0; i < _verts.Length; i++)
                {
                    neighborSum[i] = Vector3.zero;
                    neighborCount[i] = 0;
                }

                for (int i = 0; i < _triangles.Count; i += 3)
                {
                    Accumulate(neighborSum, neighborCount, _verts, _triangles[i], _triangles[i + 1]);
                    Accumulate(neighborSum, neighborCount, _verts, _triangles[i + 1], _triangles[i + 2]);
                    Accumulate(neighborSum, neighborCount, _verts, _triangles[i + 2], _triangles[i]);
                }

                for (int i = 0; i < _verts.Length; i++)
                {
                    if (neighborCount[i] == 0) continue;

                    Vector3 p = Vector3.Lerp(_verts[i], neighborSum[i] / neighborCount[i], RELAX_STRENGTH);

                    // 표면으로 되돌리기 — 거리장 값만큼 기울기 반대로 두 번 민다.
                    for (int step = 0; step < 2; step++)
                    {
                        p -= FieldNormal(p, _holes) * SampleField(p, _holes);
                    }
                    _verts[i] = p;
                }
            }
        }

        private static void SmoothNormals(Vector3[] _normals, List<int> _triangles)
        {
            var sum = new Vector3[_normals.Length];

            for (int pass = 0; pass < NORMAL_SMOOTH_ITERATIONS; pass++)
            {
                for (int i = 0; i < sum.Length; i++) sum[i] = _normals[i];

                for (int i = 0; i < _triangles.Count; i += 3)
                {
                    AccumulateNormal(sum, _normals, _triangles[i], _triangles[i + 1]);
                    AccumulateNormal(sum, _normals, _triangles[i + 1], _triangles[i + 2]);
                    AccumulateNormal(sum, _normals, _triangles[i + 2], _triangles[i]);
                }

                for (int i = 0; i < _normals.Length; i++)
                {
                    if (sum[i].sqrMagnitude > 1e-12f) _normals[i] = sum[i].normalized;
                }
            }
        }

        private static void AccumulateNormal(Vector3[] _sum, Vector3[] _normals, int _a, int _b)
        {
            _sum[_a] += _normals[_b];
            _sum[_b] += _normals[_a];
        }

        private static void Accumulate(Vector3[] _sum, int[] _count, Vector3[] _verts, int _a, int _b)
        {
            _sum[_a] += _verts[_b];
            _count[_a]++;
            _sum[_b] += _verts[_a];
            _count[_b]++;
        }

        /// <summary>격자 변 하나를 둘러싼 셀 넷을 이어 사각형(삼각형 둘)으로 만든다.</summary>
        private static void AddQuad(List<int> _triangles, int[] _cellVertex, int _nx, int _ny,
            int _x0, int _y0, int _z0, int _x1, int _y1, int _z1,
            int _x2, int _y2, int _z2, int _x3, int _y3, int _z3)
        {
            int v0 = _cellVertex[(_z0 * _ny + _y0) * _nx + _x0];
            int v1 = _cellVertex[(_z1 * _ny + _y1) * _nx + _x1];
            int v2 = _cellVertex[(_z2 * _ny + _y2) * _nx + _x2];
            int v3 = _cellVertex[(_z3 * _ny + _y3) * _nx + _x3];
            if (v0 < 0 || v1 < 0 || v2 < 0 || v3 < 0) return;

            _triangles.Add(v0); _triangles.Add(v1); _triangles.Add(v2);
            _triangles.Add(v0); _triangles.Add(v2); _triangles.Add(v3);
        }

        /// <summary>X 길이를 TARGET_LENGTH에 맞추고, 바닥을 y=0에 붙이고, X·Z를 가운데로 옮긴다.</summary>
        private static void Normalize(Vector3[] _verts)
        {
            if (_verts.Length == 0) return;

            Vector3 lo = _verts[0], hi = _verts[0];
            for (int i = 1; i < _verts.Length; i++)
            {
                lo = Vector3.Min(lo, _verts[i]);
                hi = Vector3.Max(hi, _verts[i]);
            }

            float scale = hi.x - lo.x > 1e-5f ? TARGET_LENGTH / (hi.x - lo.x) : 1f;
            var pivot = new Vector3((lo.x + hi.x) * 0.5f, lo.y, (lo.z + hi.z) * 0.5f);
            for (int i = 0; i < _verts.Length; i++) _verts[i] = (_verts[i] - pivot) * scale;
        }

        // ── 에셋 저장 ───────────────────────────────────────────────────

        private static Mesh SaveMesh(Mesh _mesh)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MESH_PATH);
            if (existing != null)
            {
                // 새로 만들어 덮으면 GUID가 바뀌어 프리팹 참조가 끊긴다 — 내용만 갈아 끼운다.
                // 단 CopySerialized는 쓰지 않는다: 디스크는 새 것이 되는데 **그리는 데 쓰는 버퍼는 옛 것**이라
                // 정점 수가 바뀌어도 화면은 그대로였다(실측). Mesh API로 넣어야 버퍼까지 다시 올라간다.
                existing.Clear();
                existing.indexFormat = _mesh.indexFormat;
                existing.SetVertices(_mesh.vertices);
                existing.SetNormals(_mesh.normals);
                existing.SetUVs(0, _mesh.uv);
                existing.SetTriangles(_mesh.triangles, 0);
                existing.RecalculateBounds();
                existing.RecalculateTangents();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(_mesh);
                return existing;
            }

            AssetDatabase.CreateAsset(_mesh, MESH_PATH);
            return _mesh;
        }

        private static Material SaveMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader) { name = "M_Cheese" };
            mat.SetColor("_BaseColor", CHEESE_COLOR);
            mat.SetColor("_Color", CHEESE_COLOR);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", CHEESE_METALLIC);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", CHEESE_SMOOTHNESS);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", CHEESE_SMOOTHNESS);

            var existing = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mat, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mat);
                return existing;
            }

            AssetDatabase.CreateAsset(mat, MATERIAL_PATH);
            return mat;
        }

        /// <summary>
        /// 콜라이더는 붙이지 않는다 — 지금 쓰임은 장식이고, 이 삼각형 수의 MeshCollider는 낭비다.
        /// 물리가 필요해지면 박스를 손으로 얹는 편이 싸다.
        /// </summary>
        private static void SavePrefab(Mesh _mesh, Material _material)
        {
            var root = new GameObject("CheeseWedge");
            var filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;

            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string _parent, string _name)
        {
            if (!AssetDatabase.IsValidFolder(_parent + "/" + _name))
            {
                AssetDatabase.CreateFolder(_parent, _name);
            }
        }
    }
}
