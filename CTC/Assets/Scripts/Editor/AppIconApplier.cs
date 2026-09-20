using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 게임 실행 아이콘 = **게임 이름 로고**(2026-09-19 사용자 결정 — 전에는 치즈 든 쥐 그림).
    /// 큰 크기는 「CANADIAN / MOUSE」 두 줄, **48px 이하**(창 제목줄·작업 표시줄)는 두 줄이 뭉개져 안 읽혀서 「CM」 머리글자를 쓴다.
    /// 그림은 스토어 로고(`docs/store-assets/library-logo-1280x720.png`)와 같은 노랑·검은 테두리·Arial Black이다.
    /// </summary>
    public static class AppIconApplier
    {
        private const string FULL = "Assets/Materials/UI/Icons/AppIcon_Name_1024.png";
        private const string SMALL = "Assets/Materials/UI/Icons/AppIcon_NameSmall_1024.png";
        private const int SMALL_MAX_PX = 48;

        [MenuItem("Tools/CTC/Apply App Icon (이름 로고)")]
        public static void Apply()
        {
            Configure(FULL);
            Configure(SMALL);
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(FULL);
            var small = AssetDatabase.LoadAssetAtPath<Texture2D>(SMALL);
            if (full == null || small == null)
            {
                Debug.LogError($"[AppIconApplier] 아이콘 그림이 없다: {FULL} / {SMALL}");
                return;
            }

            int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < sizes.Length; i++) icons[i] = sizes[i] <= SMALL_MAX_PX ? small : full;
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Any);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);   // 기본 아이콘
            AssetDatabase.SaveAssets();
            Debug.Log($"[AppIconApplier] 실행 아이콘 교체 — 크기 {string.Join(",", sizes)} (≤{SMALL_MAX_PX}px는 CM)");
        }

        // 아이콘은 원본 그대로 쓴다 — 압축·밉맵·2의 거듭제곱 맞춤이 끼면 작은 크기에서 글자가 뭉개진다.
        private static void Configure(string _path)
        {
            if (!(AssetImporter.GetAtPath(_path) is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
