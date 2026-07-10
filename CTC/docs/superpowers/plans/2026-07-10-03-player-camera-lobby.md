# Plan 3: 플레이어 & 카메라 & 로비 역할 선택 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** NGO 접속 후 로비에서 역할(쥐/고양이)을 선택하고, 시작 시 역할별 프리팹(Quirky 모델+애니메이션)이 스폰되어 3인칭 자유 궤도 카메라(Cinemachine)로 플레이한다.

**Architecture:** 순수 로직(카메라 각도·입력 변환·애니 상태·로비 규칙)은 static 클래스로 분리해 EditMode TDD. 네트워크 상태는 LobbyManager(NetworkBehaviour, in-scene) 하나가 소유 — `NetworkList<LobbyPlayerState>` + `NetworkVariable<bool> gameStarted`. 자동 플레이어 스폰을 끄고 서버가 역할별 프리팹을 `SpawnAsPlayerObject`. 카메라는 씬에 1대(Cinemachine OrbitalFollow), 로컬 소유자 스폰 시 바인딩.

**Tech Stack:** Unity 6000.5.2f1, NGO 2.12.0, Cinemachine 3.1.4(신규 설치), Input System 1.19(`Keyboard.current`/`Mouse.current` 직접 폴링), URP 17.5.

**스펙:** [../specs/2026-07-10-player-camera-lobby-design.md](../specs/2026-07-10-player-camera-lobby-design.md)

---

## 실행 환경 주의 (모든 태스크 공통)

- 코드 수정 후 컴파일 확인: `powershell -NoProfile -File tools/focus-unity.ps1`로 에디터 포커스(도메인 리로드 유발) → `u_console`(MCP)로 에러 0 확인.
- EditMode 테스트 실행: `u_editor_menu`(MCP)로 `Tools/CTC/Run EditMode Tests` 실행 → `u_console`에서 `[EditModeTests] 완료 — Passed: N, Failed: 0` 확인.
- **Unity에서는 컴파일 에러 = 테스트 실행 불가.** 따라서 각 TDD 태스크는 "테스트 + 잘못된 기본값을 반환하는 스텁"을 함께 작성해 RED를 확인한 뒤 구현한다.
- 레거시 `Input.*` 금지(런타임 예외). `UnityEngine.Input` 대신 `UnityEngine.InputSystem`.
- CharacterController가 있는 오브젝트는 transform 위치 직접 세팅 금지 → `CC.Move()`. (회전은 직접 세팅 가능)
- 커밋 메시지 끝에: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`
- git 작업 디렉터리는 저장소 루트가 `c:\GitFork\The-Cheese-Thief-and-the-Cat`, Unity 프로젝트가 그 하위 `CTC/`. 아래 경로는 모두 `CTC/` 기준.

---

### Task 1: Cinemachine 3.1.4 설치 + asmdef 참조

**Files:**
- Modify: `Packages/manifest.json`
- Modify: `Assets/Scripts/CTC.Runtime.asmdef`

- [ ] **Step 1: manifest.json에 패키지 추가**

`"com.unity.collab-proxy"` 줄 위에 삽입:

```json
    "com.unity.cinemachine": "3.1.4",
```

- [ ] **Step 2: CTC.Runtime.asmdef 참조 추가**

`references` 배열에 `"Unity.Cinemachine"` 추가:

```json
    "references": [
        "Unity.Netcode.Runtime",
        "Unity.Netcode.Components",
        "Unity.Networking.Transport",
        "Unity.InputSystem",
        "Unity.Multiplayer.Playmode",
        "Unity.Cinemachine"
    ],
```

- [ ] **Step 3: 에디터 포커스 → 패키지 해석·컴파일 확인**

Run: `powershell -NoProfile -File tools/focus-unity.ps1` → 잠시 대기 → MCP `u_console`
Expected: 컴파일 에러 0. (패키지 다운로드로 수십 초 걸릴 수 있음 — u_console이 안 뜨면 다시 포커스)

- [ ] **Step 4: Commit**

```bash
git add CTC/Packages/manifest.json CTC/Packages/packages-lock.json CTC/Assets/Scripts/CTC.Runtime.asmdef
git commit -m "chore: install Cinemachine 3.1.4 and reference from runtime asmdef"
```

---

### Task 2: CameraOrbitLogic (TDD)

**Files:**
- Create: `Assets/Scripts/Cameras/CameraOrbitLogic.cs`
- Test: `Assets/Scripts/Tests/EditMode/CameraOrbitLogicTests.cs`

- [ ] **Step 1: 테스트 + 스텁(잘못된 값 반환) 작성**

`Assets/Scripts/Tests/EditMode/CameraOrbitLogicTests.cs`:

```csharp
using CTC.Runtime.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class CameraOrbitLogicTests
    {
        [Test]
        public void ClampPitch_WithinRange_Unchanged()
        {
            Assert.AreEqual(20f, CameraOrbitLogic.ClampPitch(20f, -30f, 70f));
        }

        [Test]
        public void ClampPitch_BelowMin_ClampsToMin()
        {
            Assert.AreEqual(-30f, CameraOrbitLogic.ClampPitch(-45f, -30f, 70f));
        }

        [Test]
        public void ClampPitch_AboveMax_ClampsToMax()
        {
            Assert.AreEqual(70f, CameraOrbitLogic.ClampPitch(90f, -30f, 70f));
        }

        [Test]
        public void WrapYaw_WithinRange_Unchanged()
        {
            Assert.AreEqual(90f, CameraOrbitLogic.WrapYaw(90f), 1e-4f);
        }

        [Test]
        public void WrapYaw_Over180_WrapsToNegative()
        {
            Assert.AreEqual(-170f, CameraOrbitLogic.WrapYaw(190f), 1e-4f);
        }

        [Test]
        public void WrapYaw_UnderMinus180_WrapsToPositive()
        {
            Assert.AreEqual(170f, CameraOrbitLogic.WrapYaw(-190f), 1e-4f);
        }

        [Test]
        public void Step_MouseRight_IncreasesYaw()
        {
            Vector2 next = CameraOrbitLogic.Step(new Vector2(0f, 20f), new Vector2(10f, 0f), 0.5f, -30f, 70f);
            Assert.AreEqual(5f, next.x, 1e-4f);
            Assert.AreEqual(20f, next.y, 1e-4f);
        }

        [Test]
        public void Step_MouseUp_DecreasesPitch_NonInverted()
        {
            // 마우스 위(+delta.y) = 시선 위 = 카메라가 내려감(pitch 감소)
            Vector2 next = CameraOrbitLogic.Step(new Vector2(0f, 20f), new Vector2(0f, 10f), 0.5f, -30f, 70f);
            Assert.AreEqual(15f, next.y, 1e-4f);
        }

        [Test]
        public void Step_PitchIsClamped()
        {
            Vector2 next = CameraOrbitLogic.Step(new Vector2(0f, -25f), new Vector2(0f, 100f), 0.5f, -30f, 70f);
            Assert.AreEqual(-30f, next.y, 1e-4f);
        }
    }
}
```

`Assets/Scripts/Cameras/CameraOrbitLogic.cs` (스텁 — 일부러 그대로 반환):

```csharp
using UnityEngine;

