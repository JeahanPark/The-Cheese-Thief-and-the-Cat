// McpBridgeInputAction.cs
// McpBridge partial - InputActionAsset(.inputactions) 편집 핸들러.
// actions: add_action | remove_action | list_actions

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class McpBridge
{
    // ---- u_editor_input 라우터 ----

    private static string RouteInput(BridgeRequest _req)
    {
        return (_req.subAction ?? "").ToLowerInvariant() switch
        {
            "add_action"    => AddInputAction(_req),
            "remove_action" => RemoveInputAction(_req),
            "list_actions"  => ListInputActions(_req),
            _               => BuildError($"u_editor_input: unknown subAction '{_req.subAction}'")
        };
    }

    // ---- add_action ----

    private static string AddInputAction(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.assetPath))
            return BuildError("'assetPath' (.inputactions file path) is required");
        if (string.IsNullOrEmpty(_req.actionMap))
            return BuildError("'actionMap' is required");
        if (string.IsNullOrEmpty(_req.actionName))
            return BuildError("'actionName' is required");

        string fullPath = Path.GetFullPath(_req.assetPath);
        if (!File.Exists(fullPath))
            return BuildError($"File not found: '{_req.assetPath}'");

        try
        {
            string json = File.ReadAllText(fullPath);
            var asset = JsonUtility.FromJson<InputActionsJson>(json);
            if (asset == null || asset.maps == null)
                return BuildError("Failed to parse .inputactions file");

            // 대상 ActionMap 찾기
            InputActionMapJson targetMap = null;
            foreach (var map in asset.maps)
            {
                if (map.name == _req.actionMap)
                {
                    targetMap = map;
                    break;
                }
            }

            if (targetMap == null)
                return BuildError($"ActionMap '{_req.actionMap}' not found");

            // 중복 확인
            if (targetMap.actions != null)
            {
                foreach (var existingAction in targetMap.actions)
                {
                    if (existingAction.name == _req.actionName)
                        return BuildError($"Action '{_req.actionName}' already exists in '{_req.actionMap}'");
                }
            }

            // 새 액션 추가
            var newAction = new InputActionJson
            {
                name = _req.actionName,
                type = string.IsNullOrEmpty(_req.actionType) ? "Button" : _req.actionType,
                id = Guid.NewGuid().ToString(),
                expectedControlType = "",
                processors = "",
                interactions = ""
            };

            var actionList = targetMap.actions != null
                ? new List<InputActionJson>(targetMap.actions)
                : new List<InputActionJson>();
            actionList.Add(newAction);
            targetMap.actions = actionList.ToArray();

            // 바인딩 추가 (bindingPath가 있는 경우)
            if (!string.IsNullOrEmpty(_req.bindingPath))
            {
                var newBinding = new InputBindingJson
                {
                    name = "",
                    id = Guid.NewGuid().ToString(),
                    path = _req.bindingPath,
                    interactions = "",
                    processors = "",
                    groups = "",
                    action = _req.actionName,
                    isComposite = false,
                    isPartOfComposite = false
                };

                var bindingList = targetMap.bindings != null
                    ? new List<InputBindingJson>(targetMap.bindings)
                    : new List<InputBindingJson>();
                bindingList.Add(newBinding);
                targetMap.bindings = bindingList.ToArray();
            }

            // 저장
            string output = JsonUtility.ToJson(asset, true);
            File.WriteAllText(fullPath, output);
            AssetDatabase.Refresh();

            string bindingInfo = !string.IsNullOrEmpty(_req.bindingPath)
                ? $" with binding '{_req.bindingPath}'"
                : "";
            return BuildSuccess($"Action '{_req.actionName}' added to '{_req.actionMap}'{bindingInfo}");
        }
        catch (Exception e)
        {
            return BuildError($"Failed to edit .inputactions: {e.Message}");
        }
    }

    // ---- remove_action ----

    private static string RemoveInputAction(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.assetPath))
            return BuildError("'assetPath' (.inputactions file path) is required");
        if (string.IsNullOrEmpty(_req.actionMap))
            return BuildError("'actionMap' is required");
        if (string.IsNullOrEmpty(_req.actionName))
            return BuildError("'actionName' is required");

        string fullPath = Path.GetFullPath(_req.assetPath);
        if (!File.Exists(fullPath))
            return BuildError($"File not found: '{_req.assetPath}'");

        try
        {
            string json = File.ReadAllText(fullPath);
            var asset = JsonUtility.FromJson<InputActionsJson>(json);
            if (asset == null || asset.maps == null)
                return BuildError("Failed to parse .inputactions file");

            InputActionMapJson targetMap = null;
            foreach (var map in asset.maps)
            {
                if (map.name == _req.actionMap) { targetMap = map; break; }
            }
            if (targetMap == null)
                return BuildError($"ActionMap '{_req.actionMap}' not found");

            // 액션 제거
            bool found = false;
            if (targetMap.actions != null)
            {
                var actionList = new List<InputActionJson>(targetMap.actions);
                found = actionList.RemoveAll(a => a.name == _req.actionName) > 0;
                targetMap.actions = actionList.ToArray();
            }

            if (!found)
                return BuildError($"Action '{_req.actionName}' not found in '{_req.actionMap}'");

            // 관련 바인딩도 제거
            if (targetMap.bindings != null)
            {
                var bindingList = new List<InputBindingJson>(targetMap.bindings);
                bindingList.RemoveAll(b => b.action == _req.actionName);
                targetMap.bindings = bindingList.ToArray();
            }

            string output = JsonUtility.ToJson(asset, true);
            File.WriteAllText(fullPath, output);
            AssetDatabase.Refresh();

            return BuildSuccess($"Action '{_req.actionName}' removed from '{_req.actionMap}'");
        }
        catch (Exception e)
        {
            return BuildError($"Failed to edit .inputactions: {e.Message}");
        }
    }

    // ---- list_actions ----

    private static string ListInputActions(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.assetPath))
            return BuildError("'assetPath' (.inputactions file path) is required");

        string fullPath = Path.GetFullPath(_req.assetPath);
        if (!File.Exists(fullPath))
            return BuildError($"File not found: '{_req.assetPath}'");

        try
        {
            string json = File.ReadAllText(fullPath);
            var asset = JsonUtility.FromJson<InputActionsJson>(json);
            if (asset == null || asset.maps == null)
                return BuildError("Failed to parse .inputactions file");

            var result = new List<string>();
            foreach (var map in asset.maps)
            {
                if (!string.IsNullOrEmpty(_req.actionMap) && map.name != _req.actionMap)
                    continue;

                if (map.actions != null)
                {
                    foreach (var action in map.actions)
                    {
                        result.Add($"{map.name}/{action.name} ({action.type})");
                    }
                }
            }

            string list = string.Join(", ", result);
            return BuildSuccess($"Actions: {list}");
        }
        catch (Exception e)
        {
            return BuildError($"Failed to read .inputactions: {e.Message}");
        }
    }

    // ---- InputActions JSON DTOs ----

    [Serializable]
    private class InputActionsJson
    {
        public string name;
        public InputActionMapJson[] maps;
    }

    [Serializable]
    private class InputActionMapJson
    {
        public string name;
        public string id;
        public InputActionJson[] actions;
        public InputBindingJson[] bindings;
    }

    [Serializable]
    private class InputActionJson
    {
        public string name;
        public string type;
        public string id;
        public string expectedControlType;
        public string processors;
        public string interactions;
    }

    [Serializable]
    private class InputBindingJson
    {
        public string name;
        public string id;
        public string path;
        public string interactions;
        public string processors;
        public string groups;
        public string action;
        public bool isComposite;
        public bool isPartOfComposite;
    }
}
