using System;
using UnityEngine;

namespace WesQA
{
    /// <summary>합성 프레임 캡처→요청 width 리사이즈→JPG→base64. end-of-frame 코루틴에서만 호출.</summary>
    public static class Screenshotter
    {
        public static object[] Capture(int width)
        {
            var src = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Texture2D outTex = src;
                bool resized = false;
                if (width > 0 && width < src.width)
                {
                    int height = Mathf.Max(1, Mathf.RoundToInt(src.height * (width / (float)src.width)));
                    var rt = RenderTexture.GetTemporary(width, height);
                    Graphics.Blit(src, rt);
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    outTex = new Texture2D(width, height, TextureFormat.RGB24, false);
                    outTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    outTex.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                    resized = true;
                }
                byte[] jpg = outTex.EncodeToJPG(80);
                if (resized) UnityEngine.Object.Destroy(outTex);
                return new object[] { Convert.ToBase64String(jpg), "jpg" };
            }
            finally
            {
                UnityEngine.Object.Destroy(src);
            }
        }
    }
}
