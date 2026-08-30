using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 앱 아이콘을 Player Settings에 꽂는다 — Tools > CTC > Apply App Icon (2026-08-30).
    ///
    /// 스팀 출시 점검에서 **아이콘이 하나도 없다**는 게 나왔다(`m_BuildTargetIcons: []`).
    /// 그러면 작업표시줄·바탕화면·스팀 라이브러리에 유니티 기본 아이콘이 뜬다.
    ///
    /// 그림은 `docs/design-vault/스토어 아트 프롬프트.md`가 원본이고, 여기서는 배선만 한다.
    /// 유니티가 필요한 크기로 알아서 줄이므로 **1024 원본 한 장만** 넣는다.
    /// </summary>
    public static class AppIconBinder
    {
        private const string ICON_PATH = "Assets/Materials/UI/Icons/AppIcon_1024.png";

        [MenuItem("Tools/CTC/Apply App Icon")]
        public static void Apply()
        {
            if (!File.Exists(ICON_PATH))
            {
                Debug.LogError($"[AppIcon] 아이콘이 없다: {ICON_PATH}");
                return;
            }

            ConfigureImport();

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(ICON_PATH);
            if (icon == null)
            {
                Debug.LogError($"[AppIcon] 텍스처로 못 읽었다: {ICON_PATH}");
                return;
            }

            var target = NamedBuildTarget.Standalone;

            // 크기 슬롯 수는 플랫폼이 정한다 — 전부 같은 원본으로 채우고 축소는 유니티에 맡긴다.
            int[] sizes = PlayerSettings.GetIconSizes(target, IconKind.Any);
            if (sizes == null || sizes.Length == 0)
            {
                Debug.LogError("[AppIcon] 아이콘 슬롯을 못 읽었다");
                return;
            }

            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < icons.Length; i++) icons[i] = icon;

            PlayerSettings.SetIcons(target, icons, IconKind.Any);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AppIcon] 완료 — 슬롯 {sizes.Length}개에 {Path.GetFileName(ICON_PATH)} 배선");
        }

        /// <summary>
        /// 아이콘은 **압축하지 않는다** — 작은 크기로 줄여 쓰는 그림이라 블록 아티팩트가 그대로 보인다.
        /// `isReadable`이 꺼져 있으면 유니티가 빌드 때 축소본을 못 만든다.
        /// </summary>
        private static void ConfigureImport()
        {
            var importer = AssetImporter.GetAtPath(ICON_PATH) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;

            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                dirty = true;
            }
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                dirty = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;   // 아이콘은 3D에 안 붙는다
                dirty = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }
            if (importer.maxTextureSize != 1024)
            {
                importer.maxTextureSize = 1024;
                dirty = true;
            }
            if (!importer.sRGBTexture)
            {
                importer.sRGBTexture = true;
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }
    }
}
