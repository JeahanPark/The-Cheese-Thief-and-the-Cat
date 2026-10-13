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
| 19 | **팩 캐릭터에 셰이프키를 쓸 때 LOD·기본 표정을 놓침** | 팩 프리팹은 **LODGroup + `Mesh_LOD0..3`**이다. 첫 렌더러만 굴리면 **멀어졌을 때 눈이 멈춘다.** 그리고 팩 컨트롤러의 Shapekey 레이어가 기본으로 `Eyes_Annoyed`를 틀어 **`eyes.annoyed`를 매 프레임 100으로 쓴다** — 지금 게임의 기본 얼굴이 그것이라, 「안 쓰는 표정」이라며 지우면 모든 캐릭터 얼굴이 조용히 바뀐다(2026-09-13 둘 다 물릴 뻔했다). 팩 FBX 메시 이름도 LOD 넷 다 그냥 `"Mesh"`라 이름으로 파일을 만들면 겹친다 | 렌더러는 `GetComponentsInChildren`으로 **전부** 굴리고, 셰이프키는 버리지 말고 담는다(`EyeSplitMeshBaker`). 메시는 이름이 아니라 **원본 프리팹의 같은 렌더러**로 찾는다 |
| 20 | **사격·판정 레이가 트리거에 막힘** | 맵 옷(`MapDresser`)의 팩 가구가 카메라 페이더용 **트리거 콜라이더**를 달고 온다. `RaycastAll` 기본값은 트리거도 맞혀 **쥐 총알이 통 앞에서 사라졌다**(qa_v41 「쥐끼리」 재현, 2026-09-14) | 판정 레이는 `QueryTriggerInteraction.Ignore`. 막히면 추측 말고 `TestRaycastBetween(from, to)`로 무엇에 맞았는지 본다 |
| 21 | **투명한 Image에 `Outline`을 닮** | `Outline`은 테두리가 아니라 **그래픽을 네 방향으로 밀어 찍은 복사본**이다. 바탕이 투명하면 가려 주는 원본이 없어 **칸 전체가 선 색으로 칠해진다**(상점 상세 사다리에서 걸릴 뻔했다, 2026-09-19) | 테두리를 달 칸의 바탕은 **불투명**하게 — 뒤와 같은 색을 불투명으로 칠한다(`ShopPopup.DETAIL_BG`) |
| 22 | **생성기가 머티리얼 에셋을 지웠다 다시 만듦**(`DeleteAsset` → `CreateAsset`) | GUID는 그대로라 에디터 화면·YAML은 멀쩡한데, **그 실행에서 다시 굽지 않은 에셋**(대기실 몸 `RoomMouse` 등)의 가져오기 결과에 잠깐 비었던 참조가 굳어 **빌드에서만 머티리얼 칸이 비어 마젠타**로 떴다(2026-09-19). 재가져오기로도 안 풀렸고 그 씬 생성 메뉴를 다시 돌려서야 풀렸다 | 있는 머티리얼은 **그 객체를 고쳐 쓴다**(`EditorAssetUtil.CreateUrpMaterial`). 빌드에서만 깨지는 그림은 추측 말고 개발 빌드에 `TestDumpRenderers(이름)`로 머티리얼·셰이더를 찍는다 |
| 23 | **`NetworkManager.Shutdown()` 직후에 `NetworkConfig.NetworkTransport`를 바꿈** | `Shutdown()`은 플래그만 세우고 실제 정리(`ShutdownInternal` → `transport.Shutdown()`·`OnTransportEvent -=`)는 **다음 네트워크 업데이트**에 그 순간의 트랜스포트로 돈다. 스팀 방을 나가며 바로 UTP로 되돌리니 NGO가 **UTP를 내리고 Facepunch는 산 채로** 남았다 — 릴레이 소켓·이벤트 구독이 누적돼 다음 스팀 세션은 이벤트가 두 번씩 들어가고, 재호스트는 리슨 소켓이 「이미 있다」로 실패하고, 방장이 나가도 손님이 안 끊겼다(2026-09-20, 「방 만들기→나가기→빠른 참가→에러」). 에러도 경고도 없다 | 트랜스포트 교체는 **`OnClientStopped`/`OnServerStopped` 뒤에**(`SteamLobbyManager.OnNetworkStopped`). 세션이 도는 동안엔 `NetworkConfig.NetworkTransport`를 건드리지 않는다. 트랜스포트 `Shutdown()`은 관리자를 **null로 비운다**(Facepunch 패치) |
| 24 | **커스텀 셰이더에 카메라 가림막 페이드가 안 먹음** | `OccluderFader`는 URP Lit의 속성(`_Surface`·`_SrcBlend`·`_DstBlend`·`_ZWrite`·`_BaseColor.a`)으로 반투명을 켠다. 맵 전용 `CTC/MapTriplanar`엔 그게 없어 **벽·선반 판벽이 흐린 목록엔 들어가는데 화면엔 불투명**했다 — 캐릭터를 가린 채였고, 카메라가 벽 속으로 들어가 뒷면이 안 그려질 때만 비쳐 보여 「부딪쳐야만 투명해진다」로 보였다(2026-09-20 사용자 보고). 에러 없음 | 가림막이 될 물체의 셰이더엔 저 넷을 `[HideInInspector]`로 두고 패스에 `Blend [_SrcBlend] [_DstBlend]`·`ZWrite [_ZWrite]`, 출력 알파는 `_BaseColor.a`. 페이더는 `_Surface`가 없는 셰이더는 **건드리지 않는다**. 확인은 개발 빌드에서 `TestStageShelfOcclusion` → `TestProbeOcclusion`(흐린 것마다 셰이더·큐·알파가 찍힌다) |

