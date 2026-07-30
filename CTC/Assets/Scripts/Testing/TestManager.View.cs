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
    }
}
#endif
