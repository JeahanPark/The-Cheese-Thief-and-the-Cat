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

        /// <summary>
        /// QA: 3판 시리즈 상태 — "round|catWins|mouseWins|active". 컴포넌트가 없으면 "Missing".
        ///
        /// 시리즈는 **판 사이 전이가 전부**인데(봇 재투입·단계 초기화·시작 조건) 그걸 읽는 훅이
        /// 하나도 없었다 — 2·3판에 봇이 안 서는 것을 아무도 못 봤다(2026-09-09 감사 P0-4).
        /// 복제되는 NetworkVariable이라 어느 인스턴스에서 물어도 같다.
        /// </summary>
        public string TestGetSeries()
        {
            var series = FindFirstObjectByType<MatchSeries>();
            if (series == null)
            {
                GameDebug.LogWarning("[TestManager] MatchSeries 없음 — 시리즈를 못 읽는다");
                return MISSING;
            }
            return $"{series.Round}|{series.CatWins}|{series.MouseWins}|{series.Active}";
        }

        /// <summary>QA: 지금 열려 있는 씬 이름 — 시리즈가 대기실로 되돌아갔는지 본다.</summary>
        public string TestGetActiveSceneName()
            => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        /// <summary>
        /// 서버 전용 — 단계를 2단계(깡패)로 **직접** 세운다. <c>true</c>면 섰다.
        ///
        /// 왜 필요한가: 「2단계로 끝난 판 뒤 단계가 남는가」(2026-09-09 감사 P0-5)를 재려면
        /// **판이 2단계에서 끝난 상태**를 만들어야 하는데, 정상 경로는 깡패 쥐(사람)와
        /// 고양이 사망이 둘 다 필요하다. 봇만 도는 판에서는 봇에 clientId가 없어
        /// <see cref="TestSetMouseJob"/>으로 깡패를 만들 수 없다.
        /// 여기서 만드는 것은 **전제**뿐이고, 재는 것(다음 판이 Hunt로 돌아오는가)은 그대로다.
        /// </summary>
        public bool TestForceGangPhase()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var phase = FindFirstObjectByType<PhaseDirector>();
            if (nm == null || !nm.IsServer || phase == null)
            {
                GameDebug.LogWarning("[TestManager] TestForceGangPhase — 서버가 아니거나 PhaseDirector가 없다");
                return false;
            }

            // TestBurnPhaseClock과 같은 이유로 리플렉션이다 — 이름이 사라지면 **크게 운다**.
            var field = typeof(PhaseDirector).GetField(
                "m_Phase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var variable = field != null
                ? field.GetValue(phase) as Unity.Netcode.NetworkVariable<MatchPhase>
                : null;
            if (variable == null)
            {
                GameDebug.LogError("[TestManager] PhaseDirector.m_Phase 이름·타입이 바뀌었다 — TestForceGangPhase 무동작");
                return false;
            }
            variable.Value = MatchPhase.Gang;
            return true;
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
