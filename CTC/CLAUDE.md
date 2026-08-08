# CTC Project - Claude Instructions

치즈 도둑과 고양이(CTC) — Unity 6000.5.2f1 / URP / Netcode for GameObjects 2.12 / MPPM 2.0. 3인칭 비대칭 멀티 스텔스 숨바꼭질 (고양이 1~2 vs 쥐 3~6).

프로젝트 상세(장르·톤·경로 규약): [.claude/project-context.md](.claude/project-context.md)

## 코딩 규칙

- `m_` 멤버 변수 접두 · `_` 매개변수 접두 · public 필드 금지(`[SerializeField]` + 프로퍼티) · 상수 `UPPER_SNAKE_CASE` · Manager/Controller/Worker/Component 아키텍처 — 상세는 [claude-gamedev-kit/gamedev-team/agents/client.md](C:/GitFork/claude-gamedev-kit/gamedev-team/agents/client.md)의 "코딩 규칙" 섹션 참조.
  - WES 프로젝트 사본(`C:/GitFork/WES_Project/WES/.claude/agents/client.md`)은 **참조하지 않는다** — WES 전용으로 하드코딩돼 있고 CTC에 없는 경로(`Assets/CSVInfo/`, `document/design/client-spec/`)를 전제한다.

### CTC 전용 규칙 (kit 규칙보다 우선, 2026-07-30 지정)

| # | 규칙 | 금지 |
|---|---|---|
| 1 | 비동기는 **UniTask** (`async UniTask`, `UniTask.Delay`, `CancellationToken`) | 코루틴(`IEnumerator`·`StartCoroutine`) — kit의 `Co` 접두 규칙을 대체 |
| 2 | 버튼·이벤트 구독은 **R3** (`button.OnClickAsObservable().Subscribe(...).AddTo(this)`) | `onClick.AddListener` |
| 3 | UI 상태 반영은 **R3 구독**(`ReadOnlyReactiveProperty`) | `Update()`에서 값 폴링·오브젝트 탐색 |
| 4 | 버튼 핸들러 이름은 **`OnClick~`** | 람다에 로직 직접 작성 |
| 5 | UI는 **MVC** — View=UI 표시만 / Model=상태 소유 Component / Controller=입력→서버 RPC | View가 상태를 조회·판정 |
| 6 | Model 공급·씬 기능은 **Worker** (`LocalPlayerBinder`·`MapDirector`·`PlayerSpawner` 등) | 매니저 비대화 |
| 7 | 기능은 **컴포넌트 조합** (기능 1개 = 컴포넌트 1개) | 기존 클래스에 기능 덧붙이기 |
| 8 | **코드를 고치면 [코드 다이어그램](docs/design-vault/코드%20다이어그램.md)의 머메이드(클래스·워크플로우)를 같이 갱신한다** | 다이어그램 방치 |
| 9 | **Unity 객체는 `if (x != null)`로 검사** — `GameObject`·`Component`·`ScriptableObject` 등 `UnityEngine.Object` 파생 | `?.` · `??` · `??=` (Unity의 `==` 오버로드를 건너뛰어 **파괴된 객체가 통과** → `MissingReferenceException`) |

- 규칙 9 예외: 순수 C# 객체에는 `?.`를 써도 된다 — C# 이벤트(`HpChanged?.Invoke`), `CancellationTokenSource`,
  `MaterialPropertyBlock`, R3 `Subject`, 리플렉션 결과 등. `UnityEngine.Object` 파생인지만 보면 된다.

- UniTask·R3는 `CTC.Runtime.asmdef`에 참조 배선 완료(`UniTask`, `R3.Unity`). R3 코어는 `Assets/Packages/R3.1.3.1/`의 DLL이라 자동 참조된다.

### 데이터 (CSV — 2026-07-31)

- **밸런스 수치는 코드에 박지 않는다.** 원본은 `Assets/Resources/CSVInfo/*.csv` (WES 파이프라인 이식).
  - 표 4종: `ShopInfo`(가격) · `WeaponInfo`(무기 티어) · `PlayerInfo`(HP·이속·회전·덮치기·발소리) · `BalanceInfo`(그 외 전역 — 대시·설치 사거리·수입 노출·요새 힐 포함)
  - 컬럼 추가·변경 후 **`Tools/CTC/Convert CSV → Info`** 실행 → `Info/Generator/ConvertInfo/*.cs` 재생성.
    헤더는 `필드명.타입` 형식(`Cost.int`, `Role.RoleId`). 생성 파일은 손으로 고치지 않는다.
  - 읽기는 `InfoManager.Instance.Balance` / `.PlayerOf(role)` / `.WeaponOf(tier)` / `.Shop`.
  - `PlayerInfo`만 프리팹에 굽히므로 바꾸면 **씬 재생성**이 필요하다. 나머지는 재컴파일 없이 다음 플레이에 반영.
- 구조 제약(`MIN_GRADE`·`MAX_GRADE`·`HOTBAR_SLOTS`·`NO_EARNER`·`CAT_CAPACITY`)과 연출 수치(볼륨·크로스페이드·토스트 시간)는 CSV로 내리지 않는다 — 튜닝 대상이 아니다.
- **코드에 남은 밸런스는 0이다**(2026-07-31 확인). 수치를 고쳐 달라는 요청이 오면 먼저 CSV를 본다.

### 디버그 로그·빌드

- 로그는 `GameDebug.Log/LogWarning/LogError` (WES 방식, `[Conditional("DEVELOPMENT_BUILD")]`) — 배포 빌드에선 전부 제거된다.
- 빌드 메뉴 2종: `Tools/CTC/Build Windows (개발용 — 로그·콘솔 켜짐)` → `Builds/Windows-Dev`,
  `Tools/CTC/Build Windows (배포용)` → `Builds/Windows`.
