// McpBridgePlayMode.cs
// MCP Bridge 핸들러: play_mode_control
// Unity Editor의 Play Mode 진입 / 종료 / 상태 조회를 처리한다.
//
// 요청 파라미터:
//   playModeAction : "enter" | "exit" | "status"

using UnityEditor;

public static partial class McpBridge
{
    private static string PlayModeControl(BridgeRequest _req)
    {
        string act = string.IsNullOrEmpty(_req.playModeAction)
            ? "status"
            : _req.playModeAction.ToLowerInvariant();

        switch (act)
        {
            case "enter":
                if (EditorApplication.isPlaying)
                    return BuildError("이미 Play Mode 중입니다.");
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                EditorApplication.isPlaying = true;
                return BuildSuccess("Play Mode 진입 요청됨");

            case "exit":
                if (!EditorApplication.isPlaying)
                    return BuildError("Play Mode가 아닙니다.");
                EditorApplication.isPlaying = false;
                return BuildSuccess("Play Mode 종료 요청됨");

            case "status":
                bool isPlaying = EditorApplication.isPlaying;
                return $"{{\"success\":true,\"message\":\"OK\",\"isPlaying\":{(isPlaying ? "true" : "false")}}}";

            default:
                return BuildError($"알 수 없는 playModeAction: '{_req.playModeAction}'. 'enter' | 'exit' | 'status' 중 하나를 사용하세요.");
        }
    }
}
