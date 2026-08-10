// TestManager의 일부 — 쥐 직업과 2단계 (v3.9)
#if UNITY_EDITOR
using CTC.Runtime.Core;
using CTC.Runtime.Match;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>QA: 로컬 쥐의 직업 — "None"|"Hero"|"Gang"|"Minion".</summary>
        public string TestGetMouseJob()
        {
            var job = LocalComponent<MouseJob>();
            return job != null ? job.Value.ToString() : "None";
        }

        /// <summary>QA: 지정 클라이언트의 직업(복제 확인용).</summary>
        public string TestGetMouseJobOf(long _clientId)
        {
            var player = NetUtil.FindPlayerObject((ulong)_clientId);
            var job = player != null ? player.GetComponent<MouseJob>() : null;
            return job != null ? job.Value.ToString() : "None";
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

        /// <summary>QA: 지금 단계 — "Hunt"|"Gang".</summary>
        public string TestGetPhase()
        {
            var phase = FindFirstObjectByType<PhaseDirector>();
            return phase != null ? phase.Phase.ToString() : "Hunt";
        }

        /// <summary>QA: 2단계 남은 시간(초). 1단계면 0.</summary>
        public float TestGetPhaseRemaining()
        {
            var phase = FindFirstObjectByType<PhaseDirector>();
            return phase != null ? phase.Remaining : -1f;
        }

        /// <summary>QA: 매치 결과 — "None"|"MouseWin"|"CatWin"|"GangWin".</summary>
        public string TestGetMatchResult()
        {
            var match = FindFirstObjectByType<MatchManager>();
            return match != null ? match.Result.ToString() : "None";
        }

        /// <summary>서버 전용 — 2단계 시계를 거의 다 태운다(시간 초과 판정을 재는 데 쓴다).</summary>
        public void TestBurnPhaseClock()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var phase = FindFirstObjectByType<PhaseDirector>();
            if (nm == null || !nm.IsServer || phase == null) return;

            // NetworkVariable을 직접 못 만지므로 리플렉션 없이 갈 방법이 없다 — QA 전용 경로다.
            var field = typeof(PhaseDirector).GetField(
                "m_Remaining", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var variable = field?.GetValue(phase) as Unity.Netcode.NetworkVariable<float>;
            if (variable != null) variable.Value = 0.2f;
        }
    }
}
#endif
