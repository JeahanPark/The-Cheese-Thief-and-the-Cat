// TestManager의 일부 — 상점 부스 · 철문 · 점원 (2026-09-19)
// 개발 빌드에도 실린다 — 빌드 E2E QA(wesqa)의 진입점이라서. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.Core;
using CTC.Runtime.Map;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>
        /// 그 플레이어에게 가장 가까운 상점의 철문 — **이 인스턴스 화면에서** 어떻게 보이나.
        /// "closed=True lowered=1.00 cowering=True center=3.20,-4.10 front=3.20,-2.80 dist=1.30". 몸이나 상점이 없으면 "missing".
        /// </summary>
        public string TestGetNearestShutter(long _clientId)
        {
            var body = NetUtil.FindPlayerObject((ulong)_clientId);
            if (body == null) return "missing";
            ShopSpot best = null;
            float bestSqr = float.MaxValue;
            foreach (ShopSpot spot in ShopSpot.All)
            {
                if (spot == null) continue;
                float sqr = (spot.transform.position - body.transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = spot;
            }
            if (best == null) return "missing";

            var shutter = best.GetComponent<ShopShutter>();
            var keeper = best.GetComponentInChildren<ShopKeeper>(true);
            Vector3 c = best.transform.position;
            Vector3 f = best.FrontPoint;
            return $"closed={(shutter != null && shutter.Closed)} lowered={(shutter != null ? shutter.Lowered : -1f):0.00} " +
                   $"cowering={(keeper != null && keeper.Cowering)} center={c.x:F2},{c.z:F2} front={f.x:F2},{f.z:F2} " +
                   $"dist={Mathf.Sqrt(bestSqr):0.00}";
        }

        /// <summary>이 인스턴스가 보는 상점 철문 — "open=3 closed=1".</summary>
        public string TestGetShopShutters()
        {
            int open = 0, closed = 0;
            foreach (ShopSpot spot in ShopSpot.All)
            {
                if (spot == null) continue;
                if (spot.Closed) closed++;
                else open++;
            }
            return $"open={open} closed={closed}";
        }

        /// <summary>
        /// 로컬 몸을 그 플레이어에게 가장 가까운 상점 **중심에서** 지정 거리만큼 떨어진 자리로 옮긴다 — 창구 앞 **대각선**(앞·오른쪽).
        /// 철문 반경을 재는 용도 — 실제 거리(m)를 돌려준다. 못 찾으면 -1.
        /// 앞으로 곧장 가면 창구 앞(1.3m)에 선 손님 쥐와 겹치고, 뒤로 가면 부스가 등진 벽 속일 수 있다(부스는 맵 가운데를 본다).
        /// </summary>
        public float TestTeleportNearShopOf(long _clientId, float _meters)
        {
            var target = NetUtil.FindPlayerObject((ulong)_clientId);
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var me = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (target == null || me == null) return -1f;
            ShopSpot best = null;
            float bestSqr = float.MaxValue;
            foreach (ShopSpot spot in ShopSpot.All)
            {
                if (spot == null) continue;
                float sqr = (spot.transform.position - target.transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = spot;
            }
            if (best == null) return -1f;

            Vector3 diagonal = (best.transform.forward + best.transform.right).normalized;
            Vector3 pos = best.transform.position + diagonal * _meters;
            pos.y = me.transform.position.y;
            var cc = me.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            me.transform.position = pos;
            me.transform.LookAt(new Vector3(best.transform.position.x, pos.y, best.transform.position.z));
            if (cc != null) cc.enabled = true;
            Vector3 d = best.transform.position - pos;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
#endif
