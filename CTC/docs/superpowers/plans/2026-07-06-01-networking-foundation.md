# 네트워킹 기반 (Plan 1) 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** NGO(Netcode for GameObjects)를 설치하고, 로컬호스트로 2인이 접속해 네트워크 동기화되는 플레이어 이동을 Multiplayer Play Mode에서 확인한다.

**Architecture:** 순수 이동 로직은 `MovementInput` 정적 클래스로 분리해 EditMode 테스트로 검증한다. 네트워크 이동은 `PlayerMovement`(NetworkBehaviour)가 소유자 입력을 받아 `ClientNetworkTransform`(소유자 권위)로 동기화한다. `NetworkManager` + `UnityTransport`(로컬호스트)로 호스트/클라이언트를 붙이고, 간단한 Host/Client 버튼 UI로 접속을 시작한다. (Steam 트랜스포트·서버권위 이동 검증은 후속 계획.)

**Tech Stack:** Unity 6 (URP 17.5), Netcode for GameObjects 2.x, Unity Transport, Multiplayer Play Mode, Unity Test Framework, Unity Input System(기존).

---

## 파일 구조

계획 1에서 만들거나 건드리는 파일:

- Create: `Assets/_Project/Scripts/Runtime/CTC.Runtime.asmdef` — 런타임 코드 어셈블리(NGO 참조)
- Create: `Assets/_Project/Scripts/Runtime/Player/MovementInput.cs` — 순수 입력→방향 변환 로직
- Create: `Assets/_Project/Scripts/Runtime/Player/PlayerMovement.cs` — NetworkBehaviour, 소유자 이동
- Create: `Assets/_Project/Scripts/Runtime/Networking/ConnectionUI.cs` — Host/Client 접속 버튼
- Create: `Assets/_Project/Tests/EditMode/CTC.Tests.EditMode.asmdef` — EditMode 테스트 어셈블리
- Create: `Assets/_Project/Tests/EditMode/MovementInputTests.cs` — MovementInput 테스트
- Create: `Assets/_Project/Prefabs/Player.prefab` — 네트워크 플레이어 프리팹(에디터에서 구성)
- Create: `Assets/_Project/Scenes/Game.unity` — 네트워킹 테스트 씬
- Modify: `Packages/manifest.json` — NGO / Transport / MPPM 패키지 추가

> 모든 게임 코드는 `Assets/_Project/` 아래에 둔다. 기존 `Assets/TutorialInfo`, `Assets/Readme.asset`는 Unity 템플릿 잔재이며 이 계획에서 건드리지 않는다(나중 정리).

---

## Task 1: 패키지 설치 (NGO / Transport / MPPM)

**Files:**
- Modify: `Packages/manifest.json`

- [ ] **Step 1: manifest.json에 패키지 추가**

`Packages/manifest.json`의 `"dependencies"` 블록에 아래 3줄을 추가한다(알파벳 순서 위치, 예: `com.unity.multiplayer.center` 아래·`com.unity.render-pipelines...` 위):

```json
    "com.unity.multiplayer.playmode": "1.4.0",
    "com.unity.netcode.gameobjects": "2.12.0",
```

> **버전 주의(중요):** Unity 6000.5에서는 NGO **2.7.0 이상**이어야 한다. 2.0.x는 폐기 API(GetEntityId·SceneHandle.GetRawData)를 써서 CS0619 컴파일 에러(패키지 코드라 억제 불가)가 난다. 최신 2.x(작성 시점 2.12.0)를 쓴다.
> `com.unity.transport`는 NGO의 의존성으로 **자동 해결**되므로 manifest에 따로 명시하지 않는다. PM이 제안하는 최신 2.x가 2.12.0과 다르면 그 번호로 맞춘다.

- [ ] **Step 2: Unity가 패키지를 복원하도록 에디터를 포커스**

Unity 에디터를 활성화하면 자동으로 패키지를 복원한다. (에디터가 닫혀 있으면 열기.)
Expected: 콘솔에 컴파일 에러 없음. `Window > Package Manager > In Project`에 "Netcode for GameObjects", "Multiplayer Play Mode", "Unity Transport"가 보인다.

- [ ] **Step 3: 커밋**

```bash
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "chore: add NGO, Unity Transport, Multiplayer Play Mode packages"
```

---