namespace CTC.Runtime.Cameras
{
    /// <summary>
    /// 궤도 카메라 각도 계산(클램프·랩·마우스 델타 적용).
    /// MonoBehaviour와 분리된 순수 함수라 EditMode 테스트가 가능하다.
    /// yaw 규약: Cinemachine OrbitalFollow HorizontalAxis와 동일한 [-180, 180).
    /// </summary>
    public static class CameraOrbitLogic
    {
        public static float ClampPitch(float pitchDeg, float minDeg, float maxDeg)
        {
            return pitchDeg; // 스텁
        }

        public static float WrapYaw(float yawDeg)
        {
            return yawDeg; // 스텁
        }

        /// <summary>current=(yaw,pitch)에 마우스 델타를 적용한 다음 각도를 반환. 비반전(마우스 위=시선 위).</summary>
        public static Vector2 Step(Vector2 current, Vector2 mouseDelta, float sensitivity, float minPitch, float maxPitch)
        {
            return current; // 스텁
        }
    }
}
```

- [ ] **Step 2: RED 확인**

Run: focus-unity → `u_console` 컴파일 에러 0 확인 → `u_editor_menu`로 `Tools/CTC/Run EditMode Tests` → `u_console`
Expected: `Failed: 7` (ClampPitch_WithinRange·WrapYaw_WithinRange만 우연히 통과)

- [ ] **Step 3: 구현**

```csharp
        public static float ClampPitch(float pitchDeg, float minDeg, float maxDeg)
        {
            return Mathf.Clamp(pitchDeg, minDeg, maxDeg);
        }

        public static float WrapYaw(float yawDeg)
        {
            return Mathf.Repeat(yawDeg + 180f, 360f) - 180f;
        }

        public static Vector2 Step(Vector2 current, Vector2 mouseDelta, float sensitivity, float minPitch, float maxPitch)
        {
            float yaw = WrapYaw(current.x + mouseDelta.x * sensitivity);
            float pitch = ClampPitch(current.y - mouseDelta.y * sensitivity, minPitch, maxPitch);
            return new Vector2(yaw, pitch);
        }
```

- [ ] **Step 4: GREEN 확인**

Run: focus-unity → `Tools/CTC/Run EditMode Tests` → `u_console`
Expected: `Failed: 0` (기존 MovementInput 4개 포함 Passed: 13)

- [ ] **Step 5: Commit**

```bash
git add CTC/Assets/Scripts/Cameras CTC/Assets/Scripts/Tests/EditMode/CameraOrbitLogicTests.cs*
git commit -m "feat: add CameraOrbitLogic with pitch clamp and yaw wrap (TDD)"
```

(`.meta` 파일도 함께 커밋 — 이하 모든 태스크 동일)

---

### Task 3: MovementInput 카메라 상대 변환 (TDD)

**Files:**
- Modify: `Assets/Scripts/Player/MovementInput.cs`
- Test: `Assets/Scripts/Tests/EditMode/MovementInputTests.cs` (테스트 추가)

- [ ] **Step 1: 테스트 + 스텁 추가**

`MovementInputTests.cs`에 테스트 추가:

```csharp
        [Test]
        public void ToWorldDirection_YawZero_SameAsToDirection()
        {
            var dir = MovementInput.ToWorldDirection(0f, 1f, 0f);
            Assert.AreEqual(0f, dir.x, 1e-4f);
            Assert.AreEqual(1f, dir.z, 1e-4f);
        }

        [Test]
        public void ToWorldDirection_Yaw90_ForwardBecomesPositiveX()
        {
            var dir = MovementInput.ToWorldDirection(0f, 1f, 90f);
            Assert.AreEqual(1f, dir.x, 1e-4f);
            Assert.AreEqual(0f, dir.z, 1e-4f);
        }

        [Test]
        public void ToWorldDirection_PreservesMagnitudeClamp()
        {
            var dir = MovementInput.ToWorldDirection(1f, 1f, 45f);
            Assert.AreEqual(1f, dir.magnitude, 1e-4f);
        }
```

`MovementInput.cs`에 스텁 추가:

```csharp
        /// <summary>카메라 yaw(도) 기준으로 회전된 월드 이동 방향을 반환한다.</summary>
        public static Vector3 ToWorldDirection(float horizontal, float vertical, float cameraYawDegrees)
        {
            return Vector3.zero; // 스텁
        }
```

- [ ] **Step 2: RED 확인**

Run: focus-unity → `Tools/CTC/Run EditMode Tests` → `u_console`
Expected: `Failed: 3`

- [ ] **Step 3: 구현**

```csharp
        public static Vector3 ToWorldDirection(float horizontal, float vertical, float cameraYawDegrees)
        {
            return Quaternion.Euler(0f, cameraYawDegrees, 0f) * ToDirection(horizontal, vertical);
        }
```

- [ ] **Step 4: GREEN 확인**

Expected: `Failed: 0, Passed: 16`

- [ ] **Step 5: Commit**

```bash
git add CTC/Assets/Scripts/Player/MovementInput.cs CTC/Assets/Scripts/Tests/EditMode/MovementInputTests.cs
git commit -m "feat: add camera-relative movement direction (TDD)"
```

---

### Task 4: AnimationStateLogic (TDD)

**Files:**
- Create: `Assets/Scripts/Player/AnimationStateLogic.cs`
- Test: `Assets/Scripts/Tests/EditMode/AnimationStateLogicTests.cs`

- [ ] **Step 1: 테스트 + 스텁 작성**

`Assets/Scripts/Tests/EditMode/AnimationStateLogicTests.cs`:

```csharp
using CTC.Runtime.Player;
using NUnit.Framework;

