# 대기실 앞단 개편 — 작업 계획

설계: [2026-09-14-frontdoor-repo-style-design.md](../specs/2026-09-14-frontdoor-repo-style-design.md)
상태 표기: ☐ 할 일 · ◐ 진행 중 · ☑ 완료(검증까지) · ✕ 보류(사유)

## 1. 순수 로직 (EditMode TDD)

- ☑ `ConnectionGateLogic` — 판정 순서·거절 코드·문구·`IsGateCode`
- ☑ `LobbyPasswordLogic` — 정리(공백 제거·대문자·10자)·유무·페이로드
- ☑ `ServerListLogic` — 검색(단어 전부 포함)·페이지·빠른 참가 고르기(가장 찬 방)
- ☑ `SteamLobbyLogic` 키 추가(`pw`·`ver`)
- 테스트: `FrontDoorLogicTests` 12개

## 2. 접속 관문

- ☑ `ConnectionGate` — `OnInstantiated`에서 승인 설치, 호스트 비밀번호·잠금, 거절 코드 보관(관문 코드만)
- ☑ 대기실이 서면 열기 / 시작하면 닫기 (`RoomDirector`)
- ☑ `SteamLobbyManager` — 잠금 → `SetJoinable`, 방 데이터 `pw`·`ver`, 검색 필터(태그·버전·빈자리)
- ☑ `MppmAutoConnect` — 관문 거절이면 자동 접속을 멈춘다

## 3. 세션 시작 Worker

- ☑ `SessionStarter` — 방 만들기 / 공개 방 / 목록 참가 / 빠른 참가 / 친구 오버레이 / 초대
- ☑ `SteamLobbyManager` 생성·참가 → `UniTask<bool>` (실패하면 버튼 풀림)
- ☑ 비밀번호 필요 시 팝업 → `ConnectionData` → (로컬) 재시도
- ☑ 대기실에서 끊기면 메인에서 사유 알림(`FrontDoorNotice`)

## 4. 팝업

- ☑ `TextInputPopup` · `MessagePopup` + 프리팹·Addressables 등록(`Build Popup Prefabs`)

## 5. 화면

- ☑ Intro: 방 만들기 / 공개 게임 / 친구 방 참가 / 설정 / 종료 + NetworkRoot·RoomEntry·SessionStarter
- ☑ Lobby(서버 목록): 8줄·검색칸·이전/다음·새로 만들기(이름 팝업)·뒤로=Intro
- ☑ Room 나가기·끊김 → Intro (`Room.unity` 직렬화 값도 교체)
- ☑ 씬 재생성: Setup Intro Scene · Setup Lobby Scene

## 6. 검증

- ☑ EditMode 전체 908 통과(새 테스트 12)
- ☑ 플레이: 메인 → 방 만들기(건너뛰기) → 대기실 → 나가기 → 메인 (스크린샷)
- ☑ 플레이: 공개 게임 선택 팝업 → 서버 목록(11개·1/2 페이지) (스크린샷)
- ☑ MPPM `qa_v56` 7/7: 비밀번호 방 — 자동 접속 거절 / 틀림 / 맞음 / 시작 후 닫힘 / 난입 CLOSED
- ☑ `qa_v53` 회귀 12/12(Game 단독 경로) · 서버 목록 → `TestCreateRoom` → 대기실, 클론 둘 자동 합류(인원 3)
- ☑ 코드 다이어그램 갱신(규칙 8)
- ☐ 스팀 실측(개발용 빌드 2대) — 사용자 몫: 친구 전용 방·초대·공개 목록·빠른 참가

## 진행 기록

- 2026-09-14 계획 작성 → 구현 → EditMode 907 통과.
- 2026-09-14 `qa_v56` 1차 FAIL 4 — 원인 둘을 고쳤다:
  ① 트랜스포트 끊김 문장("[Disconnect Event]…")까지 거절로 적어 클론 자동 접속이 멈췄다 → 관문 코드만 적는다.
  ② 호스트보다 먼저 두드리던 클론의 UTP 재시도가 `IsListening`으로 새 참가를 막았다 → 붙기 전 시도를 걷고 시작한다.
- 2026-09-14 2차도 FAIL — **스팀 클라이언트가 켜져 있어** 호스트가 스팀 방(Facepunch)으로 열렸고 클론(UTP)이 못 붙었다.
  스팀 종료 → 클론 하나가 죽고 MPPM 모달(함정 9·10) → 닫고 클론 재시작.
- 2026-09-14 3차 FAIL 1 — 호스트가 대기실 씬을 여는 사이 클론이 `CLOSED`를 받고 자동 접속을 멈췄다(관문은 옳았다).
  → 자동 접속은 **비밀번호 거절일 때만** 멈춘다. 4차 `qa_v56` 7/7 · `qa_v53` 12/12.
- 남은 것: 스팀 실측(친구 전용 방·초대·공개 목록·빠른 참가)은 계정 둘이 필요해 사용자 몫.
