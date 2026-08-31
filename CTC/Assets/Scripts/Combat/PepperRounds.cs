using CTC.Runtime.Core;
using Unity.Netcode;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 후추 탄 보유 플래그(쥐 프리팹, 빌드 심화 v1 · 후추 선반 ①). 상점 구매로 켜지고
    /// 판이 끝날 때까지 남는다. 실제 도트 적용은 MousePopgun 명중 처리에서.
    /// </summary>
    public class PepperRounds : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> m_Owned = new NetworkVariable<bool>();

        public bool Owned => m_Owned.Value;

        /// <summary>보유 스트림(HUD "보유" 표시용) — 구독 즉시 현재 값이 흐른다.</summary>
        public R3.Observable<bool> OwnedChanged => m_Owned.AsObservableWithCurrent();

        /// <summary>서버 전용(ShopController).</summary>
        public void ServerSetOwned()
        {
            if (!IsServer) return;
            m_Owned.Value = true;
        }
    }
}
