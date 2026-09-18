using System.Collections.Generic;
using CTC.Runtime.Ai;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 감사 D-09 · D-10 · D-12 — **실제로 지은** 창고 맵(손 배치, 정식 기본 맵)에서 격자·폴리곤 굽기를 잰다.
    ///
    /// 맵은 `MapBuilder.Build`를 그대로 태워 짓는다 — 콜라이더 이름이 곧 분류 규칙이라(`NavGridBaker.IsBlockingName`)
    /// 이름을 흉내 낸 가짜 지오메트리로는 「이름 규약 밖의 물건이 섞였다」를 못 본다.
    /// 씬은 열지 않는다: 지금 열린 씬에 GameObject만 세우고 픽스처가 끝나면 지운다.
    ///
    /// 설계 메모(D-09): 격자는 **의도적으로 이름으로만** 막는 것을 고른다(바닥 `Floor`도 굵은 박스라
    /// 「트리거가 아닌 박스 = 막는다」로는 못 가른다). 그래서 D-09는 「이름 밖 콜라이더도 막아라」가 아니라
    /// **「몸 높이를 막는 콜라이더가 전부 이름 규약 안에 있고 격자에 찍혔다」**를 불변식으로 건다.
    /// </summary>
    [NUnit.Framework.Category("Asset")]
    public class WarehouseNavBakeTests
    {
        private const string WAREHOUSE = "Assets/Prefabs/Maps/Layout_Warehouse01.asset";
        private const string MODULE_CATALOG = "Assets/Prefabs/Maps/RoomModuleCatalog.asset";

        /// <summary>이 높이 이하 윗면은 딛고 넘는다 — `MapBuilder.AGENT_CLIMB`(0.4)와 같은 값.</summary>
        private const float STEP_HEIGHT = 0.4f;

        /// <summary>이 높이 이상에서 시작하는 물건은 머리 위라 걷기를 안 막는다 — `MapBuilder.AGENT_HEIGHT`(1.2)와 같은 값.</summary>
        private const float BODY_HEIGHT = 1.2f;

        /// <summary>폴리곤 굽기에서 「걸을 수 없음」 area — `MapBuilder.CollectSources`의 NOT_WALKABLE_AREA.</summary>
        private const int NOT_WALKABLE_AREA = 1;

        private GameObject m_BuilderGo;
        private MapBuilder m_Builder;
        private MapLayout m_Layout;

        [OneTimeSetUp]
        public void BuildWarehouse()
        {
            m_Layout = AssetDatabase.LoadAssetAtPath<MapLayout>(WAREHOUSE);
            Assert.IsNotNull(m_Layout, $"레이아웃 에셋이 없다: {WAREHOUSE}");
            Assert.IsTrue(m_Layout.HandPlaced, "창고 맵은 손 배치여야 한다 — 전제가 깨졌다");

            m_BuilderGo = new GameObject("__WarehouseNavBakeTests");
            m_Builder = m_BuilderGo.AddComponent<MapBuilder>();
            m_Builder.Build(m_Layout);
            Assert.IsTrue(m_Builder.BuiltRoot != null, "MapBuilder가 맵 루트를 안 만들었다");
        }

        [OneTimeTearDown]
        public void DestroyWarehouse()
        {
            if (m_Builder != null) m_Builder.Clear();
            if (m_BuilderGo != null) Object.DestroyImmediate(m_BuilderGo);
            NavGridService.Clear();
        }

        // ══════════════════════ D-09 · 격자가 막는 콜라이더를 놓치지 않는다 ══════════════════════

        [NUnit.Framework.Property("CaseId", "D-09")]
        [Test]
        public void Warehouse_EverySolidBodyHeightColliderIsStampedIntoTheGrid()
        {
            Transform root = m_Builder.BuiltRoot;
            NavGrid grid = NavGridBaker.Bake(root, m_Layout.HalfExtent);

            List<string> findings = AuditGridCoverage(root, grid, out int solidCount);
            Assert.Greater(solidCount, 50, "막는 콜라이더가 거의 없다 — 맵이 안 지어졌거나 검산이 헛돈다");
            Assert.IsEmpty(findings, $"격자가 놓친 막는 콜라이더 {findings.Count}건:\n" + string.Join("\n", findings));
        }

        [NUnit.Framework.Property("CaseId", "D-09")]
        [Test]
        public void RoomModulePrefabs_EverySolidColliderFollowsTheNamingRule()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RoomModuleCatalog>(MODULE_CATALOG);
            Assert.IsNotNull(catalog, $"모듈 카탈로그가 없다: {MODULE_CATALOG}");

            var prefabs = new List<GameObject>();
            foreach (RoomModuleCatalog.Entry entry in catalog.Entries)
            {
                if (entry.Prefab != null) prefabs.Add(entry.Prefab);
            }
            if (catalog.DoorFrame != null) prefabs.Add(catalog.DoorFrame);
            if (catalog.WallPlug != null) prefabs.Add(catalog.WallPlug);
            Assert.Greater(prefabs.Count, 0, "카탈로그에 프리팹이 하나도 없다 — 검산이 헛돈다");

            var findings = new List<string>();
            foreach (GameObject prefab in prefabs)
            {
                foreach (Collider col in prefab.GetComponentsInChildren<Collider>(true))
                {
                    if (col.isTrigger) continue;
                    string why = NamingViolation(col);
                    if (why != null) findings.Add($"{prefab.name}/{PathOf(col.transform, prefab.transform)} — {why}");
                }
            }
            Assert.IsEmpty(findings, $"이름 규약 밖의 막는 콜라이더 {findings.Count}건(격자가 못 본다):\n" + string.Join("\n", findings));
        }

        /// <summary>검산 자신이 도는지 — 규약 밖 이름(`Crate`)의 막는 박스를 세우면 정말 잡는가.</summary>
        [NUnit.Framework.Property("CaseId", "D-09")]
        [Test]
        public void GridCoverageAudit_CatchesABlockerOutsideTheNamingRule()
        {
            Transform root = m_Builder.BuiltRoot;
            GameObject crate = AddTestBox(root, "Crate", new Vector3(0f, 1f, -8f), new Vector3(2f, 2f, 2f));
            try
            {
                NavGrid grid = NavGridBaker.Bake(root, m_Layout.HalfExtent);
                List<string> findings = AuditGridCoverage(root, grid, out _);
                Assert.IsTrue(findings.Exists(_f => _f.Contains("Crate")),
                    "규약 밖 막는 박스를 세웠는데 검산이 못 잡았다:\n" + string.Join("\n", findings));
            }
            finally
            {
                Object.DestroyImmediate(crate);
            }
        }

        // ══════════════════════ D-10 · 폴리곤 굽기가 막는 물건을 걷는 면으로 넣지 않는다 ══════════════════════

        [NUnit.Framework.Property("CaseId", "D-10")]
        [Test]
        public void Warehouse_NoWalkableNavMeshSourceStandsAboveStepHeight()
        {
            List<NavMeshBuildSource> sources = MapBuilder.CollectSources(m_Builder.BuiltRoot);
            Assert.Greater(sources.Count, 50, "폴리곤 소스가 거의 없다 — 맵이 안 지어졌거나 수집이 헛돈다");

            List<string> findings = AuditWalkableSources(m_Builder.BuiltRoot);
            Assert.IsEmpty(findings, $"걷는 면으로 들어간 막는 물건 {findings.Count}건(윗면이 섬이 된다):\n" + string.Join("\n", findings));
        }

        /// <summary>현재 분류 규칙 — 트리거·덧바닥은 빠지고, 이름 규약의 물건은 걸을 수 없음, 전역 바닥은 걷는 면.</summary>
        [NUnit.Framework.Property("CaseId", "D-10")]
        [Test]
        public void CollectSources_ClassifiesByTriggerAndNamingRule()
        {
            var root = new GameObject("__CollectSourcesRules");
            try
            {
                AddTestBox(root.transform, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(10f, 0.5f, 10f));
                AddTestBox(root.transform, "Floor_Deco", new Vector3(0f, 0.02f, 0f), new Vector3(4f, 0.04f, 4f));
                AddTestBox(root.transform, "CorridorFloor_1", new Vector3(3f, 0.02f, 0f), new Vector3(2f, 0.04f, 4f));
                AddTestBox(root.transform, "Wall_Shelf_1", new Vector3(1f, 2.5f, 0f), new Vector3(1f, 5f, 4f));
                AddTestBox(root.transform, "CorridorWall_2", new Vector3(2f, 1.2f, 0f), new Vector3(1f, 2.4f, 4f));
                AddTestBox(root.transform, "Obstacle", new Vector3(-2f, 1.5f, 0f), Vector3.one * 2.4f);
                AddTestBox(root.transform, "Boundary_N", new Vector3(0f, 2.5f, 5f), new Vector3(10f, 5f, 0.5f));
                GameObject trigger = AddTestBox(root.transform, "Wall_Trigger", new Vector3(-4f, 1f, 0f), Vector3.one);
                trigger.GetComponent<BoxCollider>().isTrigger = true;

                List<NavMeshBuildSource> sources = MapBuilder.CollectSources(root.transform);
                Assert.AreEqual(5, sources.Count, "트리거·덧바닥(Floor_·CorridorFloor) 셋은 소스에서 빠진다");

                int walkable = 0, blocked = 0;
                foreach (NavMeshBuildSource s in sources)
                {
                    if (s.area == NOT_WALKABLE_AREA) blocked++;
                    else walkable++;
                }
                Assert.AreEqual(1, walkable, "걷는 면은 전역 바닥 하나다");
                Assert.AreEqual(4, blocked, "Wall·CorridorWall·Obstacle·Boundary는 걸을 수 없음");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>검산 자신이 도는지 — 규약 밖 이름의 키 큰 박스는 걷는 면(area 0)으로 들어가 잡혀야 한다.</summary>
        [NUnit.Framework.Property("CaseId", "D-10")]
        [Test]
        public void WalkableSourceAudit_CatchesATallBoxOutsideTheNamingRule()
        {
            Transform root = m_Builder.BuiltRoot;
            int baseline = AuditWalkableSources(root).Count;
            GameObject crate = AddTestBox(root, "Crate", new Vector3(0f, 1f, -8f), new Vector3(2f, 2f, 2f));
            try
            {
                List<string> findings = AuditWalkableSources(root);
                Assert.AreEqual(baseline + 1, findings.Count,
                    "규약 밖 키 큰 박스 하나를 세웠는데 검산 결과가 하나가 아니다:\n" + string.Join("\n", findings));
            }
            finally
            {
                Object.DestroyImmediate(crate);
            }
        }

        // ══════════════════════ D-12 · 손 배치 맵의 걷는 면이 한 덩어리다 ══════════════════════

        [NUnit.Framework.Property("CaseId", "D-12")]
        [Test]
        public void Warehouse_WalkableGridIsOneConnectedPiece()
        {
            // 봉인(SealUnreachable) **전** 격자를 본다 — 봉인 후엔 구조상 늘 한 덩어리라 아무것도 못 잡는다
            NavGrid grid = NavGridBaker.StampTerrain(m_Builder.BuiltRoot, m_Layout.HalfExtent);
            List<(int size, Vector3 at)> pieces = InteriorPieces(grid, m_Layout.HalfExtent);

            Assert.Greater(pieces.Count, 0, "걸을 수 있는 셀이 하나도 없다");
            Assert.AreEqual(1, pieces.Count,
                $"걷는 면이 {pieces.Count}조각이다 — 큰 조각 외엔 봉인돼 아무도 못 간다:\n" + DescribePieces(pieces));
        }

        /// <summary>검산 자신이 도는지 — 벽 네 장으로 빈 바닥을 두르면 두 조각이 된다.</summary>
        [NUnit.Framework.Property("CaseId", "D-12")]
        [Test]
        public void ConnectivityCheck_CatchesAnEnclosedPocket()
        {
            Transform root = m_Builder.BuiltRoot;
            var ring = new List<GameObject>
            {
                AddTestBox(root, "Wall_TestN", new Vector3(0f, 1f, -6f), new Vector3(4.5f, 2f, 0.5f)),
                AddTestBox(root, "Wall_TestS", new Vector3(0f, 1f, -10f), new Vector3(4.5f, 2f, 0.5f)),
                AddTestBox(root, "Wall_TestE", new Vector3(2f, 1f, -8f), new Vector3(0.5f, 2f, 4.5f)),
                AddTestBox(root, "Wall_TestW", new Vector3(-2f, 1f, -8f), new Vector3(0.5f, 2f, 4.5f)),
            };
            try
            {
                NavGrid grid = NavGridBaker.StampTerrain(root, m_Layout.HalfExtent);
                List<(int size, Vector3 at)> pieces = InteriorPieces(grid, m_Layout.HalfExtent);
                Assert.GreaterOrEqual(pieces.Count, 2, "빈 바닥을 벽으로 둘렀는데 조각이 안 늘었다 — 검산이 헛돈다");
            }
            finally
            {
                foreach (GameObject go in ring) Object.DestroyImmediate(go);
            }
        }

        // ══════════════════════ 검산 도구 ══════════════════════

        private static GameObject AddTestBox(Transform _parent, string _name, Vector3 _center, Vector3 _size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = _name;
            box.transform.SetParent(_parent, false);
            box.transform.localPosition = _center;
            box.transform.localScale = _size;
            return box;
        }

        /// <summary>몸 높이 띠(STEP_HEIGHT~BODY_HEIGHT)를 막는 트리거 아닌 콜라이더가 이름 규약 안에 있고 격자 중심 셀이 막혔나.</summary>
        private static List<string> AuditGridCoverage(Transform _root, NavGrid _grid, out int _solidCount)
        {
            var findings = new List<string>();
            _solidCount = 0;
            foreach (Collider col in _root.GetComponentsInChildren<Collider>(true))
            {
                if (col.isTrigger) continue;
                if (!(col is BoxCollider box))
                {
                    findings.Add($"{PathOf(col.transform, _root)} — 박스가 아닌 콜라이더({col.GetType().Name})는 격자가 아예 안 본다");
                    continue;
                }
                WorldBox(box.transform.localToWorldMatrix, box.center, box.size, out Vector3 min, out Vector3 max);
                if (max.y <= STEP_HEIGHT || min.y >= BODY_HEIGHT) continue;   // 바닥이거나 머리 위
                _solidCount++;

                string why = NamingViolation(col);
                if (why != null)
                {
                    findings.Add($"{PathOf(col.transform, _root)} — {why}");
                    continue;
                }

                Vector3 center = (min + max) * 0.5f;
                _grid.ToCell(center, out int cx, out int cz);
                if (!_grid.IsTerrainBlocked(cx, cz))
                    findings.Add($"{PathOf(col.transform, _root)} — 중심 셀 ({cx},{cz}) @({center.x:0.##},{center.z:0.##})이 격자에서 뚫려 있다");
            }
            return findings;
        }

        /// <summary>격자가 막는 것으로 보려면 트리거 아닌 박스 + 막는 이름 + 덧바닥 이름 아님. 몸 높이를 안 막으면 null.</summary>
        private static string NamingViolation(Collider _col)
        {
            if (!(_col is BoxCollider box)) return $"박스가 아닌 콜라이더({_col.GetType().Name})는 격자가 아예 안 본다";

            WorldBox(box.transform.localToWorldMatrix, box.center, box.size, out Vector3 min, out Vector3 max);
            if (max.y <= STEP_HEIGHT || min.y >= BODY_HEIGHT) return null;

            string name = box.gameObject.name;
            if (NavGridBaker.IsDecorFloorName(name)) return $"덧바닥 이름인데 높이 {max.y:0.##} m까지 솟아 몸을 막는다";
            if (!NavGridBaker.IsBlockingName(name)) return $"이름 `{name}`이 규약(Wall·Boundary·Obstacle·Corridor) 밖이다";
            return null;
        }

        /// <summary>폴리곤 소스 중 걷는 면(area 0)인데 윗면이 STEP_HEIGHT보다 높은 것.</summary>
        private static List<string> AuditWalkableSources(Transform _root)
        {
            var findings = new List<string>();
            List<NavMeshBuildSource> sources = MapBuilder.CollectSources(_root);
            for (int i = 0; i < sources.Count; i++)
            {
                NavMeshBuildSource s = sources[i];
                if (s.area == NOT_WALKABLE_AREA) continue;

                WorldBox(s.transform, Vector3.zero, s.size, out Vector3 min, out Vector3 max);
                if (max.y <= STEP_HEIGHT) continue;
                Vector3 c = (min + max) * 0.5f;
                findings.Add($"소스 #{i} @({c.x:0.##},{c.z:0.##}) 윗면 {max.y:0.##} m — 걷는 면(area {s.area})으로 들어갔다");
            }
            return findings;
        }

        /// <summary>박스(로컬 중심·크기)를 행렬로 옮긴 월드 AABB — 여덟 꼭짓점으로 잰다.</summary>
        private static void WorldBox(Matrix4x4 _matrix, Vector3 _center, Vector3 _size, out Vector3 _min, out Vector3 _max)
        {
            _min = Vector3.positiveInfinity;
            _max = Vector3.negativeInfinity;
            Vector3 half = _size * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 p = _matrix.MultiplyPoint3x4(_center + new Vector3(half.x * sx, half.y * sy, half.z * sz));
                _min = Vector3.Min(_min, p);
                _max = Vector3.Max(_max, p);
            }
        }

        /// <summary>경계벽 안쪽(셀 중심이 ±반치수 안)의 뚫린 셀을 4방 연결로 묶는다 — 큰 순서.</summary>
        private static List<(int size, Vector3 at)> InteriorPieces(NavGrid _grid, float _halfExtent)
        {
            int w = _grid.Width, h = _grid.Height;
            var label = new bool[w * h];
            var stack = new Stack<int>();
            var pieces = new List<(int size, Vector3 at)>();

            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    int seed = _grid.Index(x, z);
                    if (label[seed] || !OpenInterior(_grid, x, z, _halfExtent)) continue;

                    label[seed] = true;
                    stack.Push(seed);
                    int size = 0;
                    while (stack.Count > 0)
                    {
                        int cell = stack.Pop();
                        size++;
                        int cx = cell % w, cz = cell / w;
                        TryPush(cx + 1, cz);
                        TryPush(cx - 1, cz);
                        TryPush(cx, cz + 1);
                        TryPush(cx, cz - 1);
                    }
                    pieces.Add((size, _grid.ToWorld(x, z)));
                }
            }

            pieces.Sort((_a, _b) => _b.size.CompareTo(_a.size));
            return pieces;

            void TryPush(int _cx, int _cz)
            {
                if (!_grid.InBounds(_cx, _cz)) return;
                int i = _grid.Index(_cx, _cz);
                if (label[i] || !OpenInterior(_grid, _cx, _cz, _halfExtent)) return;
                label[i] = true;
                stack.Push(i);
            }
        }

        private static bool OpenInterior(NavGrid _grid, int _cx, int _cz, float _halfExtent)
        {
            if (_grid.IsTerrainBlocked(_cx, _cz)) return false;
            Vector3 c = _grid.ToWorld(_cx, _cz);
            return Mathf.Abs(c.x) < _halfExtent && Mathf.Abs(c.z) < _halfExtent;
        }

        private static string DescribePieces(List<(int size, Vector3 at)> _pieces)
        {
            var lines = new List<string>();
            float cellArea = NavGrid.CELL_SIZE * NavGrid.CELL_SIZE;
            for (int i = 0; i < _pieces.Count && i < 20; i++)
            {
                lines.Add($"#{i} {_pieces[i].size * cellArea:0.##} m² — 첫 셀 @({_pieces[i].at.x:0.##},{_pieces[i].at.z:0.##})");
            }
            return string.Join("\n", lines);
        }

        private static string PathOf(Transform _t, Transform _root)
        {
            string path = _t.name;
            for (Transform p = _t.parent; p != null && p != _root; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
