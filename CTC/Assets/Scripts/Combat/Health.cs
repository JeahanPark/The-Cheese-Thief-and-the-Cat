using CTC.Runtime.Core;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>피해를 받기 전에 일부를 다른 자원으로 대신 내는 것(치즈 방패). 서버에서만 불린다.</summary>
    public interface IDamageAbsorber
    {
        int ServerAbsorb(int _damage);   // 돌려주는 값 = HP로 갈 피해
    }

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

        /// <summary>
        /// 서버 전용. 실제 깎인 HP(과잉타격 제외)를 반환 — 장난기 적립용(스펙 v3.2).
        /// 흡수(치즈 방패)는 **HP를 쓰기 전에** 끼어들어 피해를 줄이므로, 돌려주는 값은
        /// 치즈로 막고 남은 "정말 깎인 HP"다 — 고양이는 치즈가 막아 준 몫으로는 장난기를 못 번다(의도).
        /// </summary>
        public int TakeDamage(int _damage)
        {
            if (!IsServer || IsDead) return 0;

            IDamageAbsorber absorber = GetComponent<IDamageAbsorber>();
            if (absorber != null) _damage = absorber.ServerAbsorb(_damage);

            int before = m_Hp.Value;
            m_Hp.Value = CombatLogic.ApplyDamage(before, _damage);
            int dealt = before - m_Hp.Value;
            if (IsDead) Died?.Invoke(this);
            return dealt;
        }

        /// <summary>
        /// 서버 전용 — HP를 직접 세운다. **죽은 대상도 되살린다**(직업 승격 등 규칙 밖의 보정용).
        /// <see cref="Heal"/>과 나눈 이유: 회복은 "죽으면 끝"이라는 규칙을 지켜야 하는데,
        /// 예외를 Heal에 섞으면 회복 효과가 시체를 일으킨다.
        /// </summary>
        public void ServerSetHp(int _hp)
        {
            if (!IsServer) return;
            m_Hp.Value = UnityEngine.Mathf.Clamp(_hp, 0, m_MaxHp);
        }

        /// <summary>
        /// 서버 전용 — 최대 HP를 바꾼다. **스폰 전에 부른다**: `OnNetworkSpawn`이 현재 HP를
        /// 최대치로 채우므로 그전에 세워 두면 그대로 시작한다.
        /// 같은 프리팹을 다른 단단함으로 쓰는 곳이 근거다(치즈 벽감 vs 강아지 벽감, v3.8 5번).
        /// </summary>
        public void ServerSetMaxHp(int _maxHp)
        {
            if (_maxHp <= 0) return;

            m_MaxHp = _maxHp;
            if (IsSpawned && IsServer) m_Hp.Value = UnityEngine.Mathf.Clamp(m_Hp.Value, 0, m_MaxHp);
        }

        /// <summary>서버 전용 회복(요새 거점). 죽은 대상은 회복 불가 — 부활 없음(원작 동일).</summary>
        public void Heal(int _amount)
        {
            if (!IsServer || IsDead || _amount <= 0) return;

            m_Hp.Value = UnityEngine.Mathf.Min(m_MaxHp, m_Hp.Value + _amount);
        }
    }
}
