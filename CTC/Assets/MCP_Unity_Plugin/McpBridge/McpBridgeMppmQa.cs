// McpBridgeMppmQa.cs
// MPPM QA 멀티 검증 전용 수집 엔드포인트.
// MultiplayerQaProbe가 Temp/mppm-qa/probe_host.json / probe_clone.json 에 기록한 양측 스냅샷을
// QA 1콜(mppm_collect)로 한 번에 읽어 { host, clone } 형태로 반환한다.
// 클론 에디터는 파이프를 열지 않으므로(IsCloneEditor 차단) 파일 경유가 클론 상태 수집의 유일 경로다.

using System.IO;
using System.Text;

public static partial class McpBridge
{
    private const string MPPM_QA_DIR = "Temp/mppm-qa";
    private const string MPPM_QA_HOST_FILE = "probe_host.json";
    private const string MPPM_QA_CLONE_FILE = "probe_clone.json";

    private static string RouteMppmQa(BridgeRequest _req)
    {
        // 현재는 collect 단일 동작. 향후 reset/clear 등 확장 여지를 위해 라우팅 형태 유지.
        return CollectMppmQa();
    }

    private static string CollectMppmQa()
    {
        string hostJson = ReadProbeFile(MPPM_QA_HOST_FILE);
        string cloneJson = ReadProbeFile(MPPM_QA_CLONE_FILE);

        var sb = new StringBuilder();
        sb.Append("{\"success\":true,\"message\":\"OK\",\"host\":");
        sb.Append(hostJson ?? "null");
        sb.Append(",\"clone\":");
        sb.Append(cloneJson ?? "null");
        sb.Append("}");
        return sb.ToString();
    }

    // 프로브 스냅샷 파일을 읽어 raw JSON 문자열을 반환한다(프로브가 이미 유효 JSON으로 기록).
    // 파일 없음/읽기 실패 시 null → 해당 역할 미접속으로 QA가 판정.
    private static string ReadProbeFile(string _fileName)
    {
        try
        {
            string path = Path.Combine(MPPM_QA_DIR, _fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            string content = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(content) ? null : content;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
