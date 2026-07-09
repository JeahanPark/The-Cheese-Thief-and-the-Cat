// McpBridgeComponents.cs
// McpBridge partial - 컴포넌트 추가/제거/프로퍼티 설정 핸들러.
// actions: add | remove | set_property | list

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static partial class McpBridge
{
    // ---- u_editor_component 라우터 ----

    private static string RouteComponent(BridgeRequest _req)
    {
        return (_req.subAction ?? "").ToLowerInvariant() switch
        {
            "add"          => AddComponent(_req),
            "remove"       => RemoveComponent(_req),
            "set_property" => SetProperty(_req),
            "list"         => ListComponents(_req),
            _              => BuildError($"u_editor_component: unknown subAction '{_req.subAction}'")
        };
    }

    private static string AddComponent(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.componentType))
            return BuildError("'componentType' is required");

        var type = FindComponentType(_req.componentType);
        if (type == null)
            return BuildError($"Component type '{_req.componentType}' not found");

        var (go, prefabRoot) = FindTarget(_req);
        if (go == null)
            return BuildError($"Target '{_req.target}' not found");

        if (go.GetComponent(type) != null)
            return BuildError($"'{_req.componentType}' is already attached to '{go.name}'");

        Undo.RecordObject(go, "MCP Add Component");
        var component = go.AddComponent(type);
        if (component == null)
            return BuildError($"Failed to add '{_req.componentType}' (check for missing dependencies)");

        SaveTarget(go, prefabRoot);
        return BuildSuccess($"Component '{_req.componentType}' added to '{go.name}'");
    }

    private static string RemoveComponent(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.componentType))
            return BuildError("'componentType' is required");

        var type = FindComponentType(_req.componentType);
        if (type == null)
            return BuildError($"Component type '{_req.componentType}' not found");

        var (go, prefabRoot) = FindTarget(_req);
        if (go == null)
            return BuildError($"Target '{_req.target}' not found");

        var component = go.GetComponent(type);
        if (component == null)
            return BuildError($"Component '{_req.componentType}' not found on '{go.name}'");

        Undo.DestroyObjectImmediate(component);
        SaveTarget(go, prefabRoot);
        return BuildSuccess($"Component '{_req.componentType}' removed from '{go.name}'");
    }

    private static string SetProperty(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.componentType))
            return BuildError("'componentType' is required");
        if (string.IsNullOrEmpty(_req.propertyName))
            return BuildError("'propertyName' is required");

        var type = FindComponentType(_req.componentType);
        if (type == null)
            return BuildError($"Component type '{_req.componentType}' not found");

        var (go, prefabRoot) = FindTarget(_req);
        if (go == null)
            return BuildError($"Target '{_req.target}' not found");

        var component = go.GetComponent(type);
        if (component == null)
            return BuildError($"Component '{_req.componentType}' not found on '{go.name}'");

        Undo.RecordObject(component, "MCP Set Property");
        var error = TrySetMember(component, _req.propertyName, _req.propertyValue);
        if (error != null)
            return BuildError(error);

        SaveTarget(go, prefabRoot);
        return BuildSuccess($"'{_req.propertyName}' set on '{_req.componentType}' of '{go.name}'");
    }

    private static string ListComponents(BridgeRequest _req)
    {
        var (go, _) = FindTarget(_req);
        if (go == null)
            return BuildError($"Target '{_req.target}' not found");

        var names = string.Join(",", go.GetComponents<Component>().Select(c => $"\"{c.GetType().Name}\""));
        return $"{{\"success\":true,\"message\":\"OK\",\"components\":[{names}]}}";
    }

    private static string TrySetMember(Component _component, string _memberName, string _value)
    {
        var type = _component.GetType();

        // 상속 체인(제네릭 베이스 포함)을 순회하여 필드 탐색
        var field = FindFieldInHierarchy(type, _memberName);
        if (field != null)
        {
            try
            {
                field.SetValue(_component, ConvertValue(_value, field.FieldType));
                return null;
            }
            catch (Exception e)
            {
                return $"Failed to set field '{_memberName}': {e.Message}";
            }
        }

        // 상속 체인(제네릭 베이스 포함)을 순회하여 프로퍼티 탐색
        var prop = FindPropertyInHierarchy(type, _memberName);
        if (prop != null && prop.CanWrite)
        {
            try
            {
                prop.SetValue(_component, ConvertValue(_value, prop.PropertyType));
                return null;
            }
            catch (Exception e)
            {
                return $"Failed to set property '{_memberName}': {e.Message}";
            }
        }

        return $"Field or property '{_memberName}' not found on '{type.Name}'";
    }

    private static object ConvertValue(string _value, Type _targetType)
    {
        if (_targetType == typeof(string))  return _value;
        if (_targetType == typeof(int))     return int.Parse(_value);
        if (_targetType == typeof(float))   return float.Parse(_value);
        if (_targetType == typeof(bool))    return bool.Parse(_value);
        if (_targetType == typeof(double))  return double.Parse(_value);
        if (_targetType.IsEnum)             return Enum.Parse(_targetType, _value);
        return Convert.ChangeType(_value, _targetType);
    }
}
