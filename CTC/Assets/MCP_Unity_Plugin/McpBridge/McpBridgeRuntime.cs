// McpBridgeRuntime.cs
// MCP Bridge 핸들러: u_play_set_transform | u_editor_set_transform | u_play_click | u_play_invoke
// 런타임/에디터 오브젝트를 직접 조작한다.

using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static partial class McpBridge
{
    // ---- u_play: unified play mode router ----

    private static string RoutePlay(BridgeRequest _req)
    {
        return (_req.subAction ?? "").ToLowerInvariant() switch
        {
            "control" => PlayModeControl(_req),
            "click"   => ClickUi(_req),
            "invoke"  => InvokeRuntime(_req),
            _         => BuildError($"u_play: unknown action '{_req.subAction}'")
        };
    }

    // ---- u_play_set_transform ----

    private static string PlaySetTransform(BridgeRequest _req)
    {
        if (!Application.isPlaying)
            return BuildError("Play Mode 중이 아닙니다.");
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' is required");

        var go = FindRuntimeObject(_req.target);
        if (go == null)
            return BuildError($"GameObject '{_req.target}' not found in scene");

        return ApplyTransform(go, _req);
    }

    // ---- u_editor_set_transform ----

    private static string EditorSetTransform(BridgeRequest _req)
    {
        if (Application.isPlaying)
            return BuildError("에디터 모드에서만 사용 가능합니다.");
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' is required");

        var (go, prefabRoot) = FindTarget(_req);
        if (go == null)
            return BuildError($"GameObject '{_req.target}' not found in scene");

        var result = ApplyTransform(go, _req);

#if UNITY_EDITOR
        if (prefabRoot != null)
            PrefabUtility.SavePrefabAsset(prefabRoot);
        else
            EditorUtility.SetDirty(go);
#endif

        return result;
    }

    // ---- Transform 적용 공통 ----

    private static string ApplyTransform(GameObject _go, BridgeRequest _req)
    {
        var t = _go.transform;
        var sb = new System.Text.StringBuilder();

        if (_req.hasPosX || _req.hasPosY || _req.hasPosZ)
        {
            var pos = t.position;
            if (_req.hasPosX) pos.x = _req.posX;
            if (_req.hasPosY) pos.y = _req.posY;
            if (_req.hasPosZ) pos.z = _req.posZ;
            t.position = pos;
            sb.Append($" position=({pos.x}, {pos.y}, {pos.z})");
        }

        if (_req.hasRotX || _req.hasRotY || _req.hasRotZ)
        {
            var rot = t.eulerAngles;
            if (_req.hasRotX) rot.x = _req.rotX;
            if (_req.hasRotY) rot.y = _req.rotY;
            if (_req.hasRotZ) rot.z = _req.rotZ;
            t.eulerAngles = rot;
            sb.Append($" rotation=({rot.x}, {rot.y}, {rot.z})");
        }

        if (_req.hasScaleX || _req.hasScaleY || _req.hasScaleZ)
        {
            var scale = t.localScale;
            if (_req.hasScaleX) scale.x = _req.scaleX;
            if (_req.hasScaleY) scale.y = _req.scaleY;
            if (_req.hasScaleZ) scale.z = _req.scaleZ;
            t.localScale = scale;
            sb.Append($" scale=({scale.x}, {scale.y}, {scale.z})");
        }

        if (sb.Length == 0)
            return BuildError("변경할 값이 지정되지 않았습니다.");

        return BuildSuccess($"'{_go.name}' Transform 변경:{sb}");
    }

    // ---- u_play_click ----

    private static string ClickUi(BridgeRequest _req)
    {
        if (!Application.isPlaying)
            return BuildError("Play Mode 중이 아닙니다.");
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' is required");

        var go = FindRuntimeObject(_req.target);
        if (go == null)
            return BuildError($"GameObject '{_req.target}' not found in scene");

        var eventData = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(go, eventData, ExecuteEvents.pointerClickHandler);
        return BuildSuccess($"'{go.name}' 클릭 이벤트 발생");
    }

    // ---- u_play_invoke ----

    private static string InvokeRuntime(BridgeRequest _req)
    {
        if (!Application.isPlaying)
            return BuildError("Play Mode 중이 아닙니다.");
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' is required");
        if (string.IsNullOrEmpty(_req.componentType))
            return BuildError("'componentType' is required");
        if (string.IsNullOrEmpty(_req.methodName))
            return BuildError("'methodName' is required");

        var compType = FindComponentType(_req.componentType);
        if (compType == null)
            return BuildError($"Component type '{_req.componentType}' not found");

        // target이 "@type" 또는 "*"이면 GameObject 탐색 없이 컴포넌트 타입으로 직접 FindFirstObjectByType
        Component comp;
        if (_req.target == "*" || _req.target.StartsWith("@"))
        {
            comp = (Component)UnityEngine.Object.FindFirstObjectByType(compType);
            if (comp == null)
                return BuildError($"Component '{_req.componentType}' not found in any scene (including DontDestroyOnLoad)");
        }
        else
        {
            var go = FindRuntimeObject(_req.target);
            if (go == null)
                return BuildError($"GameObject '{_req.target}' not found in scene");
            comp = go.GetComponent(compType);
            if (comp == null)
                return BuildError($"Component '{_req.componentType}' not found on '{_req.target}'");
        }

        var method = compType.GetMethod(_req.methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
            return BuildError($"Method '{_req.methodName}' not found on '{_req.componentType}'");

        var parameters = method.GetParameters();

        object[] methodArgs = null;
        if (parameters.Length > 0)
        {
            methodArgs = new object[parameters.Length];
            string[] parts = string.IsNullOrEmpty(_req.args)
                ? System.Array.Empty<string>()
                : _req.args.Split(',');

            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < parts.Length)
                {
                    string raw = parts[i].Trim();
                    var paramType = parameters[i].ParameterType;
                    if (raw == "null" || raw == "")
                    {
                        methodArgs[i] = paramType.IsValueType
                            ? Activator.CreateInstance(paramType)
                            : null;
                    }
                    else
                    {
                        try { methodArgs[i] = Convert.ChangeType(raw, paramType); }
                        catch
                        {
                            methodArgs[i] = paramType.IsValueType
                                ? Activator.CreateInstance(paramType)
                                : null;
                        }
                    }
                }
                else
                {
                    var paramType = parameters[i].ParameterType;
                    methodArgs[i] = paramType.IsValueType
                        ? Activator.CreateInstance(paramType)
                        : null;
                }
            }
        }

        try
        {
            var result = method.Invoke(comp, methodArgs);
            string resultStr = result != null ? result.ToString() : "void";
            return BuildSuccess($"'{_req.componentType}.{_req.methodName}' 호출 완료. 반환값: {resultStr}");
        }
        catch (TargetInvocationException e)
        {
            return BuildError($"메서드 실행 중 예외: {e.InnerException?.Message ?? e.Message}");
        }
    }

    // ---- 헬퍼: 런타임 씬에서 이름으로 GameObject 탐색 (DontDestroyOnLoad 포함) ----

    private static GameObject FindRuntimeObject(string _name)
    {
        if (_name.Contains('/'))
        {
            var parts = _name.Split('/');
            var roots = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var root in roots)
            {
                if (root.transform.parent != null) continue;
                if (root.name != parts[0]) continue;
                Transform current = root.transform;
                bool found = true;
                for (int i = 1; i < parts.Length; i++)
                {
                    Transform next = null;
                    foreach (Transform child in current)
                    {
                        if (child.name == parts[i]) { next = child; break; }
                    }
                    if (next == null) { found = false; break; }
                    current = next;
                }
                if (found) return current.gameObject;
            }
            return null;
        }

        // GameObject.Find는 DontDestroyOnLoad를 못 찾을 수 있으므로 FindObjectsByType으로 폴백
        var found_go = GameObject.Find(_name);
        if (found_go != null) return found_go;

        var all = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in all)
        {
            if (go.transform.parent == null && go.name == _name)
                return go;
        }
        return null;
    }
}
