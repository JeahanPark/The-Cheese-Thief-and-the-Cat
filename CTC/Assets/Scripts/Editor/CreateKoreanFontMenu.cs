using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CTC.Editor
{
    /// <summary>
    /// 한글 TMP 폰트 에셋 생성(Pretendard, 동적 아틀라스) + TMP 기본 폰트 교체.
    /// 동적 모드라 글리프는 사용 시점에 추가 — 한글 전량 베이크 불필요.
    /// </summary>
    public static class CreateKoreanFontMenu
    {
        private const string FONT_PATH = "Assets/Fonts/Pretendard-Regular.otf";
        private const string ASSET_PATH = "Assets/Fonts/Pretendard-Regular SDF.asset";
        private const string TMP_SETTINGS_PATH = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("Tools/CTC/Create Korean Font Asset")]
        public static void Create()
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ASSET_PATH);
            if (fontAsset == null)
            {
                Font font = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);
                if (font == null) { Debug.LogError($"[KoreanFont] 폰트 없음: {FONT_PATH}"); return; }

                fontAsset = TMP_FontAsset.CreateFontAsset(
                    font, 60, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                fontAsset.name = "Pretendard-Regular SDF";
                fontAsset.isMultiAtlasTexturesEnabled = true;

                AssetDatabase.CreateAsset(fontAsset, ASSET_PATH);
                fontAsset.material.name = fontAsset.name + " Material";
                fontAsset.atlasTexture.name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }

            // TMP 기본 폰트 교체 — 씬 생성기가 만드는 모든 TMP 텍스트가 이 폰트를 쓴다
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TMP_SETTINGS_PATH);
            if (settings == null) { Debug.LogError($"[KoreanFont] TMP Settings 없음: {TMP_SETTINGS_PATH}"); return; }

            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = fontAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[KoreanFont] Pretendard SDF 생성·기본 폰트 교체 완료");
        }
    }
}
