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

        /// <summary>
        /// QA: 이동 폴리곤이 **몇 조각으로 끊겨 있나**. "조각수 최대조각 삼각형수".
        /// 하나여야 정상이다 — 여럿이면 그 사이는 길찾기가 못 넘어간다.
        /// </summary>
        public string TestNavMeshIslands()
        {
            UnityEngine.AI.NavMeshTriangulation tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
            int triCount = tri.indices.Length / 3;
            if (triCount == 0) return "0 폴리곤";

            // 꼭짓점을 좌표로 합쳐 인접(변 공유)을 만든다 — 인덱스가 달라도 같은 자리면 같은 점이다
            var byPos = new System.Collections.Generic.Dictionary<Vector3, int>();
            var canon = new int[tri.vertices.Length];
            for (int i = 0; i < tri.vertices.Length; i++)
            {
                Vector3 key = new Vector3(
                    Mathf.Round(tri.vertices[i].x * 100f), Mathf.Round(tri.vertices[i].y * 100f),
                    Mathf.Round(tri.vertices[i].z * 100f));
                if (!byPos.TryGetValue(key, out int id)) { id = byPos.Count; byPos[key] = id; }
                canon[i] = id;
            }

            var edgeOwner = new System.Collections.Generic.Dictionary<long, int>();
            var parent = new int[triCount];
            for (int i = 0; i < triCount; i++) parent[i] = i;

            int Find(int _x) { while (parent[_x] != _x) { parent[_x] = parent[parent[_x]]; _x = parent[_x]; } return _x; }
            void Union(int _a, int _b) { int ra = Find(_a), rb = Find(_b); if (ra != rb) parent[rb] = ra; }

            for (int t = 0; t < triCount; t++)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = canon[tri.indices[t * 3 + e]];
                    int b = canon[tri.indices[t * 3 + (e + 1) % 3]];
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (edgeOwner.TryGetValue(key, out int other)) Union(t, other);
                    else edgeOwner[key] = t;
                }
            }

            var sizes = new System.Collections.Generic.Dictionary<int, int>();
            for (int t = 0; t < triCount; t++)
            {
                int r = Find(t);
                sizes[r] = sizes.TryGetValue(r, out int n) ? n + 1 : 1;
            }

            int largest = 0;
            foreach (int n in sizes.Values) largest = Mathf.Max(largest, n);
            return $"조각 {sizes.Count} · 최대 {largest} / 전체 {triCount}";
        }

        /// <summary>
        /// QA: 그 자리가 폴리곤 위인지, 그 자리가 속한 폴리곤의 높이는 얼마인지.
        /// "hit=x,y,z dist=.. " — y가 예상 밖이면 벽 위나 다른 층에 붙은 것이다.
        /// </summary>
        public string TestNavSample(float _x, float _z)
        {
            var p = new Vector3(_x, 0f, _z);
            for (float r = 0.5f; r <= 8f; r *= 2f)
            {
                if (!UnityEngine.AI.NavMesh.SamplePosition(
                        p, out UnityEngine.AI.NavMeshHit hit, r, UnityEngine.AI.NavMesh.AllAreas))
                {
                    continue;
                }
                return $"r={r} hit={hit.position.x:F2},{hit.position.y:F2},{hit.position.z:F2} " +
                       $"dist={Vector3.Distance(p, hit.position):F2}";
            }
            return "8m 안에 폴리곤 없음";
        }

        /// <summary>
        /// QA: 두 좌표 사이 길찾기를 직접 물어본다 — "샘플결과 status 코너수 첫코너".
        /// 봇이 안 움직일 때 **어느 층에서 끊겼는지**를 이걸로 가른다(폴리곤이 없나·길이 없나).
        /// </summary>
        public string TestNavPath(float _x1, float _z1, float _x2, float _z2)
        {
            var a = new Vector3(_x1, 0f, _z1);
            var b = new Vector3(_x2, 0f, _z2);

            bool okA = UnityEngine.AI.NavMesh.SamplePosition(
                a, out UnityEngine.AI.NavMeshHit ha, 5f, UnityEngine.AI.NavMesh.AllAreas);
            bool okB = UnityEngine.AI.NavMesh.SamplePosition(
                b, out UnityEngine.AI.NavMeshHit hb, 5f, UnityEngine.AI.NavMesh.AllAreas);
            if (!okA || !okB) return $"sample a={okA} b={okB}";

            var path = new UnityEngine.AI.NavMeshPath();
            UnityEngine.AI.NavMesh.CalculatePath(
                ha.position, hb.position, UnityEngine.AI.NavMesh.AllAreas, path);

            Vector3[] c = path.corners;
            string first = c.Length > 0 ? $"{c[0].x:F1},{c[0].z:F1}" : "-";
            string last = c.Length > 0 ? $"{c[c.Length - 1].x:F1},{c[c.Length - 1].z:F1}" : "-";
            return $"{path.status} corners={c.Length} {first} → {last}";
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

        // ===== 직업권 자리 (테크 v3 계획 3 Task 4) =====

        /// <summary>
        /// QA: 깡패권·용사권이 든 벽감/상자의 좌표 — "Gang:x,z|Hero:x,z" (못 찾으면 그 칸이 빈다).
        ///
        /// **서버 전용이다.** 벽 뒤 내용물은 복제하지 않는다(열기 전엔 아무도 몰라야 한다) —
        /// 그래서 호스트에서만 답이 나온다. 판을 두 번 열어 이 문자열이 바뀌는지를 재는 훅이고,
        /// 벽을 부숴 눈으로 확인하지 않기 위한 것이다.
        /// </summary>
        public string TestGetJobTicketPositions()
        {
            // **가드를 실제로 건다**(2026-09-09 감사) — 예전에는 주석뿐이라, 클라에서 부르면
            // 늘 "Gang:|Hero:"가 나왔다. 두 판을 비교하는 검사가 그걸 「자리가 안 바뀐다」로
            // 읽어, 셔플이 죽어 있어도 통과했다.
            if (!RequireServer(nameof(TestGetJobTicketPositions))) return MISSING;

            string gang = FindTicketPos(CTC.Runtime.Map.LootId.GangTicket);
            string hero = FindTicketPos(CTC.Runtime.Map.LootId.HeroTicket);
            if (gang == MISSING || hero == MISSING)
            {
                GameDebug.LogWarning($"[TestManager] 직업권을 못 찾았다 — Gang:{gang} Hero:{hero}");
            }
            return $"Gang:{gang}|Hero:{hero}";
        }

        private static string FindTicketPos(CTC.Runtime.Map.LootId _id)
        {
            foreach (var alcove in FindObjectsByType<CTC.Runtime.Map.Alcove>(FindObjectsSortMode.None))
            {
                if (!alcove.BoxContains(_id)) continue;
                UnityEngine.Vector3 p = alcove.transform.position;
                return $"{p.x:0.#},{p.z:0.#}";
            }
            foreach (var box in FindObjectsByType<CTC.Runtime.Map.LootBox>(FindObjectsSortMode.None))
            {
                if (!box.Contains(_id)) continue;
                UnityEngine.Vector3 p = box.transform.position;
                return $"{p.x:0.#},{p.z:0.#}";
            }
            return MISSING;   // 빈 문자열이면 「자리가 안 바뀐다」로 읽힌다(2026-09-09 감사)
        }

        /// <summary>
        /// QA: 맵에 깔린 상자·벽감이 품은 치즈 총합. **셔플이 총량을 안 건드렸는지**를 재는 값이라
        /// 판마다 같아야 한다 — 이게 흔들리면 자리를 바꾼 게 아니라 내용을 만들거나 지운 것이다.
        /// </summary>
        public int TestGetMapLootCheese()
        {
            int sum = 0;
            foreach (var alcove in FindObjectsByType<CTC.Runtime.Map.Alcove>(FindObjectsSortMode.None))
            {
                sum += alcove.BoxCheese;
            }
            foreach (var box in FindObjectsByType<CTC.Runtime.Map.LootBox>(FindObjectsSortMode.None))
            {
                sum += box.CheeseInside;
            }
            return sum;
        }
    }
}
#endif
