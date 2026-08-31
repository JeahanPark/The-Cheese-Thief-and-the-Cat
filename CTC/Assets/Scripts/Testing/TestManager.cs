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
    // wesqa가 리플렉션으로 Instance.<method>를 부르므로 **타입은 하나로 유지**해야 한다.
    // 595줄이 한 파일이라 QA 항목을 찾기 어려워 개편 5단계(2026-07-31)에서 partial 4파일로 갈랐다.
    public partial class TestManager : MonoSingleton<TestManager>
    {
        private const float KEY_PRESS_SECONDS = 0.1f;

        /// <summary>
        /// 씬 로드 직후 **메인 스레드에서** 싱글턴을 만들어 둔다.
        /// wesqa RPC는 소켓 스레드에서 들어오는데, 거기서 Instance가 처음 생성되면
        /// GameObject 생성·FindFirstObjectByType이 Unity API 스레드 제약에 걸려 실패한다
        /// (개편 5단계에서 Testing을 별도 어셈블리로 뺀 뒤 실측, 2026-07-31).
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceOnMainThread()
        {
            _ = Instance;
        }

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

            // Key 열거형 이름을 먼저 본다 — FindKeyOnCurrentKeyboardLayout은 '표시명' 기준이라
            // 한국어 레이아웃에서 "backquote" 같은 이름을 못 찾는다(2026-08-24 실측: ` 주입 무반응).
            UnityEngine.InputSystem.Controls.KeyControl key = null;
            if (System.Enum.TryParse(_keyName, true, out Key parsed) && parsed != Key.None)
            {
                key = keyboard[parsed];
            }
            if (key == null) key = keyboard.FindKeyOnCurrentKeyboardLayout(_keyName);
            if (key == null)
            {
                Debug.LogError($"[TestManager] Key '{_keyName}' not found");
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
        /// 씬 직행 — 빌드 QA용(2026-08-31). 방 만들기는 스팀 필수라 스팀 없는 빌드 QA는
        /// Game.unity 단독 경로(TestStartHost)로 우회한다. 유니티 로드라 in-scene NGO 스폰이
        /// 필요한 흐름(대기실→인게임)엔 못 쓴다 — 단독 QA 전용.
        /// </summary>
        public void TestLoadScene(string _sceneName)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(_sceneName);
        }

        /// <summary>인트로 → 로비. 빌드 E2E QA용(2026-08-31) — 버튼과 같은 사(私)경로를 SendMessage로 탄다.</summary>
        public void TestIntroStart()
        {
            var intro = FindAnyObjectByType<Intro.IntroController>();
            if (intro == null) { GameDebug.LogError("[TestManager] IntroController 없음"); return; }
            intro.SendMessage("OnClickStart");
        }

        /// <summary>로비 → 방 만들기(대기실). 빌드 E2E QA용 — 방장 NGO 호스트가 선다.</summary>
        public void TestCreateRoom()
        {
            var browser = FindAnyObjectByType<Lobby.Browser.LobbyBrowserController>();
            if (browser == null) { GameDebug.LogError("[TestManager] LobbyBrowserController 없음"); return; }
            browser.SendMessage("OnClickCreate");
        }

        /// <summary>대기실 강제 시작(인원 조건 무시) — RoomDirector.DevForceStartMatch 통로.</summary>
        public void TestRoomForceStart()
        {
            var director = FindAnyObjectByType<Room.RoomDirector>();
            if (director == null) { GameDebug.LogError("[TestManager] RoomDirector 없음"); return; }
            director.DevForceStartMatch();
        }

        /// <summary>프레임 시간 한 줄 — 개발 빌드 성능 측정용(2026-08-31). smooth는 최근 평균.</summary>
        public string TestGetFrameStats()
        {
            float smooth = Time.smoothDeltaTime;
            float raw = Time.unscaledDeltaTime;
            return $"fps={(smooth > 0f ? 1f / smooth : 0f):F1} smoothMs={smooth * 1000f:F1} rawMs={raw * 1000f:F1}";
        }

        /// <summary>씬·NetworkManager 상태 한 줄 — 클론이 왜 못 붙는지 가를 때(2026-08-31).</summary>
        public string TestGetNetDiag()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var anyNm = UnityEngine.Object.FindFirstObjectByType<Unity.Netcode.NetworkManager>(
                FindObjectsInactive.Include);
            var auto = UnityEngine.Object.FindFirstObjectByType<Networking.MppmAutoConnect>(
                FindObjectsInactive.Include);
            return $"scene={scene} singleton={(nm != null ? nm.gameObject.name : "null")} " +
                   $"nmInScene={(anyNm != null ? anyNm.gameObject.name : "null")} " +
                   $"autoConnect={(auto != null ? "yes" : "no")} " +
                   $"listening={(nm != null && nm.IsListening)}";
        }

        /// <summary>
        /// 클론 수동 접속 — MppmAutoConnect가 못 붙을 때의 QA 우회로(2026-08-31).
        /// 같은 공유 포트 파일을 읽으므로 자동 경로와 조건이 같다. 결과를 문자열로 돌려준다.
        /// </summary>
        public string TestStartClient()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null) return "no-networkmanager";
            if (nm.IsListening) return $"already listening client={nm.IsConnectedClient}";

            Networking.HostPortFallback.ApplyHostPortToTransport(nm);
            bool started = nm.StartClient();
            return $"startclient={started}";
        }

        /// <summary>
        /// HostPortFallback 경유 호스트 시작 — 선택 포트를 공유 파일에 기록해 클론이 따라온다.
        /// QA에서 NetworkManager.StartHost를 직접 호출하면 파일이 스테일로 남아
        /// 클론이 죽은 포트로 UTP 타임아웃(60초+)에 빠진다.
        /// </summary>
        public void TestStartHost()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null) { GameDebug.LogError("[TestManager] NetworkManager 없음"); return; }

            // 이미 호스팅/접속 중이면 조용히 무시한다 — 그대로 두면 HostPortFallback이
            // 제가 잡은 포트에 재바인드를 시도해 "7788~7807 전부 바인드 실패"라는
            // 가짜 에러를 띄운다(2026-08-14 실측: QA 스크립트의 중복 호출마다 반복).
            if (nm.IsListening)
            {
                GameDebug.Log("[TestManager] TestStartHost — 이미 호스팅/접속 중, 무시");
                return;
            }

            bool started = Networking.HostPortFallback.StartHost(nm);
            GameDebug.Log($"[TestManager] TestStartHost → {started}");
        }

        public void TestSelectRoleCat()
        {
            LobbyController lobby = FindLobby();
            if (lobby == null) return;

            lobby.DevSelectCat();
            GameDebug.Log("[TestManager] TestSelectRoleCat → LobbyController.DevSelectCat");
        }

        public void TestSelectRoleMouse()
        {
            LobbyController lobby = FindLobby();
            if (lobby == null) return;

            lobby.DevSelectMouse();
            GameDebug.Log("[TestManager] TestSelectRoleMouse → LobbyController.DevSelectMouse");
        }

        public void TestStartGame()
        {
            LobbyController lobby = FindLobby();
            if (lobby == null) return;

            lobby.DevStartGame();
            GameDebug.Log("[TestManager] TestStartGame → LobbyController.DevStartGame");
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

        /// <summary>QA: 이 인스턴스의 NGO 클라이언트 ID — MPPM 인스턴스 번호와 다르다.</summary>
        public long TestGetLocalClientId()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm != null && nm.IsListening ? (long)nm.LocalClientId : -1;
        }

        private static FortressShield NearestFortress()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (local == null) return null;

            FortressShield best = null;
            float bestSqr = float.MaxValue;
            foreach (var fort in FindObjectsByType<FortressShield>())
            {
                float d = UnityEngine.Vector3.SqrMagnitude(fort.transform.position - local.transform.position);
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = fort;
            }
            return best;
        }

        private static LobbyController FindLobby()
        {
            var lobby = FindFirstObjectByType<LobbyController>();
            if (lobby == null)
            {
                GameDebug.LogError("[TestManager] LobbyController not found in scene");
            }
            return lobby;
        }
    }
}
#endif
