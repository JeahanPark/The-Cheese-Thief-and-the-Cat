// McpBridgeInstantiate.cs
// MCP Bridge 핸들러: instantiate_prefab
// 씬 또는 프리팹의 특정 GameObject 하위에 프리팹 인스턴스를 배치한다.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class McpBridge
{
    private static string InstantiatePrefab(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.prefabPath))
            return BuildError("'prefabPath' is required");

        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' (parentTarget) is required");

        // 배치할 프리팹 로드
        var prefabToInstantiate = AssetDatabase.LoadAssetAtPath<GameObject>(_req.prefabPath);
        if (prefabToInstantiate == null)
            return BuildError($"Prefab not found: '{_req.prefabPath}'");

        // 부모 GameObject 탐색
        GameObject parentGo = null;

        if (!string.IsNullOrEmpty(_req.parentPrefabPath))
        {
            // 프리팹 내부에서 부모 탐색
            var parentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(_req.parentPrefabPath);
            if (parentPrefab == null)
                return BuildError($"Parent prefab not found: '{_req.parentPrefabPath}'");

            parentGo = parentPrefab.name == _req.target
                ? parentPrefab
                : FindInHierarchy(parentPrefab.transform, _req.target);
        }
        else
        {
            // 씬에서 부모 탐색
            foreach (var root in GetAllSceneRoots())
            {
                if (root.name == _req.target)
                {
                    parentGo = root;
                    break;
                }
                var found = FindInHierarchy(root.transform, _req.target);
                if (found != null)
                {
                    parentGo = found;
                    break;
                }
            }
        }

        if (parentGo == null)
            return BuildError($"Parent GameObject not found: '{_req.target}'");

        // 프리팹 인스턴스 생성
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabToInstantiate, parentGo.transform);
        if (instance == null)
            return BuildError("Failed to instantiate prefab");

        // 저장
        if (!string.IsNullOrEmpty(_req.parentPrefabPath))
        {
            var parentPrefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(_req.parentPrefabPath);
            EditorUtility.SetDirty(parentPrefabRoot);
            PrefabUtility.SavePrefabAsset(parentPrefabRoot);
            AssetDatabase.SaveAssets();
        }
        else
        {
            EditorUtility.SetDirty(parentGo);
            EditorSceneManager.MarkSceneDirty(parentGo.scene);
            EditorSceneManager.SaveScene(parentGo.scene);
        }

        return BuildSuccess($"'{prefabToInstantiate.name}' instantiated under '{_req.target}'");
    }
}
