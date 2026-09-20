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

        /// <summary>
        /// 메인 → 서버 목록(Lobby). 빌드 E2E QA용(2026-08-31). 2026-09-14부터 메인에 「게임 시작」이 없다 —
        /// 「공개 게임 → 서버 목록」과 같은 곳으로 간다(선택 팝업은 사람용이라 건너뛴다).
        /// </summary>
        public void TestIntroStart()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
        }

        /// <summary>
        /// 서버 목록 → 공개 방 만들기(대기실). 빌드 E2E QA용 — 방장 NGO 호스트가 선다.
        /// 이름 팝업은 사람용이라 건너뛰고 <see cref="Lobby.FrontDoor.SessionStarter"/>를 바로 부른다.
        /// </summary>
        public void TestCreateRoom()
        {
            var starter = FindAnyObjectByType<Lobby.FrontDoor.SessionStarter>();
            if (starter == null) { GameDebug.LogError("[TestManager] SessionStarter 없음"); return; }
            starter.HostPublic("QA 방");
        }

        /// <summary>
        /// 메인·서버 목록에서 **비밀번호 방**을 연다(2026-09-14) — 팝업 없이 같은 절차. 빈 값이면 비밀번호 없음.
        /// 스팀이 없으면 로컬 호스트다(관문은 같다).
        /// </summary>
        public void TestFrontDoorHost(string _password)
        {
            var starter = FindAnyObjectByType<Lobby.FrontDoor.SessionStarter>();
            if (starter == null) { GameDebug.LogError("[TestManager] SessionStarter 없음"); return; }
            starter.DevHostFriends(_password);
        }

        /// <summary>
        /// 로컬 호스트에 **이 비밀번호로 한 번** 참가를 시도한다(재시도·팝업 없음). 결과는
        /// <see cref="TestGetGateState"/>의 reject로 읽는다.
        /// </summary>
        public void TestFrontDoorJoin(string _password)
        {
            var starter = FindAnyObjectByType<Lobby.FrontDoor.SessionStarter>();
            if (starter == null) { GameDebug.LogError("[TestManager] SessionStarter 없음"); return; }
            starter.DevJoinLocalOnce(_password);
        }

        /// <summary>관문 상태 한 줄 — accepting·비밀번호 유무·마지막 거절 사유·접속 상태.</summary>
        public string TestGetGateState()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return $"accepting={Networking.ConnectionGate.Accepting} " +
                   $"hostPw={Networking.ConnectionGate.HostHasPassword} " +
                   $"reject={Networking.ConnectionGate.LastRejectReason} " +
                   $"approval={(nm != null && nm.NetworkConfig.ConnectionApproval)} " +
                   $"listening={(nm != null && nm.IsListening)} " +
                   $"connected={(nm != null && nm.IsConnectedClient)} " +
                   $"server={(nm != null && nm.IsServer)} " +
                   $"clients={(nm != null && nm.IsServer ? nm.ConnectedClientsIds.Count : -1)}";
        }

        /// <summary>대기실 봇 수 지정 — 방장(서버)만. 남은 자리를 넘으면 서버가 깎는다.</summary>
        public void TestSetRoomBots(int _count)
        {
            var session = FindAnyObjectByType<Room.RoomSession>();
            if (session == null) { GameDebug.LogError("[TestManager] RoomSession 없음"); return; }
            session.SetBotCount(_count);
        }

        /// <summary>대기실 봇 수 조회.</summary>
        public int TestGetRoomBots()
        {
            var session = FindAnyObjectByType<Room.RoomSession>();
            return session != null ? session.BotCount : -1;
        }

        /// <summary>대기실 맵 고르기 — 방장(서버)만. 배열 순서가 곧 맵 인덱스다(RoomMapCatalog).</summary>
        public void TestSelectRoomMap(int _index)
        {
            var session = FindAnyObjectByType<Room.RoomSession>();
            if (session == null) { GameDebug.LogError("[TestManager] RoomSession 없음"); return; }
            session.SelectMap(_index);
        }

        /// <summary>
        /// 대기실 한 줄 — 봇·맵(고른 것/개수)·시작 가능·시작 중·시작 버튼이 눌리는 상태인가(2026-09-19).
        /// 판에서 돌아온 대기실이 설정을 들고 있는지, 시작 버튼이 실제로 눌리는지를 같이 본다.
        /// </summary>
        public string TestGetRoomState()
        {
            var session = FindAnyObjectByType<Room.RoomSession>();
            if (session == null) return "missing";
            var catalog = FindAnyObjectByType<Room.RoomMapCatalog>();
            var controller = FindAnyObjectByType<Room.RoomController>();
            UnityEngine.UI.Button start = controller != null ? controller.DevStartButton : null;
            bool pressable = start != null && start.gameObject.activeInHierarchy && start.interactable;
            return $"bots={session.BotCount} map={session.MapIndex} maps={(catalog != null ? catalog.Count : -1)} " +
                   $"canStart={session.CanStart} starting={session.Starting} startPressable={pressable}";
        }

        /// <summary>
        /// 시작 버튼을 **사람처럼** 누른다 — 버튼 한가운데에 UI 레이를 쏴서 맨 위에 맞은 것을 누른다.
        /// 그래서 흐린(잠긴) 버튼은 안 눌리고, 투명한 무언가가 버튼을 덮고 있으면 그것이 클릭을 먹는다.
        /// 강제 시작(`TestRoomForceStart`)과 달리 시작 조건을 그대로 지난다. 시작이 걸렸으면 true.
        /// </summary>
        public bool TestRoomClickStart()
        {
            var controller = FindAnyObjectByType<Room.RoomController>();
            var session = FindAnyObjectByType<Room.RoomSession>();
            UnityEngine.UI.Button start = controller != null ? controller.DevStartButton : null;
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (start == null || session == null || events == null)
            {
                GameDebug.LogError("[TestManager] 대기실 시작 버튼·EventSystem 없음");
                return false;
            }

            var rect = (RectTransform)start.transform;
            Canvas canvas = start.GetComponentInParent<Canvas>();
            Camera eye = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var pointer = new UnityEngine.EventSystems.PointerEventData(events)
            {
                position = RectTransformUtility.WorldToScreenPoint(eye, rect.TransformPoint(rect.rect.center))
            };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            events.RaycastAll(pointer, hits);
            if (hits.Count == 0) { GameDebug.LogWarning("[TestManager] 시작 버튼 자리에 아무것도 안 맞는다"); return false; }

            GameObject target = UnityEngine.EventSystems.ExecuteEvents
                .GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject);
            if (target != start.gameObject)
            {
                GameDebug.LogWarning($"[TestManager] 시작 버튼 자리의 클릭을 '{hits[0].gameObject.name}'가 먹는다");
                return false;
            }

            pointer.pointerPressRaycast = hits[0];
            pointer.pointerCurrentRaycast = hits[0];
            UnityEngine.EventSystems.ExecuteEvents.Execute(target, pointer,
                UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            return session.Starting;
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

        /// <summary>
        /// 「서버 전용」이라고 적힌 훅의 **실제** 가드(2026-09-09 감사 E-22).
        ///
        /// 주석에만 적어 두고 피호출부가 막게 두면, 클라에서 부른 훅이 **경고 한 줄 없이
        /// 아무 일도 안 한다** — QA는 「했는데 효과가 없다」로 읽어 엉뚱한 곳을 판다.
        /// </summary>
        private static bool RequireServer(string _hook)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsServer) return true;

            GameDebug.LogWarning($"[TestManager] {_hook} — 서버 전용인데 클라이언트에서 불렀다(무동작)");
            return false;
        }

        public void TestSelectRoleCat()
        {
            LobbyController lobby = FindLobby();
            if (lobby == null) { GameDebug.LogWarning("[TestManager] LobbyController 없음 — 역할을 못 고른다"); return; }

            lobby.DevSelectCat();
            GameDebug.Log("[TestManager] TestSelectRoleCat → LobbyController.DevSelectCat");
        }

        public void TestSelectRoleMouse()
        {
            LobbyController lobby = FindLobby();
            if (lobby == null) { GameDebug.LogWarning("[TestManager] LobbyController 없음 — 역할을 못 고른다"); return; }

            lobby.DevSelectMouse();
            GameDebug.Log("[TestManager] TestSelectRoleMouse → LobbyController.DevSelectMouse");
        }

        public void TestStartGame()
        {
            LobbyController lobby = FindLobby();
            if (lobby == null) { GameDebug.LogWarning("[TestManager] LobbyController 없음 — 판을 못 시작한다"); return; }

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

        /// <summary>
        /// 「못 찾았다·미배선」을 뜻하는 문자열 센티널(2026-09-09 감사 T9).
        ///
        /// 열거 이름을 돌려주는 훅은 **정상값과 겹치지 않는 값**으로 없음을 말해야 한다 —
        /// `MouseJobId.None`·`MatchResult.None`은 「아직 없다」는 **정상**이라,
        /// 미배선을 같은 글자로 돌려주면 빈 씬에서도 단언이 통과한다.
        /// </summary>
        public const string MISSING = "Missing";

        /// <summary>
        /// 서버(호스트) 전용: 고양이 **전원** 즉사 — 쥐 승리 판정 QA. 죽인 마릿수를 돌려준다.
        ///
        /// 예전에는 첫 마리에서 `return` 했다(2026-09-09 감사) — 이름은 복수인데 한 마리만
        /// 죽여서, **고양이 2인 판에서 「쥐 승」이 영영 안 났다**. 그 경로가 계측에서 빠져 있었다.
        /// </summary>
        public int TestKillCat()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
            {
                GameDebug.LogWarning("[TestManager] TestKillCat — 서버 아님");
                return -1;
            }

            int killed = 0;
            foreach (var health in FindObjectsByType<Health>())
            {
                var tag = health.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Cat) continue;
                if (health.IsDead) continue;
                health.TakeDamage(999999);
                killed++;
            }

            if (killed == 0) GameDebug.LogWarning("[TestManager] TestKillCat — 살아 있는 고양이가 없다");
            else GameDebug.Log($"[TestManager] TestKillCat → 고양이 {killed}마리 처치");
            return killed;
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
