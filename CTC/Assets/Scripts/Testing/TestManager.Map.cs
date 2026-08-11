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
        /// <summary>
        /// 로컬 플레이어를 다른 플레이어 바로 앞으로 순간이동(전투 QA용, CC 토글).
        ///
        /// `_clientId`를 주면 **그 사람에게** 간다. 안 주면(-1) 스폰 순서상 처음 만나는 남에게 간다 —
        /// 이게 기본이던 시절 qa_v37이 `_clientId`를 넘겨도 시그니처에 없어 통째로 버려졌고,
        /// 고양이가 늘 호스트(먼저 스폰된 쥐)를 잡아 엉뚱한 쥐를 가뒀다(2026-08-09 규명).
        /// </summary>
        public void TestTeleportToOtherPlayer(long _clientId = -1)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (local == null) { GameDebug.LogWarning("[TestManager] 로컬 플레이어 없음"); return; }

            if (_clientId >= 0)
            {
                var target = CTC.Runtime.Core.NetUtil.FindPlayerObject((ulong)_clientId);
                if (target == null) { GameDebug.LogWarning($"[TestManager] clientId {_clientId} 플레이어 없음"); return; }
                MoveInFrontOf(local, target);
                return;
            }

            foreach (var kv in nm.SpawnManager.SpawnedObjects)
            {
                var other = kv.Value;
                if (other == null || other == local || other.GetComponent<CTC.Runtime.Player.PlayerMovement>() == null) continue;

                MoveInFrontOf(local, other);
                return;
            }
        }

        // ===== 제3세력 강아지 (v3.8 5번) =====

        /// <summary>QA: 맵에 서 있는 강아지 벽감의 HP(없으면 -1). 치즈 벽감보다 단단해야 한다.</summary>
        public int TestGetDogAlcoveHp()
        {
            foreach (var alcove in FindObjectsByType<CTC.Runtime.Map.Alcove>(FindObjectsSortMode.None))
            {
                if (alcove.Payload != CTC.Runtime.Map.AlcovePayload.Dog) continue;
                var hp = alcove.GetComponent<CTC.Runtime.Combat.Health>();
                return hp != null ? hp.Hp : -1;
            }
            return -1;
        }

        /// <summary>서버 전용 — 강아지 벽감을 부순다(부수는 데 19초를 쓰지 않으려고).</summary>
        public void TestBreakDogAlcove()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) { GameDebug.LogWarning("[TestManager] 서버 전용"); return; }

            foreach (var alcove in FindObjectsByType<CTC.Runtime.Map.Alcove>(FindObjectsSortMode.None))
            {
                if (alcove.Payload != CTC.Runtime.Map.AlcovePayload.Dog) continue;
                var hp = alcove.GetComponent<CTC.Runtime.Combat.Health>();
                if (hp != null) hp.TakeDamage(hp.MaxHp);
                return;
            }
        }

        /// <summary>QA: 지금 살아 있는 강아지 수.</summary>
        public int TestGetDogCount() =>
            FindObjectsByType<CTC.Runtime.Ai.DogBrain>(FindObjectsSortMode.None).Length;

        /// <summary>QA: 강아지 HP(없으면 -1).</summary>
        public int TestGetDogHp()
        {
            var dog = FindFirstObjectByType<CTC.Runtime.Ai.DogBrain>();
            if (dog == null) return -1;
            var hp = dog.GetComponent<CTC.Runtime.Combat.Health>();
            return hp != null ? hp.Hp : -1;
        }

        /// <summary>QA: 강아지 상태·위치·길찾기 내부 — "state x,z graph=.. path=.. idx=.. stuck=..".</summary>
        public string TestGetDogDiagnostics()
        {
            var brain = FindFirstObjectByType<CTC.Runtime.Ai.DogBrain>();
            if (brain == null) return "";

            var mover = brain.GetComponent<CTC.Runtime.Ai.DogMover>();
            Vector3 p = brain.transform.position;
            return $"{brain.State} {p.x:F2},{p.z:F2} {(mover != null ? mover.DevDiagnostics : "no-mover")}";
        }

        /// <summary>QA: 강아지 위치 "x,z"(없으면 빈 문자열) — 실제로 맵을 가로지르는지 잰다.</summary>
        public string TestGetDogPos()
        {
            var brain = FindFirstObjectByType<CTC.Runtime.Ai.DogBrain>();
            if (brain == null) return "";

            Vector3 p = brain.transform.position;
            return $"{p.x:F2},{p.z:F2}";
        }

        /// <summary>
        /// QA: 구워진 이동 폴리곤 수 (2026-08-12). 0이면 길찾기가 통째로 죽은 것이고,
        /// 그러면 강아지도 봇도 한 발을 못 뗀다.
        ///
        /// 예전 이름은 `TestGetDogPrunedEdges`였다 — 웨이포인트 그래프에서 벽에 막힌 간선을
        /// 몇 개 걷어냈는지 세던 값이다. 폴리곤으로 바꾸면서 그 개념 자체가 없어졌다.
        /// </summary>
        public int TestGetNavMeshPolygons()
        {
            UnityEngine.AI.NavMeshTriangulation tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
            return tri.indices != null ? tri.indices.Length / 3 : 0;
        }

        /// <summary>QA: 강아지와 로컬 플레이어의 수평 거리(둘 중 하나가 없으면 -1).</summary>
        public float TestGetDogDistance()
        {
            var dog = FindFirstObjectByType<CTC.Runtime.Ai.DogBrain>();
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (dog == null || local == null) return -1f;

            return CTC.Runtime.Ai.DogAiLogic
                .Flat(dog.transform.position - local.transform.position).magnitude;
        }

        /// <summary>로컬 플레이어를 강아지 코앞으로 옮긴다 — 물리는 것을 재현한다.</summary>
        public void TestTeleportToDog()
        {
            var dog = FindFirstObjectByType<CTC.Runtime.Ai.DogBrain>();
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            if (dog == null || local == null) { GameDebug.LogWarning("[TestManager] 강아지/로컬 없음"); return; }

            MoveInFrontOf(local, dog.NetworkObject);
        }

        /// <summary>서버 전용 — 강아지에게 피해(처치 검증). 실제 전투 경로가 아니라 결과만 본다.</summary>
        public void TestDamageDog(int _damage)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) { GameDebug.LogWarning("[TestManager] 서버 전용"); return; }

            var dog = FindFirstObjectByType<CTC.Runtime.Ai.DogBrain>();
            var hp = dog != null ? dog.GetComponent<CTC.Runtime.Combat.Health>() : null;
            if (hp != null) hp.TakeDamage(_damage);
        }

        private static void MoveInFrontOf(Unity.Netcode.NetworkObject _local, Unity.Netcode.NetworkObject _other)
        {
            var cc = _local.GetComponent<UnityEngine.CharacterController>();
            UnityEngine.Vector3 pos = _other.transform.position - _other.transform.forward * 1.2f;
            if (cc != null) cc.enabled = false;
            _local.transform.position = pos;
            _local.transform.LookAt(_other.transform.position);
            if (cc != null) cc.enabled = true;
            GameDebug.Log($"[TestManager] TeleportToOtherPlayer → {pos}");
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
