using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 개인 치즈 지갑. 서버만 기록(수입·차감 판정 권위), 전원 읽기(자기 HUD·관전).
    /// 쥐·고양이 프리팹 양쪽에 붙는다 — 원작처럼 술래(고양이)도 수입 가능.
    /// </summary>
    public class CheeseWallet : NetworkBehaviour
    {
        private readonly NetworkVariable<int> m_Cheese = new NetworkVariable<int>();

        public int Cheese => m_Cheese.Value;

        /// <summary>서버 전용.</summary>
        public void AddCheese(int _amount)
        {
            if (!IsServer || _amount <= 0) return;
            m_Cheese.Value += _amount;
        }

        /// <summary>서버 전용. 잔액이 충분할 때만 차감하고 성공 여부를 돌려준다(상점 구매).</summary>
        public bool TrySpend(int _amount)
        {
            if (!IsServer || _amount <= 0 || m_Cheese.Value < _amount) return false;

            m_Cheese.Value -= _amount;
            return true;
        }

        /// <summary>서버 전용. 잔액 한도 내에서 있는 만큼 차감(유지비 — 부족해도 남은 걸 다 먹음).</summary>
        public void SpendUpTo(int _amount)
        {
            if (!IsServer || _amount <= 0) return;
            m_Cheese.Value -= Mathf.Min(m_Cheese.Value, _amount);
        }
    }
}