## Task 2: 프로젝트 폴더 & 런타임 어셈블리 정의

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/CTC.Runtime.asmdef`

- [ ] **Step 1: 폴더 생성**

아래 폴더를 만든다(빈 폴더는 Unity에서 `.meta`가 생성됨):
- `Assets/_Project/Scripts/Runtime/Player/`
- `Assets/_Project/Scripts/Runtime/Networking/`
- `Assets/_Project/Tests/EditMode/`
- `Assets/_Project/Prefabs/`
- `Assets/_Project/Scenes/`

- [ ] **Step 2: 런타임 asmdef 작성**

`Assets/_Project/Scripts/Runtime/CTC.Runtime.asmdef`:

```json
{
    "name": "CTC.Runtime",
    "rootNamespace": "CTC.Runtime",
    "references": [
        "Unity.Netcode.Runtime",
        "Unity.Netcode.Components",
        "Unity.Networking.Transport"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: 컴파일 확인**

Unity 에디터로 포커스 후 콘솔 확인.
Expected: 컴파일 에러 없음(어셈블리 참조가 유효).

- [ ] **Step 4: 커밋**

```bash
git add Assets/_Project
git commit -m "chore: scaffold _Project folders and CTC.Runtime asmdef"
```

---

## Task 3: 순수 이동 로직 `MovementInput` (TDD)

**Files:**
- Create: `Assets/_Project/Tests/EditMode/CTC.Tests.EditMode.asmdef`
- Create: `Assets/_Project/Tests/EditMode/MovementInputTests.cs`
- Create: `Assets/_Project/Scripts/Runtime/Player/MovementInput.cs`

- [ ] **Step 1: EditMode 테스트 asmdef 작성**

`Assets/_Project/Tests/EditMode/CTC.Tests.EditMode.asmdef`:

```json
{
    "name": "CTC.Tests.EditMode",
    "rootNamespace": "CTC.Tests",
    "references": [
        "CTC.Runtime",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [ "Editor" ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [ "nunit.framework.dll" ],
    "autoReferenced": false,
    "defineConstraints": [ "UNITY_INCLUDE_TESTS" ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: 실패하는 테스트 작성**

`Assets/_Project/Tests/EditMode/MovementInputTests.cs`:

```csharp
using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class MovementInputTests
    {
        [Test]
        public void ZeroInput_ReturnsZeroDirection()
        {
            Assert.AreEqual(Vector3.zero, MovementInput.ToDirection(0f, 0f));
        }

        [Test]
        public void ForwardInput_MapsToPositiveZ()
        {
            var dir = MovementInput.ToDirection(0f, 1f);
            Assert.AreEqual(new Vector3(0f, 0f, 1f), dir);
        }

        [Test]
        public void DiagonalInput_IsClampedToUnitLength()
        {
            var dir = MovementInput.ToDirection(1f, 1f);
            Assert.AreEqual(1f, dir.magnitude, 1e-4f);
        }

        [Test]
        public void PartialInput_BelowUnit_IsNotScaledUp()
        {
            var dir = MovementInput.ToDirection(0.5f, 0f);
            Assert.AreEqual(new Vector3(0.5f, 0f, 0f), dir);
        }
    }
}
```

- [ ] **Step 3: 테스트가 실패하는지 확인**

Test Runner 창(`Window > General > Test Runner` > EditMode > Run All), 또는 CLI:

```bash
"C:/Program Files/Unity/Hub/Editor/<version>/Editor/Unity.exe" -batchmode -runTests -testPlatform EditMode -projectPath "c:/GitFork/The-Cheese-Thief-and-the-Cat/CTC" -testResults "c:/GitFork/The-Cheese-Thief-and-the-Cat/CTC/Temp/editmode-results.xml" -logFile -
```

Expected: 컴파일 에러 "The name 'MovementInput' does not exist" 로 실패(아직 미구현).

- [ ] **Step 4: 최소 구현 작성**

`Assets/_Project/Scripts/Runtime/Player/MovementInput.cs`:

```csharp
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 원시 입력(수평/수직 축)을 평면(XZ) 이동 방향으로 변환한다.
    /// 대각선 입력이 1을 넘으면 단위 길이로 정규화해 대각 가속을 막는다.
    /// MonoBehaviour와 분리된 순수 함수라 EditMode 테스트가 가능하다.
    /// </summary>
    public static class MovementInput
    {
        public static Vector3 ToDirection(float horizontal, float vertical)
        {
            var v = new Vector3(horizontal, 0f, vertical);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }
    }
}
```

- [ ] **Step 5: 테스트 통과 확인**

Test Runner에서 Run All (또는 Step 3의 CLI 재실행).
Expected: `MovementInputTests`의 4개 테스트 모두 PASS.

- [ ] **Step 6: 커밋**

```bash
git add Assets/_Project/Scripts/Runtime/Player/MovementInput.cs Assets/_Project/Tests
git commit -m "feat: add MovementInput direction logic with EditMode tests"
```

---

## Task 4: `PlayerMovement` NetworkBehaviour

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Player/PlayerMovement.cs`

