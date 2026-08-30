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
