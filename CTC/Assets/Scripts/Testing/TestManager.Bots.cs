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

        /// <summary>
        /// QA: 쥐 봇의 테크 현황 한 줄 — "mice=8 techs=12 buyers=6" (계획 3 Task 6).
        /// **상점까지 걸어가는 비용**을 전/후로 비교할 때 쓴다 — 이동이 늘면 산 테크가 준다.
        /// </summary>
        public string TestGetBotTechStats()
        {
            var manager = TestBotManager.Instance;
            return manager != null ? manager.MouseTechStats() : "";
        }

        /// <summary>
        /// QA: 상점 이동 한 줄 — "seekers=5 reached=4 unreached=1 seek=61.5 inside=18.0".
        /// unreached가 0이 아니면 그 판의 숫자는 밸런스가 아니라 봇의 길찾기를 재고 있다.
        /// </summary>
        public string TestGetBotShopStats()
        {
            var manager = TestBotManager.Instance;
            return manager != null ? manager.ShopStats() : "";
        }

        /// <summary>
        /// QA: **어디서 지는가** 한 줄 (2026-09-09, 쥐 승률 진단).
        /// "elapsed=612 mice=8 alive=0 dead=8 earned=4210 techs=9 deaths=41:Farm,58:Shop,..."
        ///
        /// `deaths`의 뒷칸이 그때 하던 일이다 — `Farm`에 몰리면 밸런스,
        /// `Shop`/`Travel`에 몰리면 이동 비용·봇 문제다.
        /// </summary>
        public string TestGetBotMouseReport()
        {
            var manager = TestBotManager.Instance;
            return manager != null ? manager.MouseReport() : "";
        }

        /// <summary>
        /// QA: **봇이 얼마나 멀리서 보는가** — "sights=812 avg=21.4 max=97.3 far=118".
        /// `far`는 35m를 넘겨 본 표본 수다. 크면 그 판은 밸런스가 아니라 봇의 눈을 재고 있다.
        /// </summary>
        public string TestGetBotSightStats()
        {
            var manager = TestBotManager.Instance;
            return manager != null ? manager.SightStats() : "";
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
