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
        public int ServerAbsorb(int _damage)
        {
            if (!IsServer) return _damage;

            float ratio = TechOwner.Param(gameObject, TechId.CheeseShield, 1);
            CheeseWallet wallet = GetComponent<CheeseWallet>();
            if (ratio <= 0f || wallet == null) return _damage;

            CheeseShieldLogic.Absorb(_damage, ratio, Info.InfoManager.Instance.Balance.CheeseShieldRate,
                wallet.CheeseValue, out int hpDamage, out int cheeseCost);
            // 낼 게 없으면 지갑을 건드리지 않는다 — 결과가 같은데 차감만 하는 일이 없도록.
            if (cheeseCost <= 0) return _damage;
            if (wallet.TrySpend(cheeseCost)) return hpDamage;
            return _damage;   // 차감 실패(경쟁)면 원래 피해
        }
    }
}
