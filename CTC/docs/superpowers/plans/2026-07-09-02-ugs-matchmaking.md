# UGS 매치메이킹 (Plan 2) 구현 계획

> **상태: ⏸ 대기** — Unity Dashboard 프로젝트 연결(사용자 결정) 필요. 착수 시 이 계획을 현행 코드 기준으로 재검토할 것(작성 시점 이후 Plan 4~8이 진행됨 — HostPortFallback·MppmAutoConnect 로컬 직결 경로를 Relay로 대체)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** UGS Multiplayer Services(Relay+Lobby 통합 Sessions API)로 참가코드 기반 방 생성/참가를 붙이고, 연결 계층을 인터페이스로 추상화해 출시 시 Steam으로 교체 가능하게 만든다.

**Architecture:** `com.unity.services.multiplayer`(통합 SDK)의 Sessions API를 쓴다. `ISessionService` 인터페이스로 세션 수명주기(생성·코드참가·종료)를 감싸고, UGS 구현(`UgsSessionService`)은 `CreateSessionAsync().WithRelayNetwork()`가 NGO 호스트 시작까지 자동 처리하는 경로를 쓴다. 나중에 Steam 전환 시 이 인터페이스의 Steam 구현만 추가한다(전송은 NGO NetworkTransport 교체 1곳). 익명 인증으로 프로토타이핑한다.

**Tech Stack:** com.unity.services.multiplayer 2.2.4, Unity Services Core + Authentication(익명), NGO 2.12.0, UnityTransport(Relay 모드)

**사전 조사 근거 (2026-07 확인):**
- 구형 `com.unity.services.relay`/`lobby` 패키지는 deprecated — 통합 패키지가 현행. 최신 2.2.4(최소 Unity 6000.0)
- Sessions API(`MultiplayerService.Instance.CreateSessionAsync/JoinSessionByCodeAsync`)가 권장 경로, `WithRelayNetwork()`가 NGO 자동 통합
- 무료 티어: Relay 월평균 50 CCU, 세션당 최대 150명. Lobby rate limit: 플레이어당 Create 20회/분, Join 1회/초
- Steam 대비 격리 지점 3곳: 세션 수명주기(ISession), 조인 식별자(join code vs SteamID), 인증(UGS 익명 vs 불필요)

---

## 파일 구조

- Create: `Assets/Scripts/Network/Session/ISessionService.cs` — 세션 추상화 인터페이스
- Create: `Assets/Scripts/Network/Session/JoinCodeUtil.cs` — 참가코드 정규화/검증 (순수 로직)
- Create: `Assets/Scripts/Network/Session/UgsSessionService.cs` — UGS Sessions 구현
- Create: `Assets/Scripts/Network/Session/UgsBootstrap.cs` — UnityServices 초기화 + 익명 로그인
- Modify: `Assets/Scripts/Network/ConnectionUI.cs` — Create/Join(코드 입력) UI 추가
- Test: `Assets/Scripts/Tests/EditMode/JoinCodeUtilTests.cs`
- Modify: `Packages/manifest.json`, `Assets/Scripts/CTC.Runtime.asmdef`

---

## Task 1: 패키지 설치

**Files:**
- Modify: `Packages/manifest.json`

- [ ] **Step 1: manifest.json에 패키지 추가**

`"dependencies"`에 추가 (알파벳 순, `com.unity.render-pipelines...` 위):

```json
    "com.unity.services.multiplayer": "2.2.4",
```

- [ ] **Step 2: 에디터 복원 확인**

Expected: 콘솔 에러 0. Package Manager In Project에 "Multiplayer Services" 표시. `com.unity.services.core`·`authentication`은 의존성으로 자동 설치.

- [ ] **Step 3: 커밋**

```bash
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "chore: add UGS Multiplayer Services SDK 2.2.4"
```

---

## Task 2: UGS 프로젝트 연결 (수동 — 사용자 작업)

- [ ] **Step 1: Unity Dashboard에서 프로젝트 생성**

