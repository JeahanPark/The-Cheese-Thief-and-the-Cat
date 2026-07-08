// McpBridgeSceneView.cs
// u_editor_sceneview 핸들러 — Scene View 캡처 + 카메라 시점 제어

using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class McpBridge
{
    private static string RouteSceneView(BridgeRequest _req)
    {
        string sub = (_req.subAction ?? "").ToLowerInvariant();
        return sub switch
        {
            "screenshot" => SceneViewScreenshot(_req),
            "focus"      => SceneViewFocus(_req),
            "preset"     => SceneViewPreset(_req),
            "get"        => SceneViewGet(_req),
            _            => BuildError($"Unknown subAction: '{_req.subAction}'. Use: screenshot|focus|preset|get")
        };
    }

    // ---- subAction 핸들러 ----

    private static string SceneViewScreenshot(BridgeRequest _req)
    {
        string savePath = _req.screenshotPath;
        if (string.IsNullOrEmpty(savePath))
            savePath = Path.Combine(Directory.GetCurrentDirectory(), "screenshot_sceneview.png");

        string dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var sv = GetActiveSceneView();
        if (sv == null) return BuildError("SceneView를 열 수 없습니다");

        var cam = sv.camera;
        if (cam == null) return BuildError("SceneView 카메라가 없습니다");

        int w = Mathf.Max((int)cam.pixelWidth, 1);
        int h = Mathf.Max((int)cam.pixelHeight, 1);

        RenderTexture rt = null;
        Texture2D tex = null;
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;

        try
        {
            rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;

            tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(savePath, png);
        }
        catch (System.Exception e)
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            if (rt != null) Object.DestroyImmediate(rt);
            if (tex != null) Object.DestroyImmediate(tex);
            return BuildError($"스크린샷 실패: {e.Message}");
        }

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        if (rt != null) Object.DestroyImmediate(rt);
        if (tex != null) Object.DestroyImmediate(tex);

        return BuildSuccess($"씬뷰 스크린샷 저장됨: {savePath}");
    }

    private static string SceneViewFocus(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' is required for focus");

        var (go, _) = FindTarget(_req);
        if (go == null)
            return BuildError($"Target '{_req.target}' not found");

        if (!TryGetRotationByPreset(_req.angle ?? "persp", out Quaternion rot))
            return BuildError($"Invalid angle '{_req.angle}'. Use: top|front|side|persp");

        Bounds bounds = ComputeBounds(go);
        float distance = Mathf.Max(bounds.extents.magnitude * 1.5f, 1f);

        var sv = GetActiveSceneView();
        if (sv == null) return BuildError("SceneView를 열 수 없습니다");

        sv.LookAt(bounds.center, rot, distance);
        sv.Repaint();

        return BuildSuccess(
            $"씬뷰 정렬됨: target={go.name}, pivot=({bounds.center.x:F2},{bounds.center.y:F2},{bounds.center.z:F2}), size={distance:F2}");
    }

    private static string SceneViewPreset(BridgeRequest _req)
    {
        if (!TryGetRotationByPreset(_req.view, out Quaternion rot))
            return BuildError($"Invalid view '{_req.view}'. Use: top|front|side|persp");

        var sv = GetActiveSceneView();
        if (sv == null) return BuildError("SceneView를 열 수 없습니다");

        Vector3 pivot = sv.pivot;
        float size = _req.hasSize ? _req.size : sv.size;

        sv.LookAt(pivot, rot, size);
        sv.Repaint();

        return BuildSuccess(
            $"씬뷰 시점 전환됨: view={_req.view}, pivot=({pivot.x:F2},{pivot.y:F2},{pivot.z:F2}), size={size:F2}");
    }

    private static string SceneViewGet(BridgeRequest _req)
    {
        var sv = GetActiveSceneView();
        if (sv == null) return BuildError("SceneView를 열 수 없습니다");

        Vector3 pivot = sv.pivot;
        Vector3 euler = sv.rotation.eulerAngles;
        return "{" +
            "\"success\":true," +
            $"\"pivot\":{{\"x\":{pivot.x:F4},\"y\":{pivot.y:F4},\"z\":{pivot.z:F4}}}," +
            $"\"rotation_euler\":{{\"x\":{euler.x:F4},\"y\":{euler.y:F4},\"z\":{euler.z:F4}}}," +
            $"\"size\":{sv.size:F4}," +
            $"\"in2DMode\":{(sv.in2DMode ? "true" : "false")}," +
            $"\"orthographic\":{(sv.orthographic ? "true" : "false")}" +
            "}";
    }

    // ---- 헬퍼 ----

    private static SceneView GetActiveSceneView()
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null)
        {
            sv = EditorWindow.GetWindow<SceneView>(utility: false, title: null, focus: false);
        }
        return sv;
    }

    private static bool TryGetRotationByPreset(string _key, out Quaternion _rot)
    {
        switch ((_key ?? "persp").ToLowerInvariant())
        {
            case "top":   _rot = Quaternion.LookRotation(Vector3.down); return true;
            case "front": _rot = Quaternion.Euler(0, 180, 0);          return true;
            case "side":  _rot = Quaternion.Euler(0, 90, 0);           return true;
            case "persp": _rot = Quaternion.Euler(30, 45, 0);          return true;
            default:      _rot = Quaternion.identity;                   return false;
        }
    }

    private static Bounds ComputeBounds(GameObject _go)
    {
        var renderers = _go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(_go.transform.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
