// McpBridgeScene.cs
// scene_open / scene_save / scene_create 핸들러

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class McpBridge
{
    // ---- u_editor_scene 라우터 ----

    private static string RouteScene(BridgeRequest _req)
    {
        return (_req.subAction ?? "").ToLowerInvariant() switch
        {
            "open"   => SceneOpen(_req),
            "save"   => SceneSave(_req),
            "create" => SceneCreate(_req),
            _        => BuildError($"u_editor_scene: unknown subAction '{_req.subAction}'")
        };
    }

    private static string SceneOpen(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.scenePath))
            return BuildError("scenePath is required");

        if (!File.Exists(_req.scenePath))
            return BuildError($"씬 파일을 찾을 수 없습니다: {_req.scenePath}");

        bool saved = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        if (!saved)
            return BuildError("현재 씬 저장이 취소되었습니다");

        var scene = EditorSceneManager.OpenScene(_req.scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
            return BuildError($"씬을 열지 못했습니다: {_req.scenePath}");

        return BuildSuccess($"씬 열기 완료: {scene.name}");
    }

    private static string SceneSave(BridgeRequest _req)
    {
        bool result = EditorSceneManager.SaveOpenScenes();
        return result
            ? BuildSuccess("씬 저장 완료")
            : BuildError("씬 저장 실패");
    }

    private static string SceneCreate(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.scenePath))
            return BuildError("scenePath is required");

        string dir = Path.GetDirectoryName(_req.scenePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        bool saved = EditorSceneManager.SaveScene(newScene, _req.scenePath);
        if (!saved)
            return BuildError($"씬 저장 실패: {_req.scenePath}");

        AssetDatabase.Refresh();
        return BuildSuccess($"씬 생성 완료: {_req.scenePath}");
    }
}
