// McpBridgeReferences.cs
// McpBridge partial - Inspector Object 참조 배치 매핑 핸들러.
// action: set_reference
//
// mappingsJson 형식:
// [{"propertyName":"m_Field","referenceTarget":"ChildName"},...]
// referenceTarget이 "Assets/"로 시작하면 에셋 경로로 처리.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static partial class McpBridge
{
    // ---- u_editor_reference 라우터 ----

    private static string RouteReference(BridgeRequest _req)
    {
        return (_req.subAction ?? "").ToLowerInvariant() switch
        {
            "set_reference"  => SetReference(_req),
            "connect_button" => ConnectButton(_req),
            _                => BuildError($"u_editor_reference: unknown subAction '{_req.subAction}'")
        };
    }

    private static string SetReference(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.componentType))
            return BuildError("'componentType' is required");
        if (string.IsNullOrEmpty(_req.mappingsJson))
            return BuildError("'mappingsJson' is required");

        var compType = FindComponentType(_req.componentType);
        if (compType == null)
            return BuildError($"Component type '{_req.componentType}' not found");

        var (go, prefabRoot) = FindTarget(_req);
        if (go == null)
            return BuildError($"Target '{_req.target}' not found");

        var component = go.GetComponent(compType);
        if (component == null)
            return BuildError($"Component '{_req.componentType}' not found on '{go.name}'");

        // mappingsJson 파싱 (JsonUtility 배열 래퍼 트릭)
        ReferenceMapping[] mappings;
        try
        {
            string wrapped = "{\"items\":" + _req.mappingsJson + "}";
            var wrapper = JsonUtility.FromJson<ReferenceMappingArray>(wrapped);
            mappings = wrapper?.items;
        }
        catch (Exception e)
        {
            return BuildError($"Failed to parse mappingsJson: {e.Message}");
        }

        if (mappings == null || mappings.Length == 0)
            return BuildError("mappingsJson is empty or invalid");

        Undo.RecordObject(component, "MCP Set Reference");

        var errors = new List<string>();
        int successCount = 0;

        foreach (var mapping in mappings)
        {
            var error = ApplyReferenceMapping(component, compType, mapping, go, prefabRoot);
            if (error != null)
                errors.Add($"{mapping.propertyName}: {error}");
            else
                successCount++;
        }

        SaveTarget(go, prefabRoot);

        if (errors.Count == 0)
            return BuildSuccess($"{successCount}/{mappings.Length} mappings applied to '{_req.componentType}'");

        string errorSummary = string.Join(", ", errors);
        return BuildError($"{successCount}/{mappings.Length} applied. Failed: {errorSummary}");
    }

    private static string ApplyReferenceMapping(
        Component _component, Type _compType,
        ReferenceMapping _mapping,
        GameObject _contextGo, GameObject _prefabRoot)
    {
        if (string.IsNullOrEmpty(_mapping.propertyName))
            return "propertyName is required";
        if (string.IsNullOrEmpty(_mapping.referenceTarget))
            return "referenceTarget is required";

        // 배열 인덱스 표기 파싱: "m_Cells[0]" → fieldName="m_Cells", arrayIndex=0
        string fieldName = _mapping.propertyName;
        int arrayIndex = -1;
        var bracketMatch = System.Text.RegularExpressions.Regex.Match(_mapping.propertyName, @"^(.+)\[(\d+)\]$");
        if (bracketMatch.Success)
        {
            fieldName = bracketMatch.Groups[1].Value;
            arrayIndex = int.Parse(bracketMatch.Groups[2].Value);
        }

        // 필드/프로퍼티 타입 추론 (제네릭 베이스 포함)
        var field = FindFieldInHierarchy(_compType, fieldName);
        Type fieldType = field?.FieldType;

        if (fieldType == null)
        {
            var prop = FindPropertyInHierarchy(_compType, fieldName);
            fieldType = prop?.PropertyType;
        }

        if (fieldType == null)
            return $"Field or property '{fieldName}' not found on '{_compType.Name}'";

        // 배열/리스트인 경우 요소 타입 결정
        Type elementType = null;
        if (arrayIndex >= 0)
        {
            if (fieldType.IsArray)
            {
                elementType = fieldType.GetElementType();
            }
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            {
                elementType = fieldType.GetGenericArguments()[0];
            }
            else
            {
                return $"Field '{fieldName}' is not an array or List type";
            }
        }

        Type resolveType = (arrayIndex >= 0) ? elementType : fieldType;

        // 참조 오브젝트 해석
        var resolveError = ResolveReference(_mapping, resolveType, _contextGo, _prefabRoot, out var refObj);
        if (resolveError != null)
            return resolveError;

        // 필드에 할당
        try
        {
            if (arrayIndex >= 0)
            {
                // 배열/리스트 요소 할당
                if (field != null)
                {
                    var arr = field.GetValue(_component);
                    if (fieldType.IsArray)
                    {
                        var array = (Array)arr;
                        if (array == null || array.Length <= arrayIndex)
                        {
                            // 배열 확장
                            int newLen = arrayIndex + 1;
                            var newArray = Array.CreateInstance(elementType, newLen);
                            if (array != null)
                                Array.Copy(array, newArray, array.Length);
                            array = newArray;
                            field.SetValue(_component, array);
                        }
                        array.SetValue(refObj, arrayIndex);
                    }
                    else
                    {
                        // List<T>
                        var list = arr as System.Collections.IList;
                        if (list == null)
                        {
                            list = (System.Collections.IList)Activator.CreateInstance(fieldType);
                            field.SetValue(_component, list);
                        }
                        while (list.Count <= arrayIndex)
                            list.Add(null);
                        list[arrayIndex] = refObj;
                    }
                }
                else
                {
                    return $"Array index assignment requires a field, not a property";
                }
            }
            else if (field != null)
            {
                field.SetValue(_component, refObj);
            }
            else
            {
                var prop = FindPropertyInHierarchy(_compType, fieldName);
                prop.SetValue(_component, refObj);
            }
            return null;
        }
        catch (Exception e)
        {
            return $"Assignment failed: {e.Message}";
        }
    }

    private static string ResolveReference(
        ReferenceMapping _mapping, Type _fieldType,
        GameObject _contextGo, GameObject _prefabRoot,
        out UnityEngine.Object _result)
    {
        _result = null;

        // 에셋 경로 (Assets/ 로 시작)
        if (_mapping.referenceTarget.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            _result = AssetDatabase.LoadAssetAtPath(_mapping.referenceTarget, _fieldType);
            if (_result == null)
                return $"Asset not found at '{_mapping.referenceTarget}'";
            return null;
        }

        // 이름으로 GameObject 탐색 (같은 프리팹 or 씬)
        // referenceTarget에 '/'가 포함되면 경로 탐색 (예: "Parent/Child/Text")
        GameObject refGo = null;
        bool isPath = _mapping.referenceTarget.Contains('/');

        if (_prefabRoot != null)
        {
            if (isPath)
                refGo = FindByPath(_prefabRoot.transform, _mapping.referenceTarget);
            else
                refGo = _prefabRoot.name == _mapping.referenceTarget
                    ? _prefabRoot
                    : FindInHierarchy(_prefabRoot.transform, _mapping.referenceTarget);
        }
        else
        {
            foreach (var root in GetAllSceneRoots())
            {
                if (isPath)
                {
                    refGo = FindByPath(root.transform, _mapping.referenceTarget);
                }
                else
                {
                    if (root.name == _mapping.referenceTarget) { refGo = root; break; }
                    var found = FindInHierarchy(root.transform, _mapping.referenceTarget);
                    if (found != null) { refGo = found; break; }
                }
                if (refGo != null) break;
            }
        }

        if (refGo == null)
            return $"GameObject '{_mapping.referenceTarget}' not found";

        // 필드 타입에 맞게 추출
        if (_fieldType == typeof(GameObject))
        {
            _result = refGo;
        }
        else if (_fieldType == typeof(Transform))
        {
            _result = refGo.transform;
        }
        else if (typeof(Component).IsAssignableFrom(_fieldType))
        {
            Type resolveType = _fieldType;
            if (!string.IsNullOrEmpty(_mapping.referenceComponentType))
                resolveType = FindComponentType(_mapping.referenceComponentType) ?? _fieldType;

            _result = refGo.GetComponent(resolveType);
            if (_result == null)
                return $"Component '{resolveType.Name}' not found on '{refGo.name}'";
        }
        else
        {
            return $"Field type '{_fieldType.Name}' is not a supported reference type";
        }

        return null;
    }

    // ---- DTO ----

    [Serializable]
    private class ReferenceMapping
    {
        public string propertyName;
        public string referenceTarget;
        public string referenceComponentType;
    }

    [Serializable]
    private class ReferenceMappingArray
    {
        public ReferenceMapping[] items;
    }
}