- 인게임 로그 콘솔(` 키)은 `DebugConsoleGate`가 **개발용 빌드에서만** 켠다 — `GameDebug`와 같은 기준.
- **적용 범위**: **전 파일 적용 완료**(2026-07-31, [코드 개편 계획](docs/design-vault/코드%20개편%20계획.md) 1~5단계 + 잔여 3건). 예외 없이 위 규칙대로 쓴다.

## unity-scanner (에셋 YAML 탐색 — 토큰 절약)

- 프리팹/씬/.asset의 **내용 파악·검색·참조 추적**은 파일을 Read로 통째로 읽지 말고 `unity-scanner`(CLI, `%LOCALAPPDATA%\unity-scanner\unity-scanner.exe`)를 우선 사용한다.
  - 목록: `unity-scanner list -p <프로젝트경로> Assets/... --depth N`
  - 요약 읽기: `unity-scanner read -p <프로젝트경로> <에셋경로>` (prefab/scene/asset 계층·컴포넌트 요약)
  - 검색: `unity-scanner search -p <프로젝트경로> <질의>` (파일명·GameObject·컴포넌트·GUID)
  - 참조 추적: `unity-scanner refs -p <프로젝트경로> <에셋경로|GUID>`
- 예외: YAML을 **정확히 수정**해야 하거나 특정 직렬화 값(해시 등)을 원문으로 검증할 때만 Read/Grep 직접 사용.

## MCP Rules

- Unity 관련 작업(프리팹, 컴포넌트, Inspector 참조 등)은 MCP 도구를 우선 사용한다.
- 사용 가능한 도구:
  - **에디터**: `u_editor_component` / `u_editor_gameobject` / `u_set_transform` / `u_editor_prefab` / `u_editor_scene` / `u_editor_asset` / `u_editor_tag_layer` / `u_editor_menu` / `u_editor_sceneview` / `u_editor_input`
  - **플레이모드**: `u_play` / `u_set_transform` (mode='play')
  - **공통**: `u_console` / `u_screenshot`
  - **MPPM**: `mppm_collect` (가상 플레이어 로그/상태 수집, 포트 7788)
- 도구 사용법은 원본 저장소 문서 [C:/GitFork/MCP_Unity/MCP_Unity_Plugin/README.md](C:/GitFork/MCP_Unity/MCP_Unity_Plugin/README.md)를 참고한다 (프로젝트 내 `Assets/MCP_Unity_Plugin/`에는 README 없음).
- 에셋(스크립트, 프리팹 등)을 추가/수정/삭제한 경우 작업 완료 후 반드시 `u_editor_asset(action: refresh)`를 호출하여 Unity 에디터에 반영한다.
- **MCP 플러그인 수정 시**: 반드시 원본 저장소(`C:\GitFork\MCP_Unity\MCP_Unity_Plugin`)에서 먼저 수정한 뒤 프로젝트(`Assets/MCP_Unity_Plugin/`)로 복사한다. 프로젝트 내 파일을 직접 수정하지 않는다.
- **MCP 서버 재빌드/재시작**: 배치 파일 `C:\GitFork\MCP_Unity\MCP\MCP\MCP\stop_and_rebuild.bat`를 실행한다.
- **McpBridge 미응답 시** (도메인 리로드 후 에디터 미포커스): `powershell -NoProfile -File tools/focus-unity.ps1`

## CTC 코드 제약

- 레거시 `Input.*` API 금지 (Input System 사용)
- CharacterController 오브젝트에 transform 직접 세팅 금지 → `CC.Move()` 사용
- EditMode 테스트 실행: 메뉴 `Tools/CTC/Run EditMode Tests` (`u_editor_menu`로 호출 가능)

## Dev-QA Workflow Rules

- 기능 개발 시 **개발 → QA → 판정** 사이클을 따른다.
- QA는 MCP 도구(`u_play`, `u_console`, `u_screenshot`, `u_editor_component`)로 플레이모드에서 실제 검증한다. 멀티플레이 검증은 MPPM 가상 플레이어 + `mppm_collect`.
- **자동 수정**: 코드 에러, 런타임 에러, MCP 도구 부족 등 기술적 문제는 자동으로 수정 후 재시도한다.
- **중단 조건**: 기획적으로 시나리오 진행이 불가능한 경우에만 사용자에게 보고하고 중단한다.
- **세션 양도**: MCP 자체 수정이 불가피할 경우, `/rename`으로 세션 이름을 붙이고 사용자에게 재빌드 후 `/resume`으로 복귀하도록 안내한다.
- **서브에이전트**: 워크플로우 사이클은 순차 실행. 개발 단계 내 독립적인 코드 작업이 여러 개일 때만 서브에이전트를 사용한다.
- **wesqa E2E 도구 — 이식 완료, 실사용 중**(2026-07-12 `e8fa264`). C# 인게임 서버 `Assets/WesQA/`(플레이 진입 시 포트 `5001+MPPM인덱스`로 기동) + Python 클라이언트 `tools/wesqa/`.
  - MPPM 3인 실플레이 QA 14항목: `python tools/wesqa/tests/qa_v36.py`. **MonoBehaviour를 재배치했으면 이걸 돌려야 한다** — EditMode는 순수 로직만 덮는다.
  - 진입점은 `TestManager` 하나다. wesqa가 `Instance.<method>` 리플렉션으로 부르므로 **타입을 쪼개거나 Editor 전용 어셈블리로 옮기면 안 된다**(실측으로 QA 전멸).

---

**Note:** These are baseline rules with standard priority. They may be adjusted or overridden based on specific project requirements or context.
