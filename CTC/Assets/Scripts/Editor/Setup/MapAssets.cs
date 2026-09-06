using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 맵 테마 에셋의 경로 단일 출처 — 굽는 쪽(<see cref="MapMaterialBaker"/>)과
    /// 쓰는 쪽(SceneComposer)이 같이 본다. 따로 들면 한쪽만 고쳤을 때
    /// 에러 없이 Gridbox로 되돌아간다(PropAssets와 같은 이유).
    /// </summary>
    internal static class MapAssets
    {
        public const string SHADER_NAME = "CTC/MapTriplanar";
        public const string TEX_DIR = "Assets/Materials/Map/Textures";
        public const string MAT_DIR = "Assets/Materials/Map";

        public const string FLOOR_TEX = TEX_DIR + "/T_PantryFloor.png";
        public const string WALL_TEX = TEX_DIR + "/T_PantryWall.png";
        public const string ROOM_FLOOR_TEX = TEX_DIR + "/T_PantryRoomFloor.png";

        public const string GROUND_MAT = MAT_DIR + "/M_MapGround.mat";
        public const string BOUNDARY_MAT = MAT_DIR + "/M_MapBoundary.mat";
        public const string ROOM_FLOOR_MAT = MAT_DIR + "/M_MapRoomFloor.mat";
        public const string ROOM_WALL_MAT = MAT_DIR + "/M_MapRoomWall.mat";
        public const string CORRIDOR_WALL_MAT = MAT_DIR + "/M_MapCorridorWall.mat";

        // ── 방 모듈(2026-09-07) — 굽는 쪽(RoomModuleBuilder)과 배선(SceneComposer)·풀 빌더가 같이 본다 ──
        public const string MODULE_DIR = "Assets/Prefabs/Maps/Modules";
        public const string MODULE_CATALOG = "Assets/Prefabs/Maps/RoomModuleCatalog.asset";
        public const string DOOR_FRAME_PREFAB = MODULE_DIR + "/DoorFrame.prefab";
        public const string WALL_PLUG_PREFAB = MODULE_DIR + "/WallPlug.prefab";
        public const string MODULE_BIG_PANTRY01 = MODULE_DIR + "/Module_Big_Pantry01.prefab";
        public const string MODULE_SMALL_CELLAR01 = MODULE_DIR + "/Module_Small_Cellar01.prefab";

        // ── 팬트리 가구(Poly Universal Pack 선별 임포트, 2026-08-31) ──
        private const string PACK = "Assets/polyperfect/Poly Universal Pack/- Prefabs";

        public static readonly string[] SHELF_PREFABS =
        {
            PACK + "/Fantasy/Bakery Fantasy/shelf-wall-a-fantasy.prefab",
            PACK + "/Fantasy/Bakery Fantasy/shelf-wall-a-pastry-fantasy.prefab",
            PACK + "/Survival/shelf-wall-wood.prefab",
            PACK + "/Fantasy/Furniture Fantasy/shelf-simple-a-fantasy.prefab",
        };

        public static readonly string[] CRATE_PREFABS =
        {
            PACK + "/Farm/Crates Farm/crate-wood-big.prefab",
            PACK + "/Farm/Crates Farm/crate-wood-medium.prefab",
            PACK + "/Farm/Crates Farm/crate-wood-medium-potatoes-a.prefab",
            PACK + "/Farm/Crates Farm/crate-wood-small.prefab",
        };

        public static readonly string[] OBSTACLE_PREFABS =
        {
            PACK + "/Fantasy/barrel-wood-a-closed-a-fantasy.prefab",
            PACK + "/Fantasy/barrel-wood-a-closed-b-fantasy.prefab",
            PACK + "/Fantasy/barrel-wood-b-closed-fantasy.prefab",
            PACK + "/Farm/Crops Farm/pumpkin-b.prefab",
            PACK + "/Farm/Crops Farm/pumpkin-ripe-a.prefab",
        };

        /// <summary>경로 배열 → 프리팹 배열. 없는 것은 건너뛴다(팩 미임포트 시 빈 배열 → 드레싱 생략).</summary>
        public static GameObject[] LoadPrefabs(string[] _paths)
        {
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (string path in _paths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) list.Add(go);
                else Debug.LogWarning($"[MapAssets] 가구 프리팹 없음(건너뜀): {path}");
            }
            return list.ToArray();
        }

        /// <summary>없으면 null + 경고 — 부르는 쪽이 Gridbox로 되돌아간다(마젠타 방지, 함정 6).</summary>
        public static Material LoadMaterial(string _path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(_path);
            if (mat == null)
                Debug.LogWarning($"[MapAssets] 머티리얼 없음(Gridbox로 대체): {_path} — Tools/CTC/Build Map Materials를 먼저 돌려라");
            return mat;
        }
    }
}
