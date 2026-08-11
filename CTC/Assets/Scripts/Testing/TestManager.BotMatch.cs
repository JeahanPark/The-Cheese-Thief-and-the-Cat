// TestManager의 일부 — 봇 판을 자동으로 돌리는 데 필요한 손잡이들 (2026-08-11).
#if UNITY_EDITOR
using CTC.Runtime.Bots;
using CTC.Runtime.Match;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>
        /// QA: 판을 배속으로 돌린다. 12분 × 20판 = 4시간이 5배면 48분이 된다.
        ///
        /// 봇만 도는 판은 네트워크 지연이 없어 배속이 안전하다. **사람이 낀 판에서는 쓰지 않는다** —
        /// 입력·보간이 배속을 못 따라가 측정이 오염된다.
        /// </summary>
        public void TestSetTimeScale(float _scale)
        {
            Time.timeScale = Mathf.Clamp(_scale, 0.1f, 20f);
            // 물리도 같이 당겨야 CharacterController가 배속에서 벽을 뚫지 않는다
            Time.fixedDeltaTime = 0.02f / Time.timeScale;
        }

        /// <summary>QA: 배속을 되돌린다.</summary>
        public void TestResetTimeScale()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        /// <summary>
        /// QA: 인원 조건을 건너뛰고 판을 연다 — **봇만 도는 측정 판**용.
        /// 사람이 하나라도 끼면 그 사람이 가만히 서 있는 것만으로 승패가 뒤집힌다.
        /// </summary>
        public void TestForceStartGame()
        {
            var lobby = FindFirstObjectByType<Lobby.LobbyController>();
            if (lobby != null) lobby.DevForceStartGame();
        }

        /// <summary>QA: 매치 남은 시간(초). 1단계 시계다.</summary>
        public float TestGetMatchRemaining()
        {
            var match = FindFirstObjectByType<MatchManager>();
            return match != null ? match.RemainingSeconds : -1f;
        }

        /// <summary>
        /// QA: 한 판의 결과를 한 줄로 — 러너가 이걸 그대로 CSV에 적는다.
        /// "result,remaining,mice,cats,firstSight,jailed,phase"
        /// </summary>
        public string TestGetMatchSummary()
        {
            var match = FindFirstObjectByType<MatchManager>();
            var phase = FindFirstObjectByType<PhaseDirector>();
            var bots = TestBotManager.Instance;

            string result = match != null ? match.Result.ToString() : "None";
            float remaining = match != null ? match.RemainingSeconds : -1f;
            int mice = bots != null ? bots.MouseCount : 0;
            int cats = bots != null ? bots.CatCount : 0;
            float firstSight = bots != null ? bots.FirstSightSeconds() : -1f;
            int jailed = TestGetJailedCount();
            string phaseName = phase != null ? phase.Phase.ToString() : "Hunt";

            return $"{result},{remaining:F0},{mice},{cats},{firstSight:F1},{jailed},{phaseName}";
        }
    }
}
#endif
