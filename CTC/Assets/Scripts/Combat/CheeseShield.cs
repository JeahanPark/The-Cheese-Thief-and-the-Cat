using CTC.Runtime.Economy;
using Unity.Netcode;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 치즈 방패 — 쥐 프리팹에만 붙는다. 레벨은 TechSlots, 비율은 TechInfo P1,
    /// 환율(1 HP = 몇 치즈)은 BalanceInfo.CheeseShieldRate.
    /// </summary>
    public class CheeseShield : NetworkBehaviour, IDamageAbsorber
    {
        // 총알·덮치기 한 방마다 부르는 경로라 GetComponent를 매 타격 돌리지 않는다(리뷰 4).
        private CheeseWallet m_Wallet;
        private TechSlots m_Slots;
        private Health m_Health;

        private void Awake()
        {
            m_Wallet = GetComponent<CheeseWallet>();
            m_Slots = GetComponent<TechSlots>();
            m_Health = GetComponent<Health>();
        }

        public int ServerAbsorb(int _damage)
        {
            if (!IsServer) return _damage;

            // Unity 오브젝트라 != null로 검사한다(규칙 9).
            int level = m_Slots != null ? m_Slots.LevelOf(TechId.CheeseShield) : 0;
            float ratio = TechLogic.ParamOf(TechId.CheeseShield, level, 1);
            if (ratio <= 0f || m_Wallet == null || m_Health == null) return _damage;

            CheeseShieldLogic.Absorb(_damage, ratio, Info.InfoManager.Instance.Balance.CheeseShieldRate,
                m_Wallet.CheeseValue, m_Health.Hp, out int hpDamage, out int cheeseCost);
            // 낼 게 없으면 지갑을 건드리지 않는다 — 결과가 같은데 차감만 하는 일이 없도록.
            if (cheeseCost <= 0) return _damage;
            if (m_Wallet.TrySpend(cheeseCost)) return hpDamage;
            return _damage;   // 차감 실패(경쟁)면 원래 피해
        }
    }
}
