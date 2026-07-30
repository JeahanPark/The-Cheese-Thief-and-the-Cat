using CTC.Runtime.Core;
using R3;
using Unity.Netcode;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 개인 치즈 지갑. 서버만 기록(수입·차감 판정 권위), 전원 읽기(자기 HUD·관전).
    /// 쥐·고양이 프리팹 양쪽에 붙는다 — 원작처럼 술래(고양이)도 수입 가능.
    ///
    /// MVC의 Model (개편 3b, 2026-07-31): 값은 `NetworkVariable`이 소유하고
    /// View에는 <see cref="Cheese"/> 스트림으로만 내보낸다 — View가 매 프레임 읽지 않는다.
    /// </summary>
    public class CheeseWallet : NetworkBehaviour
    {
        private readonly NetworkVariable<int> m_Cheese = new NetworkVariable<int>();

        /// <summary>잔액 스트림 — 구독 즉시 현재 값이 한 번 흐른다.</summary>
        public Observable<int> Cheese => m_Cheese.AsObservableWithCurrent();

        /// <summary>즉시 값(서버 판정·QA용). 표시는 <see cref="Cheese"/>를 구독한다.</summary>
        public int CheeseValue => m_Cheese.Value;

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
    }
}
