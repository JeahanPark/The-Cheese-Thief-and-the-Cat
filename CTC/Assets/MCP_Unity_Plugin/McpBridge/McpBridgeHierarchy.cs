// McpBridgeHierarchy.cs
// MCP Bridge 핸들러: get_hierarchy
// 씬 전체 또는 특정 프리팹의 GameObject 계층 구조를 JSON으로 반환한다.
//
// 요청 파라미터:
//   prefabPath : 프리팹 에셋 경로. 생략 시 현재 씬 전체 반환.
//   maxCount   : 최대 노드 수 (기본값 500, 대형 씬 보호용)

using System;
using System.Text;
using UnityEditor;
using UnityEngine;

public static partial class McpBridge
{
    private static string GetHierarchy(BridgeRequest _req)
    {
        int maxNodes = _req.maxCount > 0 ? _req.maxCount : 500;
        var sb = new StringBuilder(4096);
        var counter = new int[] { 0 };

        try
        {
            if (!string.IsNullOrEmpty(_req.prefabPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_req.prefabPath);
                if (prefab == null)
                    return BuildError($"Prefab not found: '{_req.prefabPath}'");

                sb.Append("{\"success\":true,\"message\":\"OK\",\"source\":\"prefab\",\"hierarchy\":");
                AppendNodeJson(sb, prefab, 0, maxNodes, counter);
                sb.Append('}');
                return sb.ToString();
            }

            // 씬 전체
            var roots = GetAllSceneRoots();
            sb.Append("{\"success\":true,\"message\":\"OK\",\"source\":\"scene\",\"hierarchy\":[");
            for (int i = 0; i < roots.Length; i++)
            {
                if (i > 0) sb.Append(',');
                AppendNodeJson(sb, roots[i], 0, maxNodes, counter);
            }
            sb.Append("]}");
            return sb.ToString();
        }
        catch (Exception e)
        {
            return BuildError($"get_hierarchy failed: {e.Message}");
        }
    }

    // depth 제한 10, 노드 수 제한으로 무한 순환 방지
    private static void AppendNodeJson(StringBuilder sb, GameObject go, int depth, int maxNodes, int[] counter)
    {
        counter[0]++;

        sb.Append("{\"name\":\"");
        sb.Append(Escape(go.name));
        sb.Append("\",\"active\":");
        sb.Append(go.activeSelf ? "true" : "false");
        sb.Append(",\"path\":\"");
        sb.Append(Escape(GetGameObjectPath(go)));
        sb.Append("\",\"components\":[");

        var components = go.GetComponents<Component>();
        bool firstComp = true;
        foreach (var c in components)
        {
            if (c == null) continue;
            if (!firstComp) sb.Append(',');
            firstComp = false;
            sb.Append('"');
            sb.Append(Escape(c.GetType().Name));
            sb.Append('"');
        }

        sb.Append("],\"children\":[");

        if (depth < 10 && counter[0] < maxNodes)
        {
            bool firstChild = true;
            foreach (Transform child in go.transform)
            {
                if (counter[0] >= maxNodes) break;
                if (!firstChild) sb.Append(',');
                firstChild = false;
                AppendNodeJson(sb, child.gameObject, depth + 1, maxNodes, counter);
            }
        }

        sb.Append("]}");
    }

    // "RootName/Child/GrandChild" 형식의 전체 경로 반환
    private static string GetGameObjectPath(GameObject go)
    {
        var path = go.name;
        var parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
