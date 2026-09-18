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

        /// <summary>
        /// 부스 창구에서 앞으로 이만큼 — 사는 자리(봇 목적지·QA 워프). 부스 몸통이 막혀 있어(2026-09-19) 가운데로는 못 간다.
        /// 원반(반경 3m) 안이어야 한다.
        /// </summary>
        public const float FRONT_DISTANCE = 1.3f;

        private ShopShutter m_Shutter;

        /// <summary>창구 앞 사는 자리(월드).</summary>
        public Vector3 FrontPoint => transform.position + transform.forward * FRONT_DISTANCE;

        /// <summary>철문이 닫혀 있나 — 철문이 없는 상점은 늘 열려 있다.</summary>
        public bool Closed => m_Shutter != null && m_Shutter.Closed;

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

        /// <summary>
        /// 이 지점이 **철문이 열린** 상점 안인가 — 사는 판정(2026-09-19). 고양이가 가까이 와 철문이 닫힌 상점에서는 못 산다.
        /// </summary>
        public static bool AnyOpenContains(Vector3 _point)
        {
            float r = Radius;
            foreach (ShopSpot spot in s_All)
            {
                if (spot.Closed) continue;
                if (ShopSpotLogic.Contains(spot.transform.position, _point, r)) return true;
            }
            return false;
        }

        /// <summary>
        /// 가장 가까운 상점의 **창구 앞**(없으면 false) — 봇 목적지·QA 워프 공용. <paramref name="_openOnly"/>면 철문이 닫힌 상점은 건너뛴다.
        /// 가운데(옛 `Nearest`)로 보내면 막힌 부스 몸통 속이다(2026-09-19).
        /// 명부를 쓰므로 탐색이 없다(규칙 3). 상점 자리는 판마다 바뀐다 — 부르는 쪽이 좌표를 캐시하면 지난 판으로 걷는다.
        /// </summary>
        public static bool NearestFront(Vector3 _from, bool _openOnly, out Vector3 _front)
        {
            _front = Vector3.zero;
            float bestSqr = float.MaxValue;
            foreach (ShopSpot spot in s_All)
            {
                if (spot == null) continue;   // 규칙 9
                if (_openOnly && spot.Closed) continue;
                Vector3 p = spot.FrontPoint;
                float sqr = (p - _from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                _front = p;
            }
            return bestSqr < float.MaxValue;
        }

        private void Awake() => m_Shutter = GetComponent<ShopShutter>();

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
