// WES에서 이식(2026-07-12) — 골격만: SimulateKeyPress(Input System StateEvent 주입) + CTC 시나리오 3종.
// WES 원본의 인게임 의존 메서드(InGameController/Popup/FullPlay 등)는 CTC 미이식 상태라 제외.
// WesQA InvokeBridge가 리플렉션으로 Instance.<method>(kwargs)를 호출하는 QA 진입점 싱글턴.
// 개발 빌드에도 실린다(2026-08-31) — 빌드 E2E QA(wesqa)의 진입점이라서. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

        // ===== 구매 훅 (테크 v3로 이관, 2026-09-07) =====
        //
        // **이름은 그대로 둔다** — wesqa 스크립트가 문자열로 부른다. 속만 TechId로 바꿨다.
        // 옛 상점 항목 ↔ 테크 대응: 새총/기관총 = Gun Lv1/Lv2(두 번째 호출이 Lv2), 후추 탄 = Pepper,
        // 발톱 강화 = CatClaw, 이속 강화 = CatStomp.
        //
        // **전부 로컬 플레이어에만 작동한다.** Buy/Use는 `RequireOwnership` 소유자 RPC라
        // 남의 몸에서 불러도 서버가 버린다. 클론(다른 클라이언트)을 움직이려면
        // `MultiplayerQaProbe`의 `TestMp*` 커맨드를 쓴다.
        //
        // 상점 자리 규칙은 **면제되지 않는다** — 같은 `ShopController.Buy`를 타므로 상점 밖 쥐는
        // 서버가 거절한다. 시간 결핍을 건너뛰고 구매만 보려면 `TestWarpToNearestShop`을 먼저 부른다.

        /// <summary>로컬 플레이어 구매 — QA용 파라미터리스 래퍼.</summary>
        public void TestBuySlingshot() => BuyLocal(TechId.Gun);

        /// <summary>두 번째 Gun 구매 = Lv2(기관총). 새총을 먼저 사 둬야 한다.</summary>
        public void TestBuyMachineGun()
        {
            // Lv1이 없으면 이 호출은 기관총이 아니라 **새총**을 산다 — 조용히 어긋나면 QA가 오진한다
            TechSlots slots = LocalComponent<TechSlots>();
            if (slots != null && slots.LevelOf(TechId.Gun) == 0)
                GameDebug.LogWarning("[TestManager] TestBuyMachineGun — 새총(Lv1)이 없다. 이번 구매는 Lv1이 된다");
            BuyLocal(TechId.Gun);
        }

        public void TestBuyDash() => BuyLocal(TechId.Dash);

        public void TestBuyPepperShot() => BuyLocal(TechId.Pepper);   // 후추 탄(빌드 심화 v1)

        /// <summary>
        /// 옛 훅은 "요새가 선다"였다 — 테크 v3에서 구매(설치권)와 설치가 갈렸으므로 둘 다 한다.
        /// 이미 설치권이 있으면 구매를 건너뛰고 설치만 한다.
        ///
        /// 구매가 **복제될 때까지 기다렸다가** 설치한다(리뷰 C1) — 클론에서 같은 프레임에 이어 부르면
        /// 아직 레벨이 0이라 설치가 조용히 버려진다. wesqa는 반환값을 안 보므로
        /// fire-and-forget(`UniTaskVoid`)이면 된다 — 이웃 훅(`TestWalkTowards`)과 같은 꼴.
        /// </summary>
        public void TestBuyFortress()
        {
            BuyThenPlaceAsync(TechId.Fortress, null, destroyCancellationToken).Forget();
        }

        public void TestBuyCatAttack() => BuyLocal(TechId.CatClaw);

        public void TestBuyCatSpeed() => BuyLocal(TechId.CatStomp);

        /// <summary>
        /// 위치 지정 설치(핫바 고스트 확정과 같은 경로) — 서버 거리·겹침 검증 QA용. 창고만 조준 설치다.
        /// 설치권(Lv1)이 없으면 먼저 사고 **복제를 기다린 뒤** 놓는다. 설치비는 서버가 따로 깎는다.
        /// </summary>
        public void TestPlaceWarehouseAt(float _x, float _z)
        {
            BuyThenPlaceAsync(TechId.Warehouse, new UnityEngine.Vector3(_x, 0f, _z), destroyCancellationToken).Forget();
        }

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

        /// <summary>
        /// QA: 씬에 서 있는 구조물(요새·창고 등) 중 HP가 가장 낮은 값. 없으면 -1.
        /// 쥐가 **제 구조물을 못 친다**를 재는 데 쓴다 — 하나라도 깎이면 규칙이 샌 것이다.
        /// </summary>
        public int TestGetLowestStructureHp()
        {
            int lowest = -1;
            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h.NetworkObject == null || h.NetworkObject.IsPlayerObject) continue;
                if (h.GetComponent<CTC.Runtime.Map.Alcove>() != null) continue;
                if (h.GetComponent<CTC.Runtime.Map.LootBox>() != null) continue;
                if (h.GetComponent<CTC.Runtime.Ai.DogBrain>() != null) continue;

                if (lowest < 0 || h.Hp < lowest) lowest = h.Hp;
            }
            return lowest;
        }

        /// <summary>
        /// QA: 로컬 고양이 기준으로 서 있는 구조물(요새·창고)마다 덮치기 판정 단계를 적는다 —
        /// "왜 안 맞나"를 서버 판정과 같은 규칙으로 되짚는다(사거리·부채꼴·차폐).
        /// </summary>
        public string TestDebugPounceTargets()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var pounce = local != null ? local.GetComponent<CatPounce>() : null;
            if (pounce == null) return "no cat";

            var sb = new System.Text.StringBuilder();
            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h.NetworkObject == null || h.NetworkObject.IsPlayerObject) continue;
                if (h.GetComponent<CTC.Runtime.Map.Alcove>() != null) continue;
                if (h.GetComponent<CTC.Runtime.Map.LootBox>() != null) continue;
                if (h.GetComponent<CTC.Runtime.Ai.DogBrain>() != null) continue;

                UnityEngine.Vector3 to = h.transform.position - local.transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                float ang = UnityEngine.Vector3.Angle(new UnityEngine.Vector3(local.transform.forward.x, 0f, local.transform.forward.z), to);
                bool blocked = SightLine.IsBlocked(local.transform, h.transform);
                string blocker = "";
                if (blocked)
                {
                    UnityEngine.Vector3 from = local.transform.position + UnityEngine.Vector3.up * SightLine.RAY_HEIGHT;
                    UnityEngine.Vector3 dst = h.transform.position + UnityEngine.Vector3.up * SightLine.RAY_HEIGHT;
                    UnityEngine.Vector3 dir = dst - from;
                    foreach (UnityEngine.RaycastHit hit in UnityEngine.Physics.RaycastAll(from, dir.normalized, dir.magnitude, ~0, UnityEngine.QueryTriggerInteraction.Ignore))
                    {
                        var root = hit.transform.root;
                        if (root == local.transform.root || root == h.transform.root) continue;
                        if (root.GetComponent<UnityEngine.CharacterController>() != null) continue;
                        blocker = hit.transform.name + "@" + root.name;
                        break;
                    }
                }
                sb.Append($"{h.name}: hp={h.Hp} dist={dist:F2} angle={ang:F0} blocked={blocked} {blocker}; ");
            }
            return sb.ToString();
        }

        // ===== 상자 (v3.9) =====

        /// <summary>QA: 맵에 서 있는 상자 수.</summary>
        public int TestGetBoxCount()
            => FindObjectsByType<CTC.Runtime.Map.LootBox>(FindObjectsSortMode.None).Length;

        /// <summary>QA: 맵의 상자 전부가 품은 치즈 합계 — 지점 매장량에서 뗀 몫과 같아야 한다.</summary>
        public int TestGetBoxCheeseTotal()
        {
            var director = FindFirstObjectByType<CTC.Runtime.Map.MapDirector>();
            if (director == null || director.ActiveLayout == null) return -1;
            return CTC.Runtime.Map.LootBoxLogic.TotalCheese(director.ActiveLayout.Boxes);
        }

        /// <summary>서버 전용 — 상자 하나를 부순다. 위치를 "x,z"로 준다(없으면 빈 문자열).</summary>
        public string TestBreakFirstBox()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return "";

            foreach (var box in FindObjectsByType<CTC.Runtime.Map.LootBox>(FindObjectsSortMode.None))
            {
                var health = box.GetComponent<Health>();
                if (health == null || health.IsDead) continue;

                Vector3 p = box.transform.position;
                health.TakeDamage(health.MaxHp);
                return $"{p.x:F2},{p.z:F2}";
            }
            return "";
        }

        /// <summary>QA: 상자를 막고 있는 벽감 수 — 강아지 벽감은 안 센다(총량이 새는지 본다).</summary>
        public int TestGetCheeseAlcoveCount()
        {
            int count = 0;
            foreach (var alcove in FindObjectsByType<CTC.Runtime.Map.Alcove>(FindObjectsSortMode.None))
            {
                if (alcove.Payload == CTC.Runtime.Map.AlcovePayload.Box) count++;
            }
            return count;
        }

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
                // **치즈가 든 벽만** 부순다. 강아지 벽은 상자가 안 나오고, 직업권 벽은 치즈가
                // 0덩이라 치즈 항목이 통째로 헛돈다(2026-08-09·08-11 실측).
                if (alcove.Payload != CTC.Runtime.Map.AlcovePayload.Box) continue;
                if (alcove.BoxCheese <= 0) continue;

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

        /// <summary>QA: 벽 뒤 상자 하나에 넣는 치즈 기본값.</summary>
        public int TestGetAlcoveCheesePerWall() => CTC.Runtime.Map.AlcoveLogic.CheesePerAlcove;

        /// <summary>QA: 벽 뒤 상자들이 품은 치즈 합계 — 자리마다 내용물이 달라 합으로 재야 한다.</summary>
        public int TestGetAlcoveCheeseTotal()
        {
            var director = FindFirstObjectByType<CTC.Runtime.Map.MapDirector>();
            if (director == null || director.ActiveLayout == null) return -1;
            return CTC.Runtime.Map.AlcoveLogic.TotalCheese(director.ActiveLayout.Alcoves);
        }

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

        /// <summary>
        /// QA: 클립이 하나라도 안 붙은 `SfxId` 목록(쉼표 구분). 비어 있어야 정상.
        ///
        /// `LoadClip`은 경로가 틀려도 **null을 조용히 넣는다** — 그러면 그 소리만 안 난다.
        /// 배선 누락은 화면에 안 보이고 로그도 안 남아서, 이렇게 전수로 세지 않으면 못 잡는다.
        /// </summary>
        public string TestGetUnwiredSfxIds()
        {
            var missing = new System.Collections.Generic.List<string>();
            foreach (CTC.Runtime.Audio.SfxId id in System.Enum.GetValues(typeof(CTC.Runtime.Audio.SfxId)))
            {
                if (string.IsNullOrEmpty(CTC.Runtime.Audio.SfxPlayer.ClipNameOf(id))) missing.Add(id.ToString());
            }
            return string.Join(",", missing);
        }

        /// <summary>
        /// QA: 창고가 **생산을 몇 번 했는가**(`WarehouseProduction.TickCount`).
        ///
        /// **소리·이펙트가 울린 횟수가 아니다**(2026-09-09 감사). 예전 주석은 둘이 같다고
        /// 단언했지만 이 훅은 그걸 확인하지 않는다 — 소리 호출을 통째로 떼도 이 수는 그대로다.
        /// 실제 재생 횟수는 `TestGetSfxPlayCount`(미구현 부채, `qa_v47` ⑦-b SKIP)가 답할 자리다.
        /// </summary>
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

        /// <summary>QA: 가장 가까운 요새 위치 "x,z" (없으면 빈 문자열).</summary>
        public string TestGetFortressPos()
        {
            FortressShield fort = NearestFortress();
            if (fort == null) return "";
            UnityEngine.Vector3 p = fort.transform.position;
            return $"{p.x:F2},{p.z:F2}";
        }

        /// <summary>QA: 로컬 플레이어 ↔ 가장 가까운 요새 평면 거리 (없으면 -1).</summary>
        public float TestGetFortressDistance()
        {
            FortressShield fort = NearestFortress();
            if (fort == null) return -1f;
            UnityEngine.Vector3 p = fort.transform.position;
            return TestGetDistanceTo(p.x, p.z);
        }

        /// <summary>
        /// 설치권을 사고(미보유 시) **복제를 기다린 뒤** 설치한다 — 옛 `BuyAt` 한 방을 두 단계로 나눈 것.
        /// <paramref name="_at"/>가 null이면 즉발 설치(요새), 있으면 그 좌표에 조준 설치(창고)다.
        /// </summary>
        private static async UniTaskVoid BuyThenPlaceAsync(
            TechId _techId, UnityEngine.Vector3? _at, CancellationToken _token)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var shop = local != null ? local.GetComponent<ShopController>() : null;
            if (shop == null) { GameDebug.LogWarning("[TestManager] ShopController 없음"); return; }

            var slots = local.GetComponent<TechSlots>();
            if (slots != null && slots.LevelOf(_techId) == 0)
            {
                shop.Buy(_techId);
                // 여기서 기다리지 않으면 클론에서 설치가 통째로 사라진다(리뷰 C1)
                if (!await WaitForTechLevel(slots, _techId, 1, _token)) return;
            }

            UseDeny deny = shop.CanUse(_techId);
            if (deny != UseDeny.None)
            {
                GameDebug.LogWarning($"[TestManager] {_techId} 설치 거절: {deny}");
                return;
            }

            if (_at.HasValue)
            {
                var pos = new UnityEngine.Vector3(_at.Value.x, local.transform.position.y, _at.Value.z);
                shop.UseAt(_techId, pos);
                GameDebug.Log($"[TestManager] {_techId} 설치 요청 @ {pos}");
            }
            else
            {
                shop.Use(_techId);
                GameDebug.Log($"[TestManager] {_techId} 즉발 설치 요청");
            }
        }

        private static void BuyLocal(TechId _techId)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var shop = local != null ? local.GetComponent<ShopController>() : null;
            if (shop == null) { GameDebug.LogWarning("[TestManager] ShopController 없음"); return; }
            shop.Buy(_techId);
            GameDebug.Log($"[TestManager] BuyLocal({_techId}) 요청");
        }
    }
}
#endif
