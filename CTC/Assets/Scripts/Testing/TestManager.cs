// WES에서 이식(2026-07-12) — 골격만: SimulateKeyPress(Input System StateEvent 주입) + CTC 시나리오 3종.
// WES 원본의 인게임 의존 메서드(InGameController/Popup/FullPlay 등)는 CTC 미이식 상태라 제외.
// WesQA InvokeBridge가 리플렉션으로 Instance.<method>(kwargs)를 호출하는 QA 진입점 싱글턴.
#if UNITY_EDITOR
using System.Collections;
using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CTC.Runtime.Testing
{
    public class TestManager : MonoSingleton<TestManager>
    {
        // ===== 범용 입력 시뮬레이션 =====

        public void SimulateKeyPress(string _keyName)
        {
            StartCoroutine(CoSimulateKeyPress(_keyName));
        }

        private IEnumerator CoSimulateKeyPress(string _keyName)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                GameDebug.LogError("[TestManager] Keyboard not found");
                yield break;
            }

            var key = keyboard.FindKeyOnCurrentKeyboardLayout(_keyName);
            if (key == null)
            {
                GameDebug.LogError($"[TestManager] Key '{_keyName}' not found");
                yield break;
            }

            using (StateEvent.From(keyboard, out var eventPtr))
            {
                key.WriteValueIntoEvent(1f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }

            GameDebug.Log($"[TestManager] Key '{_keyName}' down");
            yield return new WaitForSeconds(0.1f);

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

        /// <summary>로컬 플레이어 구매 — QA용 파라미터리스 래퍼.</summary>
        public void TestBuySlingshot() => BuyLocal(ShopItemId.Slingshot);
        public void TestBuyMachineGun() => BuyLocal(ShopItemId.MachineGun);
        public void TestBuyDash() => BuyLocal(ShopItemId.Dash);
        public void TestBuyDecoy() => BuyLocal(ShopItemId.Decoy);
        public void TestBuyFortress() => BuyLocal(ShopItemId.Fortress);
        public void TestBuyCatAttack() => BuyLocal(ShopItemId.CatAttack);
        public void TestBuyCatSpeed() => BuyLocal(ShopItemId.CatSpeed);

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
