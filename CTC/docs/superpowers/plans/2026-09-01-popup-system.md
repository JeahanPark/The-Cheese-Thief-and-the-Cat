# 팝업 시스템 구현 계획 (WES 이식 + CTC 적응)

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans.

**Goal:** 팝업이 프리팹으로 관리되고, `PopupManager.Open<T>()` 한 줄로 어느 화면에서든 열린다. 겹치면 스택으로 쌓이고 ESC가 위에서부터 닫는다.

**결정 (2026-09-01 사용자):**
- **Addressables 사용** — WES와 같은 방식. 어차피 쓸 것이라 지금 세운다 (`com.unity.addressables` 2.11.1 설치됨, 그룹은 아직 없음)
- **딤은 팝업이 각자 소유** — WES와 같은 방식(`BasePopup`이 자기 자식으로 생성)
- 팝업만 프리팹으로. **HUD·씬 레이아웃은 빌더 코드 유지**(화면당 1벌, 안 겹침, 톤 일치가 자동)

**WES와 달라지는 곳 (CTC 제약):**

| # | WES | CTC | 이유 |
|---|---|---|---|
| 1 | 씬 Controller가 ESC 구독 → `CloseTop()` | **`EscapeRouter`가 단독**으로 읽고 스택이 비면 옵션창 | CLAUDE.md: ESC는 EscapeRouter만 읽는다(같은 프레임 경합) |
| 2 | 모달 중 클릭만 막음 | `UiFocus.SetModalOpen(OpenedCount > 0)` | 안 하면 팝업 뒤에서 캐릭터가 움직인다 |
| 3 | UniRx | R3 | 프로젝트 규약 |
| 4 | 핸들 Release 안 함 | 닫을 때 `Addressables.Release` | WES 누수 그대로 옮기지 않는다 |
| 5 | 씬 전환 시 인스턴스 방치 | `CloseAll()` 명시 호출 | NGO SceneManager 경유라 타이밍이 다르다 |
| 6 | 네트워크 상태를 Start에서 1회 pull | `LocalPlayerBinder` 재구독 지점 | 스폰 전에 열면 영구 빈 상태가 된다 |

---

### Task 1: BasePopup + PopupManager (TDD 가능한 부분부터)

**Files:**
- Create: `Assets/Scripts/UI/Popup/BasePopup.cs` · `Assets/Scripts/UI/Popup/PopupManager.cs`
- Create: `Assets/Scripts/UI/Popup/PopupStackLogic.cs` (순수)
- Test: `Assets/Scripts/Tests/EditMode/PopupStackLogicTests.cs`

- [ ] 순수 로직 테스트 먼저 — 스택 판정만 떼어낸다

```csharp
[Test] public void CloseTop_Removes_Last() { ... }
[Test] public void ModalOpen_Follows_Count() { Assert.IsTrue(PopupStackLogic.ModalOpen(1)); Assert.IsFalse(PopupStackLogic.ModalOpen(0)); }
[Test] public void SortOrder_Increases_With_Depth() { ... }
```

- [ ] `BasePopup` — `Open()`/`Close()`/`OnOpened()`/`OnClosing()` 가상 훅 + **자기 딤 생성**(검정 0.55, raycastTarget, 첫 sibling). `PopupOpenPolicy { StackOnTop, CloseAllAndOpen }` [SerializeField]
- [ ] `PopupManager` — MonoSingleton(`DontDestroyOnLoad`), `InitializeForScene(Canvas)`, `Open<T>()`, `FindOpen<T>()`, `Close()`, `CloseTop()`, `CloseAll()`, `OpenedCount`
  - `Open<T>()`: Addressables 동기 로드(`WaitForCompletion`) → Instantiate → 스택 push → `SetAsLastSibling` → `UiFocus.SetModalOpen(true)`
  - `Close()`: `OnClosing()` → Destroy → 핸들 Release → 스택 pop → `UiFocus.SetModalOpen(OpenedCount > 0)`
  - 주소 = `typeof(T).Name` (WES 규약). **단, 로드 실패 시 에러 로그를 반드시 남긴다** — WES는 조용히 실패한다

### Task 2: Addressables 그룹 + 옵션창 프리팹 (첫 이주)

**Files:**
- Create: `Assets/GameResource/UI/Popup/OptionsPopup.prefab`
- Create: `Assets/Scripts/Editor/Setup/PopupPrefabBuilder.cs` (빌더가 프리팹을 굽는다 — 손으로 안 만든다)
- Modify: `LobbyCanvasBuilder.BuildOptionsPopup` → 프리팹 굽기로 전환

- [ ] `PopupPrefabBuilder` — 지금 `BuildOptionsPopup`의 생성 코드를 그대로 옮겨 **프리팹으로 저장**하는 메뉴(`Tools/CTC/Build Popup Prefabs`). UI를 코드로 짓는 방식은 유지하되 산출물이 프리팹이 된다
- [ ] `OptionsPopup`이 `BasePopup` 상속. 참조는 빌더가 프리팹에 굽는다(SerializeField 그대로)
- [ ] Addressables 그룹 `Popup` 생성 + 주소 `OptionsPopup` 등록 (에디터 API로 자동 — 손 등록 금지)
- [ ] 4개 씬 빌더에서 옵션창 생성 코드 제거 → `PopupManager` 프리팹만 세움

### Task 3: EscapeRouter 통합

**Files:** `Assets/Scripts/Core/EscapeRouter.cs`

- [ ] 우선순위 사슬 개정: 배치 취소 → **팝업 스택(`CloseTop`)** → 채팅 → 옵션창 열기
- [ ] `m_Options` 직접 참조 제거 — `PopupManager.Open<OptionsPopup>()`으로 대체

### Task 4: 검증

- [ ] EditMode(스택 로직) + 빌드 실검증: 인트로/대기실/인게임에서 옵션창 열기, ESC 닫기, 2겹 스택
- [ ] WES `TestManager` 팝업 시나리오 이식(빈 스택 CloseTop no-op·2겹·ESC 와이어링)
- [ ] `UiFocus.ModalOpen`이 팝업 열림과 일치하는지(인게임에서 팝업 뒤 이동 불가)

### Task 5: 문서

- [ ] 코드 다이어그램 + CLAUDE.md에 "팝업은 프리팹 + Addressables, HUD는 빌더" 규약 한 줄

---

## 함정 (WES 실측에서 그대로 옮겨오는 것)

1. **주소 오타가 런타임까지 안 잡힌다** — 주소는 클래스명. 로드 실패 로그를 반드시 남긴다
2. 딤은 팝업 소유라 **2겹이면 짙어진다** — 사용자 결정이므로 그대로 두되, 3겹 이상 쌓이는 UI는 만들지 않는다
3. 씬 전환 시 `CloseAll()` 안 하면 인스턴스가 남는다(NGO SceneManager 경유)
4. 팝업이 로컬 플레이어를 보면 **스폰 전에 열렸을 때 빈 상태로 굳는다** — `LocalPlayerBinder` 재구독
