// McpBridgeMenu.cs
// MCP Bridge 핸들러: u_editor_menu
// Unity Editor의 메뉴 항목을 경로로 실행한다 (예: "Tools/Map Generator/Bake NavMesh").

using UnityEditor;

public static partial class McpBridge
{
    private static string ExecuteMenu(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.menuPath))
            return BuildError("'menuPath' is required");

        bool ok = EditorApplication.ExecuteMenuItem(_req.menuPath);
        if (!ok)
            return BuildError($"Menu not found or execution failed: '{_req.menuPath}'");

        return BuildSuccess($"Menu executed: '{_req.menuPath}'");
    }
}
