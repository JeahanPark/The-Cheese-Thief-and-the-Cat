# Canadian Mouse (캐네디언 마우스)

> 3인칭 비대칭 멀티플레이 — **고양이 1~2 vs 쥐 3~6**.
> 쥐는 위험한 치즈 지점에서 돈을 벌어 무장하고, 고양이는 쥐를 사냥한다. 쥐가 벌어 둔 돈으로 고양이를 쓰러뜨리면 쥐의 승리.
> 1인 개발 · Unity 6 · Netcode for GameObjects · Steam 출시 준비 중.

폴더·원격 이름 `The-Cheese-Thief-and-the-Cat`은 초기 가제이고, 코드 약칭 `CTC`는 거기서 왔습니다.

## 한눈에

| | |
|---|---|
| 장르 | 3D 비대칭 경제전 (원작 규칙: 스타크래프트 UMS 「약올리기 DX」의 3D 번안) |
| 인원 | 2~8인 온라인 (호스트 권위) · 봇 보충 |
| 플랫폼 | Windows PC · Steam (Facepunch 트랜스포트) |
| 엔진 | Unity 6000.5 · URP · Netcode for GameObjects 2.12 · Cinemachine 3 · Input System |
| 비동기·반응형 | UniTask · R3 |
| 코드 | C# 557파일 · 약 8만 줄 · EditMode 테스트 1,100여 개 · Python E2E 스위트 |
| 기간 | 2026-07 ~ (진행 중) |

## 핵심 시스템

| 시스템 | 요점 | 코드 |
|---|---|---|
| 경제 | 치즈 지점 채굴 → 상점 구매(무기 티어·대시·요새·창고) · 수치는 전부 CSV | `Assets/Scripts/Economy`, `Assets/Resources/CSVInfo` |
| 전투 | 쥐 총기 3티어 · 고양이 덮치기·전직 스킬 4종 · 깡패/용사 쥐 변신 · 스턴·무적 | `Assets/Scripts/Combat`, `Player` |
| 네트워크 | NGO 호스트 권위 · 접속 승인 관문(비밀번호·정원) · 스팀 로비·빠른 참가·친구 초대 · 로컬 UTP 폴백 | `Assets/Scripts/Network`, `Lobby`, `Room` |
| AI | 자체 격자 A\* 길찾기 · 쥐/고양이/강아지 봇 · 봇 전용 실험실 씬 | `Assets/Scripts/Ai`, `Bots`, `Scenes/BotLab.unity` |
| 맵 | 코드로 굽는 밤 식료품 창고(트리플래너 셰이더·천장·빛줄기·후보정) · 카메라 가림막 페이드 | `Assets/Scripts/Map`, `Rendering`, `Shaders` |
| UI | MVC(View=표시 · Model=상태 · Controller=입력→RPC) · R3 구독 · 미니맵·채팅 명령 자동완성·상점·옵션 | `Assets/Scripts/UI` |
| 애니메이션 | 팩 리그에 Blender 스크립트로 자작 클립 추가(쥐 6 · 고양이 5) · 파이프라인 검증 도구 | `tools/blender`, `Assets/Scripts/Editor` |
| 사운드 | BGM·효과음을 생성 AI로 뽑아 LUFS 기준 후처리 · 추격곡 크로스페이드 | `Assets/Scripts/Audio`, `tools/audio` |

## 품질 관리

- **EditMode 테스트**: 규칙·수치·로직은 `*Logic` 순수 C# 클래스로 분리해 테스트한다(`Assets/Scripts/Tests`).
- **E2E 실플레이 QA**: 인게임 리플렉션 서버(`Assets/WesQA`) + Python 클라이언트(`tools/wesqa`). Multiplayer Play Mode로 3인 동시 플레이를 띄워 시나리오 60여 개를 자동 검증한다.
- **문서**: 설계 스펙 `CTC/docs/superpowers/specs`, 코드 구조 다이어그램 `CTC/docs/design-vault/코드 다이어그램.md`(코드와 함께 갱신).

## 개발 방식

Claude Code(AI 에이전트)를 짝 프로그래머로 두고 개발했습니다. 저장소의 `CLAUDE.md`·`.claude/`는 그 작업 규칙(코딩 규약·함정 기록·QA 절차)입니다. 설계 판단·검수·플레이 테스트는 직접 했고, 반복 작업과 자동화는 에이전트에 맡겼습니다.

## 열어 보기

1. Unity **6000.5.2f1** 설치 후 `CTC/` 폴더를 연다. 패키지는 매니페스트대로 자동 복원된다.
2. 메뉴 `Tools/CTC/Setup Networking Scene`으로 씬·프리팹을 굽는다(코드로 생성한다).
3. `Assets/Scenes/Intro.unity`에서 플레이 → 「봇과 연습」으로 혼자 바로 해볼 수 있다. 여러 명은 Multiplayer Play Mode 가상 플레이어로.
4. 빌드: `Tools/CTC/Build Windows (배포용)` → `Builds/Windows`.

## 저장소 구성

```
CTC/            Unity 프로젝트 (Assets/Scripts 가 본체)
CTC/tools/      wesqa(E2E QA) · blender(클립 생성) · audio(사운드 후처리) · ContentBuilder(스팀 업로드)
CTC/docs/       설계 스펙 · 디자인 볼트
art/blender/    애니메이션 작업 파일
```

서드파티 패키지·에셋의 라이선스는 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)를 참고하세요. 아트 에셋은 Unity Asset Store 라이선스라 **열람 목적으로만** 포함돼 있습니다.
