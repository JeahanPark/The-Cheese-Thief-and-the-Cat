using UnityEngine;

namespace WesQA
{
    /// <summary>플레이모드 진입 시 WesPocoServer를 자동 기동. Editor 전용 어셈블리(릴리스 빌드 미포함).</summary>
    public static class WesQABootstrap
    {
        private static WesPocoServer _server;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            // 이전 세션 좀비 리스너 정리 — teardown 없이 플레이가 끝나면(예외/도메인 리로드 꺼짐)
            // 정적 서버가 포트를 쥔 채 남아 다음 부트가 SocketException으로 죽는다
            StopServer();

            int instance = ResolveInstanceIndex();
            _server = new WesPocoServer(5001 + instance);
            _server.Start();
            Debug.Log($"[WesQA] server starting on port {5001 + instance}");

            Application.quitting -= StopServer; // 에디터에선 플레이 종료 시에도 발화
            Application.quitting += StopServer;
        }

        private static void StopServer()
        {
            if (_server == null) return;
            _server.Stop();
            _server = null;
            Debug.Log("[WesQA] server stopped");
        }

        // M4: MPPM 가상 플레이어 태그 `wes<N>`(예 wes1, wes2)에서 N을 파싱해 인덱스로 사용.
        // [CTC 이식 수정] 태그 미설정 폴백 — 메인 에디터 = 0, 태그 없는 클론 = 1.
        // (기존엔 클론도 0 폴백이라 메인 에디터와 포트 5001 바인드 충돌 → SocketException)
        // 클론 판별은 CTC 기존 패턴(MppmAutoConnect.cs): dataPath에 "/Library/VP/" 포함.
        private static int ResolveInstanceIndex()
        {
            try
            {
                foreach (var tag in GetMppmTags())
                {
                    if (!string.IsNullOrEmpty(tag) && tag.StartsWith("wes") &&
                        int.TryParse(tag.Substring(3), out int n))
                        return n;
                }

                bool isClone = Application.dataPath.Replace('\\', '/').Contains("/Library/VP/");
                if (isClone)
                {
                    Debug.LogWarning("[WesQA] MPPM 태그 wes<N> 미설정 — instance 1로 폴백. 클론 2개 이상이면 태그 필요");
                    return 1;
                }
            }
            catch { }
            return 0;
        }

        // 현재 MPPM 가상 플레이어에 부여된 태그 배열을 반환. 실패/미사용 시 빈 배열.
        // [CTC 이식 수정] MPPM 2.0.1 네임스페이스는 Unity.Multiplayer.PlayMode(대문자 M) —
        // WES(1.x)의 Unity.Multiplayer.Playmode에서 조정. asmdef 참조명은 그대로 Unity.Multiplayer.Playmode.
        // (CTC 기존 코드 MultiplayerQaProbe.cs가 Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor를
        //  같은 패턴으로 사용 중 — 컴파일 검증된 네임스페이스)
        private static string[] GetMppmTags()
        {
            try { return Unity.Multiplayer.PlayMode.CurrentPlayer.ReadOnlyTags() ?? new string[0]; }
            catch { return new string[0]; }
        }
    }
}
