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
    // TestManager의 일부 — 맵·스폰·이동 (개편 5단계 분할, 2026-07-31)
    public partial class TestManager
    {
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

        /// <summary>QA: 카메라-플레이어 거리. 페이드 방식은 벽에 붙어도 이 값이 변하지 않아야 한다.</summary>
        public float TestGetCameraDistance()
        {
            var cam = FindFirstObjectByType<CTC.Runtime.Cameras.OrbitCameraController>();
            return cam != null ? cam.DevDistanceToTarget() : -1f;
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
    }
}
#endif