namespace CTC.Tests
{
    public class AnimationStateLogicTests
    {
        [Test]
        public void BelowWalkThreshold_ReturnsIdle()
        {
            Assert.AreEqual("Idle_A", AnimationStateLogic.FromSpeed(0.05f, 0.1f, 3.5f));
        }

        [Test]
        public void BetweenThresholds_ReturnsWalk()
        {
            Assert.AreEqual("Walk", AnimationStateLogic.FromSpeed(1.5f, 0.1f, 3.5f));
        }

        [Test]
        public void AtOrAboveRunThreshold_ReturnsRun()
        {
            Assert.AreEqual("Run", AnimationStateLogic.FromSpeed(3.5f, 0.1f, 3.5f));
        }

        [Test]
        public void ExactlyWalkThreshold_ReturnsWalk()
        {
            Assert.AreEqual("Walk", AnimationStateLogic.FromSpeed(0.1f, 0.1f, 3.5f));
        }
    }
}
```

`Assets/Scripts/Player/AnimationStateLogic.cs` (스텁):

```csharp
namespace CTC.Runtime.Player
{
    /// <summary>
    /// 수평 이동 속도를 Quirky Series 애니메이터 상태 이름으로 매핑한다.
    /// AC_Cat/AC_Mouse는 파라미터 없이 상태만 있어 CrossFade(상태명)로 구동한다.
    /// </summary>
    public static class AnimationStateLogic
    {
        public const string Idle = "Idle_A";
        public const string Walk = "Walk";
        public const string Run = "Run";

        public static string FromSpeed(float speed, float walkThreshold, float runThreshold)
        {
            return Idle; // 스텁
        }
    }
}
```

- [ ] **Step 2: RED 확인** — Expected: `Failed: 3`

- [ ] **Step 3: 구현**

```csharp
        public static string FromSpeed(float speed, float walkThreshold, float runThreshold)
        {
            if (speed >= runThreshold) return Run;
            if (speed >= walkThreshold) return Walk;
            return Idle;
        }
```

- [ ] **Step 4: GREEN 확인** — Expected: `Failed: 0, Passed: 20`

- [ ] **Step 5: Commit**

```bash
git add CTC/Assets/Scripts/Player/AnimationStateLogic.cs* CTC/Assets/Scripts/Tests/EditMode/AnimationStateLogicTests.cs*
git commit -m "feat: add speed-to-animation-state mapping (TDD)"
```

---

### Task 5: RoleId + LobbyRoleLogic (TDD)

**Files:**
- Create: `Assets/Scripts/Lobby/RoleId.cs`
- Create: `Assets/Scripts/Lobby/LobbyRoleLogic.cs`
- Test: `Assets/Scripts/Tests/EditMode/LobbyRoleLogicTests.cs`

- [ ] **Step 1: 테스트 + 스텁 작성**

`Assets/Scripts/Lobby/RoleId.cs`:

```csharp
namespace CTC.Runtime.Lobby
{
    public enum RoleId : byte
    {
        None = 0,
        Mouse = 1,
        Cat = 2
    }
}
```

`Assets/Scripts/Tests/EditMode/LobbyRoleLogicTests.cs`:

```csharp
using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests
{
    public class LobbyRoleLogicTests
    {
        [Test]
        public void CanSelect_Mouse_AlwaysAllowed()
        {
            var roles = new[] { RoleId.None, RoleId.Mouse };
            Assert.IsTrue(LobbyRoleLogic.CanSelect(roles, 0, RoleId.Mouse, 1));
        }

        [Test]
        public void CanSelect_Cat_WhenCapacityFree_Allowed()
        {
            var roles = new[] { RoleId.None, RoleId.Mouse };
            Assert.IsTrue(LobbyRoleLogic.CanSelect(roles, 0, RoleId.Cat, 1));
        }

        [Test]
        public void CanSelect_Cat_WhenAnotherIsCat_Denied()
        {
            var roles = new[] { RoleId.None, RoleId.Cat };
            Assert.IsFalse(LobbyRoleLogic.CanSelect(roles, 0, RoleId.Cat, 1));
        }

        [Test]
        public void CanSelect_Cat_ReselectBySameCat_Allowed()
        {
            // 자기 자신이 이미 고양이면 재선택은 정원을 소모하지 않는다
            var roles = new[] { RoleId.Cat, RoleId.Mouse };
            Assert.IsTrue(LobbyRoleLogic.CanSelect(roles, 0, RoleId.Cat, 1));
        }

        [Test]
        public void CanSelect_None_Denied()
        {
            var roles = new[] { RoleId.Mouse, RoleId.Mouse };
            Assert.IsFalse(LobbyRoleLogic.CanSelect(roles, 0, RoleId.None, 1));
        }

        [Test]
        public void CanSelect_InvalidIndex_Denied()
        {
            var roles = new[] { RoleId.None };
            Assert.IsFalse(LobbyRoleLogic.CanSelect(roles, 5, RoleId.Mouse, 1));
        }

        [Test]
        public void CanStart_OneCatOneMouse_True()
        {
            Assert.IsTrue(LobbyRoleLogic.CanStart(new[] { RoleId.Cat, RoleId.Mouse }));
        }

        [Test]
        public void CanStart_AnyoneUnselected_False()
        {
            Assert.IsFalse(LobbyRoleLogic.CanStart(new[] { RoleId.Cat, RoleId.None }));
        }

        [Test]
        public void CanStart_NoCat_False()
        {
            Assert.IsFalse(LobbyRoleLogic.CanStart(new[] { RoleId.Mouse, RoleId.Mouse }));
        }

        [Test]
        public void CanStart_NoMouse_False()
        {
            Assert.IsFalse(LobbyRoleLogic.CanStart(new[] { RoleId.Cat }));
        }

        [Test]
        public void CanStart_SinglePlayer_False()
        {
            Assert.IsFalse(LobbyRoleLogic.CanStart(new[] { RoleId.Cat }));
        }
    }
}
```

`Assets/Scripts/Lobby/LobbyRoleLogic.cs` (스텁):

```csharp
using System.Collections.Generic;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 로비 역할 선택·시작 가능 판정 규칙. NetworkBehaviour와 분리된 순수 함수(EditMode TDD).
    /// </summary>
    public static class LobbyRoleLogic
    {
        /// <summary>selfIndex 참가자가 requested 역할을 고를 수 있는가. 고양이는 정원(catCapacity) 검사.</summary>
        public static bool CanSelect(IReadOnlyList<RoleId> roles, int selfIndex, RoleId requested, int catCapacity)
        {
            return false; // 스텁
        }

        /// <summary>전원 역할 선택 완료 + 고양이·쥐 각 1 이상이어야 시작 가능.</summary>
        public static bool CanStart(IReadOnlyList<RoleId> roles)
        {
            return false; // 스텁
        }
    }
}
```

- [ ] **Step 2: RED 확인** — Expected: `Failed: 4` (Denied/False 계열 7개는 스텁이 우연히 통과)

- [ ] **Step 3: 구현**

```csharp
        public static bool CanSelect(IReadOnlyList<RoleId> roles, int selfIndex, RoleId requested, int catCapacity)
        {
            if (requested == RoleId.None) return false;
            if (selfIndex < 0 || selfIndex >= roles.Count) return false;
            if (requested != RoleId.Cat) return true;

            int otherCats = 0;
            for (int i = 0; i < roles.Count; i++)
            {
                if (i != selfIndex && roles[i] == RoleId.Cat) otherCats++;
            }
            return otherCats < catCapacity;
        }

        public static bool CanStart(IReadOnlyList<RoleId> roles)
        {
            if (roles.Count < 2) return false;

            int cats = 0, mice = 0;
            for (int i = 0; i < roles.Count; i++)
            {
                if (roles[i] == RoleId.None) return false;
                if (roles[i] == RoleId.Cat) cats++;
                else mice++;
            }
            return cats >= 1 && mice >= 1;
        }
