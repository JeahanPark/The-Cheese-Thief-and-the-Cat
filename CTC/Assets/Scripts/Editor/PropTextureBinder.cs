using System.IO;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 구워 둔 프롭 머티리얼에 텍스처를 물린다 — Tools > CTC > Apply Prop Textures (2026-08-30).
    ///
    /// 텍스처는 **AI가 그린 PNG**다(`docs/design-vault/텍스처 프롬프트.md`).
    /// 여기서는 임포트 설정을 맞추고 머티리얼에 꽂는 일만 한다 — 그림은 안 만든다.
    ///
    /// **UV는 1 = 1미터다.** `SdfMesher.PlanarUvs`가 정점 좌표를 그대로 UV로 쓰기 때문이다.
    /// 그래서 타일 배율이 곧 "1미터에 몇 장"이고, 텍스처는 반드시 seamless여야 한다 —
    /// 이음새가 있으면 1m마다 격자가 드러난다.
    ///
    /// 텍스처가 없으면 **아무것도 안 바꾼다.** 단색 상태 그대로 남는다.
    /// </summary>
    public static class PropTextureBinder
    {
        private const string PROP_TEX_DIR = "Assets/Materials/Props/Textures";

        /// <summary>한 장이 몇 미터를 덮는가. 1이면 1m마다 한 번 반복된다.</summary>
        private const float TILE_METERS = 1f;

        private readonly struct Binding
        {
            public readonly string TexturePath;
            public readonly string MaterialPath;
            public readonly float TileMeters;
            public readonly Color Tint;

            public Binding(string _texture, string _material, float _tileMeters, Color _tint)
            {
                TexturePath = _texture;
                MaterialPath = _material;
                TileMeters = _tileMeters;
                Tint = _tint;
            }
        }

        /// <summary>
        /// 치즈 텍스처 **한 장**을 요새·창고·쐐기가 나눠 쓴다 — 셋 다 같은 치즈다.
        /// 구분은 틴트로만 한다: 바닥에 굴러다니는 쐐기가 제일 밝고, 세워 둔 요새는 한 톤 눌러
        /// 배경 구조물처럼 읽히게 한다.
        /// </summary>
        private static readonly Binding[] BINDINGS =
        {
            new Binding(PROP_TEX_DIR + "/T_CheeseWall.png", PropAssets.FORTRESS_MATERIAL,
                TILE_METERS, new Color(0.88f, 0.80f, 0.66f)),
            // 쐐기(0.6m)·창고(1m)처럼 작은 것들이 쓴다 — 1m에 한 장이면 무늬가 안 보인다.
            new Binding(PROP_TEX_DIR + "/T_CheeseWall.png", PropAssets.CHEESE_MATERIAL,
                0.5f, Color.white),
            new Binding(PROP_TEX_DIR + "/T_BrickWall.png", PropAssets.ALCOVE_MATERIAL,
                TILE_METERS, Color.white),
            new Binding(PROP_TEX_DIR + "/T_WoodCrate.png", PropAssets.CRATE_MATERIAL,
                0.5f, Color.white),
        };

        [MenuItem("Tools/CTC/Apply Prop Textures")]
        public static void Apply()
        {
            int bound = 0;
            int missing = 0;

            for (int i = 0; i < BINDINGS.Length; i++)
            {
                Binding binding = BINDINGS[i];

                if (!File.Exists(binding.TexturePath))
                {
                    Debug.LogWarning($"[PropTextures] 텍스처가 없다(건너뜀): {binding.TexturePath}");
                    missing++;
                    continue;
                }

                ConfigureImport(binding.TexturePath);

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(binding.TexturePath);
                var material = AssetDatabase.LoadAssetAtPath<Material>(binding.MaterialPath);
                if (texture == null || material == null)
                {
                    Debug.LogWarning($"[PropTextures] 못 읽었다: {binding.TexturePath} → {binding.MaterialPath}");
                    missing++;
                    continue;
                }

                Bind(material, texture, binding.TileMeters, binding.Tint);
                bound++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PropTextures] 완료 — {bound}개 배선 / {missing}개 없음 " +
                      "(없으면 단색 그대로다)");
        }

        /// <summary>
        /// 타일링이 목적이라 **Repeat**여야 한다. Clamp면 가장자리 픽셀이 늘어나 줄무늬가 생긴다.
        /// mipmap은 켠다 — 멀리 있는 벽감이 지글거리는 것을 막는다.
        /// </summary>
        private static void ConfigureImport(string _path)
        {
            var importer = AssetImporter.GetAtPath(_path) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;

            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                dirty = true;
            }
            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                dirty = true;
            }
            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                dirty = true;
            }
            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                dirty = true;
            }
            if (!importer.sRGBTexture)
            {
                importer.sRGBTexture = true;   // 알베도다 — 선형으로 읽으면 색이 뜬다
                dirty = true;
            }
            if (importer.alphaSource != TextureImporterAlphaSource.None)
            {
                importer.alphaSource = TextureImporterAlphaSource.None;
                dirty = true;
            }
            if (importer.anisoLevel != 4)
            {
                importer.anisoLevel = 4;   // 비스듬히 보는 바닥·벽이 뭉개지는 것을 줄인다
                dirty = true;
            }
            if (importer.maxTextureSize != 1024)
            {
                importer.maxTextureSize = 1024;
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }

        /// <summary>
        /// **밑색을 되돌린다.** URP Lit은 `_BaseColor × _BaseMap`이라, 노란 머티리얼에
        /// 노란 치즈 텍스처를 물리면 두 번 곱해져 탁하게 죽는다. 색은 이제 텍스처가 들고 있고,
        /// 틴트는 같은 텍스처를 쓰는 것들을 구분하는 데만 쓴다.
        /// </summary>
        private static void Bind(Material _material, Texture2D _texture, float _tileMeters, Color _tint)
        {
            if (_material.HasProperty("_BaseMap")) _material.SetTexture("_BaseMap", _texture);
            if (_material.HasProperty("_MainTex")) _material.SetTexture("_MainTex", _texture);

            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", _tint);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", _tint);

            float tiling = _tileMeters > 0f ? 1f / _tileMeters : 1f;
            _material.mainTextureScale = new Vector2(tiling, tiling);

            EditorUtility.SetDirty(_material);
        }
    }
}
