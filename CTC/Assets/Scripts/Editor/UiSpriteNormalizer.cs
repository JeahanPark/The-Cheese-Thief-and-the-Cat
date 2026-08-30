using UnityEditor;
using UnityEngine;
using System.IO;

namespace CTC.EditorTools
{
    /// <summary>
    /// UI 텍스처를 Sprite(single)로 일관되게 정규화한다.
    /// Unity 기본 import는 Default/Sprite를 비결정적으로 정하므로 폴더 단위로 강제한다.
    /// </summary>
    public static class UiSpriteNormalizer
    {
        private const string UI_FOLDER = "Assets/Materials/UI";

        [MenuItem("Tools/CTC/Normalize UI Textures to Sprite")]
        public static void NormalizeUiFolder()
        {
            NormalizeFolder(UI_FOLDER);
        }

        public static void NormalizeFolder(string _folder)
        {
            if (!AssetDatabase.IsValidFolder(_folder))
            {
                Debug.LogWarning($"[UiSpriteNormalizer] 폴더 없음: {_folder}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { _folder });
            int changed = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Normalize(path)) changed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[UiSpriteNormalizer] {_folder}: {guids.Length}개 중 {changed}개 갱신");
        }

        public static bool Normalize(string _assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(_assetPath) as TextureImporter;
            if (importer == null) return false;

            bool dirty = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }
            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                dirty = true;
            }
            if (importer.maxTextureSize < 4096)
            {
                importer.maxTextureSize = 4096;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
            return dirty;
        }
    }
}
