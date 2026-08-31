// TestManager의 일부 — 밸런스 측정용 봇 (2026-08-11).
// 봇 자체가 개발 빌드 전용이라 이 파일도 같은 기준을 탄다.
// 개발 빌드에도 실린다(2026-08-31) — 빌드 E2E QA(wesqa)의 진입점이라서. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.Bots;
using CTC.Runtime.Core;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>서버 전용 — 봇을 세운다. 매치가 시작된 뒤에 부른다.</summary>
        public void TestSpawnBots(int _mice, int _cats)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) { GameDebug.LogWarning("[TestManager] 서버가 아니다"); return; }

            var manager = TestBotManager.Instance;
            if (manager == null) { GameDebug.LogError("[TestManager] TestBotManager 미배선"); return; }

            manager.ServerSpawn(_mice, _cats);
        }

        /// <summary>서버 전용 — 봇을 걷어낸다.</summary>
        public void TestClearBots()
        {
            var manager = TestBotManager.Instance;
            if (manager != null) manager.Stop();
        }

        /// <summary>QA: 지금 도는 봇 수.</summary>
        public int TestGetBotCount()
        {
            var manager = TestBotManager.Instance;
            return manager != null ? manager.Count : 0;
        }

        /// <summary>QA: 봇 쥐 수 — 치즈 총량이 여기 비례한다.</summary>
        public int TestGetBotMouseCount()
        {
            var manager = TestBotManager.Instance;
            return manager != null ? manager.MouseCount : 0;
        }

        /// <summary>QA: 고양이 봇이 쥐를 처음 본 시각(초). 못 봤으면 -1.</summary>
        public float TestGetBotFirstSight()
        {
            var manager = TestBotManager.Instance;
            return manager != null ? manager.FirstSightSeconds() : -1f;
        }

        /// <summary>
        /// QA: 고양이 봇이 쥐를 처음 봤을 때의 **거리**(m). 못 봤으면 -1.
        /// 맵에서 가능한 최장 직선보다 멀면 시야 판정이 새는 것이다 — 정직성 검사.
        /// </summary>
        public float TestGetBotFirstSightDistance()
        {
            var manager = TestBotManager.Instance;
            return manager != null ? manager.FirstSightDistance() : -1f;
        }

        /// <summary>QA: 쥐 봇들이 지금까지 번 치즈 합. 0이면 아무도 안 캔 것이다.</summary>
        public int TestGetBotMouseCheese()
        {
            var manager = TestBotManager.Instance;
            return manager != null ? manager.MouseCheese() : 0;
        }

        /// <summary>QA: 지금 나가 있는 지휘관 명령 수. 0이면 지휘관이 안 도는 것이다.</summary>
        public int TestGetBotOrderCount()
        {
            var manager = TestBotManager.Instance;
            return manager != null ? manager.OrderCount : 0;
        }

        /// <summary>
        /// QA: 봇 한 줄 요약 — "Mouse:Farm:37m Mouse:Travel:12m ...".
        /// **이동 거리 0인 봇을 여기서 잡는다** — 벽에 붙박인 봇은 숫자만 보면 안 보인다.
        /// </summary>
        public string TestGetBotDiagnostics()
        {
            var manager = TestBotManager.Instance;
            return manager != null ? manager.Diagnostics() : "";
        }
    }
}
#endif