애니메이션·이펙트를 다룰 때 추가로 겪은 것:
- **커브·마스크 경로는 Animator 기준**이다. Animator가 자식(`Model`)에 있으면 그 접두를 뺀다. 틀려도 에러가 없다.
- `AvatarMask.AddTransformPath`는 **계층 루트 기준** 경로를 넣는다 → 기준이 어긋나 마스크가 0개를 켠다. 경로를 직접 계산해 넣는다.
- 본을 코드로 직접 돌려도 **에디터에서는 스킨드 메시가 다시 스키닝되지 않는다.** 클립을 만들어 샘플링해야 반영된다.
- 씬 뷰 스크린샷(`u_editor_sceneview`)은 갱신이 안 붙어 같은 그림을 반복해 준다. 확인이 필요하면 **임시 카메라로 직접 렌더**한다.

### 대기실 앞단 — R.E.P.O.식 진입과 접속 관문 (2026-09-14)

메인(Intro)에서 **방 만들기(비밀번호 → 친구 전용) · 공개 게임(빠른 참가 / 서버 목록) · 친구 방 참가**로 바로 대기실에 간다.
Lobby 씬은 이제 「서버 목록」이다. 설계: `docs/superpowers/specs/2026-09-14-frontdoor-repo-style-design.md`

- **모든 접속이 `ConnectionGate`(NGO 승인)를 지난다** — `NetworkManager.OnInstantiated`에서 걸리므로 프리팹을 다시 굽지 않는다.
  거절 코드 `CLOSED`·`FULL`·`PW_REQUIRED`·`PW_WRONG`. **잠금 기본값은 열림**이라 `Game.unity` 단독(wesqa)은 그대로 붙는다.
  대기실이 서면 열리고(`RoomDirector.Start`) 시작하면 닫힌다.
- 접속 절차는 `SessionStarter` 한 곳(Intro·Lobby 공용). QA는 팝업 없이 `TestFrontDoorHost` / `TestFrontDoorJoin` / `TestGetGateState`, 스위트 `qa_v56`.
- **메인 「방 만들기」 방은 서버 목록에 안 뜬다 — 설계다**(스팀: `RequestLobbyList`는 Public·Invisible이면서 joinable인 방만 준다). 목록에 뜨는 건 「서버 목록 → 새로 만들기」·「빠른 참가」가 만든 공개 방뿐.
  친구는 초대·친구 목록 「게임 참가」로 온다 — 게임이 **꺼져 있으면** `+connect_lobby`, **켜져 있으면** `OnGameRichPresenceJoinRequested`(2026-09-20에 받기 시작 — 그 전엔 켜진 게임에서 조용히 죽었다).
