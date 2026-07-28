# CTC Project - Claude Instructions

치즈 도둑과 고양이(CTC) — Unity 6000.5.2f1 / URP / Netcode for GameObjects 2.12 / MPPM 2.0. 3인칭 비대칭 멀티 스텔스 숨바꼭질 (고양이 1~2 vs 쥐 3~6).

프로젝트 상세(장르·톤·경로 규약): [.claude/project-context.md](.claude/project-context.md)

## 코딩 규칙

- `m_` 멤버 변수 접두 · `_` 매개변수 접두 · `Co` 코루틴 접두 · public 필드 금지(`[SerializeField]` + 프로퍼티) · 상수 `UPPER_SNAKE_CASE` · Manager/Controller/Worker/Component 아키텍처 — 상세는 [claude-gamedev-kit/gamedev-team/agents/client.md](C:/GitFork/claude-gamedev-kit/gamedev-team/agents/client.md)의 "코딩 규칙" 섹션 참조.
  - WES 프로젝트 사본(`C:/GitFork/WES_Project/WES/.claude/agents/client.md`)은 **참조하지 않는다** — WES 전용으로 하드코딩돼 있고 CTC에 없는 경로(`Assets/CSVInfo/`, `document/design/client-spec/`)를 전제한다.
- **적용 범위**: 신규 코드부터 적용. 기존 파일 일괄 리네임은 **1차 최종본 이후 코드 전면 재편**에서 일괄 처리한다 — [코드 개편 계획](docs/design-vault/코드%20개편%20계획.md) 2단계. 그 전까지 기존 파일의 컨벤션은 건드리지 않는다.

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
- **wesqa E2E 도구** (`tools/wesqa/`): Python 클라이언트는 이식 완료. C# 인게임 서버 이식 후 활성화 예정 (WES 이식 ① 대기) — 그 전까지 wesqa 기반 E2E 절차는 사용하지 않는다.

---

**Note:** These are baseline rules with standard priority. They may be adjusted or overridden based on specific project requirements or context.
