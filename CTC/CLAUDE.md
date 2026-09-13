# CTC Project - Claude Instructions

**Canadian Mouse**(캐네디언 마우스) — Unity 6000.5.2f1 / URP / Netcode for GameObjects 2.12 / MPPM 2.0. 3인칭 비대칭 멀티 스텔스 숨바꼭질 (고양이 1~2 vs 쥐 3~6).

> 제목의 정본은 `ProjectSettings.productName`(= Canadian Mouse)이다. 레포 폴더·원격 이름
> `The-Cheese-Thief-and-the-Cat`과 옛 문서의 「치즈 도둑과 고양이」는 **초기 가제**다.
> 약칭 CTC는 그 가제에서 온 것이라 경로·어셈블리 이름으로만 남아 있다 — 계속 쓴다.

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
  - 표 4종: `ShopInfo`(가격) · `WeaponInfo`(무기 티어) · `PlayerInfo`(HP·이속·회전·덮치기·발소리) · `BalanceInfo`(그 외 전역 — 대시·설치 사거리·수입 노출·요새 HP 포함)
  - 컬럼 추가·변경 후 **`Tools/CTC/Convert CSV → Info`** 실행 → `Info/Generator/ConvertInfo/*.cs` 재생성.
    헤더는 `필드명.타입` 형식(`Cost.int`, `Role.RoleId`). 생성 파일은 손으로 고치지 않는다.
  - 읽기는 `InfoManager.Instance.Balance` / `.PlayerOf(role)` / `.WeaponOf(tier)` / `.Shop`.
  - `PlayerInfo`만 프리팹에 굽히므로 바꾸면 **씬 재생성**이 필요하다. 나머지는 재컴파일 없이 다음 플레이에 반영.
- 구조 제약(`MIN_GRADE`·`MAX_GRADE`·`HOTBAR_SLOTS`·`NO_EARNER`·`CAT_CAPACITY`)과 연출 수치(볼륨 비율·크로스페이드·토스트 시간)는 CSV로 내리지 않는다 — 튜닝 대상이 아니다.
- **개인 설정(마우스 감도·BGM·SFX 볼륨)은 CSV가 아니라 `Core/UserSettings`(PlayerPrefs + R3)다.** 밸런스가 아니라 그 사람 기기의 취향이라서. CSV는 *처음 켰을 때의 기본값*만 준다(`MouseSensitivityDefault`·`BgmVolumeDefault`·`SfxVolumeDefault`).
  - 값이 **두 층**이다(2026-08-09) — 스트림(지금 들리는 값)과 PlayerPrefs(저장된 값). 슬라이더는 스트림만 바꿔 즉시 들리고, 디스크는 `Save()`("확인")에서만 바뀐다. `Revert()`("취소"·ESC)가 저장된 값을 다시 흘린다. **저장된 값이 곧 baseline**이라 스냅샷을 따로 안 든다.
  - CSV 기본값은 **한 번도 저장한 적 없는 기기**에만 보인다. 이미 플레이한 사람에게 새 기본값을 주려면 옵션 창의 "기본값" 버튼을 눌러야 한다.
- **코드에 남은 밸런스는 0이다**(2026-07-31 확인). 수치를 고쳐 달라는 요청이 오면 먼저 CSV를 본다.

### 디버그 로그·빌드

- 로그는 `GameDebug.Log/LogWarning/LogError` (WES 방식, `[Conditional("DEVELOPMENT_BUILD")]`) — 배포 빌드에선 전부 제거된다.
- 빌드 메뉴 2종: `Tools/CTC/Build Windows (개발용 — 로그·콘솔 켜짐)` → `Builds/Windows-Dev`,
  `Tools/CTC/Build Windows (배포용)` → `Builds/Windows`.