- **스팀 방 나가기 순서**: `nm.Shutdown()` → `LeaveLobby()`(스팀 방만 즉시) → NGO가 멈춘 뒤 UTP 복원(함정 23). 목록은 방금 나온 내 방을 30초 숨긴다(`SteamLobbyLogic.IsRecentlyLeft`) — 스팀이 빈 방을 지우기 전에 「빠른 참가」가 자기 시체 방에 들어가려다 `DoesntExist`였다.
  버전 필터는 서버가 아니라 `RefreshLobbies`가 건다 — 다른 버전 방은 `HiddenByVersion`으로 세어 서버 목록 빈 화면에 「다른 버전의 방 N개는 숨겼습니다」를 띄운다(빌드가 어긋난 두 사람이 서로 안 보이는 걸 알 수 있게).
- **임시: 에디터에서는 `SessionStarter`가 스팀 경로를 안 탄다**(`#if UNITY_EDITOR` — 사용자 지시). 스팀 클라이언트가 떠 있으면 호스트만 Facepunch로 열려 클론(UTP)이 못 붙었다. 스팀 경로 검증은 개발용 빌드로 하고, 실측이 끝나면 분기를 걷는다. (스팀 클라이언트 자체는 여전히 함정 10 — 유니티 작업 중엔 끈다.)
- **접속 대기 로딩창**(2026-09-19)은 **게임 씬에 없다** — `Resources/UI/LoadingOverlay` 프리팹(씬 빌더가 굽는다)이 대기실 추첨 연출의
  페이드 **시작**(3.8초)에 전원 화면에서 떠(`LoadingScreen.Begin`, DontDestroyOnLoad) 씬을 건너오고, 게임 씬의 판이 서면(최소 2초) 걷혀 사라진다.
  게임 씬 캔버스에 붙였던 첫 판은 씬 로딩의 검은 화면을 못 덮었고 로컬에선 0.4초 만에 걷혀 한 번도 안 보였다. QA `qa_v61`(Intro에서 3인).

### 맵 분위기 — 전등·빛줄기·밤 하늘·후보정 (2026-09-14)

- 재료는 `Resources/Map/MapAtmosphere.asset` 한 벌(`Tools/CTC/Map/Build Atmosphere`가 굽는다). 런타임 `MapAtmosphere`를 `MapDresser`가 부른다 — **씬 재생성 없이** 붙는다.
- **밤 판정은 `RenderSettings.fog`** — BotLab(안개 끔·낮 조명)은 자동으로 빠진다.
- 색·밝기는 플레이 중 `%TEMP%/ctc-atmo.json` + `Tools/CTC/Dev/Map Atmosphere 수치 적용`으로 맞춘다. 확정값은 `MapAtmosphereBuilder` 상수로 옮긴다(재생성하면 덮인다).
- **맵 셰이더에 점광원 루프를 쓸 땐 `_ADDITIONAL_LIGHTS`만 보지 마라** — PC 렌더러는 Forward+라 그 키워드가 안 켜진다(`_CLUSTER_LIGHT_LOOP`/`_FORWARD_PLUS`도 본다). `CTC_MapTriplanar`가 이걸로 빛웅덩이를 통째로 못 받고 있었다.
- 셰이더를 고친 직후 스크린샷이 하늘색이면 **비동기 셰이더 컴파일 자리표시**다 — 한 번 더 찍는다.
- **천장**(2026-09-14): `MapCeiling`이 6m 나무 판·들보·장선·둘레 판벽·닫힌 채광창·매달린 식료품을 짓는다(재료 `Tools/CTC/Map/Build Ceiling`, 자리 `MapCeilingLogic`). **콜라이더 없음 · 그림자 안 드리움** — 달빛(방향광)이 판에 막히면 맵이 꺼멓다. **천장에 구멍은 없다**(사용자: 뚫린 것보다 덮는 게 중요) — 달빛 자리 칸은 판 밑에 닫힌 채광창(푸른 Unlit 유리 + 창살).
  카메라 최고 높이 ≈4m(거리 3 · 피치 75°) 아래로 내려오는 천장 소품을 만들지 마라(`MapCeilingLogic.ITEM_MAX_HEIGHT` 테스트).
  아래를 향한 면은 밤 환경광이 0이라 새까맣다 — `CTC/MapTriplanar._FillLight`로 채운다(기본 0).

### 기술 이펙트 (2026-09-14)

