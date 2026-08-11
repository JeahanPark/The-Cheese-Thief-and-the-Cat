// TestManager의 일부 — 밸런스 측정용 봇 (2026-08-11).
// 봇 자체가 개발 빌드 전용이라 이 파일도 같은 기준을 탄다.
#if UNITY_EDITOR
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
