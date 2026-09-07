using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 상점 자리 마커 (테크 v3). 쥐는 이 반경 안에서만 테크를 사고 키운다 — 시간 결핍의 실체.
    /// 부술 수 없다(Health 없음). 반경은 BalanceInfo.ShopSpotRadius.
    /// </summary>
    public class ShopSpot : NetworkBehaviour
    {
        private static float Radius => Info.InfoManager.Instance.Balance.ShopSpotRadius;

        /// <summary>이 지점이 어느 상점 안인가 — 서버 검증·핫바 모드 공용.</summary>
        public static bool AnyContains(Vector3 _point)
        {
            foreach (ShopSpot spot in FindObjectsByType<ShopSpot>(FindObjectsSortMode.None))
            {
                if (!spot.IsSpawned) continue;
                if (ShopSpotLogic.Contains(spot.transform.position, _point, Radius)) return true;
            }
            return false;
        }
    }
}
