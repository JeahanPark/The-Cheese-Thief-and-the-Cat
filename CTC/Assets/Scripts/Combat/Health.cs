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
        public int MaxHp => m_MaxHp;
        public bool IsDead => CombatLogic.IsDead(m_Hp.Value);

        /// <summary>서버 전용 사망 알림 (1회).</summary>
        public event System.Action<Health> Died;

        public override void OnNetworkSpawn()
        {
            if (IsServer) m_Hp.Value = m_MaxHp;
        }

        public void TakeDamage(int _damage)
        {
            if (!IsServer || IsDead) return;

            m_Hp.Value = CombatLogic.ApplyDamage(m_Hp.Value, _damage);
            if (IsDead) Died?.Invoke(this);
        }
    }
}
