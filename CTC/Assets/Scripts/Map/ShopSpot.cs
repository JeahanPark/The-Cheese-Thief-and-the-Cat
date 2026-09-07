using System.Collections.Generic;
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
        /// <summary>반경 원반 자식 이름 — 프리팹을 굽는 쪽(ShopSpotPrefabFactory)과 같이 본다.</summary>
        public const string DISC_NAME = "RadiusDisc";

        // 스폰된 상점 명부. `FindObjectsByType`를 안 쓰는 이유: 핫바가 **매 프레임** "지금 상점 안이냐"를
        // 묻는다(CTC 규칙 3 — Update에서 오브젝트 탐색 금지). 명부에 있다는 것이 곧 스폰됐다는 뜻이라
        // IsSpawned를 따로 안 본다.
        private static readonly List<ShopSpot> s_All = new List<ShopSpot>();

        /// <summary>스폰된 상점 자리 전부(읽기 전용) — 미니맵이 상점 마커를 여기서 읽는다.</summary>
        public static IReadOnlyList<ShopSpot> All => s_All;

        private static float Radius => Info.InfoManager.Instance.Balance.ShopSpotRadius;

        /// <summary>이 지점이 어느 상점 안인가 — 서버 검증·핫바 모드 공용.</summary>
        public static bool AnyContains(Vector3 _point)
        {
            float r = Radius;
            foreach (ShopSpot spot in s_All)
            {
                if (ShopSpotLogic.Contains(spot.transform.position, _point, r)) return true;
            }
            return false;
        }

        public override void OnNetworkSpawn()
        {
            s_All.Add(this);
            ResizeDisc();
        }

        public override void OnNetworkDespawn()
        {
            s_All.Remove(this);
        }

        // 원반은 프리팹을 구울 때의 반경으로 굳어 있다 — 프리팹은 다시 안 덮어쓰므로(CLAUDE.md 함정 2)
        // CSV를 고치면 **보이는 크기와 실제 판정이 어긋난다**. 스폰할 때 지금 값으로 맞춘다.
        private void ResizeDisc()
        {
            Transform disc = transform.Find(DISC_NAME);
            if (disc == null) return;

            float r = Radius;
            Vector3 s = disc.localScale;
            disc.localScale = new Vector3(r * 2f, s.y, r * 2f);
        }
    }
}
