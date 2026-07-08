// McpBridgeRefresh.cs
// MCP Bridge 핸들러: refresh_assets
// Unity Editor의 AssetDatabase를 갱신하거나 특정 에셋을 reimport한다.

using UnityEditor;

public static partial class McpBridge
{
    private static string RefreshAssets(BridgeRequest _req)
    {
        if (!string.IsNullOrEmpty(_req.assetPath))
        {
            AssetDatabase.ImportAsset(_req.assetPath, ImportAssetOptions.ForceUpdate);
            return BuildSuccess($"Reimported: '{_req.assetPath}'");
        }

        AssetDatabase.Refresh();
        return BuildSuccess("AssetDatabase refreshed");
    }
}
