using CTC.Runtime.Core;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 후추 도트 시전체 — **서버가 피격자에게 런타임으로 붙인다**(규칙 7: 기능 1개 = 컴포넌트 1개).
    /// NetworkBehaviour가 아니다: 틱은 서버만 돌고, 깎인 HP는 Health의 NetworkVariable이 나른다.
    /// 클라이언트엔 이 컴포넌트가 아예 없다. 만료되면 스스로 떨어진다.
    /// </summary>
    public class PepperDot : MonoBehaviour
    {
        private Health m_Health;
        private int m_Stacks;
        private double m_ExpireAt;
        private float m_Carry;

        /// <summary>서버 전용 — 피격 1회. 없으면 붙이고, 있으면 스택·만료 갱신.</summary>
        public static void ServerApply(Health _target, int _maxStacks, float _seconds)
        {
            if (_target == null || _target.IsDead) return;

            PepperDot dot = _target.GetComponent<PepperDot>();
            if (dot == null)
            {
                dot = _target.gameObject.AddComponent<PepperDot>();
                dot.m_Health = _target;
            }
            dot.m_Stacks = PepperDotLogic.AddStack(dot.m_Stacks, _maxStacks);
            dot.m_ExpireAt = NetworkManager.Singleton.ServerTime.Time + _seconds;
        }

        private void Update()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) { Destroy(this); return; }

            if (m_Health == null || m_Health.IsDead
                || PepperDotLogic.IsExpired(m_ExpireAt, nm.ServerTime.Time))
            {
                Destroy(this);
                return;
            }

            float dps = m_Stacks * Info.InfoManager.Instance.Balance.PepperDotDamage;
            int damage = PepperDotLogic.Accumulate(Time.deltaTime, dps, ref m_Carry);
            if (damage > 0) m_Health.TakeDamage(damage);
        }
    }
}