- [ ] **Step 1: PlayerMovement 작성**

`Assets/_Project/Scripts/Runtime/Player/PlayerMovement.cs`:

```csharp
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 소유 클라이언트에서만 입력을 읽어 CharacterController로 이동한다.
    /// 위치 동기화는 ClientNetworkTransform(소유자 권위)이 담당한다.
    /// 서버권위 이동/예측은 후속 계획에서 강화한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;

        private CharacterController _controller;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (!IsOwner) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 dir = MovementInput.ToDirection(h, v);

            _controller.Move(dir * (moveSpeed * Time.deltaTime));
        }
    }
}
```

> `Input.GetAxisRaw`는 기존 Input Manager 축을 사용한다(프로젝트에 Horizontal/Vertical 축이 기본 존재). 새 Input System으로의 이관은 Plan 3(플레이어 & 카메라)에서 다룬다.

- [ ] **Step 2: 컴파일 확인**

Unity 에디터 포커스 후 콘솔 확인.
Expected: 컴파일 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/_Project/Scripts/Runtime/Player/PlayerMovement.cs
git commit -m "feat: add owner-driven PlayerMovement NetworkBehaviour"
```

---

## Task 5: Player 프리팹 구성 (에디터)

**Files:**
- Create: `Assets/_Project/Prefabs/Player.prefab`

- [ ] **Step 1: 프리팹 GameObject 구성**

Hierarchy에서 빈 오브젝트 만들기 → 이름 `Player`. 아래 컴포넌트를 추가/설정:

1. `Capsule`(자식 메시)로 시각 표현: `Player` 아래에 3D Object > Capsule을 두고, 자식 Capsule의 CapsuleCollider는 **제거**(이동 충돌은 CharacterController가 담당).
2. `Player` 루트에 **CharacterController** 추가 — Height 2, Radius 0.5, Center (0,1,0).
3. `Player` 루트에 **NetworkObject** 추가.
4. `Player` 루트에 **ClientNetworkTransform** 추가.
   - ClientNetworkTransform이 목록에 없으면: NGO 2.x의 `com.unity.netcode.gameobjects` 샘플 "ClientNetworkTransform"을 Package Manager > Netcode for GameObjects > Samples에서 Import 하거나, 아래 Step 2로 직접 추가한다.
5. `Player` 루트에 **PlayerMovement**(Task 4) 추가.

- [ ] **Step 2: ClientNetworkTransform 확보 (샘플에 없을 때만)**

`Assets/_Project/Scripts/Runtime/Player/ClientNetworkTransform.cs` 작성:

```csharp
using Unity.Netcode.Components;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 소유자 권위 NetworkTransform. 소유 클라이언트가 위치를 기록하고
    /// 서버는 이를 다른 클라이언트로 중계한다. (NGO 공식 샘플과 동일 패턴)
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
```

그 후 Step 1의 4번에서 이 `ClientNetworkTransform`(CTC.Runtime.Player)을 추가한다.

- [ ] **Step 3: 프리팹으로 저장**

`Player` GameObject를 `Assets/_Project/Prefabs/` 로 드래그해 프리팹화한 뒤, Hierarchy의 인스턴스는 삭제한다.

- [ ] **Step 4: 커밋**

```bash
git add Assets/_Project/Prefabs Assets/_Project/Scripts/Runtime/Player
git commit -m "feat: add networked Player prefab (CharacterController + ClientNetworkTransform)"
```

---

## Task 6: Game 씬 + NetworkManager + 접속 UI

**Files:**
- Create: `Assets/_Project/Scenes/Game.unity`
- Create: `Assets/_Project/Scripts/Runtime/Networking/ConnectionUI.cs`

- [ ] **Step 1: Game 씬 생성 & 바닥/조명**

`File > New Scene`(Basic URP) → `Assets/_Project/Scenes/Game.unity`로 저장.
바닥용 3D Object > Plane 추가(Scale 5,1,5). 기본 Directional Light 유지.

- [ ] **Step 2: NetworkManager 오브젝트 구성**

빈 오브젝트 `NetworkManager` 생성 → 컴포넌트 추가:
1. **NetworkManager** 추가.
2. **Unity Transport** 추가(NetworkManager가 NetworkTransport를 요구 → Unity Transport 선택). 기본값(127.0.0.1:7777) 유지.
3. NetworkManager 인스펙터 > **Network Prefabs** 리스트에 `Player.prefab`(Task 5) 추가.
4. NetworkManager 인스펙터 > **Player Prefab** 필드에 `Player.prefab` 지정(자동 스폰).

- [ ] **Step 3: 접속 UI 스크립트 작성**

`Assets/_Project/Scripts/Runtime/Networking/ConnectionUI.cs`:

```csharp
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 개발용 접속 UI. 접속 전에는 Host/Client/Server 버튼을 IMGUI로 그린다.
    /// 로컬호스트(127.0.0.1) 기준. Steam 로비는 Plan 2에서 대체한다.
    /// </summary>
    public class ConnectionUI : MonoBehaviour
    {
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 200, 200));

            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                if (GUILayout.Button("Host")) NetworkManager.Singleton.StartHost();
                if (GUILayout.Button("Client")) NetworkManager.Singleton.StartClient();
                if (GUILayout.Button("Server")) NetworkManager.Singleton.StartServer();
            }
            else
            {
                GUILayout.Label($"Mode: {(NetworkManager.Singleton.IsHost ? "Host" : NetworkManager.Singleton.IsServer ? "Server" : "Client")}");
                GUILayout.Label($"Players: {NetworkManager.Singleton.ConnectedClientsList.Count}");
                if (GUILayout.Button("Disconnect")) NetworkManager.Singleton.Shutdown();
            }

            GUILayout.EndArea();
        }
    }
}
```

- [ ] **Step 4: ConnectionUI를 씬에 붙이기**

`NetworkManager` 오브젝트(또는 새 빈 오브젝트 `UI`)에 `ConnectionUI` 컴포넌트를 추가한다. 씬 저장.

- [ ] **Step 5: Build Settings에 씬 등록 & 컴파일 확인**

`File > Build Settings`에서 `Game.unity`를 Scenes In Build에 추가. 콘솔에 컴파일 에러 없는지 확인.

- [ ] **Step 6: 커밋**

```bash
git add Assets/_Project/Scenes Assets/_Project/Scripts/Runtime/Networking
git commit -m "feat: add Game scene with NetworkManager and dev ConnectionUI"
```

---

## Task 7: Multiplayer Play Mode로 2인 접속·이동 검증

**Files:** (코드 변경 없음 — 수동 검증)

- [ ] **Step 1: 가상 플레이어 활성화**

`Window > Multiplayer > Multiplayer Play Mode` 창 열기 → **Player 2** 체크(가상 플레이어 1개 활성화). 활성화 완료까지 대기(별도 에디터 인스턴스 로드).

- [ ] **Step 2: 메인 에디터에서 Host 시작**

`Game.unity`를 연 상태에서 Play. 좌상단 IMGUI의 **Host** 버튼 클릭.
Expected: 바닥 위에 Player 캡슐 1개 스폰. UI에 `Mode: Host`, `Players: 1`.

- [ ] **Step 3: 가상 플레이어에서 Client 접속**

Player 2 가상 창이 Play 상태가 되면 그 창의 **Client** 버튼 클릭.
Expected:
- 양쪽 창 모두 Player 캡슐 **2개** 표시.
- Host 창 UI `Players: 2`.

- [ ] **Step 4: 이동 동기화 확인**

각 창에서 그 창이 소유한 캡슐을 WASD/화살표로 움직인다.
Expected:
- 메인 창에서 움직인 캡슐이 **가상 창에서도 같은 위치로** 이동(그 반대도 성립).
- 소유하지 않은 캡슐은 그 창의 입력에 반응하지 않음(IsOwner 게이트 동작).

- [ ] **Step 5: 검증 결과 기록**

관찰 결과(스폰 수, 양방향 이동 동기화 여부)를 확인한다. 실패 시 `superpowers:systematic-debugging`으로 원인 추적(흔한 원인: Player Prefab 미지정, Network Prefabs 목록 누락, ClientNetworkTransform 권위 설정 오류).

- [ ] **Step 6: 문서 커밋(검증 완료 표시)**

이 계획 문서의 체크박스를 채우고 커밋:

```bash
git add docs/superpowers/plans/2026-07-06-01-networking-foundation.md
git commit -m "docs: mark networking foundation plan verified via MPPM"
```

---

## 완료 기준 (Definition of Done)

- [ ] EditMode 테스트(`MovementInputTests`) 4개 통과.
- [ ] MPPM 2인 접속 시 Player 프리팹이 각 클라이언트당 1개씩 스폰.
- [ ] 소유 캡슐 이동이 양방향으로 동기화됨.
- [ ] 콘솔에 에러/예외 없음.
- [ ] 모든 커밋 완료.

## 다음 계획

Plan 2(Steam 매치메이킹): Steamworks.NET 설치, Steam 로비 생성/참가, UnityTransport → Steam 트랜스포트 교체, 개발용 App ID(480/Spacewar)로 친구 접속 검증.
