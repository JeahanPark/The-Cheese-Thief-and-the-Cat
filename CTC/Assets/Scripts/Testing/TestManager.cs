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
    public class TestManager : MonoSingleton<TestManager>
    {
        private const float KEY_PRESS_SECONDS = 0.1f;

        // ===== 범용 입력 시뮬레이션 =====

        public void SimulateKeyPress(string _keyName)
        {
            SimulateKeyPressAsync(_keyName, destroyCancellationToken).Forget();
        }

        private static async UniTaskVoid SimulateKeyPressAsync(string _keyName, CancellationToken _token)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                GameDebug.LogError("[TestManager] Keyboard not found");
                return;
            }

            var key = keyboard.FindKeyOnCurrentKeyboardLayout(_keyName);
            if (key == null)
            {
                GameDebug.LogError($"[TestManager] Key '{_keyName}' not found");
                return;
            }

            using (StateEvent.From(keyboard, out var eventPtr))
            {
                key.WriteValueIntoEvent(1f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }

            GameDebug.Log($"[TestManager] Key '{_keyName}' down");
            await UniTask.Delay(TimeSpan.FromSeconds(KEY_PRESS_SECONDS), cancellationToken: _token);

            using (StateEvent.From(keyboard, out var eventPtr))
            {
                key.WriteValueIntoEvent(0f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }

            GameDebug.Log($"[TestManager] Key '{_keyName}' up");
        }

        // ===== CTC 시나리오 (기존 public 메서드 조합만 — 신규 게임 로직 없음) =====

        /// <summary>
        /// HostPortFallback 경유 호스트 시작 — 선택 포트를 공유 파일에 기록해 클론이 따라온다.
        /// QA에서 NetworkManager.StartHost를 직접 호출하면 파일이 스테일로 남아
        /// 클론이 죽은 포트로 UTP 타임아웃(60초+)에 빠진다.
        /// </summary>
        public void TestStartHost()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null) { GameDebug.LogError("[TestManager] NetworkManager 없음"); return; }

            bool started = Networking.HostPortFallback.StartHost(nm);
            GameDebug.Log($"[TestManager] TestStartHost → {started}");
        }

        public void TestSelectRoleCat()
        {
            LobbyManager lobby = FindLobby();
            if (lobby == null) return;

            lobby.DevSelectCat();
            GameDebug.Log("[TestManager] TestSelectRoleCat → LobbyManager.DevSelectCat");
        }

        public void TestSelectRoleMouse()
        {
            LobbyManager lobby = FindLobby();
            if (lobby == null) return;

            lobby.DevSelectMouse();
            GameDebug.Log("[TestManager] TestSelectRoleMouse → LobbyManager.DevSelectMouse");
        }

        public void TestStartGame()
        {
            LobbyManager lobby = FindLobby();
            if (lobby == null) return;

            lobby.DevStartGame();
            GameDebug.Log("[TestManager] TestStartGame → LobbyManager.DevStartGame");
        }

        /// <summary>로컬 플레이어를 다른 플레이어 바로 앞으로 순간이동(전투 QA용, CC 토글).</summary>
        public void TestTeleportToOtherPlayer()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (local == null) { GameDebug.LogWarning("[TestManager] 로컬 플레이어 없음"); return; }

            foreach (var kv in nm.SpawnManager.SpawnedObjects)
            {
                var other = kv.Value;
                if (other == null || other == local || other.GetComponent<CTC.Runtime.Player.PlayerMovement>() == null) continue;

                var cc = local.GetComponent<UnityEngine.CharacterController>();
                UnityEngine.Vector3 pos = other.transform.position - other.transform.forward * 1.2f;
                if (cc != null) cc.enabled = false;
                local.transform.position = pos;
                local.transform.LookAt(other.transform.position);
                if (cc != null) cc.enabled = true;
                GameDebug.Log($"[TestManager] TeleportToOtherPlayer → {pos}");
                return;
            }
        }

        /// <summary>로컬 플레이어를 지정 좌표로 순간이동(CC 토글) — 요새 차폐/진입 QA용.</summary>
        public void TestTeleportLocalPlayer(float _x, float _z)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (local == null) { GameDebug.LogWarning("[TestManager] 로컬 플레이어 없음"); return; }

            var cc = local.GetComponent<UnityEngine.CharacterController>();
            if (cc != null) cc.enabled = false;
            local.transform.position = new UnityEngine.Vector3(_x, 0.1f, _z);
            if (cc != null) cc.enabled = true;
            GameDebug.Log($"[TestManager] TeleportLocalPlayer → ({_x}, {_z})");
        }

        /// <summary>로컬 플레이어(고양이)의 덮치기 실행.</summary>
        public void TestPounce()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var pounce = local != null ? local.GetComponent<CatPounce>() : null;
            if (pounce == null) { GameDebug.LogWarning("[TestManager] CatPounce 없음"); return; }
            pounce.DevPounce();
        }

        /// <summary>로컬 플레이어(쥐)가 가장 가까운 고양이에게 딱총 발사.</summary>
        public void TestShootAtCat()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var gun = local != null ? local.GetComponent<MousePopgun>() : null;
            if (gun == null) { GameDebug.LogWarning("[TestManager] MousePopgun 없음"); return; }
            gun.DevShootAtNearestCat();
        }

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

        /// <summary>서버(호스트) 전용: 고양이 즉사 — 쥐 승리 판정 QA.</summary>
        public void TestKillCat()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
            {
                GameDebug.LogWarning("[TestManager] TestKillCat — 서버 아님");
                return;
            }

            foreach (var health in FindObjectsByType<Health>())
            {
                var tag = health.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Cat) continue;
                health.TakeDamage(999999);
                GameDebug.Log("[TestManager] TestKillCat → 고양이 처치");
                return;
            }
        }

        /// <summary>서버(호스트) 전용: 매치 재시작 — 결과 화면 QA.</summary>
        public void TestRestartMatch()
        {
            var match = FindFirstObjectByType<MatchManager>();
            if (match == null) { GameDebug.LogWarning("[TestManager] MatchManager 없음"); return; }
            match.RequestRestart();
        }

        /// <summary>서버 전용: 타이머를 3초로 — 생존승 판정 QA.</summary>
        public void TestExpireTimer()
        {
            var match = FindFirstObjectByType<MatchManager>();
            if (match == null) { GameDebug.LogWarning("[TestManager] MatchManager 없음"); return; }
            match.DevSetRemaining(3f);
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

        /// <summary>QA: 시점 각도 지정 — 벽이 카메라와 캐릭터 사이에 오게 만들어 페이드를 검증한다.</summary>
        public void TestSetCameraAngles(float _yaw, float _pitch)
        {
            var cam = FindFirstObjectByType<CTC.Runtime.Cameras.OrbitCameraController>();
            if (cam == null) { GameDebug.LogWarning("[TestManager] OrbitCameraController 없음"); return; }
            cam.DevSetAngles(_yaw, _pitch);
            GameDebug.Log($"[TestManager] 카메라 각도 → yaw {_yaw}, pitch {_pitch}");
        }

        /// <summary>QA: 카메라-플레이어 거리. 페이드 방식은 벽에 붙어도 이 값이 변하지 않아야 한다.</summary>
        public float TestGetCameraDistance()
        {
            var cam = FindFirstObjectByType<CTC.Runtime.Cameras.OrbitCameraController>();
            return cam != null ? cam.DevDistanceToTarget() : -1f;
        }

        /// <summary>QA: 이 인스턴스의 NGO 클라이언트 ID — MPPM 인스턴스 번호와 다르다.</summary>
        public long TestGetLocalClientId()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm != null && nm.IsListening ? (long)nm.LocalClientId : -1;
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

        /// <summary>QA: 서버 전용 — 모든 지점을 즉시 고갈시켜 후반 부활을 검증한다.</summary>
        public void TestDrainAllCheeseSpots()
        {
            // 감독이 복제 목록까지 같이 갱신해야 클라 표시가 따라온다
            var director = FindFirstObjectByType<CheeseSpotDirector>();
            if (director == null) { GameDebug.LogWarning("[TestManager] CheeseSpotDirector 없음"); return; }

            director.ServerDrainAll();
            GameDebug.Log("[TestManager] 전 치즈 지점 고갈");
        }

        /// <summary>
        /// QA: 목표 지점을 향해 실제로 걸어간다(지오메트리 통행 검수).
        /// 텔레포트는 벽을 통과하므로 "길이 뚫려 있는가"는 걸어봐야만 알 수 있다.
        /// </summary>
        public void TestWalkTowards(float _x, float _z, float _seconds)
        {
            WalkTowardsAsync(new UnityEngine.Vector3(_x, 0f, _z), _seconds, destroyCancellationToken).Forget();
        }

        private static async UniTaskVoid WalkTowardsAsync(
            UnityEngine.Vector3 _target, float _seconds, CancellationToken _token)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var move = local != null ? local.GetComponent<CTC.Runtime.Player.PlayerMovement>() : null;
            if (move == null) { GameDebug.LogWarning("[TestManager] PlayerMovement 없음"); return; }

            float until = UnityEngine.Time.time + _seconds;
            while (UnityEngine.Time.time < until && !_token.IsCancellationRequested)
            {
                UnityEngine.Vector3 to = _target - local.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < 1f) break;
                move.DevSetMoveInput(to.normalized);
                await UniTask.Yield(PlayerLoopTiming.Update, _token);
            }
            move.DevSetMoveInput(UnityEngine.Vector3.zero);
        }

        /// <summary>QA: 로컬 플레이어에서 좌표까지의 평면 거리.</summary>
        public float TestGetDistanceTo(float _x, float _z)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (local == null) return -1f;

            UnityEngine.Vector3 p = local.transform.position;
            return UnityEngine.Vector2.Distance(new UnityEngine.Vector2(p.x, p.z), new UnityEngine.Vector2(_x, _z));
        }

        public float TestGetDistanceFromOrigin() => TestGetDistanceTo(0f, 0f);

        /// <summary>QA: 로컬 플레이어의 높이 — 바닥이 없으면 음수로 떨어진다.</summary>
        public float TestGetLocalY()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            return local != null ? local.transform.position.y : -999f;
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

        /// <summary>QA: 로컬 플레이어의 원점 거리 — 맵 스케일이 스폰에 반영됐는지 본다.</summary>
        public float TestGetSpawnDistanceFromOrigin()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (local == null) return -1f;

            UnityEngine.Vector3 p = local.transform.position;
            return new UnityEngine.Vector2(p.x, p.z).magnitude;
        }

        /// <summary>QA: 현재 선택된 맵 인덱스와 이름.</summary>
        public string TestGetSelectedMap()
        {
            var lobby = FindLobby();
            return lobby == null ? "" : $"{lobby.MapIndex}:{lobby.MapNameAt(lobby.MapIndex)}";
        }

        /// <summary>QA: 맵 선택 요청 — 비호스트가 부르면 서버가 무시해야 한다.</summary>
        public void TestSelectMap(int _index)
        {
            var lobby = FindLobby();
            if (lobby == null) return;
            lobby.DevSelectMap(_index);
            GameDebug.Log($"[TestManager] TestSelectMap({_index})");
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

        /// <summary>QA: 지금 반투명 처리된 벽 수 — 0이면 페이드가 안 걸린 것.</summary>
        public int TestGetFadedOccluderCount()
        {
            var fader = FindFirstObjectByType<CTC.Runtime.Cameras.OccluderFader>();
            return fader != null ? fader.FadedCount : -1;
        }

        /// <summary>로컬 플레이어의 현재 치즈 — 창고 생산 검증용(반환값이 wesqa로 나간다).</summary>
        public int TestGetCheese()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var wallet = local != null ? local.GetComponent<CheeseWallet>() : null;
            return wallet != null ? wallet.Cheese : -1;
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

        /// <summary>QA: 고양이 잠행 On/Off — 키 없이 강제한다.</summary>
        public void TestSetSneak(bool _on)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var sneak = local != null ? local.GetComponent<CTC.Runtime.Player.CatSneak>() : null;
            if (sneak == null) { GameDebug.LogWarning("[TestManager] CatSneak 없음(고양이 아님)"); return; }
            sneak.DevSetSneak(_on);
        }

        /// <summary>QA: 지정 클라이언트의 잠행 상태(복제 확인용). 대상이 없으면 false.</summary>
        public bool TestGetSneaking(long _clientId)
        {
            var player = NetUtil.FindPlayerObject((ulong)_clientId);
            var sneak = player != null ? player.GetComponent<CTC.Runtime.Player.CatSneak>() : null;
            return sneak != null && sneak.Sneaking;
        }

        /// <summary>QA: 지정 클라이언트의 발소리가 지금 나고 있는가 — 잠행 무음 검증.</summary>
        public bool TestGetFootstepAudible(long _clientId)
        {
            var player = NetUtil.FindPlayerObject((ulong)_clientId);
            if (player == null) return false;

            foreach (var source in player.GetComponentsInChildren<UnityEngine.AudioSource>())
            {
                if (source.loop && source.isPlaying) return true;
            }
            return false;
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

        private static DecoyLifetime NearestDecoy()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (local == null) return null;

            DecoyLifetime best = null;
            float bestSqr = float.MaxValue;
            foreach (var decoy in FindObjectsByType<DecoyLifetime>())
            {
                float d = UnityEngine.Vector3.SqrMagnitude(decoy.transform.position - local.transform.position);
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = decoy;
            }
            return best;
        }

        /// <summary>
        /// QA: 목표가 아니라 **방향**으로 일정 시간 밀어붙인다. 통과/막힘 판정용 —
        /// TestWalkTowards는 목표 1m 앞에서 멈춰서 "뚫고 지나갔는가"를 구분하지 못한다.
        /// </summary>
        public void TestWalkDirection(float _x, float _z, float _seconds)
        {
            WalkDirectionAsync(new UnityEngine.Vector3(_x, 0f, _z).normalized, _seconds, destroyCancellationToken).Forget();
        }

        private static async UniTaskVoid WalkDirectionAsync(
            UnityEngine.Vector3 _dir, float _seconds, CancellationToken _token)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var move = local != null ? local.GetComponent<CTC.Runtime.Player.PlayerMovement>() : null;
            if (move == null) { GameDebug.LogWarning("[TestManager] PlayerMovement 없음"); return; }

            move.DevSetMoveInput(_dir);
            await UniTask.Delay(TimeSpan.FromSeconds(_seconds), cancellationToken: _token);
            move.DevSetMoveInput(UnityEngine.Vector3.zero);
        }

        /// <summary>QA: 관전 중인 대상에게 따봉 — 대상 클라이언트 ID를 직접 준다.</summary>
        public void TestCheer(long _targetClientId)
        {
            var cheer = FindFirstObjectByType<CTC.Runtime.Match.CheerDirector>();
            if (cheer == null) { GameDebug.LogWarning("[TestManager] CheerDirector 없음"); return; }
            cheer.DevCheer((ulong)_targetClientId);
        }

        /// <summary>QA: 씬에 떠 있는 따봉 마커 수 — 고양이 화면에서는 0이어야 한다.</summary>
        public int TestGetCheerMarkerCount()
        {
            int count = 0;
            foreach (var tmp in FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None))
            {
                if (tmp.gameObject.name == "CheerMarker") count++;
            }
            return count;
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

        private static LobbyManager FindLobby()
        {
            var lobby = FindFirstObjectByType<LobbyManager>();
            if (lobby == null)
            {
                GameDebug.LogError("[TestManager] LobbyManager not found in scene");
            }
            return lobby;
        }
    }
}
#endif