```

- [ ] **Step 4: GREEN 확인** — Expected: `Failed: 0, Passed: 31`

- [ ] **Step 5: Commit**

```bash
git add CTC/Assets/Scripts/Lobby CTC/Assets/Scripts/Tests/EditMode/LobbyRoleLogicTests.cs*
git commit -m "feat: add lobby role selection and start rules (TDD)"
```

---

### Task 6: LobbyPlayerState + LobbyManager

**Files:**
- Create: `Assets/Scripts/Lobby/LobbyPlayerState.cs`
- Create: `Assets/Scripts/Lobby/LobbyManager.cs`

순수 로직이 아니어서(NGO 의존) EditMode 테스트 없음 — MPPM 검증(Task 13)으로 커버.

- [ ] **Step 1: LobbyPlayerState 작성**

`Assets/Scripts/Lobby/LobbyPlayerState.cs`:

```csharp
using System;
using Unity.Netcode;

namespace CTC.Runtime.Lobby
{
    /// <summary>로비 참가자 1명의 상태. NetworkList 요소 조건: INetworkSerializable + IEquatable.</summary>
    public struct LobbyPlayerState : INetworkSerializable, IEquatable<LobbyPlayerState>
    {
        public ulong ClientId;
        public RoleId Role;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Role);
        }

        public bool Equals(LobbyPlayerState other)
        {
            return ClientId == other.ClientId && Role == other.Role;
        }
    }
}
```

- [ ] **Step 2: LobbyManager 작성**

`Assets/Scripts/Lobby/LobbyManager.cs`:

```csharp
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 접속 후 인게임 전까지의 로비 상태를 소유한다(in-scene NetworkObject).
    /// 서버가 참가자 목록·역할을 NetworkList로 복제하고, 시작 시 역할별 프리팹을 스폰한다.
    /// NetworkManager.PlayerPrefab 자동 스폰은 꺼져 있어야 한다(SetupNetworkingScene이 보장).
    /// </summary>
    public class LobbyManager : NetworkBehaviour
    {
        public const int CatCapacity = 1;

        [SerializeField] private GameObject catPrefab;
        [SerializeField] private GameObject mousePrefab;

        private NetworkList<LobbyPlayerState> _players;
        private readonly NetworkVariable<bool> _gameStarted = new NetworkVariable<bool>();

        public bool GameStarted => _gameStarted.Value;
        public int PlayerCount => _players.Count;
        public LobbyPlayerState GetPlayer(int index) => _players[index];
        public bool CanStart => LobbyRoleLogic.CanStart(SnapshotRoles());

        private void Awake()
        {
            // NetworkList는 필드 초기화 대신 Awake에서 생성 (NGO 권장 — 도메인 리로드 누수 방지)
            _players = new NetworkList<LobbyPlayerState>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
            {
                EnsureEntry(id);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager == null) return;
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void OnClientConnected(ulong clientId)
        {
            EnsureEntry(clientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            int idx = IndexOf(clientId);
            if (idx >= 0) _players.RemoveAt(idx);
        }

        private void EnsureEntry(ulong clientId)
        {
            if (IndexOf(clientId) < 0)
            {
                _players.Add(new LobbyPlayerState { ClientId = clientId, Role = RoleId.None });
            }
        }

        private int IndexOf(ulong clientId)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == clientId) return i;
            }
            return -1;
        }

        private List<RoleId> SnapshotRoles()
        {
            var roles = new List<RoleId>(_players.Count);
            for (int i = 0; i < _players.Count; i++) roles.Add(_players[i].Role);
            return roles;
        }

        /// <summary>로컬 클라이언트의 역할 선택 요청(UI에서 호출).</summary>
        public void SelectRole(RoleId role)
        {
            SelectRoleRpc(role);
        }

        [Rpc(SendTo.Server)]
        private void SelectRoleRpc(RoleId role, RpcParams rpcParams = default)
        {
            if (_gameStarted.Value) return;

            ulong sender = rpcParams.Receive.SenderClientId;
            int idx = IndexOf(sender);
            if (!LobbyRoleLogic.CanSelect(SnapshotRoles(), idx, role, CatCapacity)) return;

            LobbyPlayerState st = _players[idx];
            st.Role = role;
            _players[idx] = st;
        }

        /// <summary>호스트 UI에서 호출. 서버에서만 동작.</summary>
        public void RequestStartGame()
        {
            if (!IsServer || _gameStarted.Value) return;
            if (!LobbyRoleLogic.CanStart(SnapshotRoles())) return;

            _gameStarted.Value = true;
            SpawnPlayers();
        }

        private void SpawnPlayers()
        {
            for (int i = 0; i < _players.Count; i++)
            {
                LobbyPlayerState p = _players[i];
                GameObject prefab = p.Role == RoleId.Cat ? catPrefab : mousePrefab;
                Vector3 pos = new Vector3(i * 2f - (_players.Count - 1), 0.1f, 0f);
                GameObject go = Instantiate(prefab, pos, Quaternion.identity);
                go.GetComponent<NetworkObject>().SpawnAsPlayerObject(p.ClientId);
            }
        }

        // ---- MCP/QA 자동화용 진입점 (u_play invoke, 클론 커맨드에서 사용) ----
        public void DevSelectMouse() => SelectRole(RoleId.Mouse);
        public void DevSelectCat() => SelectRole(RoleId.Cat);
        public void DevStartGame() => RequestStartGame();
    }
}
```

- [ ] **Step 3: 컴파일 확인**

Run: focus-unity → `u_console`
Expected: 에러 0

- [ ] **Step 4: Commit**

```bash
git add CTC/Assets/Scripts/Lobby
git commit -m "feat: add LobbyManager with networked role selection and role-based spawning"
```

---

### Task 7: LobbyUI (IMGUI) + 커서 관리

**Files:**
- Create: `Assets/Scripts/Lobby/LobbyUI.cs`

기존 `ConnectionUI`와 같은 IMGUI 패턴(개발용 UI — 정식 UI는 Plan 8). 스펙의 "UGUI 오버레이"는 IMGUI로 대체한다(같은 씬 오버레이라는 본질 동일, 씬 배선 최소화).

- [ ] **Step 1: LobbyUI 작성**

`Assets/Scripts/Lobby/LobbyUI.cs`:

```csharp
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 개발용 로비 UI(IMGUI, ConnectionUI 패턴). 접속 후~시작 전까지 역할 선택/시작 버튼을 그린다.
    /// 커서: 로비 중 해제, 게임 시작 시 잠금(궤도 카메라가 잠금 상태에서만 회전).
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private LobbyManager lobby;

        private void Update()
        {
            if (lobby == null || !lobby.IsSpawned) return;

            if (lobby.GameStarted)
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            else if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void OnGUI()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || (!nm.IsClient && !nm.IsServer)) return; // 접속 전엔 ConnectionUI 담당
            if (lobby == null || !lobby.IsSpawned || lobby.GameStarted) return;

            GUILayout.BeginArea(new Rect(220, 10, 280, 320), GUI.skin.box);
            GUILayout.Label("로비 — 역할 선택");

            for (int i = 0; i < lobby.PlayerCount; i++)
            {
                LobbyPlayerState p = lobby.GetPlayer(i);
                string self = p.ClientId == nm.LocalClientId ? " (나)" : "";
                GUILayout.Label($"Client {p.ClientId}{self}: {p.Role}");
            }

            GUILayout.Space(8);
            if (GUILayout.Button("쥐로 플레이")) lobby.SelectRole(RoleId.Mouse);
            if (GUILayout.Button("고양이로 플레이")) lobby.SelectRole(RoleId.Cat);

            if (nm.IsHost)
            {
                GUILayout.Space(8);
                GUI.enabled = lobby.CanStart;
                if (GUILayout.Button("게임 시작")) lobby.RequestStartGame();
                GUI.enabled = true;
            }

            GUILayout.EndArea();
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인** — focus-unity → `u_console` 에러 0