- 입구는 `Fx.FxPlayer.PlayAt(FxId, pos)` 하나(풀 사용). 프리팹 22개는 `Tools/CTC/Fx/Build Skill Effects`가 코드로 굽는 생성물 → `Resources/Fx/FxCatalog`.
- **이미 전 클라가 도는 `*FxRpc`·`OnNetworkSpawn`에만 붙인다.** 서버 전용 판정 자리에 붙이면 호스트 화면에서만 뜬다.
- **Rpc 안에서 같은 틱에 바꾼 NetworkVariable을 읽지 마라** — Rpc가 먼저 도착해 옛 값을 읽는다(전직 빛이 안 떴다). 필요한 값은 인자로 넘긴다.
- §11: **한 번 터지고 끝난다.** 켜져 있는 동안 도는 오라·패시브 이펙트는 정보를 흘려 넣지 않는다.
- 이펙트는 0.1~0.3초라 MCP 스크린샷 왕복으로는 못 잡는다 — `TestSetTimeScale 0.1` + `TestCaptureGameView`로 찍고, 네트워크 전파는 `qa_v57`(다른 인스턴스에서 `TestGetFxCounts`)로 잰다.

### 깡패 쥐 모습 (2026-09-14)

- 켜지는 조건은 **2페이즈 && 깡패 편**(`GangLookLogic.Active`) — 표를 쥔 순간이 아니다. `GangLook`이 복제값 둘(직업·페이즈)을 각자 구독한다(통신 0).
- 몽둥이는 **같은 공격 버튼**(`MousePopgun.FireRpc` → `ServerClub`). 수치는 CSV `GangClub*` — 컬럼을 건드리면 **Convert CSV를 먼저** 돌려야 컴파일된다.
- **깡패판의 깡패는 총을 못 쓴다** — 쏘기는 몽둥이로 바뀌고, 총·후추 탄 구매도 `TechDeny.GangNoGun`으로 막힌다(`ShopController.CanBuyNow`, 조건은 `MousePopgun.ClubActive` 하나). 평소 이동은 두 발(`BipedIdle/Walk/Run`)이다.
- **변신**(`GangTransform`, 2초): 깡패판이 **지금 막** 열리면 네 발 → 벌떡 → 뒷주머니에서 몽둥이 → 모자 쓰기. 몽둥이·모자는 총처럼 알파로 나타난다 —
  시간표는 `GangTransformLogic` 하나(런타임 `GangLookVisual`과 GIF 미리보기가 같이 본다). 늦게 들어온 사람·구독 순간의 현재 값은 연출 없이 바로(`GangLook.m_PhaseBound`).
  소품 머티리얼은 **반투명으로 굽는다** — 불투명이면 URP가 알파를 조용히 무시한다.
- **변신 2초 동안 이동·공격·대시·액티브·임명을 막는다**(`GangTransformLock` — 판정은 `GangLookVisual.Transforming`, 서버 Rpc 입구도 같은 값). 하던 행동은 **결과를 성공시키고** 변신한다 — 시간을 들여 끝나는 행동은 `ICompletableAction.CompleteNow`(대시: 남은 거리를 한 번에), 총은 즉시 넣는다.
- 앞발이 짧아 **머리 꼭대기·엉덩이에 닿지 않는다**(150도는 가슴 속으로 파묻힌다). 모자는 고개를 숙여 앞발과 만나게 했다.
- GIF로 보려면 `Tools/CTC/Dev/깡패 쥐 클립 GIF 내보내기`(몽둥이·모자·눈썹을 입혀 굽는다, 변신은 알파 시간표까지). 두 발 판의 바닥은 **모델 원점**이다 — 첫 프레임 최저점(엉덩이)으로 잡으면 딛은 발이 떠 보인다.
- 두 발 클립 `BipedIdle/Walk/Run`·`ClubSwing`·`GangTransform`은 **2차 서기**다 — 몸 −42(뛰기 −36), **뒷다리 허벅지 각 = 몸 각**(다리가 세로로 선다),
  **몸 높이는 root 들어올림으로 딛은 발에 맞춰 푼다**(`tools/blender/clips/_biped.py`, 약 83mm — 손으로 적지 않는다). 1차(몸 −48 · 허벅지 +25)는 다리가 배 앞으로 뻗어 「앉은 쥐」였고 엉덩이가 −17mm 잠겼다.
  네 발 구간(변신 1~6)은 들어올림을 풀지 않고 0에 둔다 — 뒷발만 보고 풀면 웅크릴 때 앞발이 바닥을 뚫는다.
