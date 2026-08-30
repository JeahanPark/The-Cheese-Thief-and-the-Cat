using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CTC.Editor
{
    /// <summary>
    /// 거리장(SDF)에서 메시를 뽑는 공용 도구. <see cref="CheeseMeshBuilder"/>가 치즈 쐐기 하나로
    /// 검증한 방식(거리장 + naive surface nets + 이완)을 프롭 전반이 쓸 수 있게 떼어 놓은 것이다.
    ///
    /// 왜 거리장인가. 이 게임 톤이 요구하는 건 **모서리가 둥글게 깎인 통통한 덩어리**와
    /// **표면에 안으로 파인 자국**이다. 면을 손으로 짜고 표면에 구를 얹으면 파인 게 아니라
    /// 혹으로 보인다(치즈에서 실측). 거리장에서 빼면 둘 다 형상 자체로 나온다.
    /// CSG(합·차·교집합)가 한 줄이라 "벽에서 문을 도려낸다"가 그대로 코드가 된다.
    ///
    /// <see cref="CheeseMeshBuilder"/>는 일부러 손대지 않았다 — 이미 구워 둔 치즈 메시가
    /// 달라지면 안 되고, 컴파일 검증 없이 그걸 리팩터링할 이유가 없다.
    /// </summary>
    internal static class SdfMesher
    {
        // 셀의 여덟 꼭짓점을 잇는 열두 변. 비트: 1=x, 2=y, 4=z.
        private static readonly int[] EDGE_A = { 0, 1, 2, 0, 4, 5, 6, 4, 0, 1, 3, 2 };
        private static readonly int[] EDGE_B = { 1, 3, 3, 2, 5, 7, 7, 6, 4, 5, 7, 6 };

        /// <summary>거리장 기울기를 잴 때 쓰는 미소 변위. 셀 크기보다 훨씬 작아야 한다.</summary>
        private const float NORMAL_EPSILON = 0.0025f;

        // ── 거리장 원시형 ───────────────────────────────────────────────

        public static float Box(Vector3 _p, Vector3 _center, Vector3 _half)
        {
            Vector3 q = _p - _center;
            q = new Vector3(Mathf.Abs(q.x) - _half.x, Mathf.Abs(q.y) - _half.y, Mathf.Abs(q.z) - _half.z);
            Vector3 outside = Vector3.Max(q, Vector3.zero);
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
        }

        /// <summary>
        /// 모서리를 둥글린 박스. **바깥 치수는 _half 그대로다** — 안쪽으로 깎아 들어간 뒤
        /// 다시 부풀리기 때문이다. 콜라이더와 실루엣을 맞춰야 하는 구조물에 이게 중요하다.
        /// </summary>
        public static float RoundBox(Vector3 _p, Vector3 _center, Vector3 _half, float _radius)
        {
            Vector3 core = Vector3.Max(_half - Vector3.one * _radius, Vector3.zero);
            return Box(_p, _center, core) - _radius;
        }

        public static float Sphere(Vector3 _p, Vector3 _center, float _radius)
        {
            return (_p - _center).magnitude - _radius;
        }

        /// <summary>Y축 원기둥. 밑면·윗면이 평평하다.</summary>
        public static float CylinderY(Vector3 _p, Vector3 _center, float _radius, float _halfHeight)
        {
            Vector3 q = _p - _center;
            float radial = new Vector2(q.x, q.z).magnitude - _radius;
            float axial = Mathf.Abs(q.y) - _halfHeight;
            float ox = Mathf.Max(radial, 0f);
            float oy = Mathf.Max(axial, 0f);
            return Mathf.Min(Mathf.Max(radial, axial), 0f) + Mathf.Sqrt(ox * ox + oy * oy);
        }

        /// <summary>테두리를 둥글린 원기둥. RoundBox와 같은 이유로 바깥 치수는 그대로다.</summary>
        public static float RoundCylinderY(Vector3 _p, Vector3 _center, float _radius, float _halfHeight, float _round)
        {
            float r = Mathf.Max(_radius - _round, 0f);
            float h = Mathf.Max(_halfHeight - _round, 0f);
            return CylinderY(_p, _center, r, h) - _round;
        }

        /// <summary>
        /// XZ 평면에서 +X를 중심으로 반각 _halfAngleDeg 만큼 벌어진 무한 부채꼴.
        /// 치즈 바퀴에서 **한 조각을 떼어낸 자리**를 만드는 데 쓴다.
        /// </summary>
        public static float SectorX(Vector3 _p, float _halfAngleDeg)
        {
            float a = _halfAngleDeg * Mathf.Deg2Rad;
            float sin = Mathf.Sin(a);
            float cos = Mathf.Cos(a);
            float sideA = -(_p.x * sin - _p.z * cos);
            float sideB = -(_p.x * sin + _p.z * cos);
            return Mathf.Max(sideA, sideB);
        }

        public static float Union(float _a, float _b) => Mathf.Min(_a, _b);

        public static float Subtract(float _a, float _b) => Mathf.Max(_a, -_b);

        public static float Intersect(float _a, float _b) => Mathf.Max(_a, _b);

        /// <summary>
        /// 모서리에 필렛을 남기는 차집합. 각진 CSG는 이 게임 톤이 아니다 —
        /// 도려낸 자리 입구가 칼같이 서면 종이를 오린 것처럼 보인다.
        /// </summary>
        public static float SmoothSubtract(float _a, float _b, float _k)
        {
            if (_k <= 0f) return Subtract(_a, _b);
            float h = Mathf.Clamp01(0.5f - 0.5f * (_a + _b) / _k);
            return Mathf.Lerp(_a, -_b, h) + _k * h * (1f - h);
        }

        public static float SmoothUnion(float _a, float _b, float _k)
        {
            if (_k <= 0f) return Union(_a, _b);
            float h = Mathf.Clamp01(0.5f + 0.5f * (_b - _a) / _k);
            return Mathf.Lerp(_b, _a, h) - _k * h * (1f - h);
        }

        // ── 등치면 추출 (naive surface nets) ────────────────────────────

        /// <summary>
        /// 거리장에서 메시를 뽑는다. 조회표가 없는 naive surface nets라 코드가 짧고 결과가 부드럽다.
        /// _min·_max는 **형상을 완전히 감싸야 한다** — 격자 밖으로 나가면 그쪽 면이 뚫린 채 남는다.
        /// </summary>
        public static Mesh Build(string _name, Func<Vector3, float> _field, Vector3 _min, Vector3 _max,
            float _cell, int _relaxIterations, float _relaxStrength, int _normalSmoothIterations)
        {
            int nx = Mathf.CeilToInt((_max.x - _min.x) / _cell);
            int ny = Mathf.CeilToInt((_max.y - _min.y) / _cell);
            int nz = Mathf.CeilToInt((_max.z - _min.z) / _cell);

            int sx = nx + 1, sy = ny + 1, sz = nz + 1;
            var field = new float[sx * sy * sz];
            for (int z = 0; z < sz; z++)
            {
                for (int y = 0; y < sy; y++)
                {
                    for (int x = 0; x < sx; x++)
                    {
                        var p = new Vector3(_min.x + x * _cell, _min.y + y * _cell, _min.z + z * _cell);
                        field[(z * sy + y) * sx + x] = _field(p);
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
                                _min.x + (ax + (bx - ax) * t) * _cell,
                                _min.y + (ay + (by - ay) * t) * _cell,
                                _min.z + (az + (bz - az) * t) * _cell);
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
            Relax(verts, triangles, _field, _relaxIterations, _relaxStrength);

            var normals = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++) normals[i] = FieldNormal(verts[i], _field);
            SmoothNormals(normals, triangles, _normalSmoothIterations);

            // 감김 방향은 축마다 뒤집힐 수 있다. 거리장 기울기가 곧 바깥이므로 그걸 정답 삼아
            // 삼각형마다 맞춘다 — 추측하지 않고 매번 검사한다.
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 a = verts[triangles[i]], b = verts[triangles[i + 1]], c = verts[triangles[i + 2]];
                Vector3 face = Vector3.Cross(b - a, c - a);
                Vector3 outward = normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]];
                if (Vector3.Dot(face, outward) >= 0f) continue;

                int swap = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = swap;
            }

            var mesh = new Mesh { name = _name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, PlanarUvs(verts, normals));
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>
        /// 법선이 가장 많이 향하는 축을 빼고 남은 두 축을 그대로 UV로 쓴다(박스 투영).
        /// 지금 프롭 머티리얼은 전부 단색이라 UV가 화면에 안 보이지만, 나중에 텍스처를 얹을 때
        /// 최소한 늘어나 보이지는 않게 해 둔다.
        /// </summary>
        private static Vector2[] PlanarUvs(Vector3[] _verts, Vector3[] _normals)
        {
            var uvs = new Vector2[_verts.Length];
            for (int i = 0; i < _verts.Length; i++)
            {
                Vector3 v = _verts[i];
                Vector3 n = _normals[i];
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                if (ax >= ay && ax >= az) uvs[i] = new Vector2(v.z, v.y);
                else if (ay >= az) uvs[i] = new Vector2(v.x, v.z);
                else uvs[i] = new Vector2(v.x, v.y);
            }
            return uvs;
        }

        /// <summary>
        /// 서피스 넷은 격자에 붙은 정점을 그대로 쓰기 때문에 비스듬한 면과 파인 자국 테두리에
        /// **계단 자국**이 남는다. 이웃 쪽으로 살짝 당겨 접선 방향으로 펴고, 매번 거리장 위로
        /// 되돌려 놓는다 — 그래야 부드러워지면서 형태가 뭉개지지 않는다.
        /// </summary>
        private static void Relax(Vector3[] _verts, List<int> _triangles, Func<Vector3, float> _field,
            int _iterations, float _strength)
        {
            var neighborSum = new Vector3[_verts.Length];
            var neighborCount = new int[_verts.Length];

            for (int pass = 0; pass < _iterations; pass++)
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

                    Vector3 p = Vector3.Lerp(_verts[i], neighborSum[i] / neighborCount[i], _strength);

                    // 표면으로 되돌리기 — 거리장 값만큼 기울기 반대로 두 번 민다.
                    for (int step = 0; step < 2; step++)
                    {
                        p -= FieldNormal(p, _field) * _field(p);
                    }
                    _verts[i] = p;
                }
            }
        }

        /// <summary>
        /// 법선을 이웃끼리 섞는다. 둥근 모서리가 격자 몇 칸밖에 안 되면 평평한 면과 만나는 자리에서
        /// 밝기가 급히 꺾이고, 그 경계가 격자를 따라가며 **톱니**로 보인다(치즈에서 실측).
        /// </summary>
        private static void SmoothNormals(Vector3[] _normals, List<int> _triangles, int _iterations)
        {
            var sum = new Vector3[_normals.Length];

            for (int pass = 0; pass < _iterations; pass++)
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

        private static Vector3 FieldNormal(Vector3 _p, Func<Vector3, float> _field)
        {
            const float H = NORMAL_EPSILON;
            float x = _field(new Vector3(_p.x + H, _p.y, _p.z)) - _field(new Vector3(_p.x - H, _p.y, _p.z));
            float y = _field(new Vector3(_p.x, _p.y + H, _p.z)) - _field(new Vector3(_p.x, _p.y - H, _p.z));
            float z = _field(new Vector3(_p.x, _p.y, _p.z + H)) - _field(new Vector3(_p.x, _p.y, _p.z - H));

            var n = new Vector3(x, y, z);
            return n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
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

        // ── 에셋 저장 ───────────────────────────────────────────────────

        /// <summary>
        /// 같은 경로에 덮어쓴다(멱등). **새로 만들어 덮으면 GUID가 바뀌어 프리팹 참조가 끊긴다** —
        /// 내용만 갈아 끼운다. 단 EditorUtility.CopySerialized는 쓰지 않는다: 디스크는 새 것이
        /// 되는데 **그리는 데 쓰는 버퍼는 옛 것**이라 정점 수가 바뀌어도 화면이 그대로였다(실측).
        /// Mesh API로 넣어야 버퍼까지 다시 올라간다.
        /// </summary>
        public static Mesh SaveMesh(Mesh _mesh, string _path)
        {
            EnsureFolders(_path);

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(_path);
            if (existing != null)
            {
                existing.Clear();
                existing.indexFormat = _mesh.indexFormat;
                existing.SetVertices(_mesh.vertices);
                existing.SetNormals(_mesh.normals);
                existing.SetUVs(0, _mesh.uv);
                existing.SetTriangles(_mesh.triangles, 0);
                existing.RecalculateBounds();
                existing.RecalculateTangents();
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(_mesh);
                return existing;
            }

            AssetDatabase.CreateAsset(_mesh, _path);
            return _mesh;
        }

        /// <summary>URP Lit 단색 머티리얼. 이 톤은 무광 점토라 스무스니스를 낮게 둔다.</summary>
        public static Material SaveMaterial(string _path, string _name, Color _color, float _smoothness)
        {
            EnsureFolders(_path);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader) { name = _name };
            mat.SetColor("_BaseColor", _color);
            mat.SetColor("_Color", _color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", _smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", _smoothness);

            var existing = AssetDatabase.LoadAssetAtPath<Material>(_path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mat, existing);
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(mat);
                return existing;
            }

            AssetDatabase.CreateAsset(mat, _path);
            return mat;
        }

        /// <summary>에셋 경로가 가리키는 폴더를 없으면 만든다(중간 단계 포함).</summary>
        public static void EnsureFolders(string _assetPath)
        {
            string[] parts = _assetPath.Split('/');
            string current = parts[0];                      // "Assets"
            for (int i = 1; i < parts.Length - 1; i++)      // 마지막 칸은 파일 이름이다
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>
        /// 다시 들여오기까지 해야 끝이다. 내용만 갈아 끼우면 디스크는 새 것인데
        /// 메모리·GPU에는 옛 메시가 남는다(실측 — 정점 수가 바뀌었는데 렌더 결과가 바이트까지 같았다).
        /// </summary>
        public static void Commit(params string[] _meshPaths)
        {
            AssetDatabase.SaveAssets();
            foreach (string path in _meshPaths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.Refresh();
        }
    }
}