[cloud.unity.com](https://cloud.unity.com) → 조직 선택/생성 → 프로젝트 생성 (예: `CTC`)

- [ ] **Step 2: 에디터 링크**

`Edit > Project Settings > Services` → 조직 선택 → 방금 만든 프로젝트 링크.
Expected: Project ID가 표시됨 (`ProjectSettings/ProjectSettings.asset`의 cloudProjectId에 기록됨).

- [ ] **Step 3: 커밋**

```bash
git add ProjectSettings/ProjectSettings.asset
git commit -m "chore: link UGS project"
```

> 익명 인증은 Dashboard 별도 설정 불필요. Relay/Lobby는 통합 Multiplayer 서비스라 자동 사용 가능 — Dashboard에서 활성화 요구가 뜨면 활성화.

---

## Task 3: JoinCodeUtil (TDD)

**Files:**
- Create: `Assets/Scripts/Network/Session/JoinCodeUtil.cs`
- Test: `Assets/Scripts/Tests/EditMode/JoinCodeUtilTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using CTC.Runtime.Networking.Session;
using NUnit.Framework;

namespace CTC.Tests
{
    public class JoinCodeUtilTests
    {
        [TestCase("abc123", "ABC123")]           // 소문자 → 대문자
        [TestCase("  ABC123  ", "ABC123")]        // 공백 트림
        [TestCase("AbC 123", "ABC123")]           // 내부 공백 제거
        public void Normalize_CleansInput(string raw, string expected)
        {
            Assert.AreEqual(expected, JoinCodeUtil.Normalize(raw));
        }

        [TestCase("ABC123", true)]
        [TestCase("ABCD12", true)]
        [TestCase("", false)]                     // 빈 문자열
        [TestCase("AB", false)]                   // 너무 짧음
        [TestCase("ABC12!", false)]               // 특수문자
        public void IsValid_ChecksFormat(string code, bool expected)
        {
            Assert.AreEqual(expected, JoinCodeUtil.IsValid(code));
        }

        [Test]
        public void Normalize_Null_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, JoinCodeUtil.Normalize(null));
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `Tools > CTC > Run EditMode Tests` (MCP: `u_editor_menu`)
Expected: 콘솔 `[EditModeTests]`에 컴파일 에러(JoinCodeUtil 없음) 또는 FAIL.

- [ ] **Step 3: 최소 구현**

```csharp
using System.Text.RegularExpressions;

namespace CTC.Runtime.Networking.Session
{
    /// <summary>Relay 참가코드 정규화·형식 검증. 코드는 영숫자 6자 내외(UGS 발급 형식).</summary>
    public static class JoinCodeUtil
    {
        private static readonly Regex ValidPattern = new Regex("^[A-Z0-9]{4,12}$");

        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            return raw.Replace(" ", string.Empty).Trim().ToUpperInvariant();
        }

        public static bool IsValid(string code)
        {
            return !string.IsNullOrEmpty(code) && ValidPattern.IsMatch(code);
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `Tools > CTC > Run EditMode Tests`
Expected: `[EditModeTests] 완료 — Passed: 12, Failed: 0` (기존 4 + 신규 8)

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Network/Session Assets/Scripts/Tests/EditMode/JoinCodeUtilTests.cs
git commit -m "feat: add JoinCodeUtil with EditMode tests"
```

---

## Task 4: ISessionService 인터페이스

**Files:**
- Create: `Assets/Scripts/Network/Session/ISessionService.cs`

- [ ] **Step 1: 인터페이스 작성**

```csharp
using System;
using System.Threading.Tasks;

namespace CTC.Runtime.Networking.Session
{
    /// <summary>
    /// 세션(방) 수명주기 추상화. UGS Sessions가 1차 구현이며,
    /// 출시 시 Steam 로비 구현으로 교체한다. 조인 식별자는 string으로 통일
    /// (UGS=참가코드, Steam=LobbyID 문자열화).
    /// </summary>
    public interface ISessionService
    {
        /// <summary>현재 세션의 조인 식별자. 세션 없으면 null.</summary>
        string JoinCode { get; }

        bool IsInSession { get; }

        /// <summary>호스트로 세션 생성. 성공 시 조인 식별자 반환. NGO 호스트 시작까지 포함.</summary>
        Task<string> CreateAsync(int maxPlayers);

        /// <summary>조인 식별자로 참가. NGO 클라이언트 시작까지 포함.</summary>
        Task JoinByCodeAsync(string joinCode);

        /// <summary>세션 이탈 + NGO 종료.</summary>
        Task LeaveAsync();

        event Action<string> SessionError;
    }
}
```

- [ ] **Step 2: 컴파일 확인 후 커밋**

```bash
git add Assets/Scripts/Network/Session/ISessionService.cs
git commit -m "feat: add ISessionService abstraction for UGS/Steam swap"
```

---

## Task 5: UgsBootstrap (서비스 초기화 + 익명 인증)

**Files:**
- Create: `Assets/Scripts/Network/Session/UgsBootstrap.cs`
- Modify: `Assets/Scripts/CTC.Runtime.asmdef` — references에 `Unity.Services.Core`, `Unity.Services.Authentication`, `Unity.Services.Multiplayer` 추가

- [ ] **Step 1: asmdef 참조 추가**

`Assets/Scripts/CTC.Runtime.asmdef`의 references에 추가:

```json
        "Unity.Services.Core",
        "Unity.Services.Authentication",
        "Unity.Services.Multiplayer"
```

- [ ] **Step 2: UgsBootstrap 작성**

```csharp
using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace CTC.Runtime.Networking.Session
{
    /// <summary>
    /// UGS 초기화 + 익명 로그인. 세션 API 호출 전 1회 필요.
    /// 익명 계정은 기기 재설치 시 복구 불가 — 프로토타이핑 전용, 출시 시 Steam 인증으로 대체.
    /// </summary>
    public static class UgsBootstrap
    {
        private static bool s_Initialized;

        public static async Task EnsureInitializedAsync()
        {
            if (s_Initialized) return;

            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            s_Initialized = true;
            Debug.Log($"[UgsBootstrap] 로그인 완료 playerId={AuthenticationService.Instance.PlayerId}");
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인 후 커밋**

```bash
git add Assets/Scripts/Network/Session/UgsBootstrap.cs Assets/Scripts/CTC.Runtime.asmdef
git commit -m "feat: add UGS bootstrap with anonymous auth"
```

---

## Task 6: UgsSessionService (Sessions API 구현)

**Files:**
- Create: `Assets/Scripts/Network/Session/UgsSessionService.cs`

- [ ] **Step 1: 구현 작성**

```csharp
using System;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CTC.Runtime.Networking.Session
{
    /// <summary>
    /// UGS Sessions 기반 ISessionService 구현.
    /// WithRelayNetwork()가 Relay 할당·UnityTransport 설정·NGO 호스트/클라 시작을 자동 처리한다.
    /// </summary>
    public class UgsSessionService : ISessionService
    {
        private ISession m_Session;

        public string JoinCode => m_Session?.Code;
        public bool IsInSession => m_Session != null;

        public event Action<string> SessionError;

        public async Task<string> CreateAsync(int maxPlayers)
        {
            await UgsBootstrap.EnsureInitializedAsync();
            try
            {
                var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
                m_Session = await MultiplayerService.Instance.CreateSessionAsync(options);
                Debug.Log($"[UgsSessionService] 세션 생성 code={m_Session.Code}");
                return m_Session.Code;
            }
            catch (Exception e)
            {
                SessionError?.Invoke(e.Message);
                throw;
            }
        }

        public async Task JoinByCodeAsync(string joinCode)
        {
            await UgsBootstrap.EnsureInitializedAsync();
            try
            {
                string code = JoinCodeUtil.Normalize(joinCode);
                m_Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                Debug.Log($"[UgsSessionService] 세션 참가 code={code}");
            }
            catch (Exception e)
            {
                SessionError?.Invoke(e.Message);
                throw;
            }
        }

        public async Task LeaveAsync()
        {
            if (m_Session == null) return;
            await m_Session.LeaveAsync();
            m_Session = null;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인 후 커밋**

```bash
git add Assets/Scripts/Network/Session/UgsSessionService.cs
git commit -m "feat: add UgsSessionService (Sessions API + Relay)"
```

> API명이 SDK 2.2.4에서 다르면(`ISession.Code` 등) 컴파일 에러 기준으로 수정 — Sessions 문서: docs.unity.com/en-us/mps-sdk

---

## Task 7: ConnectionUI에 UGS 모드 추가

**Files:**
- Modify: `Assets/Scripts/Network/ConnectionUI.cs`

- [ ] **Step 1: Create/Join UI 추가**

기존 로컬(Host/Client/Server) 버튼 아래에 UGS 섹션을 추가한다. 전체 교체본:

```csharp
using Unity.Netcode;
using UnityEngine;
using CTC.Runtime.Networking.Session;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 개발용 접속 UI. 로컬(localhost) 버튼과 UGS Relay(참가코드) 버튼을 함께 그린다.
    /// </summary>
    public class ConnectionUI : MonoBehaviour
    {
        private readonly ISessionService m_Session = new UgsSessionService();
        private string m_CodeInput = "";
        private string m_Status = "";

        private void OnGUI()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 220, 300));

            if (!nm.IsClient && !nm.IsServer)
            {
                GUILayout.Label("— Local —");
                if (GUILayout.Button("Host")) nm.StartHost();
                if (GUILayout.Button("Client")) nm.StartClient();

                GUILayout.Space(8);
                GUILayout.Label("— UGS Relay —");
                if (GUILayout.Button("Create Session")) CreateSession();
                GUILayout.BeginHorizontal();
                m_CodeInput = GUILayout.TextField(m_CodeInput, GUILayout.Width(120));
                if (GUILayout.Button("Join")) JoinSession();
                GUILayout.EndHorizontal();
                GUILayout.Label(m_Status);
            }
            else
            {
                GUILayout.Label($"Mode: {(nm.IsHost ? "Host" : nm.IsServer ? "Server" : "Client")}");
                if (!string.IsNullOrEmpty(m_Session.JoinCode))
                    GUILayout.Label($"Join Code: {m_Session.JoinCode}");
                // ConnectedClientsList는 서버 전용 — 클라이언트에서 접근하면 NotServerException
                if (nm.IsServer)
                    GUILayout.Label($"Players: {nm.ConnectedClientsList.Count}");
                if (GUILayout.Button("Disconnect")) Disconnect();
            }

            GUILayout.EndArea();
        }

        private async void CreateSession()
        {
            m_Status = "세션 생성 중...";
            try { await m_Session.CreateAsync(6); m_Status = ""; }
            catch (System.Exception e) { m_Status = $"실패: {e.Message}"; }
        }

        private async void JoinSession()
        {
            string code = JoinCodeUtil.Normalize(m_CodeInput);
            if (!JoinCodeUtil.IsValid(code)) { m_Status = "코드 형식 오류"; return; }
            m_Status = "참가 중...";
            try { await m_Session.JoinByCodeAsync(code); m_Status = ""; }
            catch (System.Exception e) { m_Status = $"실패: {e.Message}"; }
        }

        private async void Disconnect()
        {
            if (m_Session.IsInSession) await m_Session.LeaveAsync();
            else NetworkManager.Singleton.Shutdown();
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인 후 커밋**

```bash
git add Assets/Scripts/Network/ConnectionUI.cs
git commit -m "feat: add UGS session create/join to ConnectionUI"
```

---

## Task 8: 검증 (MPPM 2인, Relay 경유)

- [ ] **Step 1: EditMode 테스트 전체 실행**

Run: `Tools > CTC > Run EditMode Tests`
Expected: 전체 PASS

- [ ] **Step 2: 메인 에디터에서 Create Session**

플레이 → `Create Session` 클릭 (MCP: `u_play` invoke `ConnectionUI.CreateSession` 불가 — private. IMGUI 수동 또는 임시 public).
Expected: 콘솔 `[UgsSessionService] 세션 생성 code=XXXXXX`, ConnectionUI에 Join Code 표시.

- [ ] **Step 3: 클론(Player 2)에서 코드 입력 후 Join**

Expected: 클론 콘솔 `세션 참가`, 호스트 Players: 2. `mppm_collect`로 host/clone 양측 connected=true, 플레이어 오브젝트 2개 확인.

- [ ] **Step 4: 이동 동기화 확인**

호스트에서 WASD 이동 → 클론 화면에서 움직임 확인(또는 mppm_collect 위치값 비교).
Expected: 양측 스냅샷의 동일 NetworkObjectId 위치가 일치(±보간 오차).

- [ ] **Step 5: Plan 2 완료 커밋 & 머지**

```bash
git add -A && git commit -m "feat: complete Plan 2 UGS matchmaking"
```

---

## 범위 밖 (후속)

- Steam 로비/트랜스포트 구현 (출시 전)
- 세션 브라우징(코드 없는 목록 참가), 재접속, 호스트 마이그레이션
- 정식 로비 UX (UGUI) — Plan 8 매치 플로우에서
