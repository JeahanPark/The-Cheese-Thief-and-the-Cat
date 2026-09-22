using System.IO;
using CTC.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace CTC.EditorTools
{
    /// <summary>
    /// UI 둥근 모서리용 9-slice 스프라이트를 코드로 굽는다(2026-09-23). 흰 둥근 사각형 한 장 —
    /// 색은 Image.color가, 모서리 크기는 <see cref="UiCornerLogic"/>가 Image마다 정한다.
    /// 손으로 그린 그림이 아니라 생성물이다 — 모양을 바꾸려면 여기 상수를 고치고 다시 돌린다.
    /// </summary>
    public static class RoundRectSpriteBuilder
    {
        private const string PATH = "Assets/Resources/UI/T_RoundRect.png";
        private const int SIZE = 64;

        [MenuItem("Tools/CTC/UI/Build Round Rect Sprite")]
        public static void Build()
        {
            float radius = UiCornerLogic.SPRITE_RADIUS_PX;
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[SIZE * SIZE];
            for (int y = 0; y < SIZE; y++)
            {
                for (int x = 0; x < SIZE; x++)
                {
                    // 픽셀 중심에서 둥근 사각형까지의 부호 거리 → 1px 폭으로 부드럽게 깎는다(안티앨리어싱)
                    float px = x + 0.5f - SIZE * 0.5f;
                    float py = y + 0.5f - SIZE * 0.5f;
                    float half = SIZE * 0.5f - radius;
                    float qx = Mathf.Abs(px) - half;
                    float qy = Mathf.Abs(py) - half;
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
                    float distance = outside + inside - radius;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance) * 255f);
                    pixels[y * SIZE + x] = new Color32(255, 255, 255, alpha);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(PATH));
            File.WriteAllBytes(PATH, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(PATH, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(PATH);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;   // 캔버스 기준 PPU와 같게 — 배수 계산이 이걸 전제한다
            importer.spriteBorder = new Vector4(radius, radius, radius, radius);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            Debug.Log($"[RoundRectSpriteBuilder] {PATH} — {SIZE}px · 반지름 {radius}px · 9-slice");
        }
    }
}
