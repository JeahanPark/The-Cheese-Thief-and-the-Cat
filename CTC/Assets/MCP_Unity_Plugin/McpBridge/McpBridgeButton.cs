// McpBridgeButton.cs
// MCP Bridge 핸들러: connect_button
// Unity Button 컴포넌트의 onClick 이벤트에 특정 컴포넌트의 메서드를 Persistent Listener로 연결한다.

using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static partial class McpBridge
{
    private static string ConnectButton(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' (Button GameObject) is required");
        if (string.IsNullOrEmpty(_req.listenerTarget))
            return BuildError("'listenerTarget' is required");
        if (string.IsNullOrEmpty(_req.listenerComponent))
            return BuildError("'listenerComponent' is required");
        if (string.IsNullOrEmpty(_req.methodName))
            return BuildError("'methodName' is required");

        // Button GameObject 탐색
        var (buttonGo, prefabRoot) = FindTarget(_req);
        if (buttonGo == null)
            return BuildError($"Button target '{_req.target}' not found");

        var button = buttonGo.GetComponent<Button>();
        if (button == null)
            return BuildError($"No Button component on '{_req.target}'");

        // Listener GameObject 탐색 (같은 prefabPath 또는 씬에서 검색)
        var listenerGo = FindGameObjectByName(_req.listenerTarget, _req.prefabPath);
        if (listenerGo == null)
            return BuildError($"Listener target '{_req.listenerTarget}' not found");

        // 컴포넌트 탐색
        var componentType = FindComponentType(_req.listenerComponent);
        if (componentType == null)
            return BuildError($"Component type '{_req.listenerComponent}' not found");

        var listenerComp = listenerGo.GetComponent(componentType);
        if (listenerComp == null)
            return BuildError($"Component '{_req.listenerComponent}' not found on '{_req.listenerTarget}'");

        // 메서드 탐색 (파라미터 없는 void 메서드)
        var method = componentType.GetMethod(
            _req.methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        if (method == null)
            return BuildError($"Parameterless method '{_req.methodName}' not found on '{_req.listenerComponent}'");

        // Persistent Listener 등록
        Undo.RecordObject(button, "MCP Connect Button");
        var action = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), listenerComp, method);
        UnityEventTools.AddPersistentListener(button.onClick, action);

        SaveTarget(buttonGo, prefabRoot);
        return BuildSuccess($"Button '{_req.target}'.onClick → '{_req.listenerComponent}.{_req.methodName}'");
    }

    private static GameObject FindGameObjectByName(string _name, string _prefabPath)
    {
        if (!string.IsNullOrEmpty(_prefabPath))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (prefab == null)
                return null;

            if (prefab.name == _name)
                return prefab;

            return FindInHierarchy(prefab.transform, _name);
        }

        foreach (var root in GetAllSceneRoots())
        {
            if (root.name == _name)
                return root;

            var found = FindInHierarchy(root.transform, _name);
            if (found != null)
                return found;
        }

        return null;
    }
}
