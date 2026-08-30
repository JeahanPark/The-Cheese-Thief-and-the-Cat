using System.IO;
using UnityEditor;
using UnityEngine;
using CTC.Runtime.Map;

namespace CTC.Editor
{
    /// <summary>
    /// 팬트리 맵 텍스처·머티리얼 생성 — Tools > CTC > Build Map Materials (팬트리).
    /// 텍스처: PNG가 이미 있으면 안 굽는다(AI 텍스처가 덮을 자리). 없으면 절차 널빤지를 굽는다.
    /// 머티리얼: CTC/MapTriplanar 5종. 셰이더가 null이면 즉시 중단 — 마젠타가 구워지는 것(함정 6)보다 실패가 낫다.
    /// </summary>
    public static class MapMaterialBaker
    {
        private const int TEX_SIZE = 1024;

        [MenuItem("Tools/CTC/Build Map Materials (팬트리)")]
        public static void Build()
        {
            EnsureFolders();

            // 널마루 바닥 — 따뜻한 갈색, 굵은 널 6줄(트리플래너 2m 타일 → 널폭 33cm)
            BakeIfMissing(MapAssets.FLOOR_TEX, 6, 11, new Color(0.42f, 0.30f, 0.18f));
            // 판벽 — 한 톤 어둡고 차분하게, 좁은 널 8줄
            BakeIfMissing(MapAssets.WALL_TEX, 8, 23, new Color(0.36f, 0.27f, 0.17f));
            // 방 바닥 — 존 구분용으로 밝고 붉은 기
            BakeIfMissing(MapAssets.ROOM_FLOOR_TEX, 6, 37, new Color(0.48f, 0.33f, 0.20f));

            Shader shader = Shader.Find(MapAssets.SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError($"[MapMaterials] 셰이더 없음: {MapAssets.SHADER_NAME} — 중단한다(마젠타 방지). " +
                               "리임포트 중이면 끝난 뒤 다시 돌려라");
                return;
            }

            var floorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MapAssets.FLOOR_TEX);
            var wallTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MapAssets.WALL_TEX);
            var roomFloorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MapAssets.ROOM_FLOOR_TEX);

            // 타일 2m: 널빤지 텍스처 한 장이 2m를 덮는다 → 널폭 25~33cm, 거대 세계 감각
            // 지면은 회색빛으로 눌러 방 바닥(밝고 붉은 기)과 갈라놓는다 — 존 구분이 게임플레이 정보다
            Save(MapAssets.GROUND_MAT, shader, floorTex, 2f, new Color(0.62f, 0.60f, 0.55f), 0f);
            Save(MapAssets.BOUNDARY_MAT, shader, wallTex, 2f, Color.white, 0.6f);       // 걸레받이 0.6m
            Save(MapAssets.ROOM_FLOOR_MAT, shader, roomFloorTex, 2f, Color.white, 0f);
            Save(MapAssets.ROOM_WALL_MAT, shader, wallTex, 2f, new Color(0.92f, 0.88f, 0.82f), 0f);
            Save(MapAssets.CORRIDOR_WALL_MAT, shader, wallTex, 1f, new Color(0.85f, 0.80f, 0.72f), 0f);

            AssetDatabase.SaveAssets();
            Debug.Log("[MapMaterials] 완료 — 텍스처 3종(있으면 유지) + 머티리얼 5종");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(MapAssets.MAT_DIR))
                AssetDatabase.CreateFolder("Assets/Materials", "Map");
            if (!AssetDatabase.IsValidFolder(MapAssets.TEX_DIR))
                AssetDatabase.CreateFolder(MapAssets.MAT_DIR, "Textures");
        }

        private static void BakeIfMissing(string _path, int _rows, int _seed, Color _base)
        {
            if (File.Exists(_path)) return;   // AI 텍스처가 이미 자리를 차지했다 — 건드리지 않는다

            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGB24, false);
            var pixels = new Color[TEX_SIZE * TEX_SIZE];
            for (int y = 0; y < TEX_SIZE; y++)
            {
                float v = (float)y / TEX_SIZE;
                for (int x = 0; x < TEX_SIZE; x++)
                    pixels[y * TEX_SIZE + x] = PantryTextureLogic.PlankColor((float)x / TEX_SIZE, v, _rows, _seed, _base);
            }
            tex.SetPixels(pixels);
            File.WriteAllBytes(_path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(_path);
            PropTextureBinder.ConfigureImport(_path);   // Repeat·mip·sRGB·aniso — 프롭과 같은 규약
            Debug.Log($"[MapMaterials] 절차 베이크: {_path} (AI 텍스처로 덮으면 자동 교체)");
        }

        private static void Save(string _path, Shader _shader, Texture2D _tex, float _tileMeters, Color _tint, float _skirt)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(_path);
            bool fresh = mat == null;
            if (fresh) mat = new Material(_shader);
            else mat.shader = _shader;

            mat.SetTexture("_BaseMap", _tex);
            mat.SetColor("_BaseColor", _tint);
            mat.SetFloat("_TileMeters", _tileMeters);
            mat.SetFloat("_SkirtMeters", _skirt);

            if (fresh) AssetDatabase.CreateAsset(mat, _path);
            else EditorUtility.SetDirty(mat);

            if (mat.shader == null)   // 함정 6 이중 검사
                Debug.LogError($"[MapMaterials] {_path} 셰이더가 비었다 — 마젠타로 구워졌을 수 있다");
        }
    }
}
