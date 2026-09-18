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
    // TestManager의 일부 — 카메라·잠행·발소리·따봉 (개편 5단계 분할, 2026-07-31)
    public partial class TestManager
    {
        /// <summary>QA: 시점 각도 지정 — 벽이 카메라와 캐릭터 사이에 오게 만들어 페이드를 검증한다.</summary>
        public void TestSetCameraAngles(float _yaw, float _pitch)
        {
            var cam = FindFirstObjectByType<CTC.Runtime.Cameras.OrbitCameraController>();
            if (cam == null) { GameDebug.LogWarning("[TestManager] OrbitCameraController 없음"); return; }
            cam.DevSetAngles(_yaw, _pitch);
            GameDebug.Log($"[TestManager] 카메라 각도 → yaw {_yaw}, pitch {_pitch}");
        }

        /// <summary>QA: 지금 반투명 처리된 벽 수 — 0이면 페이드가 안 걸린 것.</summary>
        public int TestGetFadedOccluderCount()
        {
            var fader = FindFirstObjectByType<CTC.Runtime.Cameras.OccluderFader>();
            return fader != null ? fader.FadedCount : -1;
        }

        /// <summary>QA: 고양이 잠행 On/Off — 키 없이 강제한다.</summary>
        public void TestSetSneak(bool _on)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var sneak = local != null ? local.GetComponent<CTC.Runtime.Player.Sneak>() : null;
            if (sneak == null) { GameDebug.LogWarning("[TestManager] Sneak 없음"); return; }
            sneak.DevSetSneak(_on);
        }

        /// <summary>QA: 지정 클라이언트의 잠행 상태(복제 확인용). 대상이 없으면 false.</summary>
        public bool TestGetSneaking(long _clientId)
        {
            var player = NetUtil.FindPlayerObject((ulong)_clientId);
            var sneak = player != null ? player.GetComponent<CTC.Runtime.Player.Sneak>() : null;
            return sneak != null && sneak.Sneaking;
        }

        /// <summary>QA: 지정 클라이언트의 발소리가 지금 나고 있는가 — 잠행 무음 검증.</summary>
        public bool TestGetFootstepAudible(long _clientId)
        {
            var player = NetUtil.FindPlayerObject((ulong)_clientId);
            if (player == null) return false;

            var steps = player.GetComponentInChildren<CTC.Runtime.Audio.FootstepPlayer>();
            return steps != null && steps.IsStepping;
        }

        /// <summary>QA: 관전 중인 대상에게 따봉 — 대상 클라이언트 ID를 직접 준다.</summary>
        public void TestCheer(long _targetClientId)
        {
            var cheer = FindFirstObjectByType<CTC.Runtime.Match.CheerDirector>();
            if (cheer == null) { GameDebug.LogWarning("[TestManager] CheerDirector 없음"); return; }
            cheer.DevCheer((ulong)_targetClientId);
        }

        /// <summary>
        /// QA: 씬에 떠 있는 따봉 마커 수 — 고양이 화면에서는 0이어야 한다.
        ///
        /// **전용 컴포넌트로 센다**(2026-09-09 감사). 예전에는 `gameObject.name == "CheerMarker"`
        /// 문자열 매칭이라, 마커를 개명하면 영원히 0이 되어 「고양이 화면에는 0」이
        /// **항상 통과**했다 — 같은 파일의 `HudText` 주석이 경고하는 바로 그 짓이었다.
        /// </summary>
        public int TestGetCheerMarkerCount()
        {
            return FindObjectsByType<CTC.Runtime.Match.CheerMarker>(FindObjectsSortMode.None).Length;
        }

        // ===== 채팅 (2026-08-01) =====

        /// <summary>QA: 채팅 한 줄 전송(입력창을 거치지 않고 바로 서버로).</summary>
        public void TestSendChat(string _text)
        {
            CTC.Runtime.Managers.ChatManager chat = CTC.Runtime.Managers.ChatManager.Instance;
            if (chat == null) { GameDebug.LogWarning("[TestManager] ChatManager 없음"); return; }
            chat.SendChatMessage(_text);
        }

        /// <summary>QA: 이 클라이언트의 채팅 로그 전문(수신 확인·정보 누출 확인).</summary>
        public string TestGetChatLog()
        {
            var hud = FindFirstObjectByType<CTC.Runtime.UI.ChatHud>();
            return hud != null ? hud.DevLogText : "";
        }

        /// <summary>
        /// QA: **입력창을 거쳐서** 보낸다 — 열고, 글자를 넣고, Enter를 눌러 `ChatHud.Submit()`을 태운다.
        /// `TestSendChat`은 UI를 건너뛰므로 "입력창이 글자를 제대로 넘기는가"를 못 덮는다
        /// (2026-08-09 한글 마지막 음절이 잘리던 버그가 그 틈으로 빠져나갔다).
        ///
        /// 한계: IME 조합은 OS가 만드는 상태라 여기서 재현할 수 없다 — 조합 확정 자체는 수동 확인이다.
        /// 이 훅이 지키는 건 "Submit이 입력창의 글자를 온전히 읽어 보낸다"는 배선이다.
        /// </summary>
        public void TestTypeChatAndEnter(string _text)
        {
            var hud = FindFirstObjectByType<CTC.Runtime.UI.ChatHud>();
            if (hud == null) { GameDebug.LogWarning("[TestManager] ChatHud 없음"); return; }

            hud.Open();
            var field = hud.GetComponentInChildren<TMPro.TMP_InputField>(true);
            if (field == null) { GameDebug.LogWarning("[TestManager] 채팅 입력창 없음"); return; }

            field.text = _text;
            SimulateKeyPress("enter");
        }

        /// <summary>QA: 입력창을 열고 글자만 넣는다(Enter 없음) — `/` 예약어 힌트 검증용.</summary>
        public void TestTypeChat(string _text)
        {
            var hud = FindFirstObjectByType<CTC.Runtime.UI.ChatHud>();
            if (hud == null) { GameDebug.LogWarning("[TestManager] ChatHud 없음"); return; }
            hud.Open();
            var field = hud.GetComponentInChildren<TMPro.TMP_InputField>(true);
            if (field != null) field.text = _text;
        }

        /// <summary>QA: `/` 예약어 힌트 — 떠 있으면 내용, 아니면 빈 문자열.</summary>
        public string TestGetChatHint()
        {
            var hud = FindFirstObjectByType<CTC.Runtime.UI.ChatHud>();
            return hud != null ? hud.DevHintText : "";
        }

        /// <summary>QA: 채팅 입력창 열림 여부.</summary>
        public bool TestGetChatOpen()
        {
            var hud = FindFirstObjectByType<CTC.Runtime.UI.ChatHud>();
            return hud != null && hud.IsOpen;
        }

        /// <summary>QA: 채팅 입력창 닫기 — 주입한 Enter가 포커스 문제로 씹혀 창이 열린 채 남았을 때 치운다.</summary>
        public void TestCloseChat()
        {
            var hud = FindFirstObjectByType<CTC.Runtime.UI.ChatHud>();
            if (hud != null && hud.IsOpen) hud.Close();
        }

        // ===== 인게임 콘솔 진단 (2026-08-24, ` 키가 안 열리는 문제) =====

        private MonoBehaviour FindDebugLogManager()
        {
            foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb != null && mb.GetType().Name == "DebugLogManager") return mb;
            }
            return null;
        }

        /// <summary>QA: 콘솔 내부 상태 — 어디서 끊기는지 한 줄로.</summary>
        public string TestGetConsoleState()
        {
            MonoBehaviour mb = FindDebugLogManager();
            if (mb == null) return "manager=null";

            var t = mb.GetType();
            object toggleWithKey = t.GetField("toggleWithKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(mb);
            object bindingObj = t.GetField("toggleBinding",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.GetValue(mb);
            string binding = "field-none";
            if (bindingObj is UnityEngine.InputSystem.InputAction act)
            {
                binding = $"enabled={act.enabled} bindings={act.bindings.Count}" +
                          (act.bindings.Count > 0 ? $" path={act.bindings[0].path}" : "");
            }
            object visible = t.GetProperty("IsLogWindowVisible")?.GetValue(mb);
            return $"active={mb.gameObject.activeInHierarchy} enabled={mb.enabled} " +
                   $"toggleWithKey={toggleWithKey} binding[{binding}] visible={visible}";
        }

        /// <summary>QA: 콘솔 창을 코드로 강제로 연다 — 입력 문제와 표시 문제를 가른다.</summary>
        public void TestShowDebugConsole()
        {
            MonoBehaviour mb = FindDebugLogManager();
            if (mb == null) { GameDebug.LogWarning("[TestManager] DebugLogManager 없음"); return; }
            mb.GetType().GetMethod("ShowLogWindow")?.Invoke(mb, null);
        }

        // ===== 옵션 (2026-08-01) =====

        public void TestOpenOptions()
        {
            var popups = CTC.Runtime.UI.Popup.PopupManager.Instance;
            if (popups != null) popups.Open<CTC.Runtime.UI.OptionsPopup>();
        }

        public void TestCloseOptions()
        {
            var popup = FindFirstObjectByType<CTC.Runtime.UI.OptionsPopup>();
            if (popup != null) popup.Close();
        }

        public bool TestGetOptionsOpen()
        {
            var popup = FindFirstObjectByType<CTC.Runtime.UI.OptionsPopup>();
            return popup != null && popup.IsOpen;
        }

        /// <summary>
        /// QA: 설정값 읽기 — "sensitivity" | "bgm" | "sfx". **모르는 키는 NaN + 경고**다.
        ///
        /// 예전에는 읽기·쓰기의 `default:`가 **둘 다 sfx**였다(2026-09-09 감사 T8).
        /// 오타 키(`"volume"`·`"master"`)가 쓰기도 읽기도 sfx로 새어, 왕복 단언이
        /// **자기 일관되게 통과**했다 — 어떤 단언으로도 못 잡는 거짓말이었다.
        /// </summary>
        public float TestGetSetting(string _key)
        {
            switch (_key)
            {
                case "sensitivity": return UserSettings.MouseSensitivity.Value;
                case "bgm": return UserSettings.BgmVolume.Value;
                case "sfx": return UserSettings.SfxVolume.Value;
                default:
                    GameDebug.LogWarning($"[TestManager] 모르는 설정 키 '{_key}' — sensitivity|bgm|sfx");
                    return float.NaN;
            }
        }

        /// <summary>QA: 옵션 창의 "기본값" 버튼과 같은 경로 — CSV 기본값으로 되돌린다(미리듣기).</summary>
        public void TestResetSettings() => UserSettings.ResetToDefaults();

        /// <summary>QA: 옵션 창의 "확인" — 지금 값을 디스크에 새긴다.</summary>
        public void TestSaveSettings() => UserSettings.Save();

        /// <summary>
        /// QA: 실제로 들리는 볼륨 — 이름으로 찾은 오브젝트의 `AudioSource.volume`.
        /// Model 값만 보면 "설정은 바뀌는데 소리는 그대로"인 배선 누락을 못 잡는다(2026-08-09 실측).
        /// 없으면 -1.
        /// </summary>
        public float TestGetAudioVolume(string _name)
        {
            GameObject go = GameObject.Find(_name);
            if (go == null) return -1f;
            AudioSource src = go.GetComponent<AudioSource>();
            return src != null ? src.volume : -1f;
        }

        /// <summary>QA: 옵션 창의 "취소"·ESC — 저장된 값으로 미리듣기를 되돌린다.</summary>
        public void TestRevertSettings() => UserSettings.Revert();

        /// <summary>
        /// QA: 설정값 쓰기 — 슬라이더를 드래그한 것과 같은 경로(Model에 직접).
        /// **모르는 키는 아무것도 안 쓰고 경고**한다(감사 T8) — 읽기와 짝이 맞아야
        /// 오타 키의 왕복이 실패한다.
        /// </summary>
        public void TestSetSetting(string _key, float _value)
        {
            switch (_key)
            {
                case "sensitivity": UserSettings.MouseSensitivity.Value = _value; break;
                case "bgm": UserSettings.BgmVolume.Value = _value; break;
                case "sfx": UserSettings.SfxVolume.Value = _value; break;
                default:
                    GameDebug.LogWarning($"[TestManager] 모르는 설정 키 '{_key}' — 아무것도 안 썼다");
                    break;
            }
        }

        /// <summary>QA: 로컬 플레이어 HP(관전 중이면 -1) — 역할 판별·사망 확인에 쓴다.</summary>
        public int TestGetLocalHp()
        {
            Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return -1;
            Unity.Netcode.NetworkObject me = nm.SpawnManager.GetLocalPlayerObject();
            if (me == null) return -1;
            Health hp = me.GetComponent<Health>();
            return hp != null ? hp.Hp : -1;
        }

        /// <summary>
        /// QA: 로컬 플레이어 HP를 세운다(**호스트에서만** — 서버가 아니면 false).
        /// qa_v40이 강아지 옆에 오래 서 있다가 물려 죽어 판이 끝나던 자리(90×1.4초 → 1000이 16초)를
        /// 단계 사이에 채워 두려고 있다. 판정을 대신하지 않는다 — 물렸는지는 여전히 HP가 줄었는지로 본다.
        /// </summary>
        public bool TestSetLocalHp(int _hp)
        {
            Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || nm.SpawnManager == null) return false;
            Unity.Netcode.NetworkObject me = nm.SpawnManager.GetLocalPlayerObject();
            if (me == null) return false;
            Health hp = me.GetComponent<Health>();
            if (hp == null || hp.IsDead) return false;
            hp.ServerSetHp(_hp);
            return true;
        }

        /// <summary>QA: 로컬 플레이어 역할 — "Cat" | "Mouse" | "None"(관전·미시작).</summary>
        public string TestGetRole()
        {
            Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return "None";
            Unity.Netcode.NetworkObject me = nm.SpawnManager.GetLocalPlayerObject();
            if (me == null) return "None";
            RoleTag tag = me.GetComponent<RoleTag>();
            return tag != null ? tag.Role.ToString() : "None";
        }

        /// <summary>
        /// QA: 로컬 몸의 눈동자 — "gazeX,gazeY,gazeZ|pupilX,pupilY,pupilZ"(머리 로컬 시선 · 오른눈 월드 위치).
        /// 눈은 연출이라 Model 값만 보면 배선 누락을 못 잡는다 — **실제 눈동자 트랜스폼**을 재서 준다.
        /// </summary>
        public string TestGetEyeGaze()
        {
            Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return "none";
            Unity.Netcode.NetworkObject me = nm.SpawnManager.GetLocalPlayerObject();
            if (me == null) return "none";
            var eyes = me.GetComponent<CTC.Runtime.Player.EyeLook>();
            if (eyes == null) return "none";

            Vector3 g = eyes.DevGazeRight();
            // 눈동자 구가 사라져(2026-09-13) 「실제로 움직였나」의 근거는 셰이프키 가중치다.
            string w = eyes.DevEyeWeights();
            Vector3 f = Camera.main != null ? Camera.main.transform.forward : Vector3.zero;
            var aim = me.GetComponent<CTC.Runtime.Player.GazeAim>();
            Vector2 a = aim != null ? aim.DevAngles : Vector2.zero;
            return $"gaze={g.x:F3},{g.y:F3},{g.z:F3}|weights={w}" +
                   $"|camFwd={f.x:F3},{f.y:F3},{f.z:F3}|bodyFwd={me.transform.forward.x:F3},{me.transform.forward.z:F3}" +
                   $"|aim={eyes.DevAimSource()}" +
                   $"|packed={(aim != null ? aim.DevPacked : (short)0)}|yaw={a.x:F2}|pitch={a.y:F2}";
        }

        /// <summary>
        /// QA: **남의 몸** 눈동자 — 이 기능의 값어치는 전부 여기서 나온다(2026-09-10).
        /// 3인칭이라 제 눈은 거의 못 보고, 예전엔 남의 눈이 늘 가운데 붙어 죽어 있었다.
        /// 가장 가까운 남의 몸을 잡아 "role|packed|yaw,pitch|gaze|pupil"을 준다 —
        /// `packed`가 0이 아닌 값으로 바뀌면 **복제가 이 클라까지 닿았다**는 뜻이다.
        /// </summary>
        public string TestGetOtherEyeGaze(string _role = "")
        {
            GameObject body = NearestOtherBody(_role);
            if (body == null) return "none";

            var eyes = body.GetComponent<CTC.Runtime.Player.EyeLook>();
            var aim = body.GetComponent<CTC.Runtime.Player.GazeAim>();
            if (eyes == null) return "no EyeLook";

            Vector3 g = eyes.DevGazeRight();
            string w = eyes.DevEyeWeights();
            Vector2 a = aim != null ? aim.DevAngles : Vector2.zero;
            var tag = body.GetComponent<RoleTag>();
            return $"role={(tag != null ? tag.Role.ToString() : "None")}" +
                   $"|packed={(aim != null ? aim.DevPacked : (short)0)}|yaw={a.x:F2}|pitch={a.y:F2}" +
                   $"|gaze={g.x:F3},{g.y:F3},{g.z:F3}|weights={w}" +
                   $"|aim={eyes.DevAimSource()}|mine={eyes.DevMine()}";
        }

        /// <summary>
        /// QA: 시선 복제가 **초당 몇 번** 쓰였나 — 대역폭의 유일한 실측 근거(2026-09-10).
        /// "writes=N|seconds=S|hz=H". 한 번 쓸 때 나가는 것은 `short` 2바이트 + NGO 델타 봉투다.
        /// </summary>
        public string TestGetGazeWrites()
        {
            Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
            Unity.Netcode.NetworkObject me =
                nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var aim = me != null ? me.GetComponent<CTC.Runtime.Player.GazeAim>() : null;
            if (aim == null) return "none";

            float seconds = aim.DevWriteSeconds;
            float hz = seconds > 0.01f ? aim.DevWrites / seconds : 0f;
            return $"writes={aim.DevWrites}|seconds={seconds:F2}|hz={hz:F2}|source={aim.DevSource()}";
        }

        /// <summary>
        /// QA: **두 눈동자가 같은 쪽을 보는가**(2026-09-10). 오른눈만 재던 훅으로는 못 잡던 것이다.
        ///
        /// 각 눈동자가 제 흰자 중심에서 얼마나 밀려났는지를 **몸 로컬**로 준다(흰자 반지름 단위).
        /// 몸 로컬 x가 그 캐릭터의 오른쪽이므로, 조준을 오른쪽으로 옮겼을 때
        /// **두 값이 같은 쪽으로 움직이면** 눈이 같이 도는 것이다.
        /// "offR=x,y,z|offL=x,y,z|dot=..." — dot은 두 오프셋 방향의 코사인(1이면 완전히 같은 쪽).
        /// </summary>
        public string TestGetEyePupils(string _role = "")
        {
            GameObject body = string.IsNullOrEmpty(_role) ? LocalBody() : NearestOtherBody(_role);
            if (body == null) return "none";
            var eyes = body.GetComponent<CTC.Runtime.Player.EyeLook>();
            if (eyes == null) return "no EyeLook";

            // **눈동자 구가 사라졌다**(2026-09-13 셰이프키 전환). 「실제로 움직였나」의 근거는
            // 이제 렌더러에서 되읽은 셰이프키 가중치다 — 계산값이 아니라 들어간 값이다.
            // 시선(gazeR·gazeL)은 그대로 둔다: 두 눈이 **다른 쪽을 보는지**는 여전히 여기서만 보인다.
            return $"weights={eyes.DevEyeWeights()}" +
                   $"|gazeR={eyes.DevGazeRight().x:F3},{eyes.DevGazeRight().y:F3},{eyes.DevGazeRight().z:F3}" +
                   $"|gazeL={eyes.DevGazeLeft().x:F3},{eyes.DevGazeLeft().y:F3},{eyes.DevGazeLeft().z:F3}";
        }

        /// <summary>
        /// QA: 로컬 몸의 조준을 이 각도에 **못박는다** — 전/후 시트를 같은 값에서 찍으려고.
        /// yaw 양수가 그 몸의 오른쪽, pitch 양수가 위.
        /// </summary>
        public void TestSetGazeAim(float _yawDeg, float _pitchDeg, string _role = "")
        {
            // 봇 몸은 서버가 소유하므로 호스트에서는 남의 몸도 못박을 수 있다 —
            // 쥐 얼굴을 보며 수치를 만질 때 그 쥐가 옆을 봐 줘야 안쪽 눈이 움직인다.
            GameObject body = string.IsNullOrEmpty(_role) ? LocalBody() : NearestOtherBody(_role);
            var aim = body != null ? body.GetComponent<CTC.Runtime.Player.GazeAim>() : null;
            if (aim == null) { GameDebug.LogWarning("[TestManager] GazeAim 없음"); return; }
            aim.DevForce(_yawDeg, _pitchDeg);
        }

        /// <summary>QA: 못박은 조준을 푼다.</summary>
        public void TestClearGazeAim()
        {
            GameObject body = LocalBody();
            var aim = body != null ? body.GetComponent<CTC.Runtime.Player.GazeAim>() : null;
            if (aim == null) { GameDebug.LogWarning("[TestManager] GazeAim 없음"); return; }
            aim.DevClearForce();
        }

        private static GameObject LocalBody()
        {
            Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
            Unity.Netcode.NetworkObject me =
                nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            return me != null ? me.gameObject : null;
        }

        /// <summary>QA: 위 셈을 0부터 다시 센다 — 재기 직전에 부른다.</summary>
        public void TestResetGazeWrites()
        {
            Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
            Unity.Netcode.NetworkObject me =
                nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var aim = me != null ? me.GetComponent<CTC.Runtime.Player.GazeAim>() : null;
            if (aim == null) { GameDebug.LogWarning("[TestManager] GazeAim 없음"); return; }
            aim.DevResetWrites();
        }

        /// <summary>
        /// 이 클라에서 **가장 가까운 남의 몸**. 봇도 남이다 — 귀 명부(`EarPerk.DevAll`)를 쓴다.
        /// 봇 몸은 플레이어 오브젝트가 아니라 clientId로는 못 찾는다.
        /// </summary>
        private static GameObject NearestOtherBody(string _role = "")
        {
            RoleId want = RoleId.None;
            if (!string.IsNullOrEmpty(_role) && !Enum.TryParse(_role, true, out want)) want = RoleId.None;

            Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
            Unity.Netcode.NetworkObject me =
                nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            Vector3 from = me != null ? me.transform.position : Vector3.zero;

            GameObject best = null;
            float bestSqr = float.PositiveInfinity;
            foreach (CTC.Runtime.Player.EarPerk body in CTC.Runtime.Player.EarPerk.DevAll)
            {
                if (body == null) continue;
                if (me != null && body.gameObject == me.gameObject) continue;
                if (want != RoleId.None && body.DevRole != want) continue;
                float sqr = (body.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = body.gameObject;
            }
            return best;
        }

        // TestHideEyes / TestShowEyes는 2026-09-13에 지웠다 — 얹은 구를 껐다 켜는 훅이었는데
        // 셰이프키로 갈아타면서 **끌 것이 없어졌다**(눈이 곧 모델 메시다).
        // 「전/후」를 찍으려면 눈을 감추는 게 아니라 가중치를 0으로 두면 된다.

        /// <summary>QA: 로컬 몸의 눈을 지금 감게 한다 — 깜빡임은 랜덤 주기라 기다리면 못 잡는다.</summary>
        public void TestBlinkEyes()
        {
            Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
            Unity.Netcode.NetworkObject me =
                nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var eyes = me != null ? me.GetComponent<CTC.Runtime.Player.EyeLook>() : null;
            if (eyes == null) { GameDebug.LogWarning("[TestManager] EyeLook 없음"); return; }
            eyes.DevBlinkNow();
        }

        /// <summary>QA: 게임플레이 입력이 막혀 있는가(채팅 타이핑·옵션 창).</summary>
        public bool TestGetInputBlocked() => UiFocus.BlocksGameplay;

        /// <summary>QA: 커서가 잠겨 있는가 — 옵션 창이 열리면 false, 닫히면 true여야 한다.</summary>
        public bool TestGetCursorLocked() => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>
        /// 조작이 왜 안 먹는지 한 줄로 캔다(2026-09-03) — 조작 불가는 원인이 여럿이라
        /// 하나씩 물어보면 왕복만 는다: 로컬 몸·소유·플레이어오브젝트·모달·타이핑·커서·팝업 수.
        /// </summary>
        public string TestGetControlDiag()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            Unity.Netcode.NetworkObject me =
                nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;

            string body = "none";
            if (me != null)
            {
                var move = me.GetComponent<Player.PlayerMovement>();
                body = $"{me.gameObject.name} owner={me.IsOwner} playerObj={me.IsPlayerObject} " +
                       $"mine={(move != null && Player.LocalControl.Mine(move))}";
            }

            int popups = UI.Popup.PopupManager.Instance != null
                ? UI.Popup.PopupManager.Instance.OpenedCount : -1;

            return $"body=({body}) modal={UiFocus.ModalOpen} typing={UiFocus.IsTyping} " +
                   $"blocks={UiFocus.BlocksGameplay} cursor={Cursor.lockState} popups={popups}";
        }

        /// <summary>
        /// QA: 로컬 몸의 귀 한 줄 —
        /// "turn=deg|perk=0..1|peaks=N|steps=N|shots=N|kind=step|turnR=|turnL=|near=Cat@5.2m|range=20.0|stepping=1|sneak=0".
        ///
        /// **`peaks`가 새 계약의 계측값이다**(2026-09-10). 귀가 걸음 단위로 튕기게 바뀐 뒤로
        /// `perk`는 순간값이라 표본을 아무 때나 뜨면 대부분 0이 나온다(걸음 사이엔 풀려 있다) —
        /// 그건 회귀가 아니다. <see cref="TestResetEarCounters"/>로 창을 열고
        /// **그 창 안에 몇 번 쫑긋했나**를 세야 한다: 뛸 때 N회 이상 · 살금살금이면 0회.
        /// `steps`와 `shots`를 나눠 세는 이유는 잠행이 **걸음만** 줄이기 때문이다
        /// (덮치기·스킬 소리는 잠행 중에도 들려 귀가 선다 — 안 나누면 오독한다).
        /// </summary>
        public string TestGetEarState()
        {
            Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
            Unity.Netcode.NetworkObject me =
                nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            var ears = me != null ? me.GetComponent<CTC.Runtime.Player.EarPerk>() : null;
            if (ears == null) return "none";

            return $"turn={ears.DevTurnDegrees:F2}|perk={ears.DevPerk:F3}|" +
                   $"peaks={ears.DevPerkPeaks}|steps={ears.DevStepImpulses}|shots={ears.DevShotImpulses}|" +
                   $"kind={ears.DevLastKind}|" +
                   $"turnR={ears.DevTurnRight:F2}|turnL={ears.DevTurnLeft:F2}|" +
                   NearestEnemyLine(me.transform.position, ears.DevRole);
        }

        /// <summary>QA: 지정 역할의 몸 중 로컬에서 가장 가까운 것의 귀 — 봇(=사람 아닌 몸) 귀를 재는 쪽.</summary>
        public string TestGetEarStateOfRole(string _role, bool _botOnly = false)
        {
            if (!System.Enum.TryParse(_role, true, out RoleId role)) return "none";

            Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
            Unity.Netcode.NetworkObject me =
                nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            Vector3 from = me != null ? me.transform.position : Vector3.zero;

            CTC.Runtime.Player.EarPerk best = NearestBody(role, from, _botOnly);
            if (best == null) return "none";

            return $"turn={best.DevTurnDegrees:F2}|perk={best.DevPerk:F3}|" +
                   $"peaks={best.DevPerkPeaks}|steps={best.DevStepImpulses}|shots={best.DevShotImpulses}|" +
                   $"kind={best.DevLastKind}|" +
                   $"self={Vector3.Distance(best.transform.position, from):F2}m|" +
                   NearestEnemyLine(best.transform.position, best.DevRole);
        }

        /// <summary>
        /// QA: 모든 몸의 귀 셈(쫑긋 횟수·들린 걸음·들린 일회성)을 0으로. **계측 창을 여는 훅**이다.
        /// 몇 마리를 비웠는지 돌려준다.
        /// </summary>
        public int TestResetEarCounters()
        {
            int count = 0;
            foreach (CTC.Runtime.Player.EarPerk body in CTC.Runtime.Player.EarPerk.DevAll)
            {
                if (body == null) continue;
                body.DevResetCounters();
                count++;
            }
            return count;
        }

        /// <summary>
        /// QA: 서버가 조종하는 몸(봇)의 잠행을 켜고 끈다 — 역할 지정("Cat"|"Mouse"), 바뀐 마릿수를 준다.
        /// 봇은 플레이어 오브젝트가 아니라 <see cref="TestSetSneak"/>로는 못 건드린다.
        /// 「살금살금이면 귀가 안 선다」를 재려면 소리를 내는 쪽이 잠행해야 해서 필요하다.
        /// </summary>
        public int TestSetBotSneak(string _role, bool _on)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) { GameDebug.LogWarning("[TestManager] 서버가 아니다"); return 0; }
            if (!System.Enum.TryParse(_role, true, out RoleId role)) return 0;

            // 두뇌가 매 틱 잠행을 다시 정하므로 값만 써 놓으면 곧 덮인다 — 판단 자체를 잠근다.
            CTC.Runtime.Bots.BotAgent.DevSneakOverride = _on ? 1 : 0;

            int count = 0;
            foreach (CTC.Runtime.Player.EarPerk body in CTC.Runtime.Player.EarPerk.DevAll)
            {
                if (body == null || body.DevRole != role) continue;
                var netObj = body.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null && netObj.IsPlayerObject) continue;   // 사람 몸은 제 클라가 정한다
                var sneak = body.GetComponent<CTC.Runtime.Player.Sneak>();
                if (sneak == null) continue;
                sneak.DevSetSneak(_on);
                count++;
            }
            GameDebug.Log($"[TestManager] 봇 잠행 {role} → {_on} ({count}마리)");
            return count;
        }

        /// <summary>
        /// QA: 로컬 몸을 지정 역할의 가장 가까운 몸에서 _meters m 떨어진 자리로 옮기고 마주 본다.
        /// 「같은 거리에서 뛸 때와 살금살금일 때」를 재려면 거리를 손으로 잡아야 한다 —
        /// 소리를 내는 쪽은 안 건드리고 **듣는 쪽만** 옮긴다. 실제 거리(m)를 돌려준다.
        /// </summary>
        public float TestTeleportAtRangeFrom(string _role, float _meters, float _yawOffset = 0f, bool _botOnly = false)
        {
            if (!System.Enum.TryParse(_role, true, out RoleId role)) return -1f;

            var nm = Unity.Netcode.NetworkManager.Singleton;
            var me = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (me == null) return -1f;

            CTC.Runtime.Player.EarPerk target = NearestBody(role, me.transform.position, _botOnly);
            if (target == null) return -1f;

            Vector3 away = me.transform.position - target.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = target.transform.forward;
            Vector3 pos = target.transform.position + away.normalized * _meters;
            pos.y = target.transform.position.y;

            var cc = me.GetComponent<UnityEngine.CharacterController>();
            if (cc != null) cc.enabled = false;
            me.transform.position = pos;
            me.transform.LookAt(new Vector3(target.transform.position.x, pos.y, target.transform.position.z));
            // 몸을 소리 쪽에서 _yawOffset도 돌려 세우면 **귀가 돌아간 그림**이 남는다
            // (정면으로 마주 보면 귀 각이 0이라 "돌았다"를 못 보여 준다).
            if (Mathf.Abs(_yawOffset) > 0.01f) me.transform.Rotate(0f, _yawOffset, 0f, Space.World);
            if (cc != null) cc.enabled = true;

            Vector3 delta = target.transform.position - pos;
            delta.y = 0f;
            return delta.magnitude;
        }

        /// <summary>
        /// 가장 가까운 반대편 몸 한 줄 — "near=Cat@5.2m|range=20.0|stepping=1|sneak=0".
        /// 봇 몸은 플레이어 오브젝트가 아니라 clientId로 못 찾는다 — 귀 명부(EarPerk.DevAll)로 훑는다.
        /// </summary>
        private static string NearestEnemyLine(Vector3 _from, RoleId _myRole)
        {
            RoleId enemy = _myRole == RoleId.Mouse ? RoleId.Cat
                : _myRole == RoleId.Cat ? RoleId.Mouse : RoleId.None;
            if (enemy == RoleId.None) return "near=none";

            CTC.Runtime.Player.EarPerk best = NearestBody(enemy, _from);
            if (best == null) return "near=none";

            Vector3 flat = best.transform.position - _from;
            flat.y = 0f;

            var steps = best.GetComponentInChildren<CTC.Runtime.Audio.FootstepPlayer>();
            var sneak = best.GetComponent<CTC.Runtime.Player.Sneak>();
            return $"near={enemy}@{flat.magnitude:F2}m|range={(steps != null ? steps.CurrentRange : -1f):F1}|" +
                   $"stepping={(steps != null && steps.IsStepping ? 1 : 0)}|" +
                   $"sneak={(sneak != null && sneak.Sneaking ? 1 : 0)}";
        }

        /// <param name="_botOnly">
        /// 사람·대기실 봇의 몸(플레이어 오브젝트)을 뺀다. 대기실이 채운 봇은 몸만 서고
        /// **두뇌가 없어 한 발도 안 뗀다** — 「발소리가 들리면」을 재려면 실제로 걷는
        /// TestBotManager 봇을 잡아야 한다(2026-09-08 실측: 이걸 안 걸러 60표본 전부 stepping=0).
        /// </param>
        private static CTC.Runtime.Player.EarPerk NearestBody(RoleId _role, Vector3 _from, bool _botOnly = false)
        {
            CTC.Runtime.Player.EarPerk best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (CTC.Runtime.Player.EarPerk body in CTC.Runtime.Player.EarPerk.DevAll)
            {
                if (body == null || body.DevRole != _role) continue;
                if (_botOnly)
                {
                    var netObj = body.GetComponent<Unity.Netcode.NetworkObject>();
                    if (netObj == null || netObj.IsPlayerObject) continue;
                }
                Vector3 flat = body.transform.position - _from;
                flat.y = 0f;
                if (flat.magnitude >= bestDistance) continue;
                bestDistance = flat.magnitude;
                best = body;
            }
            return best;
        }

        /// <summary>
        /// QA: 3인칭 카메라를 지정 역할의 가장 가까운 몸 쪽으로 돌린다 — 눈동자는 카메라를 따라가므로
        /// 이걸로 **눈이 소리 쪽을 보는 프레임**을 만든다. 몸은 안 돌린다(가만히 서면 몸은 안 돈다).
        /// </summary>
        public float TestAimCameraAtRole(string _role, float _pitch, bool _botOnly = false)
        {
            if (!System.Enum.TryParse(_role, true, out RoleId role)) return -999f;

            var nm = Unity.Netcode.NetworkManager.Singleton;
            var me = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (me == null) return -999f;

            CTC.Runtime.Player.EarPerk target = NearestBody(role, me.transform.position, _botOnly);
            if (target == null) return -999f;

            Vector3 to = target.transform.position - me.transform.position;
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;

            var cam = FindFirstObjectByType<CTC.Runtime.Cameras.OrbitCameraController>();
            if (cam == null) return -999f;
            cam.DevSetAngles(yaw, _pitch);
            return yaw;
        }
    }
}
#endif