- 인게임 로그 콘솔(` 키, yasirkula)은 `DebugConsoleGate`가 **개발용 빌드에서만** 켠다 — 좌하단 팝업 버블은 끔. 로그 전문은 `Player.log`.
- 좌상단 FPS·프레임 타임(`Core/FrameStatsOverlay`, IMGUI, ConnectionUI 오른쪽)도 같은 기준 — 배포 빌드에선 안 그린다(2026-08-15).
- 프레임이 떨어지면 추측 말고 `Tools/CTC/Dev/Profile 3s → 상위 마커 덤프`(플레이 중) — 메인 스레드 self 상위 20을 `%TEMP%\ctc-profile.txt`에 쓴다. 에디터 3인(클론) 환경은 EditorLoop·vSync가 대부분이라 실제 성능은 개발용 빌드로 잰다(2026-08-16).
- **적용 범위**: **전 파일 적용 완료**(2026-07-31, [코드 개편 계획](docs/design-vault/코드%20개편%20계획.md) 1~5단계 + 잔여 3건). 예외 없이 위 규칙대로 쓴다.

### 길찾기·봇 실험실 (2026-08-18)

- **길찾기는 직접 만든 격자 A\***(`Assets/Scripts/Ai/Grid/`)다. 유니티 NavMesh는 아직 같이 굽지만
  **길 계산의 주인은 격자**다. 요새 벽은 "고양이가 부술 수 있는 칸", 입구는 "쥐 전용 문"으로 찍힌다
  (`NavGridBlocker` — 구조물 프리팹 루트에 붙어 있다).
- **봇 전용 실험실**: 메뉴 `Tools/CTC/Setup BotLab Scene (봇 전용 실험실)` → `Assets/Scenes/BotLab.unity`.
  플레이만 누르면 호스트·맵·판·봇이 자동으로 서고, 위에서 내려다보는 자유 카메라와 봇별 경로선이 뜬다
  (F1 오버레이 · F2 경로선 · F5 봇 다시 · F6 정지 · F7 배속 · Tab 봇 따라가기).
  **프리팹은 다시 안 굽는다** — `Setup Networking Scene`을 먼저 한 번 돌린 상태여야 한다.
- 길찾기가 이상하면 추측하지 말고 훅으로 캔다: `TestGetNavGridInfo`(격자 상태) ·
  `TestTracePath`(두 점 사이를 직접 돌려 본다) · `TestDumpGrid`(격자를 글자 그림으로) ·
  `TestGetNavStats`(경로계산·포기·우회율·목표미달·제자리걸음 한 줄).

## unity-scanner (에셋 YAML 탐색 — 토큰 절약)

- 프리팹/씬/.asset의 **내용 파악·검색·참조 추적**은 파일을 Read로 통째로 읽지 말고 `unity-scanner`(CLI, `%LOCALAPPDATA%\unity-scanner\unity-scanner.exe`)를 우선 사용한다.
  - 목록: `unity-scanner list -p <프로젝트경로> Assets/... --depth N`
  - 요약 읽기: `unity-scanner read -p <프로젝트경로> <에셋경로>` (prefab/scene/asset 계층·컴포넌트 요약)
  - 검색: `unity-scanner search -p <프로젝트경로> <질의>` (파일명·GameObject·컴포넌트·GUID)
  - 참조 추적: `unity-scanner refs -p <프로젝트경로> <에셋경로|GUID>`
- 예외: YAML을 **정확히 수정**해야 하거나 특정 직렬화 값(해시 등)을 원문으로 검증할 때만 Read/Grep 직접 사용.

## rtk (셸 출력 압축 — 토큰 절약, 2026-08-26)

`rtk`(CLI, `C:\Users\user\.local\bin\rtk.exe`)는 셸 **출력**을 압축하는 프록시다. unity-scanner와 역할이 겹치지 않는다 — 둘 다 쓴다.

| 대상 | 도구 |
|---|---|
| `.prefab` · `.unity` · `.asset` 내용·참조 | **unity-scanner** |
| git · pytest(wesqa) · ls/grep/find | **rtk** |
| Unity 에디터 조작·플레이모드 | **MCP** |
| YAML 정밀 수정·직렬화 값 원문 검증 | **내장 Read/Grep** |

- 자주 쓰는 것: `rtk git status` · `rtk git log -n 10` · `rtk git diff` · `rtk pytest` · `rtk ls <경로>` · `rtk grep <패턴> <경로>` · `rtk err <명령>`
- **자동 재작성 훅은 안 깔았다.** 명시적으로 `rtk`를 붙여 호출한다.
- **Unity YAML(`.prefab`·`.unity`·`.asset`)에는 rtk를 쓰지 않는다.** 잘려서가 아니라 **출력이 더 나쁘기 때문이다** — rtk는 도메인을 몰라서 `m_Script: {guid: 609eafc4...}`를 그대로 뱉지만, unity-scanner는 같은 걸 `Health`로 풀어 준다. YAML은 unity-scanner, 원문 검증은 내장 Read/Grep.
- rtk를 일반 파일에 쓸 때 주의 2가지 (2026-08-26 실측):
  - **`-n`을 명시해야 줄번호가 남는다.** 빠뜨리면 `file.cs:42` 클릭 링크를 못 만든다.
  - 줄 길이 기본 컷이 **80자**다. 긴 줄이 필요하면 `--max-len 200`.
  - 매치가 많으면 25줄만 보이고 나머지는 tee 로그로 빠진다(출력 마지막 줄에 경로가 찍힘) — 사라지는 건 아니다.
- 원문이 필요하면 `rtk proxy <명령>` (압축 없이 통과).
- 실측 절감(CTC): `ls -laR Assets/Scripts` 99% · 씬 grep(211매치) 88% · `git log -n 20` 67% · 짧은 grep 2%. **줄이 짧고 결과가 적으면 효과 없다.**

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
- **`u_play invoke`의 `args`는 쉼표 구분 문자열**(`"80,0"`)이다. JSON 배열(`[80,0]`)로 주면 **조용히 버려지고 전부 0**이 들어간다 — wesqa `InvokeBridge`와 같은 부류의 함정이다(2026-09-10 실측, 30분 소모).
- **에디터에서는 `GameDebug.Log/LogWarning`이 아무것도 안 찍는다**(`[Conditional("DEVELOPMENT_BUILD")]`). 훅이 조용히 실패해도 콘솔이 비어 있으니 **로그로 진단하지 마라**.
- **McpBridge 미응답 시** (도메인 리로드 후 에디터 미포커스): `powershell -NoProfile -File tools/focus-unity.ps1`

## 조용히 깨지는 함정 (2026-08-30 실측 — 전부 에러가 안 난다)

| # | 함정 | 증상 | 규칙 |
|---|---|---|---|
| 1 | `m_Name`을 직렬화 필드 이름으로 씀 | `UnityEngine.Object`가 같은 이름을 이미 쓴다. 에디터에서 `FindProperty("m_Name")`이 **오브젝트 이름**을 집어 배선이 빈다 | `m_Name`·`m_Enabled`·`m_GameObject`·`m_Script`는 필드명으로 쓰지 않는다 |
| 2 | 씬 빌더가 손으로 고친 에셋을 덮어씀 | 프리팹 좌표를 옮겨 놔도 씬 재생성이 매번 지운다 | 자리·개수의 주인이 프리팹이면 **있으면 안 덮어쓴다**. 다시 굽고 싶으면 프리팹을 지운다 |
| 3 | `AnimationUtility.SetEditorCurve`를 인메모리 클립에 씀 | 런타임 커브가 안 채워져 `SampleAnimation`이 아무 일도 안 한다 | 에셋 클립엔 `SetEditorCurve`, 인메모리 클립엔 `clip.SetCurve` |
| 4 | `EditorUtility.CopySerialized`로 메시 에셋을 덮음 | **디스크는 갱신되는데 렌더 버퍼는 옛 것**이 남는다. 정점 7203→10632인데 렌더 결과가 바이트까지 같았다 | GUID를 지키려면 `mesh.Clear()` 후 Mesh API로 직접 다시 채운다 |
| 5 | 한 머티리얼을 **UV 규약이 다른 메시**들이 나눠 씀 | 타일 배율을 한쪽에 맞추면 반대쪽이 단색으로 보인다. `M_Cheese`를 쐐기(0~1 정규화)와 창고(미터)가 같이 써서 쐐기만 무늬가 안 나왔다 | **UV는 미터 단위 박스 투영으로 통일한다**(`SdfMesher.PlanarUvs`). 새 메시 생성기도 같은 규약을 쓴다 |
| 6 | `new Material(null)` | `LoadAssetAtPath<Shader>`가 **대규모 리임포트 중에 null**을 준다. 예외도 경고도 없이 **마젠타 머티리얼이 구워져 프리팹에 박힌다** | 셰이더 로드를 널 검사하고, 구운 뒤 `mat.shader == null`을 한 번 더 본다. 큰 에셋을 지운 직후엔 리임포트가 끝난 뒤 생성기를 돌린다 |
| 7 | 상태를 **같은 프레임에 껐다 켬** | 감시자가 전환을 못 본다. `RequestRestart()`→`RequestStartGame()`을 붙여 부르니 `MatchManager`가 "시작됨→꺼짐"을 놓쳐 타이머를 안 채웠고, **2판부터 시작하자마자 시간 만료**로 끝났다 | 껐다 켜는 사이에 **한 프레임을 준다**(`UniTask.NextFrame`) |
| 8 | 한글 주석이 든 `.ps1` | PowerShell 5.1은 `.ps1`을 **ANSI로 읽는다**. 글자가 깨져 **파싱 자체가 실패**한다 — 문법 오류로만 보여 원인이 안 보인다 | `.ps1`은 **UTF-8 BOM**으로 저장한다. 만들고 나면 반드시 한 번 실행해 본다 |
| 9 | **MPPM 클론을 프로세스로 죽임** | MPPM이 「재시작할까?」 **모달**을 띄우고 그 모달이 **에디터 업데이트 루프를 멈춘다** — MCP 호출이 전부 타임아웃해 「클론이 파이프를 가로챘다」와 증상이 똑같아 보인다. 2026-09-11엔 그 상태에서 자식 파이프가 멈춰 **유니티가 fatal assert로 자진 종료**했다 | 끄려면 메뉴 `Tools/CTC/Dev/MPPM 1인 …`(MPPM API로 비활성화 — 모달이 안 뜬다). 이미 떴으면 `tools/dismiss-mppm-modal.ps1`이 **에디터가 멈춰 있어도 밖에서** 닫는다 |
| 10 | **스팀 클라이언트를 켜 둔 채 유니티 작업** | Facepunch 트랜스포트 때문에 에디터가 `steamclient64.dll`·`gameoverlayrenderer64.dll`(오버레이)을 물고, 종료·서브시스템 정리 경로에서 **크래시한다**(2026-09-11 실측) | **유니티 작업 중엔 스팀을 끈다.** 웹으로 `partner.steamgames.com`을 보는 건 상관없다 — **스팀 클라이언트 실행**이 문제다 |
| 11 | **이름 없는 씬이 열린 채 EditMode 테스트 실행** | 테스트 러너가 실행 전에 「수정된 씬을 저장하라」고 요구하는데, 열린 씬에 경로가 없으면 유니티가 **네이티브 파일 대화상자**를 띄운다. 그게 에디터 업데이트 루프를 멈춰 MCP가 전멸한다 — **「브리지가 죽었다」와 증상이 똑같다.** 2026-09-12에 세 번 물렸다 | 이미 `Tools/CTC/Run EditMode Tests`가 시작 전에 **경로 없는 씬의 수정 표시만** 지운다(저장 안 함, 경로 있는 씬은 안 건드림). 그래도 뜨면 `tools/dismiss-save-scene.ps1`이 밖에서 닫는다(`-Watch N`으로 상주) |
| 12 | **MCP가 씬을 말없이 저장하는 경로 4개** | `u_editor_scene`(`McpBridgeScene.cs:32`) · 프리팹 인스턴스화(`McpBridgeInstantiate.cs:77`) · `u_play`(`McpBridgePlayMode.cs:23`) · 씬 오브젝트 조작. 이름 없는 씬이면 전부 함정 11로 간다 | **이름 없는 씬이 열려 있는 동안 이 넷을 쓰지 않는다.** 에셋만 보는 일은 `AssetDatabase`로 되고, 미리보기는 `PreviewRenderUtility`로 씬 없이 된다 |
| 13 | **`LoadAllAssetsAtPath`로 FBX 클립 이름 훑기** | 유니티 내부의 **`__preview__` 복제 클립**까지 딸려 오는데, 그쪽은 임포터가 매핑한 이름이 아니라 **FBX 원본 테이크 이름**(`__preview__Rig\|Walk`)을 달고 있다. 이름으로 검사하면 실제와 다른 것을 본다 | 이름이 `__preview__`로 시작하는 것과 `HideFlags.HideInHierarchy`인 것을 **걸러낸다**. 이름을 정확히 아는 경우엔 `ClipParityProbe.FindClip`처럼 완전 일치로 찾으면 안 걸린다 |
| 14 | **`PreviewRenderUtility`에서 스킨드 메시 연속 렌더** | 한 동기 루프 안에서 `Render()`를 여러 번 불러도 `SkinnedMeshRenderer`가 **다시 스키닝되지 않는다**(`updateWhenOffscreen`을 켜도). 본은 실제로 움직이는데 **프레임이 전부 바이트까지 같게** 나온다 | 프레임마다 `BakeMesh`로 정적 메시에 구워 그린다(`Assets/Scripts/Editor/ClipPreviewRender.cs`가 그렇게 한다) |
| 15 | **블렌더 `-P` 스크립트에서 `SystemExit`가 아닌 예외** | 트레이스백은 찍히는데 블렌더가 **종료 코드 0**으로 끝난다. FBX 경로 오타도 저장 실패도 자동화 입장에선 **성공**이다 | `main()`을 감싸 일반 예외를 `SystemExit`로 올린다(`tools/blender/*.py`가 그렇게 한다). 그리고 **출력이 아니라 `echo $?`로 판정한다** |
| 16 | **`u_editor_asset(refresh)` 직후에 테스트 메뉴 실행** | refresh가 성공을 돌려줘도 컴파일이 **아직 큐에 있을 수 있다.** 그러면 테스트는 **직전 어셈블리**를 돌고, 새로 쓴 테스트가 실행조차 안 됐는데 「통과」로 보인다(2026-09-13에 849를 내 코드의 결과로 착각했다) | 통과 **수가 늘었는지**를 같이 본다. 컴파일이 끝난 뒤인지 확인하려면 `Logs/Editor.log`에서 리로드 완료 줄을 본다 |
| 17 | **리플렉션 QA 훅의 이름 중복** | `InvokeBridge`는 **이름만 보고** 메서드를 고른다. 같은 이름의 오버로드를 만들면 어느 쪽이 불릴지 모른다 — 컴파일은 통과한다(2026-09-13에 `TestGetTechLevel`로 그럴 뻔했다) | 훅을 새로 만들기 전에 `grep -rn "TestXxx" Assets/Scripts/Testing/`으로 **이미 있는지 본다** |
| 18 | **MCP 브리지가 컴파일마다 죽는다** | 도메인 리로드 뒤 `McpBridge`가 다시 안 켜진다. 에디터는 멀쩡한데(`Responding`·에러 0) MCP가 전부 타임아웃한다. 파이프가 **남아 있는 경우도** 있어 존재 여부로는 판정이 안 된다 | `Logs/Editor.log`의 `McpBridge` 마지막 줄이 「중지됨」이면 이 상태다. 포커스 → 재컴파일 강제 → 그래도 안 되면 **에디터 재시작**(2026-09-13에 두 번 다 재시작으로만 풀렸다). **컴파일이 필요한 편집은 모아서 한 번에** 하는 것이 가장 아낀다 |

애니메이션·이펙트를 다룰 때 추가로 겪은 것:
- **커브·마스크 경로는 Animator 기준**이다. Animator가 자식(`Model`)에 있으면 그 접두를 뺀다. 틀려도 에러가 없다.
- `AvatarMask.AddTransformPath`는 **계층 루트 기준** 경로를 넣는다 → 기준이 어긋나 마스크가 0개를 켠다. 경로를 직접 계산해 넣는다.
- 본을 코드로 직접 돌려도 **에디터에서는 스킨드 메시가 다시 스키닝되지 않는다.** 클립을 만들어 샘플링해야 반영된다.
- 씬 뷰 스크린샷(`u_editor_sceneview`)은 갱신이 안 붙어 같은 그림을 반복해 준다. 확인이 필요하면 **임시 카메라로 직접 렌더**한다.

### 블렌더로 애니메이션 클립 만들기 (2026-09-12)

리그는 **Generic**이라 유니티가 뼈를 **트랜스폼 경로 문자열**로 찾는다(`Rig/root/body/head/ear.L`).
리타깃이 없어서 이름이 한 글자만 어긋나도 **에러 없이 그 커브가 죽는다.**

| | |
|---|---|
| 도구 | Blender **5.2.1 LTS**. 버전을 올리면 **먼저 파이썬 API를 재라** — `bpy.ops.export_scene.fbx.get_rna_type()` |
| 작업 파일 | `art/blender/*.blend` — **`Assets/` 밖.** 안에 넣으면 유니티가 블렌더를 직접 실행해 변환하고, 블렌더가 없는 기계에서 프로젝트가 안 열린다 |
| 들이기 | `blender -b --factory-startup -P tools/blender/import_pack.py -- <in.fbx> <out.blend>` |
| 내보내기 | `blender -b <blend> -P tools/blender/export_clips.py -- <out.fbx>` |
| **검증** | `Tools/CTC/Dev/Probe Clip Parity` — **보고서의 첫 절이 병명이다.** 값부터 보지 마라 |
| 눈으로 | `Tools/CTC/Dev/클립 미리보기 렌더` — 오프스크린 PNG. 씬을 안 건드린다 |

**설정을 고쳤으면 반드시 자를 다시 돌린다.** 아래 넷은 전부 **에러 없이** 어긋난다:

| 설정 | 틀리면 |
|---|---|
| `apply_scale_options="FBX_SCALE_NONE"` | `ALL`이면 뼈의 **월드 스케일이 100배** 어긋난다. 합성 트랜스폼은 같아서 **뷰어로는 멀쩡해 보이고**, 팩 프리팹에 물려야 스키닝이 1/100로 쪼그라든다 |
| `add_leaf_bones=False` | 없던 말단 뼈가 생겨 경로가 어긋난다 |
| `bake_anim_simplify_factor=0.0` | 기본값 1.0이 키를 솎아내 값이 달라진다 |
| `ignore_leaf_bones=False` (들일 때) | **켜면 진짜 뼈 `tail.02`가 사라진다.** 팩마다 다르니 재 보고 정한다 |

유니티 쪽 `.meta`에 손으로 넣은 것이 둘 있다 — `preserveHierarchy: 1`과 `clipAnimations`의
`takeName: Rig|…` 매핑(블렌더가 테이크 이름을 `{오브젝트}|{액션}`으로 강제해서다).
**메타가 새로 생성되면 조용히 되돌아간다.** `RoundTrippedClip_IsNamedWalk_NotRigPipeWalk`가
그때 빨개지라고 있는 것이다.

**바닥 판정은 뼈가 아니라 메시로 한다**(2026-09-12~13에 두 번 물렸다). 발 메시는 발목뼈 끝보다
**37mm 더 아래**까지 있고 꼬리 메시는 뼈보다 **19mm 굵다** — 뼈 좌표로는 여유가 있어 보이는 자리가
실제로는 잠긴다. 기준선은 팩 자신이 정해 준다: `Walk` −9.1 · `Bounce` −8.7 · `Run` −4.7 mm.
자작 클립은 `Shoot` +0.4 · `Dash` −0.7이다.

**각도는 눈대중으로 잡지 말고 풀어라.** 그런데 **단조롭지 않을 수 있다** — `Dash`의 앞발은
0도 −21mm / 20도 −30mm(최악) / 60도 +4.6mm라, 최저점이 가슴↔앞발을 옮겨 다녀 이분법이
「해 없음」을 뱉었다. **훑어 보고서야 알았다**(`scratchpad/sweep.py` 부류).

자작 클립 셋: `Shoot`·`ShootDraw`(2026-09-12) · `Dash`(2026-09-13).
원본은 `tools/blender/clips/*.py`이고 각도마다 왜 그 값인지가 주석에 있다.
새 클립을 `AnimationStateLogic.MouseOnly`에 넣었으면 **`Tools/CTC/Dev/Bake Mouse Gun`을 돌려야**
AC_Mouse에 상태가 생긴다 — 안 돌리면 `CrossFadeInFixedTime`이 에러 없이 아무 일도 안 한다.

설계·계획: `docs/superpowers/specs/2026-09-12-blender-animation-pipeline-design.md`

### MPPM 클론이 MCP 브리지를 가로챈다 (2026-08-30)

`\\.\pipe\mcp-unity-bridge`는 이름이 하나뿐이다. **MPPM 클론도 같은 프로젝트 코드를 돌리므로
`McpBridge`가 그 이름을 먼저 잡을 수 있다.** 그러면 파이프는 존재하는데 메인 에디터가 응답을
못 해서, `u_console` 같은 호출이 전부 타임아웃한다(포커스를 아무리 줘도 안 살아난다).

- 확인: `[System.IO.Directory]::GetFiles("\\.\pipe\") | ? { $_ -match 'mcp' }` — 파이프가 있는데 응답이 없으면 이 경우다.
- 조치: 클론(`Player N` 제목의 Unity 프로세스)을 닫는다. 메뉴로 다시 띄우면 된다.

### 스팀이 켜져 있으면 MPPM 3인 테스트가 안 된다 (2026-08-30)

스팀이 살아 있으면 호스트는 **Facepunch 트랜스포트**로 방을 연다. 반면 클론은 **같은 계정 중복**이라
스팀 초기화가 실패해 UTP로 남는다 — **트랜스포트가 서로 달라 클론이 못 붙는다.**

여러 명 로컬 테스트를 하려면 **스팀을 꺼야 한다.** 그러면 양쪽 다 UTP로 붙는다.

### 에디터가 UDP 포트를 안 놓는다 (2026-08-30)

플레이를 끝내도 에디터 프로세스가 7788을 물고 있는 일이 잦다. 그래서 이 프로젝트에는
`HostPortFallback`이 있다 — 빈 포트를 찾아 올라가고 그 번호를 공유 파일에 적어 클론이 읽는다.

**로컬 호스트를 여는 새 경로를 만들면 반드시 그걸 태운다.** `nm.StartHost()`를 직접 부르면
포트가 막혔을 때 그대로 실패해 방이 안 열린다(로비의 에디터 경로가 실제로 그랬다).

### MCP 브리지가 안 붙을 때 (2026-08-30)

`tools/focus-unity.ps1`이 창을 못 찾아 `UNITY_NOT_FOUND`로 빠지던 문제를 고쳤다. **컴파일·도메인 리로드
중에는 Unity 창 제목이 비거나 바뀐다** — 하필 포커스가 가장 필요한 그 순간이다. 그러면 에디터가 안 돌아
sentinel을 기다리는 루프가 통째로 타임아웃한다.

- 제목 필터를 완화하고(CTC 우선, 없으면 남은 것), `SetForegroundWindow` 대신 **`WScript.Shell.AppActivate`** 를 먼저 쓴다.
- 출력에 `activated=True/False`가 찍히니 실패를 알 수 있다.

---

## CTC 코드 제약

- **입력 가드**: 새 게임플레이 입력을 만들면 `if (Core.UiFocus.BlocksGameplay) return;`을 넣는다 — 채팅 타이핑·옵션 창 중에 새면 안 된다.
- **ESC는 `Core/EscapeRouter`가 단독으로 읽는다.** 다른 컴포넌트에서 `escapeKey`를 읽지 않는다(같은 프레임 경합). 새 모달은 라우터의 우선순위 사슬에 끼운다.
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
- **wesqa E2E 도구 — 이식 완료, 실사용 중**(2026-07-12 `e8fa264`). C# 인게임 서버 `Assets/WesQA/`(플레이 진입 시 포트 `5001+MPPM인덱스`로 기동 — **에디터·개발용 빌드에서만**, `DEVELOPMENT_BUILD` 가드 2026-08-15) + Python 클라이언트 `tools/wesqa/`.
  - MPPM 3인 실플레이 QA: `qa_v36.py`(24항목·호스트=고양이) · `qa_v37.py`(24항목·호스트=쥐) · `qa_v38.py`(15항목·벽감·호스트=고양이).
    항목 수는 각 스위트 머리의 `EXPECTED` 상수가 원본이고 `tests/test_suite_contract.py`가 실제 구현과 대조한다 — 여기 적힌 수는 참고다.
    **MonoBehaviour를 재배치했으면 이걸 돌려야 한다** — EditMode는 순수 로직만 덮는다.
  - **Model 값만 보는 QA는 배선 누락을 못 잡는다**(2026-08-09 실측: 볼륨 설정은 바뀌는데 소리는 그대로였다).
    들리는 소리는 `TestGetAudioVolume`으로 **실제 `AudioSource.volume`**을, 보이는 것은 실제 오브젝트 수를 센다.
  - 진입점은 `TestManager` 하나다. wesqa가 `Instance.<method>` 리플렉션으로 부르므로 **타입을 쪼개거나 Editor 전용 어셈블리로 옮기면 안 된다**(실측으로 QA 전멸).
  - **채점 규약(2026-09-08)**: 스위트는 실패·미실행·중단에 **0이 아닌 종료 코드**를 낸다(`sys.exit(main())`).
    조건부로 안 도는 항목은 `skip()`으로 **결과에 남긴다** — 안 남는 건너뛰기는 통과가 아니다.
    `python -m pytest`(게임 불필요)가 이 규약과 훅 이름·인자를 정적으로 검사한다.
  - **훅 인자는 부르기 전에 대조한다**(`wesqa/hooks.py`). `InvokeBridge`가 모르는 인자를 조용히 버리고
    빈 자리에 `0`(= 호스트 clientId)을 넣기 때문이다 — 2026-08-09 「엉뚱한 쥐를 가둔」 사고의 원인.
    이름이 안 맞으면 `HookSignatureError`로 죽는다. 훅 이름을 바꾸면 파이썬도 같이 고쳐야 한다.

---

**Note:** These are baseline rules with standard priority. They may be adjusted or overridden based on specific project requirements or context.