- 소품은 `Tools/CTC/Dev/Bake Gang Props` — 모자는 **BipedIdle 자세로 뼈를 돌려 놓고** 두 귀 뼈로 잰다. 네 발 쉬는 자세로 얹으면 두 발에서 머리 속으로 눕는다.
- 화난 눈썹(`GangBrow.L/R`)은 팩에 「화남」 셰이프키가 없어 **소품**이다. 눈 자리 = `eyes.shrink`가 움직이는 정점, 눈이 보는 쪽 = 그 정점 법선 평균.
  **눈 표면에 그대로 누이면 안 된다** — 쥐 눈은 옆을 봐서 앞옆에서 막대가 곧추선다. 판을 얼굴 앞쪽으로 45% 돌렸다.
- 두 발 클립에서 앞발을 어깨 앞(40~60도)으로 들면 **앞발이 눈 원판을 뚫는다**(몸을 48도 젖혀서) — 몽둥이 발은 허리 옆(10, -45), 빈 발은 30 이하.
- `TestForceGangPhase`는 시계도 채운다 — 예전엔 0이라 다음 틱에 시간 초과로 판이 끝나 결과 패널이 화면을 덮었다. QA는 `qa_v58`(호스트=쥐, **새 판에서** — 이미 2페이즈인 판이면 ①이 실패한다).

### 용사 쥐 — 칼·스턴·무적 (2026-09-19)

기획 `docs/superpowers/specs/2026-09-19-hero-sword-design.md`(§10에 구현하며 바뀐 것). 수치는 CSV `HeroSword*`·`StunSeconds`·`HeroInvincible*`.

- **칼은 쓸 때만 보인다**(변신 `HeroDraw` 1.5초 · 휘두르기 `SwordSwing` 0.58초) — 네 발로 걸을 때 앞발에 칼을 들면 걸음마다 바닥·볼을 뚫는다.
- **칼 방향은 클립이 아니라 코드가 준다** — 리그에 손목이 없다. 칼은 `leg.F.R` 끝을 따라가고 `HeroLook.LateUpdate`가 캐릭터(Model) 기준 방향을 덮어쓴다.
  키는 `HeroSwordVisualLogic`·`HeroTransformLogic`과 블렌더 원본(`tools/blender/clips/sword_swing.py`·`hero_draw.py`의 `SWORD`)이 **같은 숫자**다 — 하나를 고치면 둘 다.
- **스턴(`Stun`)은 쥐·고양이·강아지 공통**이고 잠금은 `ActionLock` 하나(깡패 변신 || 스턴 || 용사 변신)가 본다. `Stunned` 자세는 **두 컨트롤러가 다 갖는다**
  (`AnimationStateLogic.CustomShared` — `Bake Mouse Gun`·`Bake Cat Clip States`가 각자의 FBX에서 넣는다). 맞은 사람 화면 가장자리는 `StunVignette`가 하얗게 세운다.
- 무적 표시는 **전용 림 셰이더 `CTC/HeroRim`**(깊이 검사 켬 — 빛 실루엣처럼 벽 너머로 새지 않는다)를 몸 SMR 머티리얼 칸 끝에 켜 있는 동안만 덧붙인다(`HeroInvincibleVisual`).
- 소품은 `Tools/CTC/Dev/Bake Hero Props (칼)`(씬 재생성이 같이 부른다). 눈으로는 `Tools/CTC/Dev/용사 쥐 클립 GIF 내보내기`. QA `qa_v60`(호스트=쥐, **새 판에서**).

### 상점 부스 — 쥐 점원과 철문 (2026-09-19)

기획 `docs/superpowers/specs/2026-09-19-shop-booth-design.md`. 수치는 CSV `ShopShutter*`(닫힘 5m · 살금살금 2.5m · 열림 6.5m 밖 1.5초).

