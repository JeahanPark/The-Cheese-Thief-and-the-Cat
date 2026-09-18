// TestManager의 일부 — 접속 대기 로딩창 (2026-09-19)
// 개발 빌드에도 실린다 — 빌드 E2E QA(wesqa)의 진입점이라서. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>
        /// 접속 대기 로딩창 한 줄 — "showing=True dots=.. scene=Game". 대기실 추첨 연출이 닫히며 뜨고(씬을 건너온다)
        /// 판이 서면 걷혀 **사라진다** — 떠 있지 않으면 "showing=False dots= scene=…".
        /// </summary>
        public string TestGetLoadingScreen()
        {
            UI.LoadingScreen screen = UI.LoadingScreen.Current;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (screen == null) return $"showing=False dots= scene={scene}";
            return $"showing={screen.IsShowing} dots={screen.DotsText} scene={scene}";
        }
    }
}
#endif
