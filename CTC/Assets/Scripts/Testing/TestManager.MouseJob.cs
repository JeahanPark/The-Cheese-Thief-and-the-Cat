// TestManager의 일부 — 쥐 직업과 2단계 (v3.9)
// 개발 빌드에도 실린다(2026-08-31) — 빌드 E2E QA(wesqa)의 진입점이라서. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.Core;
using CTC.Runtime.Match;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>
        /// QA: 로컬 쥐의 직업 — "None"|"Hero"|"Gang"|"Minion".
        /// **컴포넌트를 못 찾으면 "Missing"**(2026-09-09 감사 T9) — `MouseJobId.None`은
        /// 「직업이 아직 없다」는 **정상값**이라, 미배선을 같은 글자로 돌려주면
        /// 「처음에는 직업이 없다」가 빈 씬에서도 통과한다.
        /// </summary>
        public string TestGetMouseJob()
        {
            var job = LocalComponent<MouseJob>();
            return job != null ? job.Value.ToString() : MISSING;
        }

        /// <summary>QA: 지정 클라이언트의 직업(복제 확인용). 못 찾으면 "Missing".</summary>
        public string TestGetMouseJobOf(long _clientId)
        {
            var player = NetUtil.FindPlayerObject((ulong)_clientId);
            var job = player != null ? player.GetComponent<MouseJob>() : null;
            return job != null ? job.Value.ToString() : MISSING;
        }

        /// <summary>서버 전용 — 조건을 건너뛰고 직업을 새긴다(상자를 안 부수고 검증한다).</summary>
        public void TestSetMouseJob(long _clientId, string _job)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            if (!System.Enum.TryParse(_job, out MouseJobId parsed)) return;

            var player = NetUtil.FindPlayerObject((ulong)_clientId);
            var job = player != null ? player.GetComponent<MouseJob>() : null;
            if (job != null) job.ServerSetJob(parsed);
        }

        /// <summary>QA: 깡패가 임명을 시도한다(키 없이 같은 Rpc를 탄다).</summary>
        public void TestRecruit()
        {
            var recruit = LocalComponent<GangRecruit>();
            if (recruit != null) recruit.DevRecruit();
        }

        /// <summary>
        /// QA: 지금 단계 — "Hunt"|"Gang". **PhaseDirector가 없으면 "Missing"**이다.
        ///
        /// 예전에는 없으면 `"Hunt"`를 줬다(2026-09-09 감사 T9) — `qa_v43`의
        /// 「처음에는 1단계다」가 **PhaseDirector가 통째로 안 붙은 씬에서도 통과**했다.
        /// 없는 것은 없다고 말해야 한다.
        /// </summary>
        public string TestGetPhase()
        {
            var phase = FindFirstObjectByType<PhaseDirector>();
            if (phase == null)
            {
                GameDebug.LogWarning("[TestManager] PhaseDirector 없음 — 단계를 못 읽는다");
                return MISSING;
            }
            return phase.Phase.ToString();
        }

        /// <summary>QA: 2단계 남은 시간(초). 1단계면 0.</summary>
        public float TestGetPhaseRemaining()
        {
            var phase = FindFirstObjectByType<PhaseDirector>();
            return phase != null ? phase.Remaining : -1f;
        }

        /// <summary>
        /// QA: 매치 결과 — "None"|"MouseWin"|"CatWin"|"GangWin".
        /// **MatchManager가 없으면 "Missing"**(감사 T9) — `MatchResult.None`은
        /// 「아직 진행 중」이라는 정상값이다.
        /// </summary>
        public string TestGetMatchResult()
        {
            var match = FindFirstObjectByType<MatchManager>();
            if (match == null)
            {
                GameDebug.LogWarning("[TestManager] MatchManager 없음 — 결과를 못 읽는다");
                return MISSING;
            }
            return match.Result.ToString();
        }

        /// <summary>서버 전용 — 2단계 시계를 거의 다 태운다(시간 초과 판정을 재는 데 쓴다).</summary>
        public void TestBurnPhaseClock()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var phase = FindFirstObjectByType<PhaseDirector>();
            if (nm == null || !nm.IsServer || phase == null) return;

            // NetworkVariable을 직접 못 만지므로 리플렉션 없이 갈 방법이 없다 — QA 전용 경로다.
            // **이름이 사라지면 크게 운다**(2026-09-09 감사 E-23) — 예전에는 로그 한 줄 없이
            // 아무 일도 안 해서, 시계가 안 타는데도 스위트가 통과했다.
            var field = typeof(PhaseDirector).GetField(
                "m_Remaining", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null)
            {
                GameDebug.LogError("[TestManager] PhaseDirector.m_Remaining 이름이 바뀌었다 — TestBurnPhaseClock 무동작");
                return;
            }

            var variable = field.GetValue(phase) as Unity.Netcode.NetworkVariable<float>;
            if (variable == null)
            {
                GameDebug.LogError("[TestManager] PhaseDirector.m_Remaining 타입이 바뀌었다 — TestBurnPhaseClock 무동작");
                return;
            }
            variable.Value = 0.2f;
        }
    }
}
#endif
