using CTC.Runtime.Core;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>HP. 서버만 기록, 전원 읽기(HUD). 사망 이벤트는 서버에서만 발화.</summary>
    public class Health : NetworkBehaviour
    {
        [SerializeField] private int m_MaxHp = 100;

        private readonly NetworkVariable<int> m_Hp = new NetworkVariable<int>();

        public int Hp => m_Hp.Value;

        /// <summary>HP 스트림(MVC의 Model 노출) — 구독 즉시 현재 값이 흐른다.</summary>
        public Observable<int> HpChangedRx => m_Hp.AsObservableWithCurrent();
        public int MaxHp => m_MaxHp;
        public bool IsDead => CombatLogic.IsDead(m_Hp.Value);

        /// <summary>서버 전용 사망 알림 (1회).</summary>
        public event System.Action<Health> Died;

        /// <summary>HP 변화 알림(이전, 현재). NetworkVariable 동기화로 전 클라에서 발화 — 피격 연출용.</summary>
        public event System.Action<int, int> HpChanged;

        public override void OnNetworkSpawn()
        {
            if (IsServer) m_Hp.Value = m_MaxHp;
            m_Hp.OnValueChanged += OnHpValueChanged;
        }

        public override void OnNetworkDespawn()
        {
            m_Hp.OnValueChanged -= OnHpValueChanged;
        }

        private void OnHpValueChanged(int _previous, int _current)
        {
            HpChanged?.Invoke(_previous, _current);
        }

        /// <summary>서버 전용. 실제 깎인 HP(과잉타격 제외)를 반환 — 장난기 적립용(스펙 v3.2).</summary>
        public int TakeDamage(int _damage)
        {
            if (!IsServer || IsDead) return 0;

            int before = m_Hp.Value;
            m_Hp.Value = CombatLogic.ApplyDamage(before, _damage);
            int dealt = before - m_Hp.Value;
            if (IsDead) Died?.Invoke(this);
            return dealt;
        }

        /// <summary>
        /// 서버 전용 — HP를 직접 세운다. **죽은 대상도 되살린다**(감옥 수감·석방, v3.8).
        /// <see cref="Heal"/>과 나눈 이유: 회복은 "죽으면 끝"이라는 규칙을 지켜야 하는데,
        /// 감옥은 그 규칙의 예외라서다. 예외를 Heal에 섞으면 요새 회복이 시체를 일으킨다.
        /// </summary>
        public void ServerSetHp(int _hp)
        {
            if (!IsServer) return;
            m_Hp.Value = UnityEngine.Mathf.Clamp(_hp, 0, m_MaxHp);
        }

        /// <summary>서버 전용 회복(요새 거점). 죽은 대상은 회복 불가 — 부활 없음(원작 동일).</summary>
        public void Heal(int _amount)
        {
            if (!IsServer || IsDead || _amount <= 0) return;

            m_Hp.Value = UnityEngine.Mathf.Min(m_MaxHp, m_Hp.Value + _amount);
        }
    }
}
