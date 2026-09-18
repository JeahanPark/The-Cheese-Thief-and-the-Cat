using CTC.Runtime.Combat;
using CTC.Runtime.Match;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 용사 무적 (2026-09-19, 기획 `2026-09-19-hero-sword-design.md` §1·§5-2).
    ///
    /// 용사가 **Space**를 누르면 <c>HeroInvincibleSeconds</c>(3초) 동안 피해를 **아예 안 받는다**(<see cref="IDamageImmunity"/>) ·
    /// 쿨 <c>HeroInvincibleCooldown</c>(10초). 켜 있는 동안에도 칼은 휘두를 수 있다.
    /// 용사가 아니면 아무 일도 안 한다 — 쥐의 Space는 대시(<see cref="MouseDash"/>)가 읽고, 용사면 그쪽이 양보한다.
    ///
    /// 켠 시각은 서버가 복제값으로 찍는다 — 판정(면역)도 표시도 전부 그 값 하나를 본다.
    /// </summary>
    public class HeroInvincible : NetworkBehaviour, IDamageImmunity
    {
        private readonly NetworkVariable<float> m_StartedAt = new NetworkVariable<float>(-1000f);

        private MouseJob m_Job;

        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;

        /// <summary>그 몸이 용사인가 — 대시가 Space를 양보할지 볼 때 쓴다.</summary>
        public static bool IsHero(Component _body)
        {
            if (_body == null) return false;
            var job = _body.GetComponent<MouseJob>();
            return job != null && job.Value == MouseJobId.Hero;
        }

        private float Now => (float)NetworkManager.ServerTime.Time;

        /// <summary>지금 무적인가.</summary>
        public bool On => IsSpawned && HeroInvincibleLogic.On(m_StartedAt.Value, Now, Balance.HeroInvincibleSeconds);

        /// <summary>다시 켤 수 있을 때까지 남은 초(0 = 지금 켤 수 있다). HUD·QA가 본다.</summary>
        public float CooldownRemaining
            => IsSpawned ? Mathf.Max(0f, m_StartedAt.Value + Balance.HeroInvincibleCooldown - Now) : 0f;

        /// <summary>무적이 끝날 때까지 남은 초(꺼져 있으면 0). 능력 바·무적 테두리가 본다.</summary>
        public float ActiveRemaining
            => IsSpawned ? Mathf.Max(0f, m_StartedAt.Value + Balance.HeroInvincibleSeconds - Now) : 0f;

        public float ActiveTotal => Balance.HeroInvincibleSeconds;
        public float CooldownTotal => Balance.HeroInvincibleCooldown;

        public bool ServerImmune => On;

        /// <summary>
        /// 무적이 켜진 순간(전 클라 — 복제값이 바뀌는 자리). 늦게 들어온 사람에게는 스폰 때 켜져 있으면 한 번 울린다.
        /// 무적 테두리(<see cref="HeroInvincibleVisual"/>)가 구독한다.
        /// </summary>
        public event System.Action Started;

        private void Awake() => m_Job = GetComponent<MouseJob>();

        public override void OnNetworkSpawn()
        {
            m_StartedAt.OnValueChanged += OnStartedAtChanged;
            if (On) Started?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            m_StartedAt.OnValueChanged -= OnStartedAtChanged;
        }

        private void OnStartedAtChanged(float _old, float _new)
        {
            if (_new > _old) Started?.Invoke();
        }

        private void Update()
        {
            if (!LocalControl.Mine(this)) return;
            if (Core.UiFocus.BlocksGameplay) return;   // 채팅 타이핑·옵션 창 중에는 조작하지 않는다
            if (m_Job == null || m_Job.Value != MouseJobId.Hero) return;

            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.spaceKey.wasPressedThisFrame) return;

            UseRpc();
        }

        /// <summary>봇·QA 전용 — 사람의 Space와 같은 입구.</summary>
        public void DevUse() => UseRpc();

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void UseRpc()
        {
            if (m_Job == null || m_Job.Value != MouseJobId.Hero) return;
            if (ActionLock.Locked(this)) return;   // 변신·스턴 중에는 못 켠다(서버가 보는 값)
            if (!HeroInvincibleLogic.Ready(m_StartedAt.Value, Now, Balance.HeroInvincibleCooldown)) return;

            m_StartedAt.Value = Now;
            OnFxRpc();
        }

        // 켜는 순간 전 클라에 한 번 — 흰 빛이 퍼진다(§11: 한 번 터지고 끝난다. 지속 표시는 무적 테두리가 따로 한다).
        [Rpc(SendTo.Everyone)]
        private void OnFxRpc()
        {
            Fx.FxPlayer.PlayAt(Fx.FxId.HeroInvincible, transform.position + Vector3.up * 0.4f);
        }
    }
}
