using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 부숴도 고양이에게 장난기가 붙지 않는 대상 표시 (2026-08-09).
    ///
    /// <see cref="CatPounce"/>는 `!IsPlayerObject`인 모든 대상을 구조물로 보고 장난기를 준다.
    /// 그래서 벽감처럼 <see cref="Health"/>를 가진 지형물을 맵에 깔면 **자동으로 고양이 수입원**이 된다.
    /// v3.5가 정확히 이 이유로 실패했다 — "벽 때리기가 최고 효율이 되어 쥐를 쫓지 않음".
    ///
    /// 장난기의 설계 축은 **"쥐의 방어 지출 = 고양이 수입"**이다. 맵에 원래 있던 벽은
    /// 쥐가 쓴 돈이 아니므로 값을 치를 이유가 없다. 이 컴포넌트가 붙어 있으면 적립을 건너뛴다.
    ///
    /// 데이터가 없는 순수 표식이라 비어 있다(`RoleTag`처럼 값을 들지 않는 쪽).
    /// </summary>
    public class BountyExempt : MonoBehaviour
    {
    }
}
