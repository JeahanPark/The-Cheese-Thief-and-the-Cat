using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 판이 어느 단계인가를 서버가 정한다 (v3.9 — 쥐 직업 2단계).
    ///
    /// **고양이가 죽어도 판이 안 끝날 수 있다.** 깡패가 있으면 2단계가 열리고,
    /// 깡패 팀(보스+부하)이 나머지 쥐를 전멸시켜야 이긴다. 깡패가 없으면 지금까지의 규칙대로 쥐 승리다.
    ///
    /// 2단계에는 **새 시계 3분**을 준다. 남은 시간을 쓰면 고양이가 9분에 죽었을 때 2단계가
    /// 1분이라 보스가 아무것도 못 하고 끝난다.
    ///
    /// 2단계에는 **구출이 없다**(`JailDirector`가 이 값을 본다). 구출은 "쥐는 한 팀"이라는
    /// 전제 위의 장치라, 서로 죽이는 판에서 적팀 쥐가 구하러 오는 그림은 성립하지 않는다.
    /// </summary>
    public class PhaseDirector : NetworkBehaviour
    {
        private readonly NetworkVariable<MatchPhase> m_Phase = new NetworkVariable<MatchPhase>(MatchPhase.Hunt);
        private readonly NetworkVariable<float> m_Remaining = new NetworkVariable<float>();

        public MatchPhase Phase => m_Phase.Value;

        /// <summary>2단계 남은 시간(초). 1단계에서는 의미 없다.</summary>
        public float Remaining => m_Remaining.Value;

        public Observable<MatchPhase> PhaseChanged => m_Phase.AsObservableWithCurrent();
        public Observable<float> RemainingChanged => m_Remaining.AsObservableWithCurrent();

        /// <summary>2단계인가 — 구출·승패 판정이 이걸 보고 갈린다.</summary>
        public bool IsGangPhase => m_Phase.Value == MatchPhase.Gang;

        /// <summary>
        /// 서버 전용 — 고양이가 죽었다. 2단계로 갈지 답한다(false면 호출부가 지금까지대로 쥐 승리).
        /// </summary>
        public bool ServerTryEnterGangPhase()
        {
            if (!IsServer || m_Phase.Value != MatchPhase.Hunt) return false;
            if (!MouseJobLogic.ShouldEnterGangPhase(CountGang())) return false;

            m_Phase.Value = MatchPhase.Gang;
            m_Remaining.Value = MouseJobLogic.GangPhaseSeconds;
            EnterFxRpc();
            return true;
        }

        /// <summary>서버 전용 — 로비로 돌아갈 때 되돌린다.</summary>
        public void ServerReset()
        {
            if (!IsServer) return;
            m_Phase.Value = MatchPhase.Hunt;
            m_Remaining.Value = 0f;
        }

        private void Update()
        {
            if (!IsServer || m_Phase.Value != MatchPhase.Gang) return;
            if (m_Remaining.Value <= 0f) return;

            m_Remaining.Value = Mathf.Max(0f, m_Remaining.Value - Time.deltaTime);
        }

        /// <summary>2단계 승패 — 시간이 다 됐으면 나머지 쥐가 이긴다(시계를 쥔 쪽이 쫓는 쪽이다).</summary>
        public MatchResult EvaluateGangPhase()
        {
            if (m_Phase.Value != MatchPhase.Gang) return MatchResult.None;

            CountSides(out int gang, out int other);
            return MouseJobLogic.EvaluateGangPhase(gang, other, m_Remaining.Value <= 0f);
        }

        private int CountGang()
        {
            int count = 0;
            foreach (MouseJob job in FindObjectsByType<MouseJob>(FindObjectsSortMode.None))
            {
                if (job.Value == MouseJobId.Gang) count++;
            }
            return count;
        }

        // 갇힌 쥐는 살아 있는 것으로 안 센다 — 2단계에는 구출이 없어 다시는 못 나온다.
        private void CountSides(out int _gang, out int _other)
        {
            _gang = 0;
            _other = 0;

            foreach (MouseJob job in FindObjectsByType<MouseJob>(FindObjectsSortMode.None))
            {
                var tag = job.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Mouse) continue;

                var health = job.GetComponent<Health>();
                if (health != null && health.IsDead) continue;

                var jailed = job.GetComponent<JailedTag>();
                if (jailed != null && jailed.Jailed) continue;

                if (job.IsGangSide) _gang++;
                else _other++;
            }
        }

        [Rpc(SendTo.Everyone)]
        private void EnterFxRpc()
        {
            Audio.SfxPlayer.PlayUi(Audio.SfxId.GangRise);
        }
    }
}
