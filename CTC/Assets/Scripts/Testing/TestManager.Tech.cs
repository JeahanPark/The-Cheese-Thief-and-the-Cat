// TestManager의 일부 — 테크 v3 훅 (2026-09-07). wesqa가 리플렉션으로 Instance.<method>를 부르므로
// **타입을 쪼개지 않는다**(CLAUDE.md). 개발 빌드에도 실린다 — 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using CTC.Runtime.Map;
using CTC.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        // NetUtil.FindPlayerObject(clientId)는 NetworkObject를 준다 — 이웃 partial(TestManager.Balance)과 같은 방식.
        private static GameObject FindPlayerGo(long _clientId)
        {
            NetworkObject no = NetUtil.FindPlayerObject((ulong)_clientId);
            return no != null ? no.gameObject : null;
        }

        /// <summary>특정 클라이언트의 지갑 — 기존 TestGetCheese()는 로컬 전용이라 따로 둔다.</summary>
        public int TestGetCheeseOf(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            CheeseWallet w = p != null ? p.GetComponent<CheeseWallet>() : null;
            return w != null ? w.CheeseValue : -1;
        }

        /// <summary>보유 레벨(0 = 미보유, -1 = 못 찾음).</summary>
        public int TestGetTechLevel(long _clientId, string _techId)
        {
            GameObject p = FindPlayerGo(_clientId);
            TechSlots slots = p != null ? p.GetComponent<TechSlots>() : null;
            if (slots == null || !System.Enum.TryParse(_techId, out TechId id)) return -1;
            return slots.LevelOf(id);
        }

        /// <summary>보유 칸 수 — 기획 규칙 1(5칸)이 지켜지는지 본다.</summary>
        public int TestGetTechSlotCount(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            TechSlots slots = p != null ? p.GetComponent<TechSlots>() : null;
            return slots != null ? slots.Count : -1;
        }

        /// <summary>지금 상점 자리 안에 서 있는가 — 쥐의 구매 조건(기획 규칙 3).</summary>
        public bool TestIsInShop(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            return p != null && ShopSpot.AnyContains(p.transform.position);
        }

        /// <summary>실제로 씬에 선 상점 수 — CSV의 ShopSpotCount와 어긋나면 배선 사고다.</summary>
        public int TestGetShopSpotCount()
        {
            return FindObjectsByType<ShopSpot>(FindObjectsSortMode.None).Length;
        }

        /// <summary>
        /// QA: 사람의 숫자키와 같은 입구 — 판정은 서버가 한다(상점 밖 쥐는 거절).
        ///
        /// **로컬 플레이어에만 듣는다.** Buy는 `RequireOwnership` 소유자 RPC라 남의 몸에서 부르면
        /// NGO가 보내기 전에 버린다. 클론을 움직이려면 `MultiplayerQaProbe`의 `TestMp*` 커맨드를 쓴다.
        /// </summary>
        public void TestBuyTech(long _clientId, string _techId)
        {
            GameObject p = FindPlayerGo(_clientId);
            ShopController shop = p != null ? p.GetComponent<ShopController>() : null;
            if (shop == null) { GameDebug.LogWarning($"[TestManager] TestBuyTech — {_clientId} ShopController 없음"); return; }
            if (!shop.IsOwner) GameDebug.LogWarning($"[TestManager] TestBuyTech — {_clientId}는 내 소유가 아니다(소유자 RPC라 무시된다)");
            if (System.Enum.TryParse(_techId, out TechId id)) shop.Buy(id);
        }

        /// <summary>로컬 플레이어 구매 — 소유자 RPC 제약이 없는 쪽. clientId를 모르는 스크립트가 쓴다.</summary>
        public void TestBuyTechLocal(string _techId)
        {
            ShopController shop = LocalComponent<ShopController>();
            if (shop == null) { GameDebug.LogWarning("[TestManager] TestBuyTechLocal — ShopController 없음"); return; }
            if (System.Enum.TryParse(_techId, out TechId id)) shop.Buy(id);
        }

        /// <summary>QA: 즉발 액티브 사용(요새·대시). 구매와 같은 소유자 RPC 제약.</summary>
        public void TestUseTech(long _clientId, string _techId)
        {
            GameObject p = FindPlayerGo(_clientId);
            ShopController shop = p != null ? p.GetComponent<ShopController>() : null;
            if (shop == null) { GameDebug.LogWarning($"[TestManager] TestUseTech — {_clientId} ShopController 없음"); return; }
            if (System.Enum.TryParse(_techId, out TechId id)) shop.Use(id);
        }

        /// <summary>로컬 플레이어 사용 — <see cref="TestBuyTechLocal"/>의 짝.</summary>
        public void TestUseTechLocal(string _techId)
        {
            ShopController shop = LocalComponent<ShopController>();
            if (shop == null) { GameDebug.LogWarning("[TestManager] TestUseTechLocal — ShopController 없음"); return; }
            if (System.Enum.TryParse(_techId, out TechId id)) shop.Use(id);
        }

        /// <summary>
        /// QA: 가장 가까운 상점 자리로 옮긴다(시간 결핍을 건너뛰고 구매만 볼 때).
        /// <see cref="BodyPush.ServerPush"/> 경유 — **서버 전용**이다. wesqa는 호스트에서 훅을 부르므로 문제없다.
        /// </summary>
        public bool TestWarpToNearestShop(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            if (p == null) return false;

            ShopSpot best = null;
            float bestSqr = float.MaxValue;
            foreach (ShopSpot spot in FindObjectsByType<ShopSpot>(FindObjectsSortMode.None))
            {
                float d = (spot.transform.position - p.transform.position).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = spot;
            }
            if (best == null) return false;

            BodyPush push = p.GetComponent<BodyPush>();
            if (push == null) return false;

            // 높이는 건드리지 않는다 — 상점 마커는 바닥에 붙어 있어 y를 따라가면 몸이 바닥에 박힌다.
            Vector3 to = best.transform.position - p.transform.position;
            to.y = 0f;
            push.ServerPush(to);
            return true;
        }
    }
}
#endif