- [ ] **Step 3: Commit**

```bash
git add CTC/Assets/Scripts/Lobby/LobbyUI.cs*
git commit -m "feat: add dev lobby UI with role buttons and cursor management"
```

---

### Task 8: OrbitCameraController + PlayerCameraBinder

**Files:**
- Create: `Assets/Scripts/Cameras/OrbitCameraController.cs`
- Create: `Assets/Scripts/Cameras/PlayerCameraBinder.cs`

- [ ] **Step 1: OrbitCameraController 작성**

`Assets/Scripts/Cameras/OrbitCameraController.cs`:

```csharp
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Cameras
{
    /// <summary>
    /// 마우스 델타로 CinemachineOrbitalFollow 축을 구동하는 자유 궤도 카메라.
    /// 리센터 없음 — 몸과 무관하게 항상 사방을 훔쳐볼 수 있다(스텔스 핵심).
    /// 커서가 잠긴 동안만 회전(로비에선 UI 조작을 위해 커서 해제 상태).
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera), typeof(CinemachineOrbitalFollow))]
    public class OrbitCameraController : MonoBehaviour
    {
        [SerializeField] private float sensitivity = 0.15f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 70f;

        private CinemachineCamera _cam;
        private CinemachineOrbitalFollow _orbital;
        private Vector2 _angles = new Vector2(0f, 20f); // (yaw, pitch)

        private void Awake()
        {
            _cam = GetComponent<CinemachineCamera>();
            _orbital = GetComponent<CinemachineOrbitalFollow>();
        }

        /// <summary>로컬 플레이어 스폰 시 호출 — 추적 대상과 역할별 렌즈를 적용한다.</summary>
        public void Bind(Transform target, float fov, float distance)
        {
            _cam.Follow = target;
            _cam.LookAt = target;

            LensSettings lens = _cam.Lens;
            lens.FieldOfView = fov;
            _cam.Lens = lens;

            _orbital.Radius = distance;
        }

        private void Update()
        {
            if (_cam.Follow == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            _angles = CameraOrbitLogic.Step(_angles, mouse.delta.ReadValue(), sensitivity, minPitch, maxPitch);
            _orbital.HorizontalAxis.Value = _angles.x;
            _orbital.VerticalAxis.Value = _angles.y;
        }
    }
}
```

- [ ] **Step 2: PlayerCameraBinder 작성**

`Assets/Scripts/Cameras/PlayerCameraBinder.cs`:

