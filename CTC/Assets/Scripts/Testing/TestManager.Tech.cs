// TestManager의 일부 — 테크 v3 훅 (2026-09-07). wesqa가 리플렉션으로 Instance.<method>를 부르므로
// **타입을 쪼개지 않는다**(CLAUDE.md). 개발 빌드에도 실린다 — 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading;
using Cysharp.Threading.Tasks;
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
        /// <summary>구매가 실제로 반영될 때까지 기다리는 상한(초). 못 기다리면 경고를 남기고 포기한다.</summary>
        private const float TECH_LEVEL_WAIT_SECONDS = 2f;

        /// <summary>
        /// 산 테크가 **눈에 보일 때까지** 기다린다 — "사고 바로 쓴다" 훅의 유일한 안전장치.
        ///
        /// 호스트에서는 서버 RPC가 그 자리에서 돌아 `Buy` 다음 줄이면 이미 레벨이 올라 있다.
        /// 그런데 클론(원격 클라이언트)에서는 RPC가 왕복하고 `TechSlots`(NetworkList)가 복제될
        /// 때까지 몇 프레임이 걸린다 — 그 사이에 `Use`를 부르면 "아직 없다"로 조용히 버려져
        /// **설치가 아예 안 일어난 채 QA가 통과처럼 흘러간다**(2026-09-07 리뷰 C1).
        /// </summary>
        private static async UniTask<bool> WaitForTechLevel(
            TechSlots _slots, TechId _id, int _level, CancellationToken _token)
        {
            if (_slots == null) return false;

            float until = Time.realtimeSinceStartup + TECH_LEVEL_WAIT_SECONDS;
            while (_slots.LevelOf(_id) < _level)
            {
                if (_token.IsCancellationRequested) return false;
                if (Time.realtimeSinceStartup > until)
                {
                    GameDebug.LogWarning(
                        $"[TestManager] {_id} Lv{_level} 반영을 {TECH_LEVEL_WAIT_SECONDS}초 안에 못 봤다 — 구매가 거절됐을 수 있다");
                    return false;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, _token);
            }
            return true;
        }

        /// <summary>QA: 테크 한 레벨의 값(없는 행이면 -1) — 스크립트가 가격을 손으로 적지 않게 한다.</summary>
        public int TestGetTechCost(string _techId, int _level)
        {
            if (!System.Enum.TryParse(_techId, out TechId id)) return -1;
            TechRow row = TechLogic.RowOf(id, _level);
            return row.IsValid ? row.Cost : -1;
        }

        /// <summary>QA: 설치 1회 비용(설치권과 별개). 설치물이 아니면 0, 모르는 이름이면 -1.</summary>
        public int TestGetPlaceCost(string _techId)
        {
            if (!System.Enum.TryParse(_techId, out TechId id)) return -1;
            Info.BalanceInfo b = Info.InfoManager.Instance.Balance;
            switch (id)
            {
                case TechId.Fortress: return b.FortressPlaceCost;
                case TechId.Warehouse: return b.WarehousePlaceCost;
                default: return 0;
            }
        }

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
        ///
        /// <see cref="BodyPush.ServerPush"/> 경유라 **호스트 인스턴스에서 불러야 한다** — 클론에서 부르면
        /// 서버가 아니라 아무 일도 안 일어난다.
        /// 그리고 ServerPush는 다시 **소유자 RPC**다: 클론의 몸은 왕복이 끝나야 실제로 움직인다.
        /// 그래서 <c>true</c>는 "보냈다"지 "도착했다"가 아니다 — QA는 잠깐 쉰 뒤
        /// <see cref="TestIsInShop"/>로 도착을 확인해야 한다.
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

        // ===== 효과 계측 (테크 v3 계획 2a · qa_v47) =====
        // 여기 있는 것들은 전부 **읽기 전용**이다 — Model 값이 아니라 실제로 선 오브젝트·실제 HP를 센다.

        /// <summary>지금 HP(-1 = 못 찾음). NetworkVariable이라 어느 인스턴스에서 물어도 같은 값이다.</summary>
        public int TestGetHpOf(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            Combat.Health hp = p != null ? p.GetComponent<Combat.Health>() : null;
            return hp != null ? hp.Hp : -1;
        }

        /// <summary>
        /// 최대 HP(-1 = 못 찾음). <c>Health.MaxHp</c>는 스폰 뒤 <c>NetworkVariable</c>이라
        /// **어느 인스턴스에서 물어도 같은 값**이다(2026-09-08 — 두꺼운 가죽이 판 도중 올린다).
        /// </summary>
        public int TestGetMaxHpOf(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            Combat.Health hp = p != null ? p.GetComponent<Combat.Health>() : null;
            return hp != null ? hp.MaxHp : -1;
        }

        /// <summary>이 쥐가 세워 둔 창고 수 — 창고 Lv2(상한 2)가 실제로 늘었는지 본다.</summary>
        public int TestGetWarehouseCountOf(long _clientId)
        {
            return WarehouseProduction.CountOwnedBy((ulong)_clientId);
        }

        /// <summary>씬에 깔린 끈끈이 장판 수 — 10초 뒤 0으로 돌아가야 한다.</summary>
        public int TestGetStickyPatchCount()
        {
            return FindObjectsByType<StickyPatch>(FindObjectsSortMode.None).Length;
        }

        /// <summary>
        /// 이 플레이어에게 가장 가까운 요새의 최대 HP(-1 = 요새 없음). 요새 레벨(TechInfo P1)이
        /// 스폰 시 실제로 박혔는지 본다. <see cref="TestGetMaxHpOf"/>와 같이 복제되는 값이다.
        /// </summary>
        public int TestGetNearestFortressMaxHp(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            if (p == null) return -1;

            Combat.Health best = null;
            float bestSqr = float.MaxValue;
            foreach (FortressShield fort in FindObjectsByType<FortressShield>(FindObjectsSortMode.None))
            {
                float d = (fort.transform.position - p.transform.position).sqrMagnitude;
                if (d >= bestSqr) continue;
                Combat.Health hp = fort.GetComponent<Combat.Health>();
                if (hp == null) continue;
                bestSqr = d;
                best = hp;
            }
            return best != null ? best.MaxHp : -1;
        }

        /// <summary>
        /// 지금 걸을 수 있는 이속(-1 = 못 찾음) — 빠른 발(가산)·강화 스택·끈끈이 배율이 다 섞인 값.
        /// 계산은 <see cref="PlayerMovement.CurrentSpeed"/>가 단독으로 갖는다 — 여기서 식을 베끼면
        /// 갈라졌을 때 훅이 조용히 거짓말을 한다. 잠행(LShift)·바람 전직 배율은 그쪽에도 없다:
        /// 누르고 있는 동안만인 순간 값이라 QA가 재는 값이 아니다.
        /// </summary>
        public float TestGetMoveSpeedOf(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            PlayerMovement move = p != null ? p.GetComponent<PlayerMovement>() : null;
            if (move == null) return -1f;

            return move.CurrentSpeed;
        }
    }
}
#endif
