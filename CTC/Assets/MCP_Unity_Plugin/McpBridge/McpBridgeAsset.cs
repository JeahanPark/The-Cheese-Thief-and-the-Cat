// McpBridgeAsset.cs
// asset_find / asset_get_info 핸들러

using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static partial class McpBridge
{
    // ---- u_editor_asset 라우터 ----

    private static string RouteAsset(BridgeRequest _req)
    {
        return (_req.subAction ?? "").ToLowerInvariant() switch
        {
            "find"     => AssetFind(_req),
            "get_info" => AssetGetInfo(_req),
            "refresh"  => RefreshAssets(_req),
            _          => BuildError($"u_editor_asset: unknown subAction '{_req.subAction}'")
        };
    }

    private static string AssetFind(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.filter))
            return BuildError("filter is required (예: 't:Prefab', 't:Texture2D name')");

        string[] guids = string.IsNullOrEmpty(_req.folder)
            ? AssetDatabase.FindAssets(_req.filter)
            : AssetDatabase.FindAssets(_req.filter, new[] { _req.folder });

        if (guids.Length == 0)
            return BuildError($"필터 '{_req.filter}'에 해당하는 에셋을 찾을 수 없습니다");

        var sb = new StringBuilder();
        sb.Append("{\"success\":true,\"message\":\"");
        sb.Append(Escape($"{guids.Length}개 발견"));
        sb.Append("\",\"results\":[");

        for (int i = 0; i < guids.Length; i++)
        {
            if (i > 0) sb.Append(',');
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string type = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "Unknown";
            sb.Append($"{{\"guid\":\"{guids[i]}\",\"path\":\"{Escape(path)}\",\"type\":\"{Escape(type)}\"}}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private static string AssetGetInfo(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.assetPath))
            return BuildError("assetPath is required");

        string guid = AssetDatabase.AssetPathToGUID(_req.assetPath);
        if (string.IsNullOrEmpty(guid))
            return BuildError($"에셋을 찾을 수 없습니다: {_req.assetPath}");

        var type = AssetDatabase.GetMainAssetTypeAtPath(_req.assetPath);
        var importer = AssetImporter.GetAtPath(_req.assetPath);
        string assetBundleName = importer?.assetBundleName ?? "";

        // 파일 크기
        string fullPath = Path.GetFullPath(_req.assetPath);
        long fileSize = File.Exists(fullPath) ? new FileInfo(fullPath).Length : -1;

        // 의존성
        string[] deps = AssetDatabase.GetDependencies(_req.assetPath, false);

        var sb = new StringBuilder();
        sb.Append("{\"success\":true,\"message\":\"OK\"");
        sb.Append($",\"path\":\"{Escape(_req.assetPath)}\"");
        sb.Append($",\"guid\":\"{guid}\"");
        sb.Append($",\"type\":\"{Escape(type?.Name ?? "Unknown")}\"");
        sb.Append($",\"fileSizeBytes\":{fileSize}");
        sb.Append($",\"assetBundle\":\"{Escape(assetBundleName)}\"");
        sb.Append(",\"dependencies\":[");
        for (int i = 0; i < deps.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"\"{Escape(deps[i])}\"");
        }
        sb.Append("]}");
        return sb.ToString();
    }
}
