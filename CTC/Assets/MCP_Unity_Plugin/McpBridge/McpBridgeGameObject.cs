// McpBridgeGameObject.cs
// MCP Bridge 핸들러: u_editor_gameobject
// 씬 또는 프리팹의 GameObject를 생성/활성화/삭제/이름변경/복제한다.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class McpBridge
{
    // ---- u_editor_gameobject 라우터 ----

    private static string RouteGameObject(BridgeRequest _req)
    {
        return (_req.subAction ?? "").ToLowerInvariant() switch
        {
            "add"        => AddGameObject(_req),
            "delete"     => DeleteGameObject(_req),
            "rename"     => RenameGameObject(_req),
            "set_active" => SetActive(_req),
            "duplicate"   => Duplicate(_req),
            "set_parent"  => SetParent(_req),
            _             => BuildError($"u_editor_gameobject: unknown subAction '{_req.subAction}'")
        };
    }

    // ---- set_active ----

    private static string SetActive(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' is required");
        if (string.IsNullOrEmpty(_req.propertyValue))
            return BuildError("'propertyValue' is required (\"true\" or \"false\")");

        if (!bool.TryParse(_req.propertyValue, out bool active))
            return BuildError($"'propertyValue' must be \"true\" or \"false\", got: \"{_req.propertyValue}\"");

        if (!string.IsNullOrEmpty(_req.prefabPath))
        {
            var prefabContents = PrefabUtility.LoadPrefabContents(_req.prefabPath);
            if (prefabContents == null)
                return BuildError($"Failed to load prefab: '{_req.prefabPath}'");

            try
            {
                var targetGo = (_req.target == prefabContents.name)
                    ? prefabContents
                    : FindInHierarchy(prefabContents.transform, _req.target)?.gameObject;

                if (targetGo == null)
                    return BuildError($"GameObject '{_req.target}' not found in prefab");

                targetGo.SetActive(active);
                PrefabUtility.SaveAsPrefabAsset(prefabContents, _req.prefabPath);
                return BuildSuccess($"'{_req.target}' set active={active}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        var (go, prefabRoot) = FindTarget(_req);
        if (go == null)
            return BuildError($"GameObject '{_req.target}' not found");

        Undo.RecordObject(go, "MCP Set Active");
        go.SetActive(active);
        SaveTarget(go, prefabRoot);
        return BuildSuccess($"'{go.name}' set active={active}");
    }

    // ---- delete_gameobject ----

    private static string DeleteGameObject(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' is required");

        if (!string.IsNullOrEmpty(_req.prefabPath))
        {
            var prefabContents = PrefabUtility.LoadPrefabContents(_req.prefabPath);
            if (prefabContents == null)
                return BuildError($"Failed to load prefab: '{_req.prefabPath}'");

            try
            {
                // 프리팹 루트 자체는 삭제 불가
                if (_req.target == prefabContents.name)
                    return BuildError("Cannot delete the prefab root GameObject");

                var targetGo = FindInHierarchy(prefabContents.transform, _req.target)?.gameObject;
                if (targetGo == null)
                    return BuildError($"GameObject '{_req.target}' not found in prefab");

                Object.DestroyImmediate(targetGo);
                PrefabUtility.SaveAsPrefabAsset(prefabContents, _req.prefabPath);
                return BuildSuccess($"GameObject '{_req.target}' deleted from prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        var (go, prefabRoot) = FindTarget(_req);
        if (go == null)
            return BuildError($"GameObject '{_req.target}' not found");

        string deletedName = go.name;
        Undo.DestroyObjectImmediate(go);
        return BuildSuccess($"GameObject '{deletedName}' deleted");
    }

    // ---- rename_gameobject ----

    private static string RenameGameObject(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' is required");
        if (string.IsNullOrEmpty(_req.gameObjectName))
            return BuildError("'gameObjectName' (new name) is required");

        if (!string.IsNullOrEmpty(_req.prefabPath))
        {
            var prefabContents = PrefabUtility.LoadPrefabContents(_req.prefabPath);
            if (prefabContents == null)
                return BuildError($"Failed to load prefab: '{_req.prefabPath}'");

            try
            {
                var targetGo = (_req.target == prefabContents.name)
                    ? prefabContents
                    : FindInHierarchy(prefabContents.transform, _req.target)?.gameObject;

                if (targetGo == null)
                    return BuildError($"GameObject '{_req.target}' not found in prefab");

                string oldName = targetGo.name;
                targetGo.name = _req.gameObjectName;
                PrefabUtility.SaveAsPrefabAsset(prefabContents, _req.prefabPath);
                return BuildSuccess($"Renamed '{oldName}' → '{_req.gameObjectName}'");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        var (go, prefabRoot) = FindTarget(_req);
        if (go == null)
            return BuildError($"GameObject '{_req.target}' not found");

        string prevName = go.name;
        Undo.RecordObject(go, "MCP Rename GameObject");
        go.name = _req.gameObjectName;
        SaveTarget(go, prefabRoot);
        return BuildSuccess($"Renamed '{prevName}' → '{_req.gameObjectName}'");
    }

    // ---- add_gameobject ----

    private static string AddGameObject(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' (parentTarget) is required");

        if (string.IsNullOrEmpty(_req.gameObjectName))
            return BuildError("'gameObjectName' is required");

        // 프리팹 모드: LoadPrefabContents로 격리 편집 후 저장
        if (!string.IsNullOrEmpty(_req.prefabPath))
        {
            var prefabContents = PrefabUtility.LoadPrefabContents(_req.prefabPath);
            if (prefabContents == null)
                return BuildError($"Failed to load prefab: '{_req.prefabPath}'");

            try
            {
                GameObject parentGo;
                if (string.IsNullOrEmpty(_req.target) || _req.target == prefabContents.name)
                    parentGo = prefabContents;
                else
                    parentGo = FindInHierarchy(prefabContents.transform, _req.target)?.gameObject;

                if (parentGo == null)
                    return BuildError($"Parent GameObject not found: '{_req.target}'");

                GameObject newGo;
                if (parentGo.GetComponent<RectTransform>() != null)
                {
                    newGo = new GameObject(_req.gameObjectName, typeof(RectTransform));
                }
                else
                {
                    newGo = new GameObject(_req.gameObjectName);
                }
                newGo.transform.SetParent(parentGo.transform, false);

                PrefabUtility.SaveAsPrefabAsset(prefabContents, _req.prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            return BuildSuccess($"GameObject '{_req.gameObjectName}' added under '{_req.target}'");
        }

        // 씬 모드: target이 "_root_"이면 씬 루트에 추가
        if (_req.target == "_root_")
        {
            var newRootGo = new GameObject(_req.gameObjectName);
            Undo.RegisterCreatedObjectUndo(newRootGo, "MCP Add GameObject");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                newRootGo,
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorUtility.SetDirty(newRootGo);
            EditorSceneManager.MarkSceneDirty(newRootGo.scene);
            return BuildSuccess($"GameObject '{_req.gameObjectName}' added to scene root");
        }

        var (sceneParent, _) = FindTarget(_req);
        if (sceneParent == null)
            return BuildError($"Parent GameObject not found: '{_req.target}'");

        GameObject newSceneGo;
        if (sceneParent.GetComponent<RectTransform>() != null)
        {
            newSceneGo = new GameObject(_req.gameObjectName, typeof(RectTransform));
        }
        else
        {
            newSceneGo = new GameObject(_req.gameObjectName);
        }
        Undo.RegisterCreatedObjectUndo(newSceneGo, "MCP Add GameObject");
        newSceneGo.transform.SetParent(sceneParent.transform, false);

        SaveTarget(newSceneGo, null);
        return BuildSuccess($"GameObject '{_req.gameObjectName}' added under '{_req.target}'");
    }

    // ---- set_parent ----

    private static string SetParent(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' (child to move) is required");
        if (string.IsNullOrEmpty(_req.newParent))
            return BuildError("'newParent' is required");

        if (!string.IsNullOrEmpty(_req.prefabPath))
        {
            var prefabContents = PrefabUtility.LoadPrefabContents(_req.prefabPath);
            if (prefabContents == null)
                return BuildError($"Failed to load prefab: '{_req.prefabPath}'");

            try
            {
                var childGo = (_req.target == prefabContents.name)
                    ? prefabContents
                    : FindInHierarchy(prefabContents.transform, _req.target)?.gameObject;

                if (childGo == null)
                    return BuildError($"GameObject '{_req.target}' not found in prefab");
                if (childGo == prefabContents)
                    return BuildError("Cannot reparent the prefab root GameObject");

                var newParentGo = (_req.newParent == prefabContents.name)
                    ? prefabContents
                    : FindInHierarchy(prefabContents.transform, _req.newParent)?.gameObject;

                if (newParentGo == null)
                    return BuildError($"New parent '{_req.newParent}' not found in prefab");

                childGo.transform.SetParent(newParentGo.transform, false);
                PrefabUtility.SaveAsPrefabAsset(prefabContents, _req.prefabPath);
                return BuildSuccess($"'{_req.target}' reparented under '{_req.newParent}'");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        // 씬 모드
        var (childScene, _) = FindTarget(_req);
        if (childScene == null)
            return BuildError($"GameObject '{_req.target}' not found");

        // newParent를 찾기 위해 임시 request
        var parentReq = new BridgeRequest { target = _req.newParent };
        var (parentScene, _2) = FindTarget(parentReq);
        if (parentScene == null)
            return BuildError($"New parent '{_req.newParent}' not found");

        Undo.SetTransformParent(childScene.transform, parentScene.transform, "MCP Set Parent");
        childScene.transform.SetParent(parentScene.transform, false);
        SaveTarget(childScene, null);
        return BuildSuccess($"'{_req.target}' reparented under '{_req.newParent}'");
    }
}
