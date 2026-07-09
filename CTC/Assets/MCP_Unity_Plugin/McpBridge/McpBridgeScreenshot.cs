// McpBridgeScreenshot.cs
// screenshot 핸들러

using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class McpBridge
{
    private static string Screenshot(BridgeRequest _req)
    {
        string savePath = _req.screenshotPath;
        if (string.IsNullOrEmpty(savePath))
            savePath = Path.Combine(Directory.GetCurrentDirectory(), "screenshot.png");

        string dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        ScreenCapture.CaptureScreenshot(savePath);
        return BuildSuccess($"스크린샷 저장됨: {savePath}");
    }
}
