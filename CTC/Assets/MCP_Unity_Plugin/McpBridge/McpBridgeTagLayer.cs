// McpBridgeTagLayer.cs
// tag_list / tag_add / layer_list / layer_set 핸들러

using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public static partial class McpBridge
{
    // ---- u_editor_tag 라우터 ----

    private static string RouteTag(BridgeRequest _req)
    {
        return (_req.subAction ?? "").ToLowerInvariant() switch
        {
            "list"   => TagList(_req),
            "add"    => TagAdd(_req),
            "remove" => TagRemove(_req),
            "set"    => GameObjectSetTag(_req),
            _        => BuildError($"u_editor_tag: unknown subAction '{_req.subAction}'")
        };
    }

    // ---- u_editor_layer 라우터 ----

    private static string RouteLayer(BridgeRequest _req)
    {
        return (_req.subAction ?? "").ToLowerInvariant() switch
        {
            "list"       => LayerList(_req),
            "set"        => LayerSet(_req),
            "remove"     => LayerRemove(_req),
            "set_object" => GameObjectSetLayer(_req),
            _            => BuildError($"u_editor_layer: unknown subAction '{_req.subAction}'")
        };
    }

    private static string TagList(BridgeRequest _req)
    {
        string[] tags = InternalEditorUtility.tags;
        var sb = new StringBuilder();
        sb.Append("{\"success\":true,\"message\":\"");
        sb.Append(Escape($"{tags.Length}개 태그"));
        sb.Append("\",\"tags\":[");
        for (int i = 0; i < tags.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"\"{Escape(tags[i])}\"");
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private static string TagAdd(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.tagName))
            return BuildError("tagName is required");

        // 이미 존재하는지 확인
        foreach (var t in InternalEditorUtility.tags)
        {
            if (t == _req.tagName)
                return BuildSuccess($"태그 '{_req.tagName}'는 이미 존재합니다");
        }

        // TagManager.asset 직접 수정
        var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            return BuildError("TagManager.asset을 로드할 수 없습니다");
        var tagManager = new SerializedObject(tagManagerAssets[0]);
        var tagsProp = tagManager.FindProperty("tags");

        // 빈 슬롯 찾기
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            var elem = tagsProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(elem.stringValue))
            {
                elem.stringValue = _req.tagName;
                tagManager.ApplyModifiedProperties();
                return BuildSuccess($"태그 '{_req.tagName}' 추가 완료");
            }
        }

        // 빈 슬롯 없으면 배열 확장
        tagsProp.arraySize++;
        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = _req.tagName;
        tagManager.ApplyModifiedProperties();
        return BuildSuccess($"태그 '{_req.tagName}' 추가 완료");
    }

    private static string TagRemove(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.tagName))
            return BuildError("tagName is required");

        // 기본 태그는 제거 불가
        string[] builtinTags = { "Untagged", "Respawn", "Finish", "EditorOnly", "MainCamera", "Player", "GameController" };
        foreach (var bt in builtinTags)
        {
            if (bt == _req.tagName)
                return BuildError($"기본 태그 '{_req.tagName}'는 제거할 수 없습니다");
        }

        var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            return BuildError("TagManager.asset을 로드할 수 없습니다");
        var tagManager = new SerializedObject(tagManagerAssets[0]);
        var tagsProp = tagManager.FindProperty("tags");

        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            var elem = tagsProp.GetArrayElementAtIndex(i);
            if (elem.stringValue == _req.tagName)
            {
                elem.stringValue = "";
                tagManager.ApplyModifiedProperties();
                return BuildSuccess($"태그 '{_req.tagName}' 제거 완료");
            }
        }

        return BuildError($"태그 '{_req.tagName}'를 찾을 수 없습니다");
    }

    private static string LayerList(BridgeRequest _req)
    {
        var sb = new StringBuilder();
        sb.Append("{\"success\":true,\"message\":\"레이어 목록\",\"layers\":[");

        bool first = true;
        for (int i = 0; i < 32; i++)
        {
            string name = LayerMask.LayerToName(i);
            if (string.IsNullOrEmpty(name)) continue;

            if (!first) sb.Append(',');
            first = false;
            sb.Append($"{{\"index\":{i},\"name\":\"{Escape(name)}\"}}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private static string LayerSet(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.layerName))
            return BuildError("layerName is required");

        int idx = _req.layerIndex;
        if (idx < 8 || idx > 31)
            return BuildError($"layerIndex는 8~31 범위여야 합니다 (입력값: {idx})");

        var layerManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (layerManagerAssets == null || layerManagerAssets.Length == 0)
            return BuildError("TagManager.asset을 로드할 수 없습니다");
        var tagManager = new SerializedObject(layerManagerAssets[0]);
        var layersProp = tagManager.FindProperty("layers");

        if (layersProp == null || layersProp.arraySize <= idx)
            return BuildError("TagManager.asset에서 layers 속성을 찾을 수 없습니다");

        var layerElem = layersProp.GetArrayElementAtIndex(idx);
        layerElem.stringValue = _req.layerName;
        tagManager.ApplyModifiedProperties();
        return BuildSuccess($"레이어 인덱스 {idx}를 '{_req.layerName}'으로 설정했습니다");
    }

    private static string LayerRemove(BridgeRequest _req)
    {
        int idx = _req.layerIndex;
        if (idx < 8 || idx > 31)
            return BuildError($"layerIndex는 8~31 범위여야 합니다 (입력값: {idx})");

        var layerManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (layerManagerAssets == null || layerManagerAssets.Length == 0)
            return BuildError("TagManager.asset을 로드할 수 없습니다");
        var tagManager = new SerializedObject(layerManagerAssets[0]);
        var layersProp = tagManager.FindProperty("layers");

        if (layersProp == null || layersProp.arraySize <= idx)
            return BuildError("TagManager.asset에서 layers 속성을 찾을 수 없습니다");

        var layerElem = layersProp.GetArrayElementAtIndex(idx);
        string oldName = layerElem.stringValue;
        if (string.IsNullOrEmpty(oldName))
            return BuildSuccess($"레이어 인덱스 {idx}는 이미 비어있습니다");

        layerElem.stringValue = "";
        tagManager.ApplyModifiedProperties();
        return BuildSuccess($"레이어 '{oldName}'(인덱스 {idx}) 제거 완료");
    }
}
