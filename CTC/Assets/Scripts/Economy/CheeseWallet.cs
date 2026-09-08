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

        // 지금까지 **번** 총액(차감과 무관하게 늘기만 한다). 고양이 전직 문턱이 이 값을 본다
        // (2026-09-09, 계획 3 Task 5) — 잔액으로 재면 테크를 사는 순간 문턱이 되돌아가
        // "전직하려면 아무것도 사지 마라"가 된다. 전 클라 읽기다: 전직 창(View)이 구독한다.
        private readonly NetworkVariable<int> m_Earned = new NetworkVariable<int>();

        /// <summary>잔액 스트림 — 구독 즉시 현재 값이 한 번 흐른다.</summary>
        public Observable<int> Cheese => m_Cheese.AsObservableWithCurrent();

        /// <summary>즉시 값(서버 판정·QA용). 표시는 <see cref="Cheese"/>를 구독한다.</summary>
        public int CheeseValue => m_Cheese.Value;

        /// <summary>누적 획득 스트림 — 전직 창이 폴링 없이 문턱을 안다(규칙 3).</summary>
        public Observable<int> Earned => m_Earned.AsObservableWithCurrent();

        /// <summary>누적 획득 즉시 값(서버 판정·QA용).</summary>
        public int EarnedValue => m_Earned.Value;

        /// <summary>서버 전용. 벌어들인 치즈 — 잔액과 누적 획득이 함께 오른다.</summary>
        public void AddCheese(int _amount)
        {
            if (!IsServer || _amount <= 0) return;
            m_Cheese.Value += _amount;
            m_Earned.Value += _amount;
        }

        /// <summary>
        /// 서버 전용. **되돌려 받는** 치즈 — 잔액만 오르고 누적 획득은 그대로다(창고 파괴 환급).
        /// 환급이 누적에 섞이면 "내 구조물을 짓고 부수기"가 문턱을 채우는 펌프가 된다.
        /// 지금 환급이 있는 것은 쥐 테크(창고)뿐이라 고양이 전직에는 안 닿지만,
        /// 규칙을 값의 성격에 맞춰 둔다 — 나중에 환급이 붙는 고양이 테크가 생겨도 안 샌다.
        /// </summary>
        public void RefundCheese(int _amount)
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
