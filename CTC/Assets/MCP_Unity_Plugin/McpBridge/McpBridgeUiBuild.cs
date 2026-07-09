// McpBridgeUiBuild.cs
// McpBridge 핸들러: u_editor_ui_build
// layout JSON 트리를 받아 실제 Unity API(LayoutGroup 자동정렬)로 uGUI 프리팹을 빌드/저장한다.
// 외부 GPT 없이 호출 AI가 작성한 JSON을 그대로 빌드 → generate_ui_with_gpt(YAML 손작성) 대체.

using UnityEngine;
using McpUiGen;

public static partial class McpBridge
{
    private static string RouteUiBuild(BridgeRequest _req)
    {
        try
        {
            int w = _req.referenceWidth  > 0 ? _req.referenceWidth  : 1920;
            int h = _req.referenceHeight > 0 ? _req.referenceHeight : 1080;

            string path = UiBuildRunner.BuildAndSave(
                _req.layoutJson,
                _req.outputPrefabPath,
                new Vector2(w, h));

            return $"{{\"success\":true,\"prefabPath\":\"{Escape(path)}\"}}";
        }
        catch (System.Exception e)
        {
            return BuildError(e.Message);
        }
    }
}
