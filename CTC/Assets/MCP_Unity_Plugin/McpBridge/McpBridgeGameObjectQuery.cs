// McpBridgeGameObjectQuery.cs
// gameobject_find / gameobject_get / gameobject_set_tag / gameobject_set_layer 핸들러

using System.Text;
using UnityEditor;
using UnityEngine;

public static partial class McpBridge
{
    // ---- u_editor_query 라우터 ----

    private static string RouteQuery(BridgeRequest _req)
    {
        return (_req.subAction ?? "").ToLowerInvariant() switch
        {
            "find"      => GameObjectFind(_req),
            "get"       => GameObjectGet(_req),
            "hierarchy" => GetHierarchy(_req),
            _           => BuildError($"u_editor_query: unknown subAction '{_req.subAction}'")
        };
    }

    private static string GameObjectFind(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.searchQuery))
            return BuildError("searchQuery is required");

        string query = _req.searchQuery.ToLowerInvariant();
        var results = new System.Collections.Generic.List<string>();

        foreach (var root in GetAllSceneRoots())
            CollectMatchingObjects(root.transform, query, "", results);

        if (results.Count == 0)
            return BuildError($"'{_req.searchQuery}'에 해당하는 GameObject를 찾을 수 없습니다");

        var sb = new StringBuilder();
        sb.Append("{\"success\":true,\"message\":\"");
        sb.Append(Escape($"{results.Count}개 발견"));
        sb.Append("\",\"results\":[");
        for (int i = 0; i < results.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"');
            sb.Append(Escape(results[i]));
            sb.Append('"');
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private static void CollectMatchingObjects(Transform _t, string _query, string _parentPath, System.Collections.Generic.List<string> _results)
    {
        string path = string.IsNullOrEmpty(_parentPath) ? _t.name : $"{_parentPath}/{_t.name}";
        if (_t.name.ToLowerInvariant().Contains(_query))
            _results.Add(path);

        foreach (Transform child in _t)
            CollectMatchingObjects(child, _query, path, _results);
    }

    private static string GameObjectGet(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("target is required");

        var (go, _) = FindTarget(_req);
        if (go == null)
            return BuildError($"GameObject '{_req.target}'를 찾을 수 없습니다");

        var sb = new StringBuilder();
        sb.Append("{\"success\":true,\"message\":\"OK\"");
        sb.Append($",\"name\":\"{Escape(go.name)}\"");
        sb.Append($",\"activeSelf\":{go.activeSelf.ToString().ToLower()}");
        sb.Append($",\"tag\":\"{Escape(go.tag)}\"");
        sb.Append($",\"layer\":\"{Escape(LayerMask.LayerToName(go.layer))}\"");
        sb.Append($",\"layerIndex\":{go.layer}");

        // 위치/회전/크기
        sb.Append($",\"position\":{{\"x\":{go.transform.position.x},\"y\":{go.transform.position.y},\"z\":{go.transform.position.z}}}");
        sb.Append($",\"localPosition\":{{\"x\":{go.transform.localPosition.x},\"y\":{go.transform.localPosition.y},\"z\":{go.transform.localPosition.z}}}");

        // 컴포넌트 목록
        var components = go.GetComponents<Component>();
        sb.Append(",\"components\":[");
        for (int i = 0; i < components.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"');
            sb.Append(Escape(components[i] != null ? components[i].GetType().Name : "null"));
            sb.Append('"');
        }
        sb.Append("]}");

        return sb.ToString();
    }

    private static string GameObjectSetTag(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("target is required");
        if (string.IsNullOrEmpty(_req.tagName))
            return BuildError("tagName is required");

        var (go, prefabRoot) = FindTarget(_req);
        if (go == null)
            return BuildError($"GameObject '{_req.target}'를 찾을 수 없습니다");

        try
        {
            Undo.RecordObject(go, "MCP Set Tag");
            go.tag = _req.tagName;
            SaveTarget(go, prefabRoot);
            return BuildSuccess($"'{go.name}' 태그를 '{_req.tagName}'으로 설정했습니다");
        }
        catch (System.Exception e)
        {
            return BuildError($"태그 설정 실패: {e.Message} (tag_add으로 먼저 태그를 추가하세요)");
        }
    }

    private static string GameObjectSetLayer(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("target is required");
        if (string.IsNullOrEmpty(_req.layerName))
            return BuildError("layerName is required");

        int layerIdx = LayerMask.NameToLayer(_req.layerName);
        if (layerIdx < 0)
            return BuildError($"레이어 '{_req.layerName}'를 찾을 수 없습니다 (layer_set으로 먼저 레이어를 추가하세요)");

        var (go, prefabRoot) = FindTarget(_req);
        if (go == null)
            return BuildError($"GameObject '{_req.target}'를 찾을 수 없습니다");

        Undo.RecordObject(go, "MCP Set Layer");
        go.layer = layerIdx;
        SaveTarget(go, prefabRoot);
        return BuildSuccess($"'{go.name}' 레이어를 '{_req.layerName}'({layerIdx})으로 설정했습니다");
    }
}