```csharp
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Cameras
{
    /// <summary>플레이어 프리팹에 붙어, 로컬 소유자 스폰 시 씬의 궤도 카메라를 자신에게 바인딩한다.</summary>
    public class PlayerCameraBinder : NetworkBehaviour
    {
        [SerializeField] private float cameraFov = 60f;
        [SerializeField] private float cameraDistance = 4f;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            var controller = FindFirstObjectByType<OrbitCameraController>();
            if (controller == null)
            {
                Debug.LogWarning("[PlayerCameraBinder] 씬에 OrbitCameraController가 없다");
                return;
            }
            controller.Bind(transform, cameraFov, cameraDistance);
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인** — focus-unity → `u_console` 에러 0

- [ ] **Step 4: Commit**

```bash
git add CTC/Assets/Scripts/Cameras
git commit -m "feat: add orbit camera controller and per-role camera binding"
```

---

### Task 9: PlayerMovement 카메라 상대 이동 + 몸 회전

**Files:**
- Modify: `Assets/Scripts/Player/PlayerMovement.cs`

- [ ] **Step 1: PlayerMovement 전체 교체**

```csharp
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 소유 클라이언트에서만 입력을 읽어 CharacterController로 이동한다.
    /// 이동은 카메라 yaw 기준(camera-relative). 몸은 이동 입력이 있을 때만
    /// 이동 방향으로 회전한다 — 정지 시 카메라만 도는 '훔쳐보기' 성립.
    /// 위치 동기화는 ClientNetworkTransform(소유자 권위)이 담당한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float turnSpeedDegPerSec = 720f;

        private CharacterController _controller;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (!IsOwner) return;

            // 프로젝트가 신형 Input System 전용(activeInputHandler=1)이라 레거시 Input 사용 불가
            var kb = Keyboard.current;
            if (kb == null) return;

            float h = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                    - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float v = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                    - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);

            float camYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;
            Vector3 dir = MovementInput.ToWorldDirection(h, v, camYaw);

            _controller.Move(dir * (moveSpeed * Time.deltaTime));

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, look, turnSpeedDegPerSec * Time.deltaTime);
            }
        }
    }
}
```

- [ ] **Step 2: 컴파일 + 기존 테스트 회귀 확인**

Run: focus-unity → `Tools/CTC/Run EditMode Tests` → `u_console`
Expected: `Failed: 0, Passed: 31`

- [ ] **Step 3: Commit**

```bash
git add CTC/Assets/Scripts/Player/PlayerMovement.cs
git commit -m "feat: camera-relative movement with move-direction body rotation"
```

---

### Task 10: PlayerAnimationDriver

**Files:**
- Create: `Assets/Scripts/Player/PlayerAnimationDriver.cs`

- [ ] **Step 1: 작성**

```csharp
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 프레임 간 위치 변화로 수평 속도를 추정해 Idle/Walk/Run을 CrossFade한다.
    /// 소유자·원격 모두 동일 로직으로 돌므로 애니메이션 네트워크 동기화가 필요 없다.
    /// (원격은 NetworkTransform 보간 위치를 그대로 사용 — 스무딩으로 지터 흡수)
    /// </summary>
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] private float walkThreshold = 0.1f;
        [SerializeField] private float runThreshold = 3.5f;
        [SerializeField] private float crossFadeSeconds = 0.15f;
        [SerializeField] private float speedSmoothing = 0.25f;

        private Animator _animator;
        private Vector3 _lastPosition;
        private float _smoothedSpeed;
        private string _currentState = "";

        private void Start()
        {
            _animator = GetComponentInChildren<Animator>();
            _lastPosition = transform.position;
            if (_animator == null)
                Debug.LogWarning($"[PlayerAnimationDriver] {name}: 자식에 Animator가 없다");
        }

        private void Update()
        {
            if (_animator == null || Time.deltaTime <= 0f) return;

            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            delta.y = 0f;

            float rawSpeed = delta.magnitude / Time.deltaTime;
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed, speedSmoothing);

            string state = AnimationStateLogic.FromSpeed(_smoothedSpeed, walkThreshold, runThreshold);
            if (state == _currentState) return;

            _currentState = state;
            _animator.CrossFadeInFixedTime(state, crossFadeSeconds);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인** — focus-unity → `u_console` 에러 0

- [ ] **Step 3: Commit**

```bash
git add CTC/Assets/Scripts/Player/PlayerAnimationDriver.cs*
git commit -m "feat: drive Quirky animator states from estimated movement speed"
```

---

### Task 11: MultiplayerQaProbe 확장 (클론 역할 선택 + 프리팹 이름 노출)

**Files:**
- Modify: `Assets/Scripts/Network/MultiplayerQaProbe.cs`

- [ ] **Step 1: using 추가**

파일 상단 using에 추가:

```csharp
using CTC.Runtime.Lobby;
```

- [ ] **Step 2: 클론 커맨드 추가**

`ExecuteCloneCommand`의 switch에 케이스 추가:

```csharp
                case "TestMpSelectRoleMouse":
                    SelectRole(RoleId.Mouse);
                    break;
                case "TestMpSelectRoleCat":
                    SelectRole(RoleId.Cat);
                    break;
```

클래스에 메서드 추가 (`NudgeLocalPlayer` 아래):

```csharp
        // 클론 로컬에서 로비 역할 선택 — MPPM 로비 자동화용.
        private void SelectRole(RoleId role)
        {
            var lobby = FindFirstObjectByType<LobbyManager>();
            if (lobby == null || !lobby.IsSpawned)
            {
                Debug.LogWarning("[MultiplayerQaProbe] SelectRole — LobbyManager 없음/미스폰");
                return;
            }
            lobby.SelectRole(role);
            Debug.Log($"[MultiplayerQaProbe] SelectRole({role}) 요청");
        }
```

- [ ] **Step 3: 스냅샷에 프리팹 이름 노출**

`ProbeObject`에 필드 추가:

```csharp
        public string objectName;
```

`BuildSnapshot`의 `s.objects.Add(new ProbeObject { ... })`에 추가:

```csharp
                        objectName = no.gameObject.name,
```

- [ ] **Step 4: 컴파일 확인** — focus-unity → `u_console` 에러 0

- [ ] **Step 5: Commit**

