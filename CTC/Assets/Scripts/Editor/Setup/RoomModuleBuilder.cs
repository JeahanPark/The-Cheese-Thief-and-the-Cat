using System.Collections.Generic;
using CTC.Runtime.Map;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 방 모듈 프리팹을 굽는다(2026-09-07, 스펙 1단계) — 문틀·막벽·모듈 2종·카탈로그.
    ///
    /// **이미 있는 프리팹은 덮어쓰지 않는다**(함정 2). 손으로 가구를 옮겨 놓은 걸 메뉴가 매번 지우면
    /// 손조립이 성립하지 않는다. 다시 굽고 싶으면 그 프리팹 파일을 지운다. 카탈로그만 매번 갱신한다.
    ///
    /// 셸(바닥·벽·기둥·들보·문 소켓)은 코드가 굽고, v1 가구 좌표도 여기 적혀 있다 — 이 좌표가 곧 "손"이다.
    /// 이름 규약이 곧 기능이다: `Wall_*`·`Obstacle_*`의 실물 BoxCollider는 `NavGridBaker`가 격자에 찍고,
    /// `Floor_*`는 지형이 아니며, 트리거 박스는 격자가 무시한다(카메라 페이더용).
    /// </summary>
    public static class RoomModuleBuilder
    {
        private const float WALL_HEIGHT = 5f;      // MapBuilder.ROOM_WALL_HEIGHT
        private const float WALL_THICK = 0.5f;     // MapBuilder.ROOM_WALL_THICK
        private const float DOOR_SLOT = 3.6f;      // 복도 폭 3 + 여유 — MapBuilder 개구부(폭/2 + 0.25)와 같다
        private const float SHELF_SCALE = 2f;      // MapDresser.m_UnitScale
        private const float CRATE_SCALE = 1.4f;    // MapDresser.m_CrateScale
        private const float BARREL_SCALE = 2f;

        private static readonly string PACK = "Assets/polyperfect/Poly Universal Pack/- Prefabs";
        private static readonly string SHELF_A = PACK + "/Fantasy/Bakery Fantasy/shelf-wall-a-fantasy.prefab";
        private static readonly string SHELF_B = PACK + "/Fantasy/Bakery Fantasy/shelf-wall-a-pastry-fantasy.prefab";
        private static readonly string SHELF_C = PACK + "/Survival/shelf-wall-wood.prefab";
        private static readonly string CRATE_BIG = PACK + "/Farm/Crates Farm/crate-wood-big.prefab";
        private static readonly string CRATE_MED = PACK + "/Farm/Crates Farm/crate-wood-medium-potatoes-a.prefab";
        private static readonly string CRATE_SMALL = PACK + "/Farm/Crates Farm/crate-wood-small.prefab";
        private static readonly string BARREL_A = PACK + "/Fantasy/barrel-wood-a-closed-a-fantasy.prefab";
        private static readonly string BARREL_B = PACK + "/Fantasy/barrel-wood-b-closed-fantasy.prefab";
        private static readonly string PUMPKIN = PACK + "/Farm/Crops Farm/pumpkin-b.prefab";
        private static readonly string WALNUT = PACK + "/Food/walnut.prefab";
        private static readonly string ALMOND = PACK + "/Food/almond.prefab";

        [MenuItem("Tools/CTC/Map/Build Room Modules v1")]
        public static void Build()
        {
            Material wall = MapAssets.LoadMaterial(MapAssets.ROOM_WALL_MAT);
            Material floor = MapAssets.LoadMaterial(MapAssets.ROOM_FLOOR_MAT);
            Material beam = MapAssets.LoadMaterial(MapAssets.BOUNDARY_MAT);
            if (wall == null || floor == null || beam == null)
            {
                Debug.LogError("[RoomModules] 팬트리 머티리얼이 없다 — Tools/CTC/Build Map Materials 먼저 (마젠타 방지)");
                return;
            }

            EnsureFolder(MapAssets.MODULE_DIR);

            GameObject doorFrame = BuildOrKeep(MapAssets.DOOR_FRAME_PREFAB, () => MakeDoorFrame(beam));
            GameObject wallPlug = BuildOrKeep(MapAssets.WALL_PLUG_PREFAB, () => MakeWallPlug(wall));
            GameObject big = BuildOrKeep(MapAssets.MODULE_BIG_PANTRY01,
                () => MakeModule("Module_Big_Pantry01", RoomModuleLogic.BIG_SIZE, wall, floor, beam, FurnishBigPantry01));
            GameObject small = BuildOrKeep(MapAssets.MODULE_SMALL_CELLAR01,
                () => MakeModule("Module_Small_Cellar01", RoomModuleLogic.SMALL_SIZE, wall, floor, beam, FurnishSmallCellar01));

            var catalog = AssetDatabase.LoadAssetAtPath<RoomModuleCatalog>(MapAssets.MODULE_CATALOG);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RoomModuleCatalog>();
                AssetDatabase.CreateAsset(catalog, MapAssets.MODULE_CATALOG);
            }
            catalog.Set(new List<RoomModuleCatalog.Entry>
            {
                new RoomModuleCatalog.Entry { ModuleId = "Big_Pantry01", Prefab = big, Footprint = RoomModuleLogic.FOOTPRINT_BIG, Role = RoomModuleRole.Normal },
                new RoomModuleCatalog.Entry { ModuleId = "Small_Cellar01", Prefab = small, Footprint = RoomModuleLogic.FOOTPRINT_SMALL, Role = RoomModuleRole.Normal },
            }, doorFrame, wallPlug);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"[RoomModules] 완료 — 모듈 {catalog.Entries.Count} · 문틀 {(doorFrame != null)} · 막벽 {(wallPlug != null)} → {MapAssets.MODULE_CATALOG}");
        }

        // ---- 프리팹 저장 ----

        private static GameObject BuildOrKeep(string _path, System.Func<GameObject> _make)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
            if (existing != null)
            {
                Debug.Log($"[RoomModules] 있음 — 건너뜀(손조립 보존): {_path}");
                return existing;
            }

            GameObject root = _make();
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, _path);
            Object.DestroyImmediate(root);
            return asset;
        }

        private static void EnsureFolder(string _dir)
        {
            if (AssetDatabase.IsValidFolder(_dir)) return;
            string parent = System.IO.Path.GetDirectoryName(_dir).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(_dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ---- 이음새 ----

        /// <summary>문틀 — 기둥 2 + 상인방. 콜라이더 없음(벽 조각이 이미 막고, 문 폭은 비어 있어야 한다).</summary>
        private static GameObject MakeDoorFrame(Material _beam)
        {
            var root = new GameObject("DoorFrame");
            float half = DOOR_SLOT * 0.5f;
            Visual(root, _beam, "Post_L", new Vector3(-half + 0.2f, 1.6f, 0f), new Vector3(0.4f, 3.2f, 0.6f));
            Visual(root, _beam, "Post_R", new Vector3(half - 0.2f, 1.6f, 0f), new Vector3(0.4f, 3.2f, 0.6f));
            Visual(root, _beam, "Lintel", new Vector3(0f, 3.4f, 0f), new Vector3(DOOR_SLOT + 0.4f, 0.4f, 0.6f));
            return root;
        }

        /// <summary>막벽 — 문 자리를 벽으로 채운다(실물, 격자 스탬프) + 안쪽에 궤짝 하나(트리거).</summary>
        private static GameObject MakeWallPlug(Material _wall)
        {
            var root = new GameObject("WallPlug");
            Solid(root, _wall, "Wall_Plug", new Vector3(0f, WALL_HEIGHT * 0.5f, 0f), new Vector3(DOOR_SLOT, WALL_HEIGHT, WALL_THICK));
            // forward(+z)가 바깥이다 — 안쪽은 -z
            Prop(root, CRATE_MED, "Deco_Crate", new Vector3(0.6f, 0f, -0.9f), 20f, CRATE_SCALE, _blocking: false);
            return root;
        }

        // ---- 셸 ----

        private static GameObject MakeModule(string _name, Vector2 _footprint, Material _wall, Material _floor, Material _beam,
            System.Action<Transform> _furnish)
        {
            var root = new GameObject(_name);
            float hw = _footprint.x * 0.5f, hh = _footprint.y * 0.5f;

            var shell = new GameObject("Shell");
            shell.transform.SetParent(root.transform, false);

            GameObject floorGo = Solid(shell, _floor, "Floor_Module", new Vector3(0f, 0.02f, 0f), new Vector3(_footprint.x, 0.04f, _footprint.y));
            floorGo.layer = 2;   // Ignore Raycast — 카메라가 바닥과 안 부딪히게(MapBuilder와 동일)

            // 벽: 변마다 문 슬롯 양옆 2조각 — 0=N(+z) 1=S(-z) 2=E(+x) 3=W(-x)
            float slotHalf = DOOR_SLOT * 0.5f;
            for (int side = 0; side < 4; side++)
            {
                bool horizontal = side <= 1;
                float fixedCoord = side == 0 ? hh : side == 1 ? -hh : side == 2 ? hw : -hw;
                float extent = horizontal ? hw : hh;
                float length = extent - slotHalf;
                float center = slotHalf + length * 0.5f;
                string s = side == 0 ? "N" : side == 1 ? "S" : side == 2 ? "E" : "W";

                for (int k = -1; k <= 1; k += 2)
                {
                    Vector3 pos = horizontal
                        ? new Vector3(center * k, WALL_HEIGHT * 0.5f, fixedCoord)
                        : new Vector3(fixedCoord, WALL_HEIGHT * 0.5f, center * k);
                    Vector3 size = horizontal
                        ? new Vector3(length, WALL_HEIGHT, WALL_THICK)
                        : new Vector3(WALL_THICK, WALL_HEIGHT, length);
                    Solid(shell, _wall, $"Wall_{s}_{(k < 0 ? "L" : "R")}", pos, size);
                }

                // 들보 — 변 전체 길이, 문 위를 가로지른다(상인방 역할 겸). 콜라이더 없음
                Vector3 rimPos = horizontal ? new Vector3(0f, WALL_HEIGHT - 0.15f, fixedCoord) : new Vector3(fixedCoord, WALL_HEIGHT - 0.15f, 0f);
                Vector3 rimSize = horizontal ? new Vector3(extent * 2f + 0.8f, 0.3f, 0.8f) : new Vector3(0.8f, 0.3f, extent * 2f + 0.8f);
                Visual(shell, _beam, $"Rim_{s}", rimPos, rimSize);
            }

            // 모서리 기둥 — 벽보다 살짝 굵어 모서리가 읽힌다. 콜라이더 없음(벽이 막는다)
            foreach ((float sx, float sz, string n) in new[] { (1f, 1f, "NE"), (-1f, 1f, "NW"), (1f, -1f, "SE"), (-1f, -1f, "SW") })
                Visual(shell, _beam, $"Pillar_{n}", new Vector3(hw * sx, WALL_HEIGHT * 0.5f + 0.1f, hh * sz), new Vector3(0.8f, WALL_HEIGHT + 0.2f, 0.8f));

            // 문 소켓 — 변 한가운데, 바깥을 본다
            var doors = new GameObject("Doors");
            doors.transform.SetParent(root.transform, false);
            for (int side = 0; side < 4; side++)
            {
                string s = side == 0 ? "N" : side == 1 ? "S" : side == 2 ? "E" : "W";
                var socket = new GameObject($"DoorSocket_{s}");
                socket.transform.SetParent(doors.transform, false);
                socket.transform.localPosition = side == 0 ? new Vector3(0f, 0f, hh)
                    : side == 1 ? new Vector3(0f, 0f, -hh)
                    : side == 2 ? new Vector3(hw, 0f, 0f)
                    : new Vector3(-hw, 0f, 0f);
                socket.transform.localRotation = Quaternion.Euler(0f, RoomModuleLogic.SideYaw(side), 0f);
                socket.AddComponent<RoomModuleSocket>().Set(RoomModuleSocketKind.Door, side);
            }

            var furniture = new GameObject("Furniture");
            furniture.transform.SetParent(root.transform, false);
            _furnish(furniture.transform);

            return root;
        }

        // ---- v1 가구 좌표 (손조립) ----
        // 문 통로는 십자(|x|<1.8 또는 |z|<1.8)라 그 밖 사분면에만 놓는다.
        // 1단계 치즈 자리는 (±s.x/4, ∓s.y/4) — 큰방 (±3, ∓2.75) · 작은방 (±2.5, ∓2.25). 반경 2m를 비워 둔다.

        private static void FurnishBigPantry01(Transform _p)
        {
            float hw = 6f, hh = 5.5f;
            // 북벽 선반 4 (문 슬롯 양옆 2씩) — 안쪽(-z)을 본다
            WallProp(_p, SHELF_A, "Obstacle_Shelf_N0", new Vector3(-4.8f, 0f, hh), Vector3.back, SHELF_SCALE);
            WallProp(_p, SHELF_B, "Obstacle_Shelf_N1", new Vector3(-2.8f, 0f, hh), Vector3.back, SHELF_SCALE);
            WallProp(_p, SHELF_C, "Obstacle_Shelf_N2", new Vector3(2.8f, 0f, hh), Vector3.back, SHELF_SCALE);
            WallProp(_p, SHELF_A, "Obstacle_Shelf_N3", new Vector3(4.8f, 0f, hh), Vector3.back, SHELF_SCALE);
            // 서벽 선반 2 — 안쪽(+x)을 본다
            WallProp(_p, SHELF_B, "Obstacle_Shelf_W0", new Vector3(-hw, 0f, 3.6f), Vector3.right, SHELF_SCALE);
            WallProp(_p, SHELF_C, "Obstacle_Shelf_W1", new Vector3(-hw, 0f, -3.6f), Vector3.right, SHELF_SCALE);
            // 남서 궤짝더미 3
            Prop(_p, CRATE_BIG, "Obstacle_Crate_0", new Vector3(-4.4f, 0f, -4.1f), 8f, CRATE_SCALE, _blocking: true);
            Prop(_p, CRATE_MED, "Obstacle_Crate_1", new Vector3(-2.9f, 0f, -4.4f), -12f, CRATE_SCALE, _blocking: true);
            Prop(_p, CRATE_SMALL, "Obstacle_Crate_2", new Vector3(-4.3f, 0f, -2.6f), 30f, CRATE_SCALE, _blocking: true);
            // 북동 배럴 2 — 시야 끊기
            Prop(_p, BARREL_A, "Obstacle_Barrel_0", new Vector3(4.0f, 0f, 3.4f), 0f, BARREL_SCALE, _blocking: true);
            Prop(_p, BARREL_B, "Obstacle_Barrel_1", new Vector3(2.7f, 0f, 4.3f), 40f, BARREL_SCALE, _blocking: true);
            // 호박 3 — 통과 가능(트리거만)
            Prop(_p, PUMPKIN, "Deco_Pumpkin_0", new Vector3(2.5f, 0f, 2.2f), 15f, 1.6f, _blocking: false);
            Prop(_p, PUMPKIN, "Deco_Pumpkin_1", new Vector3(-2.4f, 0f, -2.3f), 70f, 1.4f, _blocking: false);
            Prop(_p, PUMPKIN, "Deco_Pumpkin_2", new Vector3(5.0f, 0f, -4.6f), 120f, 1.8f, _blocking: false);
        }

        private static void FurnishSmallCellar01(Transform _p)
        {
            float hw = 5f, hh = 4.5f;
            // 동벽 선반 2 — 안쪽(-x)을 본다
            WallProp(_p, SHELF_C, "Obstacle_Shelf_E0", new Vector3(hw, 0f, 3.0f), Vector3.left, SHELF_SCALE);
            WallProp(_p, SHELF_A, "Obstacle_Shelf_E1", new Vector3(hw, 0f, -3.0f), Vector3.left, SHELF_SCALE);
            // 남벽 선반 1 (문 슬롯 서쪽)
            WallProp(_p, SHELF_B, "Obstacle_Shelf_S0", new Vector3(-3.4f, 0f, -hh), Vector3.forward, SHELF_SCALE);
            // 북동 배럴 2
            Prop(_p, BARREL_B, "Obstacle_Barrel_0", new Vector3(3.6f, 0f, 3.3f), 0f, BARREL_SCALE, _blocking: true);
            Prop(_p, BARREL_A, "Obstacle_Barrel_1", new Vector3(2.3f, 0f, 3.9f), 55f, BARREL_SCALE, _blocking: true);
            // 남서 궤짝 2
            Prop(_p, CRATE_BIG, "Obstacle_Crate_0", new Vector3(-3.6f, 0f, -3.3f), -6f, CRATE_SCALE, _blocking: true);
            Prop(_p, CRATE_SMALL, "Obstacle_Crate_1", new Vector3(-2.2f, 0f, -3.8f), 25f, CRATE_SCALE, _blocking: true);
            // 견과 — 트리거만
            Prop(_p, WALNUT, "Deco_Walnut_0", new Vector3(-4.0f, 0f, -2.0f), 0f, 1.5f, _blocking: false);
            Prop(_p, ALMOND, "Deco_Almond_0", new Vector3(4.0f, 0f, 2.1f), 30f, 1.5f, _blocking: false);
            Prop(_p, WALNUT, "Deco_Walnut_1", new Vector3(3.6f, 0f, -4.0f), 90f, 1.5f, _blocking: false);
        }

        // ---- 부품 ----

        /// <summary>실물 박스 — BoxCollider 유지(이름이 Wall_/Obstacle_면 격자가 찍는다).</summary>
        private static GameObject Solid(GameObject _parent, Material _mat, string _name, Vector3 _center, Vector3 _size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = _name;
            box.transform.SetParent(_parent.transform, false);
            box.transform.localPosition = _center;
            box.transform.localScale = _size;
            box.GetComponent<Renderer>().sharedMaterial = _mat;
            return box;
        }

        /// <summary>보이기만 하는 박스 — 콜라이더 제거.</summary>
        private static GameObject Visual(GameObject _parent, Material _mat, string _name, Vector3 _center, Vector3 _size)
        {
            GameObject box = Solid(_parent, _mat, _name, _center, _size);
            Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }

        /// <summary>
        /// 벽에 붙는 가구 — 렌더 AABB의 뒷면을 벽 안쪽 면(+2cm)에, 바닥을 지면에 맞춘다(MapDresser와 같은 방식).
        /// `_inward`는 벽에서 방 안을 보는 방향. 항상 실물 장애물(Obstacle_).
        /// </summary>
        private static void WallProp(Transform _parent, string _prefabPath, string _name, Vector3 _wallPoint, Vector3 _inward, float _scale)
        {
            GameObject go = Spawn(_parent, _prefabPath, _name, _wallPoint, Quaternion.LookRotation(_inward), _scale);
            if (go == null) return;

            if (TryGetWorldBounds(go, out Bounds b))
            {
                float halfExt = Mathf.Abs(b.extents.x * _inward.x) + Mathf.Abs(b.extents.y * _inward.y) + Mathf.Abs(b.extents.z * _inward.z);
                float innerEdge = Vector3.Dot(b.center, _inward) - halfExt;
                float wallPlane = Vector3.Dot(_wallPoint, _inward) + WALL_THICK * 0.5f;
                go.transform.position += _inward * ((wallPlane + 0.02f) - innerEdge) + Vector3.up * (0.01f - b.min.y);
            }
            FitColliders(go, _blocking: true);
        }

        /// <summary>바닥에 놓는 소품. 실물이면 Obstacle_ 박스(격자 스탬프), 아니면 트리거 박스만(페이드).</summary>
        private static void Prop(GameObject _parent, string _prefabPath, string _name, Vector3 _pos, float _yaw, float _scale, bool _blocking)
            => Prop(_parent.transform, _prefabPath, _name, _pos, _yaw, _scale, _blocking);

        private static void Prop(Transform _parent, string _prefabPath, string _name, Vector3 _pos, float _yaw, float _scale, bool _blocking)
        {
            GameObject go = Spawn(_parent, _prefabPath, _name, _pos, Quaternion.Euler(0f, _yaw, 0f), _scale);
            if (go == null) return;
            if (TryGetWorldBounds(go, out Bounds b)) go.transform.position += Vector3.up * (0.01f - b.min.y);
            FitColliders(go, _blocking);
        }

        private static GameObject Spawn(Transform _parent, string _prefabPath, string _name, Vector3 _localPos, Quaternion _localRot, float _scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[RoomModules] 팩 프리팹 없음(건너뜀): {_prefabPath}");
                return null;
            }
            // 팩 프리팹의 변형(variant)이 아니라 복사본으로 — 모듈 프리팹이 팩에 묶이지 않게
            GameObject go = Object.Instantiate(prefab, _parent);
            go.name = _name;
            go.transform.localPosition = _localPos;
            go.transform.localRotation = _localRot;
            go.transform.localScale = prefab.transform.localScale * _scale;
            return go;
        }

        /// <summary>
        /// 팩 콜라이더·리지드바디를 버리고 렌더 AABB 박스를 단다. 실물이면 실물 박스 + 트리거 박스(페이드),
        /// 아니면 트리거 박스만. concave MeshCollider는 트리거가 못 되므로 원본은 전부 지운다(MapDresser와 같다).
        /// </summary>
        private static void FitColliders(GameObject _go, bool _blocking)
        {
            foreach (Collider c in _go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (Rigidbody r in _go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
            if (!TryGetWorldBounds(_go, out Bounds b)) return;

            Vector3 ls = _go.transform.lossyScale;
            Vector3 size = new Vector3(
                b.size.x / Mathf.Max(ls.x, 1e-4f), b.size.y / Mathf.Max(ls.y, 1e-4f), b.size.z / Mathf.Max(ls.z, 1e-4f));
            // 회전한 물건은 월드 AABB가 로컬 박스보다 크다 — 로컬 축으로 되돌려 재는 대신 AABB를 그대로 쓴다(약간 넉넉).
            Vector3 center = _go.transform.InverseTransformPoint(b.center);

            if (_blocking)
            {
                var solid = _go.AddComponent<BoxCollider>();
                solid.center = center;
                solid.size = size;
            }
            var trigger = _go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = center;
            trigger.size = size;
        }

        private static bool TryGetWorldBounds(GameObject _go, out Bounds _bounds)
        {
            Renderer[] renderers = _go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { _bounds = default; return false; }
            _bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) _bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
    }
}