- **고양이가 오면 철문이 닫히고, 닫힌 동안 그 상점에서는 못 산다** — 판정은 `ShopSpot.AnyOpenContains`(구매·팝업·봇). 「원 안인가」만 볼 땐 `AnyContains`.
- **부스 몸통은 막힌다**(콜라이더 + 격자 `Solid` 칸) — 상점으로 보낼 땐 가운데가 아니라 `ShopSpot.FrontPoint`(창구 앞 1.3m)로. 워프 훅·봇이 그렇게 한다.
- 부스는 `Tools/CTC/Dev/Bake Shop Booth (상점 부스)`가 상점 자리 프리팹에 굽는다(씬 재생성은 철문이 없을 때만 다시 굽는다). 철문 소리는 **임시로 요새 설치음**이다.
- 상점 근처에서 사는 QA는 **고양이를 먼저 치운다**(`TestTeleportNearShopOf(_clientId, 14)`) — 안 치우면 철문이 닫혀 구매가 거절된다. QA `qa_v62`(호스트=쥐).
- **값이 빠졌나를 잴 땐 잔액만 보지 않는다** — 창구 앞이 치즈 지점 채굴 반경 안인 판이 있다. 쓴 돈 = 잔액 차이 + 누적 획득(`TestGetEarnedOf`) 차이(`qa_v36`·`v46`·`v59`의 `wallet_of`).

### 사운드 생성 — ElevenLabs (2026-10-11)

- **API 키는 사용자 환경변수 `ELEVENLABS_API_KEY`에 있다.** 사용자에게 다시 만들어 달라고 하지 마라.
  이미 떠 있던 세션은 프로세스 환경에 없으니 `[Environment]::GetEnvironmentVariable("ELEVENLABS_API_KEY","User")`로 읽는다.
  키는 **파일·커밋·채팅에 적지 않는다**(권한: 효과음 · 뮤직 생성만, 이름 `ctc-env`).
- 키가 죽었으면 브라우저(Playwright, ElevenLabs 로그인 유지)에서 직접 만든다 — 개발자 → API 키 → 키 생성 → 효과음·뮤직 생성 「접근」 → 대화상자의 키를 읽어 바로 환경변수에 넣고 페이지를 닫는다. **키는 생성 순간에만 보인다.**
- 플랜: **Creator**(상업 라이선스 · 192kbps · 월 121k 크레딧, 10/27 갱신). 비용은 뮤직 분당 900 · 효과음 길이 지정 시 초당 40.
- 프롬프트·후처리 규칙은 [사운드 프롬프트](docs/design-vault/사운드%20프롬프트.md) 끝 절들(BGM v3 · 효과음 v2 · 「깨진 소리」 수리 · **2026-10-11 작성 규칙**)을 **먼저 읽는다** —
  시작 3ms 페이드인 · 45Hz 하이패스 · 피크 -1dBFS · `.meta normalize: 0`(앰비언스만 1) · 루프는 박자·조용한 지점 기준 · 연사음 길이 < 연사 간격.
- **변주 세트(발소리 1·2·3 등)는 따로 뽑지 않는다** — 가벼운 소리는 한 테이크에 "three … in a row, silence between"으로 넣고 무음에서 자르고, **울림이 긴 소리는 같은 프롬프트로 여러 번 뽑아 스펙트럼 거리 1.5~6dB인 셋을 묶는다**(`tools/audio/gen_footsteps.py` `set3` — 피치 ±3% 파생은 「똑같다」, 따로 뽑은 셋은 「연관 없다」였다). 세기는 피크가 아니라 **LUFS**로 맞춘다(발소리 −17). **발소리엔 만화 접두를 쓰지 않는다** — 폴리 어휘로(만화 접두는 「통통」 북소리가 됐다). **쥐 소리엔 'mouse'·'tiny'도 쓰지 않는다** — 쨍한 「동전 쇳소리」가 난다. 물리 동작으로 적고, 짧은 톡의 세기는 LUFS가 아니라 50ms RMS로 맞춘다.
  따로 뽑으면 재질·공간을 모델이 매번 다르게 채워 **서로 다른 발로 들린다**(고양이 1.3k/4.9k/2.6kHz). 프롬프트는 발원체·동작·재질·공간·성격을 다 채우고 `prompt_influence` 0.7↑.
  BGM은 장르·무드·악기·BPM·조성·시대 + `instrumental only`, 루프는 **제외 목록**이 일을 한다.
