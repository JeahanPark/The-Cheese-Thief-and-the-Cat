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

            foreach (var source in player.GetComponentsInChildren<UnityEngine.AudioSource>())
            {
                if (source.loop && source.isPlaying) return true;
            }
            return false;
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

        // ===== 옵션 (2026-08-01) =====

        public void TestOpenOptions()
        {
            var popup = FindFirstObjectByType<CTC.Runtime.UI.OptionsPopup>();
            if (popup != null) popup.Open();
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

        /// <summary>QA: 설정값 읽기 — "sensitivity" | "bgm" | "sfx".</summary>
        public float TestGetSetting(string _key)
        {
            switch (_key)
            {
                case "sensitivity": return UserSettings.MouseSensitivity.Value;
                case "bgm": return UserSettings.BgmVolume.Value;
                default: return UserSettings.SfxVolume.Value;
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

        /// <summary>QA: 설정값 쓰기 — 슬라이더를 드래그한 것과 같은 경로(Model에 직접).</summary>
        public void TestSetSetting(string _key, float _value)
        {
            switch (_key)
            {
                case "sensitivity": UserSettings.MouseSensitivity.Value = _value; break;
                case "bgm": UserSettings.BgmVolume.Value = _value; break;
                default: UserSettings.SfxVolume.Value = _value; break;
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

        /// <summary>QA: 게임플레이 입력이 막혀 있는가(채팅 타이핑·옵션 창).</summary>
        public bool TestGetInputBlocked() => UiFocus.BlocksGameplay;

        // ===== 감옥 (v3.8 4번) =====

        /// <summary>QA: 내가 갇혔는가.</summary>
        public bool TestGetJailed()
        {
            var tag = LocalJailedTag();
            return tag != null && tag.Jailed;
        }

        /// <summary>QA: 내 남은 체류 시간(안 갇혔으면 -1).</summary>
        public float TestGetJailRemaining()
        {
            var tag = LocalJailedTag();
            return tag != null && tag.Jailed ? tag.RemainingSeconds : -1f;
        }

        /// <summary>QA: 지금 갇힌 쥐 수(전원 기준).</summary>
        public int TestGetJailedCount()
        {
            int count = 0;
            foreach (var tag in FindObjectsByType<CTC.Runtime.Match.JailedTag>(FindObjectsSortMode.None))
            {
                if (tag.Jailed) count++;
            }
            return count;
        }

        /// <summary>QA: 감옥 좌표들 — "x,z;x,z" 형식. 구조하러 갈 자리를 QA가 알아야 한다.</summary>
        public string TestGetJailPositions()
        {
            var builder = FindFirstObjectByType<CTC.Runtime.Map.MapBuilder>();
            if (builder == null) return "";

            var parts = new System.Collections.Generic.List<string>();
            foreach (Vector3 p in builder.JailPositions) parts.Add($"{p.x:F2},{p.z:F2}");
            return string.Join(";", parts);
        }

        private static CTC.Runtime.Match.JailedTag LocalJailedTag()
        {
            Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return null;
            Unity.Netcode.NetworkObject me = nm.SpawnManager.GetLocalPlayerObject();
            return me != null ? me.GetComponent<CTC.Runtime.Match.JailedTag>() : null;
        }

        /// <summary>QA: 커서가 잠겨 있는가 — 옵션 창이 열리면 false, 닫히면 true여야 한다.</summary>
        public bool TestGetCursorLocked() => Cursor.lockState == CursorLockMode.Locked;
    }
}
#endif
