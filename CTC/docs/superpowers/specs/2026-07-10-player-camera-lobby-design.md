# Plan 3 설계 — 플레이어 & 카메라 & 로비 역할 선택

**날짜:** 2026-07-10
**상위 문서:** [MVP 로드맵](../plans/2026-07-06-mvp-roadmap.md) · [게임 디자인 스펙](2026-07-06-cheese-thief-and-cat-design.md)
**범위:** 로드맵 Plan 3 + 로비 역할 선택(원래 Plan 8 일부를 사용자 요청으로 앞당김)

## 1. 목표

NGO 접속 후 **로비에서 역할(쥐/고양이)을 선택**하고, 호스트가 시작하면 **역할별 프리팹**이 스폰되어 **3인칭 자유 궤도 카메라**로 플레이한다.

```
NGO 접속 → [로비] 역할 선택 UI (자동 플레이어 스폰 OFF)
        → 호스트 "게임 시작" → 서버가 역할별 프리팹 스폰 → [인게임]
```

## 2. 결정사항

| 결정 | 선택 | 이유 |
|------|------|------|
| 카메라 기술 | **Cinemachine 3.x** (신규 설치) | 사용자 지정. 업계 표준·충돌 처리 내장 |
| 카메라 조작 | **항상 자유 궤도** (리센터 없음) | 마우스는 카메라만 돌림. 몸은 이동 입력이 있을 때만 카메라 기준 이동 방향으로 회전 → 정지 상태에서 사방 훔쳐보기(스텔스 핵심) 성립 |
| 역할 배정 | **로비 UI에서 각자 선택** | 사용자 지정. 고양이 정원 1(2인 테스트 기준, 상수로 분리) |
| 씬 구조 | **단일 씬 내 상태 전환** (로비 UI 오버레이) | 씬 2개 관리 비용 회피. MVP 단순화 |
| 모델 | Quirky Series `Cat.prefab`·`Mouse.prefab` (Pets Vol.1) | 임포트 완료된 에셋. Idle/Walk/Run 등 애니메이터(`AC_Cat`/`AC_Mouse`) 완비 |
| 머티리얼 | 동봉 URP `SoftSurfaceGraph.shadergraph`로 신규 머티리얼 생성 | 기존 `M_Cat`/`M_Mouse`는 빌트인 셰이더(CGPROGRAM) → URP에서 마젠타 |
| 입력 | `Keyboard.current` + `Mouse.current.delta` 직접 폴링 | Plan 1 기존 패턴 유지. InputActions 에셋 도입 보류 |
| 애니메이션 동기화 | 각 클라이언트 로컬에서 이동 속도로 유도 | NetworkTransform이 위치를 옮기므로 속도는 로컬 계산 가능 → NetworkAnimator 불필요 |

## 3. 컴포넌트

### LobbyManager (NetworkBehaviour, 씬 오브젝트)

- `NetworkList<LobbyPlayerState>` — `{ ulong clientId, Role role }`. `Role = None | Mouse | Cat` (레디 체크는 Plan 8로 미룸 — 역할 선택 자체가 레디를 겸함)
- 클라이언트 → 서버 RPC: `SelectRoleServerRpc(Role)` — 정원 검사 후 반영
- 호스트 전용 `StartGame()` — 전원 역할 선택 완료 시 활성. 자동 스폰이 꺼진 상태에서 서버가 역할별 프리팹을 `SpawnAsPlayerObject`로 스폰
- 접속/이탈 콜백으로 리스트 유지
- **역할 배정 규칙은 순수 C# `LobbyRoleLogic`으로 분리** (정적 함수): 고양이 정원 검사, 시작 가능 판정(전원 역할 선택 + 고양이 ≥1 + 쥐 ≥1) → EditMode TDD

### 로비 UI (UGUI 오버레이)

- 같은 씬의 Canvas. 접속 후 표시, 시작 시 숨김
- 구성: 쥐/고양이 선택 버튼, 참가자·역할 목록 텍스트, 시작 버튼(호스트만)
- 로비 중 커서 해제(`CursorLockMode.None`), 시작 시 잠금(`Locked`)

### 플레이어 프리팹 2종 — `PlayerCat` · `PlayerMouse`

공통 구조:

```
PlayerCat/PlayerMouse (CharacterController, NetworkObject,
  │                    ClientNetworkTransform, PlayerMovement, PlayerAnimationDriver)
  └─ Model (Quirky FBX 인스턴스 + Animator: AC_Cat/AC_Mouse, URP 머티리얼)
```

- `PlayerMovement` 개조: 카메라 기준(camera-relative) 이동 + 이동 시 몸 yaw 회전. `CC.Move()` 유지(transform 직접 세팅 금지)
- `PlayerAnimationDriver`: 프레임당 수평 이동량으로 속도 추정 → Animator 파라미터(Idle/Walk/Run). 소유자·원격 동일 로직
- 역할 차등(직렬화 필드): 이동속도(쥐 > 고양이), 카메라 FOV(고양이 넓게)·거리

### 카메라 (Cinemachine 3.x)

- 씬에 `CinemachineCamera` + `OrbitalFollow`(궤도) + `RotationComposer` 1대 — 로컬 플레이어 스폰 시 Target 바인딩
- 마우스 델타 → yaw 무제한·pitch 클램프(약 -30°~70°). 리센터 없음
- **순수 로직 분리:** `CameraOrbitLogic` — pitch 클램프, yaw 랩, 마우스 델타→각도 변환 → EditMode TDD
- 원격 플레이어에는 카메라 없음(로컬 소유자 전용)

### SetupNetworkingScene.cs 갱신

- NetworkManager `PlayerPrefab` 자동 스폰 해제(로비가 스폰 담당)
- LobbyManager·로비 Canvas·CinemachineCamera 배선 추가
- `PlayerCat`/`PlayerMouse` 프리팹 재생성 메뉴 갱신, NetworkPrefabs 리스트 등록

## 4. 테스트 전략

| 대상 | 방법 |
|------|------|
| `CameraOrbitLogic` (클램프·랩·변환) | EditMode TDD |
| `MovementInput` 카메라 상대 변환 | EditMode TDD (기존 테스트 확장) |
| `LobbyRoleLogic` (정원·중복·시작 판정) | EditMode TDD |
| 로비 → 스폰 → 이동·카메라 | MPPM 2인 수동 검증 |

**MPPM 검증 시나리오:** 호스트+클론 접속 → 양측 로비 UI 확인 → 호스트 고양이·클론 쥐 선택 → 시작 → `mppm_collect`로 양측 프리팹 종류·위치 동기화 확인 → 이동·카메라 `u_screenshot` 육안.

## 5. 비범위 (후속 계획)

- 시야·발소리·후각 감지 판정 (Plan 6)
- 덮치기·포획 (Plan 5)
- 라운드 타이머·승패·결과 화면 (Plan 8) — 로비의 나머지(레디 체크 고도화, 재시작 플로우) 포함
- UGS Relay/Lobby 연동 (Plan 2) — 이 로비는 로컬 NGO 접속 위에서 동작하며 Plan 2 완료 시 그 위에 얹는다
