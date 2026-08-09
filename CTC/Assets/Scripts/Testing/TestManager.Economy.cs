// WES에서 이식(2026-07-12) — 골격만: SimulateKeyPress(Input System StateEvent 주입) + CTC 시나리오 3종.
// WES 원본의 인게임 의존 메서드(InGameController/Popup/FullPlay 등)는 CTC 미이식 상태라 제외.
// WesQA InvokeBridge가 리플렉션으로 Instance.<method>(kwargs)를 호출하는 QA 진입점 싱글턴.
#if UNITY_EDITOR
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CTC.Runtime.Testing
{
    // TestManager의 일부 — 치즈·상점·창고·설치물 (개편 5단계 분할, 2026-07-31)
    public partial class TestManager
    {
        /// <summary>서버(호스트) 전용: 모든 지갑에 치즈 지급 — 상점 QA 부트스트랩.</summary>
        public void TestGrantCheeseAll()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
            {
                GameDebug.LogWarning("[TestManager] TestGrantCheeseAll — 서버 아님");
                return;
            }

            foreach (var wallet in FindObjectsByType<CheeseWallet>())
            {
                wallet.AddCheese(1000);
            }
            GameDebug.Log("[TestManager] TestGrantCheeseAll → 전 지갑 +1000");
        }

        /// <summary>로컬 플레이어 구매 — QA용 파라미터리스 래퍼.</summary>
        public void TestBuySlingshot() => BuyLocal(ShopItemId.Slingshot);

        public void TestBuyMachineGun() => BuyLocal(ShopItemId.MachineGun);

        public void TestBuyDash() => BuyLocal(ShopItemId.Dash);

        public void TestBuyDecoy() => BuyLocal(ShopItemId.Decoy);

        public void TestBuyFortress() => BuyLocal(ShopItemId.Fortress);

        public void TestBuyCatAttack() => BuyLocal(ShopItemId.CatAttack);

        public void TestBuyCatSpeed() => BuyLocal(ShopItemId.CatSpeed);

        /// <summary>위치 지정 설치(핫바 고스트 확정과 같은 경로) — 서버 거리·겹침 검증 QA용.</summary>
        public void TestPlaceFortressAt(float _x, float _z) => PlaceLocal(ShopItemId.Fortress, _x, _z);

        public void TestPlaceDecoyAt(float _x, float _z) => PlaceLocal(ShopItemId.Decoy, _x, _z);

        public void TestPlaceWarehouseAt(float _x, float _z) => PlaceLocal(ShopItemId.Warehouse, _x, _z);

        /// <summary>QA: 지정 좌표에서 가장 가까운 치즈 지점을 점유한 클라이언트 ID(없으면 -1).</summary>
        public long TestGetCheeseEarnerNear(float _x, float _z)
        {
            var target = new UnityEngine.Vector3(_x, 0f, _z);
            CheeseSpot nearest = null;
            float best = float.MaxValue;
            foreach (var spot in FindObjectsByType<CheeseSpot>(FindObjectsSortMode.None))
            {
                float d = UnityEngine.Vector3.SqrMagnitude(spot.transform.position - target);
                if (d >= best) continue;
                best = d;
                nearest = spot;
            }
            if (nearest == null || nearest.EarnerClientId == CheeseSpotLogic.NO_EARNER) return -1;
            return (long)nearest.EarnerClientId;
        }

        /// <summary>QA: 남아 있는(고갈되지 않은) 치즈 지점 수.</summary>
        public int TestGetLiveCheeseSpotCount()
        {
            int live = 0;
            foreach (var spot in FindObjectsByType<CheeseSpot>(FindObjectsSortMode.None))
            {
                if (!spot.Depleted) live++;
            }
            return live;
        }

        /// <summary>QA: 맵에 남은 치즈 매장량 총합.</summary>
        public int TestGetTotalCheeseReserve()
        {
            int total = 0;
            foreach (var spot in FindObjectsByType<CheeseSpot>(FindObjectsSortMode.None))
            {
                total += spot.Remaining;
            }
            return total;
        }

        // ===== 벽감 (v3.8, 2026-08-09) =====

        /// <summary>QA: 지금 서 있는 벽감 수.</summary>
        public int TestGetAlcoveCount()
            => FindObjectsByType<CTC.Runtime.Map.Alcove>(FindObjectsSortMode.None).Length;

        /// <summary>QA: 바닥에 뒹구는 치즈 덩이 수.</summary>
        public int TestGetCheeseDropCount()
            => FindObjectsByType<CheeseDrop>(FindObjectsSortMode.None).Length;

        /// <summary>QA: 바닥 치즈 덩이의 합계 — 벽감이 품은 값이 온전히 나왔는지 본다.</summary>
        public int TestGetCheeseDropTotal()
        {
            int total = 0;
            foreach (var drop in FindObjectsByType<CheeseDrop>(FindObjectsSortMode.None))
            {
                total += drop.Amount;
            }
            return total;
        }

        /// <summary>
        /// QA: 서버 전용 — 첫 벽감을 실제 피해로 부순다(직접 despawn이 아니라 `Health`를 때려
        /// 파괴 경로를 그대로 태운다). 부순 자리를 "x,z"로 돌려준다. 없으면 빈 문자열.
        /// </summary>
        public string TestBreakFirstAlcove()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return "";

            foreach (var alcove in FindObjectsByType<CTC.Runtime.Map.Alcove>(FindObjectsSortMode.None))
            {
                var health = alcove.GetComponent<Health>();
                if (health == null || health.IsDead) continue;

                Vector3 p = alcove.transform.position;
                health.TakeDamage(health.MaxHp);
                return $"{p.x:F2},{p.z:F2}";
            }
            return "";
        }

        /// <summary>QA: 첫 벽감을 한 대만 때린다 — 장난기가 붙는지 보려면 파괴하면 안 된다.</summary>
        public int TestHitFirstAlcove(int _damage)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return -1;

            foreach (var alcove in FindObjectsByType<CTC.Runtime.Map.Alcove>(FindObjectsSortMode.None))
            {
                var health = alcove.GetComponent<Health>();
                if (health == null || health.IsDead) continue;
                return health.TakeDamage(_damage);
            }
            return -1;
        }

        /// <summary>QA: 로컬 플레이어를 좌표로 옮겨 바닥 치즈를 줍게 한다(트리거 통과).</summary>
        public int TestGetAlcoveCheesePerWall() => CTC.Runtime.Map.AlcoveLogic.CheesePerAlcove;

        /// <summary>QA: 서버 전용 — 모든 지점을 즉시 고갈시켜 후반 부활을 검증한다.</summary>
        public void TestDrainAllCheeseSpots()
        {
            // 감독이 복제 목록까지 같이 갱신해야 클라 표시가 따라온다
            var director = FindFirstObjectByType<CheeseSpotDirector>();
            if (director == null) { GameDebug.LogWarning("[TestManager] CheeseSpotDirector 없음"); return; }

            director.ServerDrainAll();
            GameDebug.Log("[TestManager] 전 치즈 지점 고갈");
        }

        /// <summary>QA: 최고 등급 치즈 지점 위치 "x,z" — 맵마다 좌표가 달라 QA가 물어봐야 한다.</summary>
        public string TestGetBestCheeseSpotPos()
        {
            CheeseSpot best = null;
            foreach (var spot in FindObjectsByType<CheeseSpot>(FindObjectsSortMode.None))
            {
                if (best == null || spot.Grade > best.Grade) best = spot;
            }
            if (best == null) return "";
            UnityEngine.Vector3 p = best.transform.position;
            return $"{p.x:F2},{p.z:F2}";
        }

        /// <summary>QA: 후반 부활이 일어났는가.</summary>
        public bool TestGetCheeseRevived()
        {
            var director = FindFirstObjectByType<CheeseSpotDirector>();
            return director != null && director.RevivedThisMatch;
        }

        /// <summary>QA: 창고 생산음에 실제로 배선된 클립 이름 — 빈 문자열이면 무음이다.</summary>
        public string TestGetWarehouseSfxClip()
        {
            return CTC.Runtime.Audio.SfxPlayer.ClipNameOf(CTC.Runtime.Audio.SfxId.WarehouseTick);
        }

        /// <summary>QA: 창고가 생산을 몇 번 했는가 — 소리·이펙트가 발화한 횟수와 같다.</summary>
        public int TestGetWarehouseTicks()
        {
            int total = 0;
            foreach (var w in FindObjectsByType<WarehouseProduction>(FindObjectsSortMode.None))
            {
                total += w.TickCount;
            }
            return total;
        }

        /// <summary>로컬 플레이어의 현재 치즈 — 창고 생산 검증용(반환값이 wesqa로 나간다).</summary>
        public int TestGetCheese()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var wallet = local != null ? local.GetComponent<CheeseWallet>() : null;
            return wallet != null ? wallet.CheeseValue : -1;
        }

        /// <summary>
        /// QA: 지정 좌표에서 가장 가까운 치즈 지점의 라벨 문자열("등급 · 잔량").
        /// 호스트에서만 표시가 얼어붙던 회귀(2026-07-28)를 잡는다 — 숫자만 보면 놓친다.
        /// </summary>
        public string TestGetCheeseLabelNear(float _x, float _z)
        {
            CheeseSpot nearest = NearestSpot(new UnityEngine.Vector3(_x, 0f, _z));
            return nearest != null ? nearest.LabelText : "";
        }

        private static CheeseSpot NearestSpot(UnityEngine.Vector3 _target)
        {
            CheeseSpot nearest = null;
            float best = float.MaxValue;
            foreach (var spot in FindObjectsByType<CheeseSpot>(FindObjectsSortMode.None))
            {
                float d = UnityEngine.Vector3.SqrMagnitude(spot.transform.position - _target);
                if (d >= best) continue;
                best = d;
                nearest = spot;
            }
            return nearest;
        }

        /// <summary>QA: 가장 가까운 미끼 위치 "x,z" (없으면 빈 문자열).</summary>
        public string TestGetDecoyPos()
        {
            DecoyLifetime decoy = NearestDecoy();
            if (decoy == null) return "";
            UnityEngine.Vector3 p = decoy.transform.position;
            return $"{p.x:F2},{p.z:F2}";
        }

        /// <summary>QA: 로컬 플레이어 ↔ 가장 가까운 미끼 평면 거리 (없으면 -1).</summary>
        public float TestGetDecoyDistance()
        {
            DecoyLifetime decoy = NearestDecoy();
            if (decoy == null) return -1f;
            UnityEngine.Vector3 p = decoy.transform.position;
            return TestGetDistanceTo(p.x, p.z);
        }

        private static void PlaceLocal(ShopItemId _itemId, float _x, float _z)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var shop = local != null ? local.GetComponent<ShopController>() : null;
            if (shop == null) { GameDebug.LogWarning("[TestManager] ShopController 없음"); return; }

            var pos = new UnityEngine.Vector3(_x, local.transform.position.y, _z);
            shop.BuyAt(_itemId, pos);
            GameDebug.Log($"[TestManager] PlaceLocal({_itemId}) @ {pos} 요청");
        }

        private static void BuyLocal(ShopItemId _itemId)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var shop = local != null ? local.GetComponent<ShopController>() : null;
            if (shop == null) { GameDebug.LogWarning("[TestManager] ShopController 없음"); return; }
            shop.Buy(_itemId);
            GameDebug.Log($"[TestManager] BuyLocal({_itemId}) 요청");
        }
    }
}
#endif