```bash
git add CTC/Assets/Scripts/Network/MultiplayerQaProbe.cs
git commit -m "feat: add lobby role-select clone commands and object names to QA probe"
```

---

### Task 12: SetupNetworkingScene 재작성 (URP 머티리얼 + 프리팹 2종 + 씬 배선)

**Files:**
- Modify: `Assets/Scripts/Editor/SetupNetworkingScene.cs` (전체 교체)

- [ ] **Step 1: 전체 교체**

```csharp
using System.Linq;
using CTC.Runtime.Cameras;
using CTC.Runtime.Lobby;
using CTC.Runtime.Networking;
using CTC.Runtime.Player;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// Plan 3: PlayerCat/PlayerMouse 프리팹(Quirky 모델 + URP 머티리얼)과
    /// 로비·카메라가 배선된 Game 씬을 생성한다. 재실행 시 덮어씀.
    /// Tools > CTC > Setup Networking Scene
    /// </summary>
    public static class SetupNetworkingScene
    {
        private const string PrefabDir = "Assets/Prefabs";
        private const string MaterialsDir = "Assets/Materials";
        private const string CatPrefabPath = PrefabDir + "/PlayerCat.prefab";
        private const string MousePrefabPath = PrefabDir + "/PlayerMouse.prefab";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        private const string QuirkyRoot = "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1";
        private const string CatModelPath = QuirkyRoot + "/Prefabs/Cat.prefab";
        private const string MouseModelPath = QuirkyRoot + "/Prefabs/Mouse.prefab";
        private const string CatSourceMatPath = QuirkyRoot + "/Materials/M_Cat.mat";
        private const string MouseSourceMatPath = QuirkyRoot + "/Materials/M_Mouse.mat";
        private const string UrpShaderPath =
            "Assets/Quirky Series/_Shader/URP (Unity 2019.3.0f3 and above)/SoftSurfaceGraph.shadergraph";

        [MenuItem("Tools/CTC/Setup Networking Scene")]
        public static void Run()
        {
            Material catMat = CreateUrpMaterial("M_Cat_URP", CatSourceMatPath);
            Material mouseMat = CreateUrpMaterial("M_Mouse_URP", MouseSourceMatPath);

            GameObject catPrefab = CreatePlayerPrefab(
                "PlayerCat", CatPrefabPath, CatModelPath, catMat,
                ccHeight: 0.9f, ccRadius: 0.35f,
                moveSpeed: 4.5f, runThreshold: 3.2f, fov: 70f, camDistance: 5f);

            GameObject mousePrefab = CreatePlayerPrefab(
                "PlayerMouse", MousePrefabPath, MouseModelPath, mouseMat,
                ccHeight: 0.5f, ccRadius: 0.22f,
                moveSpeed: 6f, runThreshold: 4.2f, fov: 55f, camDistance: 3f);

            CreateGameScene(catPrefab, mousePrefab);
            Debug.Log("[SetupNetworkingScene] 완료 — PlayerCat/PlayerMouse 프리팹 + Game.unity 재생성");
        }

        // Quirky 원본 머티리얼의 색/텍스처를 URP SoftSurfaceGraph 머티리얼로 복사한다.
        // (원본은 빌트인 CGPROGRAM 셰이더라 URP에서 마젠타)
        private static Material CreateUrpMaterial(string name, string sourceMatPath)
        {
            if (!AssetDatabase.IsValidFolder(MaterialsDir))
                AssetDatabase.CreateFolder("Assets", "Materials");

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(UrpShaderPath);
            var source = AssetDatabase.LoadAssetAtPath<Material>(sourceMatPath);

            var mat = new Material(shader) { name = name };
            if (source != null)
            {
                if (source.HasProperty("_Color")) mat.SetColor("_Color", source.GetColor("_Color"));
                if (source.HasProperty("_MainTex")) mat.SetTexture("_MainTex", source.GetTexture("_MainTex"));
                if (source.HasProperty("_Emission")) mat.SetFloat("_Emission", source.GetFloat("_Emission"));
            }

            string path = MaterialsDir + "/" + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject CreatePlayerPrefab(
            string name, string prefabPath, string modelPath, Material material,
            float ccHeight, float ccRadius,
            float moveSpeed, float runThreshold, float fov, float camDistance)
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            var root = new GameObject(name);
            try
            {
                var cc = root.AddComponent<CharacterController>();
                cc.height = ccHeight;
                cc.radius = ccRadius;
                cc.center = new Vector3(0f, ccHeight * 0.5f + 0.05f, 0f);

                root.AddComponent<NetworkObject>();
                root.AddComponent<ClientNetworkTransform>();

                var move = root.AddComponent<PlayerMovement>();
                SetFloat(move, "moveSpeed", moveSpeed);

                var anim = root.AddComponent<PlayerAnimationDriver>();
                SetFloat(anim, "runThreshold", runThreshold);

                var binder = root.AddComponent<PlayerCameraBinder>();
                SetFloat(binder, "cameraFov", fov);
                SetFloat(binder, "cameraDistance", camDistance);

                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;

                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateGameScene(GameObject catPrefab, GameObject mousePrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(3f, 1f, 3f);

            // NetworkManager — 자동 플레이어 스폰 없음(로비가 역할별로 스폰)
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7788);

            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = null;
            nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = catPrefab });
            nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = mousePrefab });

            nmGo.AddComponent<ConnectionUI>();
            nmGo.AddComponent<MppmAutoConnect>();
            nmGo.AddComponent<MultiplayerQaProbe>();

            // 로비 (in-scene NetworkObject — 서버 시작 시 자동 스폰)
            var lobbyGo = new GameObject("LobbyManager");
            lobbyGo.AddComponent<NetworkObject>();
            var lobby = lobbyGo.AddComponent<LobbyManager>();
            SetRef(lobby, "catPrefab", catPrefab);
            SetRef(lobby, "mousePrefab", mousePrefab);
            var lobbyUi = lobbyGo.AddComponent<LobbyUI>();
            SetRef(lobbyUi, "lobby", lobby);

            // 카메라 — Main Camera에 Brain, 별도 오브젝트에 궤도 vcam
            var mainCam = Camera.main;
            if (mainCam != null) mainCam.gameObject.AddComponent<CinemachineBrain>();

            var camGo = new GameObject("OrbitCamera");
            camGo.AddComponent<CinemachineCamera>();
            var orbital = camGo.AddComponent<CinemachineOrbitalFollow>();
            orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbital.Radius = 4f;
            orbital.TargetOffset = new Vector3(0f, 0.6f, 0f);
            orbital.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbital.HorizontalAxis.Wrap = true;
            orbital.VerticalAxis.Range = new Vector2(-30f, 70f);
            orbital.VerticalAxis.Value = 20f;
            camGo.AddComponent<CinemachineRotationComposer>();
            camGo.AddComponent<OrbitCameraController>();

            EditorSceneManager.SaveScene(scene, GameScenePath);

            if (!EditorBuildSettings.scenes.Any(s => s.path == GameScenePath))
            {
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Append(new EditorBuildSettingsScene(GameScenePath, true))
                    .ToArray();
            }
        }

        // 직렬화 private 필드 세팅 유틸 (프리팹 저장 전 인스턴스에 적용)
        private static void SetFloat(Component c, string field, float value)
        {
            var so = new SerializedObject(c);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(Component c, string field, Object value)
        {
            var so = new SerializedObject(c);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인** — focus-unity → `u_console` 에러 0

- [ ] **Step 3: 씬 재생성 실행**

Run: MCP `u_editor_menu`로 `Tools/CTC/Setup Networking Scene` 실행 → `u_console`
Expected: `[SetupNetworkingScene] 완료 — PlayerCat/PlayerMouse 프리팹 + Game.unity 재생성`, 에러/경고 확인

- [ ] **Step 4: 구 Player.prefab 삭제**

Run (MCP `u_editor_asset` 또는 git): `Assets/Prefabs/Player.prefab` + `.meta` 삭제
Expected: 콘솔에 참조 깨짐 에러 없음 (씬은 방금 재생성됨)

- [ ] **Step 5: Commit**

```bash
git add -A CTC/Assets/Prefabs CTC/Assets/Materials CTC/Assets/Scenes CTC/Assets/Scripts/Editor/SetupNetworkingScene.cs CTC/ProjectSettings
git commit -m "feat: regen scene with lobby, orbit camera, and cat/mouse player prefabs"
```

---

### Task 13: 전체 EditMode 테스트 + MPPM 2인 검증

이 태스크는 **메인 세션(MCP 보유)이 직접 수행**한다.

- [ ] **Step 1: EditMode 전체**

`Tools/CTC/Run EditMode Tests` → Expected: `Failed: 0, Passed: 31`

- [ ] **Step 2: 플레이 진입 + 호스트 시작**

`u_play`로 플레이 모드 진입 → `u_play invoke NetworkManager.StartHost`
Expected: 클론(MPPM Player 2)이 자동 접속 (클론이 구버전 코드면 사용자에게 Player 2 토글 요청)

- [ ] **Step 3: 로비 역할 선택**

- 호스트: `u_play invoke LobbyManager.DevSelectCat`
- 클론: 시스템 temp `ctc-mppm-qa/cmd_clone.json`에 `{"seq":N,"method":"TestMpSelectRoleMouse"}` (seq 단조 증가)
- `mppm_collect`로 양측 로비 상태 확인(스크린샷 병용)

- [ ] **Step 4: 게임 시작 + 스폰 확인**

`u_play invoke LobbyManager.DevStartGame` → `mppm_collect`
Expected: host/clone 양측 `connected:true`, objects에 `PlayerCat(Clone)` 1 + `PlayerMouse(Clone)` 1, 소유자 각각 호스트/클론

- [ ] **Step 5: 이동·동기화·카메라 확인**

- 클론 이동: `cmd_clone.json`에 `{"seq":N+1,"method":"TestMpNudgeLocalPlayer"}` → `mppm_collect`로 양측 위치 일치 확인
- `u_screenshot` — 3인칭 카메라 시점, 모델 마젠타 아님, 애니메이션 재생 육안 확인

- [ ] **Step 6: 핸드오프/문서 갱신 + 최종 커밋**

`.claude/handoff-active.md`를 완료 상태로 갱신(또는 삭제), 로드맵 문서의 Plan 3 행에 완료 표기.

```bash
git add -A
git commit -m "docs: mark Plan 3 player-camera-lobby complete"
```

---

## 변경 이력 (실행 중 수정)

- **Cinemachine 3.1.4 → 3.1.7**: 3.1.4의 `CinemachineStoryboard`가 Unity 6000.5에서 CS0619(obsolete-as-error)로 컴파일 불가. 본문의 "3.1.4"는 3.1.7로 읽을 것.
- **Task 12에 그레이박스 맵 추가** (사용자 요청): 씬 생성 시 평면 바닥만이 아니라 부엌 블록아웃(외벽 4 + 카운터·아일랜드·가전 블록)을 프리미티브 큐브 + 회색 URP Lit 머티리얼로 배치한다. Plan 9(아트 패스) 전까지의 플레이테스트용 지형.
- **로비 UI는 IMGUI** (Task 7에 명시) — 스펙의 "UGUI 오버레이" 표현을 대체. → **재변경(사용자 지시): Modern UI Pack(UGUI)으로 재작성** (`Assets/Modern UI Pack/`). EventSystem은 InputSystemUIInputModule 필수(레거시 Input 금지).
- **그레이박스 머티리얼**: 자체 생성 M_Greybox 대신 **Gridbox Prototype Materials** 사용 (`Assets/Thirdparty/Ciathyza/Gridbox Prototype Materials/Materials/URP/`, 사용자 지시).

## 검증 요약 (완료 기준)

| # | 확인 | 기대 |
|---|------|------|
| 1 | EditMode 테스트 | `Passed: 31, Failed: 0` |
| 2 | MPPM 2인 로비 | 양측 UI에 2명 표시, 역할 선택 반영 |
| 3 | 고양이 정원 | 한쪽이 Cat이면 다른 쪽 Cat 선택 거부 |
| 4 | 시작 후 스폰 | PlayerCat 1 + PlayerMouse 1, 위치 동기화 |
| 5 | 카메라 | 마우스 궤도 회전, 정지 중 몸 고정(훔쳐보기), 역할별 FOV/거리 차등 |
| 6 | 비주얼 | URP 머티리얼 정상(마젠타 X), Idle/Walk/Run 전환 |
