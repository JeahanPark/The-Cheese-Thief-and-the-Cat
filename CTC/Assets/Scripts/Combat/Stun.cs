using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 굳어 있는 상태 (2026-09-19, 용사 칼 — 기획 `2026-09-19-hero-sword-design.md` §4).
    ///
    /// 서버가 도장을 찍고 전 클라가 같은 값을 본다. 입력 가드(<see cref="Player.ActionLock"/>)는
    /// 소유자 쪽에서 이 값을 보고 끊고, 서버 Rpc 입구도 같은 값으로 거절한다 —
    /// 클라가 속여도 서버가 다시 본다(`GangTransformLock`과 같은 규약).
    ///
    /// 고양이·쥐·강아지 전부에 붙는다. 진영을 가리지 않는다 — 2단계에서 용사가 깡패도 멈춘다.
    /// </summary>
    public class Stun : NetworkBehaviour
    {
        private readonly NetworkVariable<float> m_Until = new NetworkVariable<float>();

        /// <summary>스턴이 걸린 순간마다 울린다(전 클라) — 머리 위 별 이펙트가 구독한다.</summary>
        public event System.Action<float> Applied;

        /// <summary>지금 굳어 있는가.</summary>
        public bool Active => IsSpawned && StunLogic.Stunned(m_Until.Value, (float)NetworkManager.ServerTime.Time);

        /// <summary>QA·HUD: 남은 초(안 굳었으면 0).</summary>
        public float Remaining => Active ? m_Until.Value - (float)NetworkManager.ServerTime.Time : 0f;

        public override void OnNetworkSpawn()
        {
            m_Until.OnValueChanged += OnUntilChanged;
        }

        public override void OnNetworkDespawn()
        {
            m_Until.OnValueChanged -= OnUntilChanged;
        }

        /// <summary>서버 전용 — 스턴을 건다(겹치면 늦게 끝나는 쪽으로 갱신, 더하지 않는다).</summary>
        public void ServerApply(float _seconds)
        {
            if (!IsServer || _seconds <= 0f) return;
            float now = (float)NetworkManager.ServerTime.Time;
            m_Until.Value = StunLogic.NextUntil(m_Until.Value, now, _seconds);
        }

        // 복제값이 바뀌는 자리 = 전 클라가 도는 길 — 이펙트는 여기서만 띄운다(서버 판정 자리 금지, CLAUDE.md 기술 이펙트).
        private void OnUntilChanged(float _old, float _new)
        {
            if (_new <= _old) return;

            float seconds = Mathf.Max(0f, _new - (float)NetworkManager.ServerTime.Time);
            Fx.FxPlayer.PlayAt(Fx.FxId.Stunned, transform.position);
            // 쥐·고양이만 굳는 자세가 있다(강아지는 드라이버가 없다) — 없으면 그냥 멈춰 선다.
            var driver = GetComponent<Player.PlayerAnimationDriver>();
            if (driver != null && seconds > 0f) driver.PlayOneShot(Player.AnimationStateLogic.Stunned, seconds);
            Applied?.Invoke(_new);
        }

        /// <summary>그 몸이 굳어 있는가 — 컴포넌트가 없으면 거짓.</summary>
        public static bool Locked(Component _body)
        {
            if (_body == null) return false;
            var stun = _body.GetComponent<Stun>();
            return stun != null && stun.Active;
        }
    }
}
