# Steam 매칭 구현 계획 (Plan 13)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 멧챠 카멜레온식 스팀 매칭 — 방 생성(공개/비공개) → 공개 방 목록 참여 + 친구 초대, 밸브 릴레이 자동 폴백. 테스트는 AppID 480.

**Architecture:** Facepunch.Steamworks + NGO Facepunch 커뮤니티 트랜스포트. NetworkManager에 UTP·Facepunch 트랜스포트 둘 다 부착, 접속 경로 선택 시 `NetworkConfig.NetworkTransport` 런타임 교체 — 에디터/MPPM QA는 기존 UTP 로컬 직결 그대로(클론은 스팀 미초기화). SteamBootstrap(초기화)·SteamLobbyManager(로비 생성/목록/참여/초대 수락)·ConnectionUI 확장(IMGUI).

**Tech Stack:** Unity 6000.5.2f1 / NGO 2.12 / Facepunch.Steamworks / `com.community.netcode.transport.facepunch` (Unity multiplayer-community-contributions)

**리스크 (T1에서 즉시 검증):** 커뮤니티 트랜스포트의 NGO 2.x 호환 — 컴파일 실패 시 트랜스포트 소스(단일 파일 수준)를 프로젝트로 가져와 직접 패치한다.

---

### Task 1: 패키지 도입 + 스팀 초기화 스모크

**Files:**
- Modify: `Packages/manifest.json`
- Create: `Assets/Scripts/Network/SteamBootstrap.cs`
- Create: `steam_appid.txt` (프로젝트 루트, 내용 `480`)

- [ ] **Step 1: manifest.json에 패키지 추가**

```json
"com.community.netcode.transport.facepunch": "https://github.com/Unity-Technologies/multiplayer-community-contributions.git?path=/Transports/com.community.netcode.transport.facepunch"
```

- [ ] **Step 2: refresh → 컴파일 확인.** NGO 2.x 비호환 CS 에러 시: 패키지 제거, `FacepunchTransport.cs`+Facepunch.Steamworks 플러그인만 `Assets/Plugins/`로 수동 이식 후 API 패치
- [ ] **Step 3: SteamBootstrap 작성**

```csharp
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 스팀 초기화(AppID 480 테스트). 스팀 미실행/미로그인이면 조용히 로컬 모드 —
    /// 기존 UTP 경로는 영향 없음. MPPM 클론은 초기화 실패가 정상(동일 계정 중복).
    /// </summary>
    public class SteamBootstrap : MonoBehaviour
    {
        public const uint APP_ID = 480; // Spacewar — 정식 입점 시 교체

        public static bool Ready { get; private set; }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            try
            {
                Steamworks.SteamClient.Init(APP_ID, true);
                Ready = true;
                Debug.Log($"[Steam] init OK: {Steamworks.SteamClient.Name}");
            }
            catch (System.Exception e)
            {
                Ready = false;
                Debug.LogWarning($"[Steam] init 실패(로컬 모드로 동작): {e.Message}");
            }
        }

        private void OnDestroy()
        {
            if (Ready) Steamworks.SteamClient.Shutdown();
            Ready = false;
        }
    }
}
```

- [ ] **Step 4: SetupNetworkingScene에 SteamBootstrap GO 배선 + 씬 재생성**
- [ ] **Step 5: 에디터 플레이 스모크** — 콘솔에 `[Steam] init OK: <닉네임>` (사용자 스팀 실행 중일 때)
- [ ] **Step 6: Commit** `feat: Facepunch Steamworks + SteamBootstrap (AppID 480)`

---

### Task 2: 듀얼 트랜스포트 + SteamLobbyManager

**Files:**
- Modify: `Assets/Scripts/Editor/SetupNetworkingScene.cs` — NetworkManager GO에 `FacepunchTransport` 추가(기본은 UTP 유지)
- Create: `Assets/Scripts/Network/SteamLobbyManager.cs`

- [ ] **Step 1: SteamLobbyManager 작성** — 핵심 API (Facepunch):

