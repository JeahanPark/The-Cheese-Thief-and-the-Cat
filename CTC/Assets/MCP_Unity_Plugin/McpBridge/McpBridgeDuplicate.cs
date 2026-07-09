// McpBridgeDuplicate.cs
// MCP Bridge 핸들러: duplicate
// 씬 또는 프리팹 내 특정 GameObject를 복제하여 같은 부모 하위에 추가한다.
//
// 요청 파라미터:
//   target         : 복제할 원본 GameObject 이름
//   gameObjectName : 복제본에 부여할 새 이름 (생략 시 원본이름 + " (Copy)")
//   prefabPath     : 프리팹 에셋 경로 (씬 오브젝트면 생략)

using UnityEditor;
using UnityEngine;

public static partial class McpBridge
{
    private static string Duplicate(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' is required");

        string newName = string.IsNullOrEmpty(_req.gameObjectName)
            ? _req.target + " (Copy)"
            : _req.gameObjectName;

        // 프리팹 모드
        if (!string.IsNullOrEmpty(_req.prefabPath))
        {
            var prefabContents = PrefabUtility.LoadPrefabContents(_req.prefabPath);
            if (prefabContents == null)
                return BuildError($"Failed to load prefab: '{_req.prefabPath}'");

            try
            {
                GameObject sourceGo = prefabContents.name == _req.target
                    ? prefabContents
                    : FindInHierarchy(prefabContents.transform, _req.target)?.gameObject;

                if (sourceGo == null)
                    return BuildError($"GameObject '{_req.target}' not found in prefab");

                var copy = Object.Instantiate(sourceGo, sourceGo.transform.parent, false);
                copy.name = newName;

                PrefabUtility.SaveAsPrefabAsset(prefabContents, _req.prefabPath);
                return BuildSuccess($"Duplicated '{_req.target}' -> '{newName}'");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        // 씬 모드
        var (sourceScene, _) = FindTarget(_req);
        if (sourceScene == null)
            return BuildError($"GameObject '{_req.target}' not found in scene");

        var sceneCopy = Object.Instantiate(sourceScene, sourceScene.transform.parent, false);
        sceneCopy.name = newName;
        Undo.RegisterCreatedObjectUndo(sceneCopy, "MCP Duplicate GameObject");

        SaveTarget(sceneCopy, null);
        return BuildSuccess($"Duplicated '{_req.target}' -> '{newName}'");
    }
}