- **효과음은 한 줄 프롬프트로 시작한다**(2026-10-11 총소리 5차) — 접두·수식어·금지어를 붙일수록 소리가 비틀렸다(「springy」→띠용 · 「punchy」→저음 쿵 · 「tiny」→쇳소리).
  `machine gun single shot` · `small pistol shot`이 4차례 손본 프롬프트를 이겼다. 여러 테이크를 늘어놓고 **사람이 고르게** 하고, 후처리는 고른 뒤 세기만.
  **길이 지정(`duration_seconds`)·자르기도 제약이다**(2026-10-13 벽감·상자) — 한 줄만 보내고 길이는 모델에 맡긴다(`tools/audio/gen_sfx.py natural`). 뽑기 전에 그 소리가 게임에서 **무슨 일**인지 코드로 확인한다(「상자 열림」은 실은 부수기였다).
- 귀로 확인은 `Tools/CTC/Audio/사운드 청취 창`. 윈도우 기본 플레이어는 0.5초 이하 소리를 놓친다.

### 블렌더로 애니메이션 클립 만들기 (2026-09-12)

리그는 **Generic**이라 유니티가 뼈를 **트랜스폼 경로 문자열**로 찾는다(`Rig/root/body/head/ear.L`).
리타깃이 없어서 이름이 한 글자만 어긋나도 **에러 없이 그 커브가 죽는다.**

**쥐 뒷다리에는 팩에 없는 무릎이 있다**(2026-09-13, `shin.B.L/R` — 뼈 13개).
블렌더는 `tools/blender/add_knees.py`(한 번만 — 두 번 돌리면 스스로 거절), 유니티는
`Tools/CTC/Dev/Bake Mouse Knees`가 프리팹에 트랜스폼 둘을 세우고 **파생 메시**(`Mesh_*_EyeSplit`)의
가중치를 나눈다. 가중치 공식은 양쪽이 같다(`KneeWeightLogic` ↔ `shin_share`, 무릎 0.617).
**`Bake Eye Split Meshes`를 돌리면 가중치가 팩 원본으로 돌아가므로 무릎도 같이 다시 굽는다**(메뉴가 알아서 부른다).
클립 자(`Probe Clip Parity`)는 무릎 커브를 「일부러 더한 뼈」로 따로 센다 — 무릎을 안 쓰는 클립에서 **움직이면** 「다르다」가 된다.

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

**내보내기 전에 포즈를 쉬는 자세로 되돌린다**(2026-09-19 — `export_clips.reset_pose`). 액션이 키를 안 찍은 뼈(뿌리·귀 등)는
**`.blend`에 저장된 포즈 값으로 구워진다.** 마지막으로 평가한 두 발 클립(뿌리 83mm 들어올림)이 남아 있어 `Shoot·Dash·Fortress·Sticky·Warehouse`가
83mm 뜬 채로 들어가 있었다(9-14부터, 에러 없음). 새 클립을 넣을 때도 `apply_clip.py`가 저장 전에 포즈를 되돌린다.

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

자작 클립 — 쥐 여섯: `Shoot`·`ShootDraw`(2026-09-12) · `Dash`·`Sticky`·`Fortress`·`Warehouse`(2026-09-13) · **고양이 다섯: `Frenzy` + 전직 스킬 넷 `CatSkillFire/Wind/Light/Dark`**(2026-09-13, `art/blender/cat.blend` → `CTC_Cat_Clips.fbx`, 전직마다 동작이 다르다 — 사용자 결정).
쥐·고양이의 모든 액티브가 전용 동작을 갖는다(덮치기는 팩 `Attack`이 맞아서 유지).
**고양이 리그는 쥐와 뼈 이름이 같고 크기가 약 2배이며, 귀의 부호만 반대다**(+가 앞으로 접힘) — `cat_frenzy.py` 머리말.
고양이 전용 상태는 `Tools/CTC/Dev/Bake Cat Clip States`로 AC_Cat에 넣는다(쥐는 `Bake Mouse Gun` — 둘 다 `RoleClipStates`).
바닥 관통은 `tools/blender/measure_clip.py`로 **1/4프레임 간격** 메시 기준으로 잰다 — 정수 프레임만 보면 키 사이 처짐을 놓친다(Fortress 6.5에서 −12.8mm).
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
