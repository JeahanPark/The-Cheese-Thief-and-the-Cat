using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 두꺼운 가죽(고양이 방어 테크, 테크 v3 계획 2b) — 살 때마다 최대 HP를 그 레벨 값으로 올린다.
    ///
    /// 레벨을 들고 있는 것은 <see cref="TechSlots"/>다. 여기가 하는 일은 "샀을 때 몸에 반영"
    /// 하나뿐이라 상태를 갖지 않는다 — 기능 1개 = 컴포넌트 1개(규칙 7).
    /// 값(레벨별 최대 HP)의 원본은 TechInfo.csv P1이다.
    /// </summary>
    public class CatHide : NetworkBehaviour
    {
        /// <summary>서버 전용(ShopController). 그 레벨의 최대 HP를 몸에 반영한다. 내리지는 않는다.</summary>
        public void ServerApplyLevel(int _level)
        {
            if (!IsServer) return;

            int max = Mathf.RoundToInt(TechLogic.ParamOf(TechId.CatHide, _level, 1));
            if (max <= 0) return;   // 표를 못 읽었다 — 0을 넘기면 최대 HP가 0인 고양이가 된다

            Health health = GetComponent<Health>();
            if (health != null) health.ServerRaiseMaxHp(max);
        }
    }
}
