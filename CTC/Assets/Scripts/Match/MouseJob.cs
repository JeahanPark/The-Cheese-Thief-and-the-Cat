using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 쥐가 무슨 직업인가 (v3.9). **상태만 갖는다** — 임명은 `GangRecruit`,
    /// 단계 전환은 `PhaseDirector`가 한다(기능 1개 = 컴포넌트 1개).
    ///
    /// 전원이 읽는다. 2단계에서 누가 누구 편인지 알아야 쏠 수 있고, HUD도 이걸 본다.
    ///
    /// **부하는 거절도 배신도 못 한다.** 그래서 이 값은 본인이 못 바꾼다 —
    /// 상자에서 주운 직업권(서버)과 보스의 임명(서버)만이 여기를 건드린다.
    /// </summary>
    public class MouseJob : NetworkBehaviour
    {
        private readonly NetworkVariable<MouseJobId> m_Job = new NetworkVariable<MouseJobId>(MouseJobId.None);

        public MouseJobId Value => m_Job.Value;

        public Observable<MouseJobId> JobChanged => m_Job.AsObservableWithCurrent();

        /// <summary>깡패 편인가(보스 또는 부하).</summary>
        public bool IsGangSide => MouseJobLogic.IsGangSide(m_Job.Value);

        /// <summary>
        /// 서버 전용 — 직업을 새긴다. 용사는 여기서 몸이 세진다.
        /// 이미 직업이 있으면 바뀌지 않는다(주운 사람이 임자, 임명은 한 번).
        /// </summary>
        public void ServerSetJob(MouseJobId _job)
        {
            if (!IsServer || m_Job.Value != MouseJobId.None) return;

            m_Job.Value = _job;
            if (_job == MouseJobId.Hero) ApplyHeroBody();

            JobFxRpc(_job);
        }

        // 용사는 "그냥 세진다" — 몸(HP)과 화력을 같이 올린다.
        // HP는 최대치를 올린 뒤 그만큼 채워 준다. 안 그러면 최대치만 늘고 지금 HP는 그대로다.
        private void ApplyHeroBody()
        {
            var health = GetComponent<Health>();
            if (health == null) return;

            int raised = Mathf.RoundToInt(health.MaxHp * MouseJobLogic.HeroHpMultiplier);
            health.ServerSetMaxHp(raised);
            health.ServerSetHp(raised);
        }

        [Rpc(SendTo.Everyone)]
        private void JobFxRpc(MouseJobId _job)
        {
            // 깡패 등장은 판의 전환점이라 멀리까지 알린다 — 다른 쥐가 대비할 수 있어야 한다.
            bool loud = _job == MouseJobId.Gang;
            Audio.SfxPlayer.PlayAt(
                loud ? Audio.SfxId.GangRise : Audio.SfxId.JobPickup, transform.position, 1f,
                loud ? Info.InfoManager.Instance.Balance.CatSkillSoundMeters : 12f);
            Fx.FxPlayer.PlayAt(_job == MouseJobId.Hero ? Fx.FxId.HeroBurst : Fx.FxId.GangSmoke, transform.position);
        }
    }
}
