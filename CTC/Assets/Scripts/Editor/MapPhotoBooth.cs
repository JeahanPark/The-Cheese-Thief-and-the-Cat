using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 스토어 스크린샷용 무(無)HUD 촬영 — Tools > CTC > Dev > 맵 사진 한 장 (2026-08-31).
    ///
    /// 게임 뷰 캡처(u_screenshot)는 개발 IMGUI(접속 패널·FPS·디버그 버블)까지 찍힌다.
    /// 여기는 **메인 카메라를 RenderTexture에 직접 렌더**하므로 월드만 나온다 —
    /// 씬 뷰 캡처는 갱신이 안 붙는 문제(CLAUDE.md)도 피해 간다.
    /// 플레이 중에 원하는 자리로 이동/텔레포트한 뒤 한 장씩 누른다.
    /// </summary>
    public static class MapPhotoBooth
    {
        private const int WIDTH = 1920;
        private const int HEIGHT = 1080;
        private const string OUT_DIR = "docs/store-assets/screenshots";

        [MenuItem("Tools/CTC/Dev/맵 사진 한 장 (1920×1080, HUD 없음)")]
        public static void Capture()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[MapPhoto] 플레이 중에만 — 맵은 런타임에 지어진다");
                return;
            }
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[MapPhoto] 메인 카메라가 없다");
                return;
            }

            var rt = new RenderTexture(WIDTH, HEIGHT, 24);
            RenderTexture prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, WIDTH, HEIGHT), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            Directory.CreateDirectory(OUT_DIR);
            string path = $"{OUT_DIR}/shot-{DateTime.Now:HHmmss}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log($"[MapPhoto] 저장: {path}");
        }
    }
}