```csharp
// 방 생성 (호스트)
var lobby = await Steamworks.SteamMatchmaking.CreateLobbyAsync(MAX_PLAYERS); // 8
lobby.SetData("game", LOBBY_TAG);           // "CTC" — 480 잡음 필터
lobby.SetData("name", _roomName);
if (_isPublic) lobby.SetPublic(); else lobby.SetFriendsOnly();
lobby.SetJoinable(true);
// → Facepunch 트랜스포트로 전환 후 StartHost, lobby.SetGameServer(SteamClient.SteamId)

// 방 목록 (참여자)
var lobbies = await Steamworks.SteamMatchmaking.LobbyList
    .WithKeyValue("game", LOBBY_TAG).WithMaxResults(20).RequestAsync();

// 참여: lobby.Join() → OnLobbyEntered에서 owner SteamId를 트랜스포트 targetSteamId로 → StartClient
// 초대 수락: SteamMatchmaking.OnGameLobbyJoinRequested 콜백 → 위와 동일 Join 경로
```

트랜스포트 전환 헬퍼: `nm.NetworkConfig.NetworkTransport = steam ? facepunchTransport : unityTransport` (Start 전만 허용). 접속 종료/실패 시 로비 Leave + UTP 복원.

- [ ] **Step 2: 순수 로직 분리 + EditMode 테스트** — `SteamLobbyLogic.RoomLabel(name, owner, count, max)`·`IsCtcLobby(gameTag)` 등 문자열/필터 함수 `FunLogic` 패턴으로. 테스트 3~4개 추가
- [ ] **Step 3: 컴파일 + 테스트 100+α 확인, Commit**

---

### Task 3: ConnectionUI 확장 (멧챠 카멜레온식)

**Files:**
- Modify: `Assets/Scripts/Network/ConnectionUI.cs`

- [ ] **Step 1: IMGUI 확장** — 미접속 상태에서:
  - 기존: `Host`/`Client` (로컬 — 에디터/MPPM용, 빌드에서도 유지)
  - 추가(SteamBootstrap.Ready일 때만): 방 이름 입력 + `[스팀 방 만들기(공개)]` `[비공개(친구만)]` / `[방 목록 새로고침]` → 방 버튼 목록(이름·인원) 클릭 참여 / 안내 "친구 초대: Shift+Tab 오버레이"
  - 접속 후: 현재 모드에 `Steam` 표기 + Disconnect 시 로비 Leave
- [ ] **Step 2: 에디터 스모크** — 사용자 스팀 계정으로 방 생성 → 방 목록에 자기 방 조회 확인 (단일 계정으로 가능한 범위)
- [ ] **Step 3: Commit**

---

### Task 4: 빌드 파이프라인 + 원격 실전 테스트 준비

**Files:**
- Create: `Assets/Scripts/Editor/BuildWindowsMenu.cs` — `Tools/CTC/Build Windows`

- [ ] **Step 1: 빌드 메뉴** — `BuildPipeline.BuildPlayer(Game.unity, Builds/Windows/CTC.exe, Win64)` + 빌드 폴더에 `steam_appid.txt`(480) 복사
- [ ] **Step 2: 빌드 실행 → 산출물 확인** (exe 기동, 스팀 init OK 로그)
- [ ] **Step 3: MPPM 회귀 QA** — 기존 UTP 로컬 2인 사이클이 그대로 도는지 (표준 절차)
- [ ] **Step 4: Commit + push**
- [ ] **Step 5 (사용자):** Builds/Windows 압축 → 친구에게 전달, 양쪽 스팀 켜고 방 생성/참여 — 유일한 수동 스텝

---

## 검증 기준

| 검증 | 방법 | 합격 |
|---|---|---|
| 스팀 초기화 | 에디터 플레이 콘솔 | `init OK: <닉네임>` |
| 방 생성/목록 | 단일 계정 스모크 | 만든 방이 목록 조회에 나옴 |
| UTP 회귀 | MPPM 표준 절차 | 기존 사이클 이상 없음 |
| EditMode | 전체 실행 | 100 + 신규 전부 통과 |
| 원격 접속 | 사용자+친구 빌드 | 방 참여·게임 사이클 완주 (사람 필요) |
