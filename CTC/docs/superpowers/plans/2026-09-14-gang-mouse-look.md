# 깡패 쥐 — 작업 계획

설계: [2026-09-14-gang-mouse-look-design.md](../specs/2026-09-14-gang-mouse-look-design.md)
상태: ☐ 할 일 · ◐ 진행 중 · ☑ 완료(검증까지) · ✕ 보류(사유)

## 1. 로직·데이터
- ☑ `GangLookLogic.Active(job, phase)` + 테스트
- ☑ CSV `BalanceInfo`: `GangClubDamage` 18 · `GangClubRange` 1.8 · `GangClubArcDegrees` 90 · `GangClubCooldown` 0.7 → Convert CSV
- ☑ 부채꼴 판정 `GangLookLogic.InArc` · 같은 편 안 때림 `CanClub` + 테스트 (따로 `GangClubLogic`을 두지 않았다 — 조건 셋이 한 파일에 모이는 편이 읽기 쉽다)

## 2. 클립 (블렌더)
- ☑ `BipedIdle` · `BipedWalk` · `BipedRun` (루프) · `ClubSwing` (원샷) — 몸 −48도(−32는 웅크린 쥐, −55는 엉덩이가 23mm 잠겼다)
- ☑ 바닥 측정(최악 Idle −17.1 · Walk −17.0 · Run −17.0 · Swing −22.3 mm) · FBX 내보내기 · `.meta` 클립 매핑(루프 플래그)
- ☑ `AnimationStateLogic.MouseOnly`에 추가 → `Bake Mouse Gun`(상태 굽기)

## 3. 메시
- ☑ 몽둥이 `Mesh_GangClub` · 모자 `Mesh_GangHat` (SDF)
- ☑ 프리팹에 붙이기: 몽둥이 = 오른 앞발(총 자리), 모자 = `head` — 꺼진 채로 굽는다
- ☑ 모자는 **BipedIdle 자세로 뼈를 돌린 채** 얹는다 — 네 발 쉬는 자세로 얹었더니 두 발에서 뒤로 누워 머리에 파묻혔다(아래 기록)

## 4. 런타임
- ☑ `PlayerAnimationDriver.Biped` — 두 발 모드(이동 상태 이름만 바꾼다)
- ☑ `GangAngryFace` — 쉬는 동안 `eyes.rabid` 55 + `eyes.annoyed` 45 (`FaceExpression`을 고치지 않고 컴포넌트 하나로 — 규칙 7)
- ☑ `GangLook` — 복제값 두 개(직업·페이즈) 구독 → 두 발·몽둥이·모자·얼굴
- ☑ `MousePopgun.FireRpc` — 깡패 활성이면 `ServerClub` 근접 판정 → `ClubSwingRpc`(모션·소리·이펙트)

## 5. 검증
- ☑ EditMode 914 · pytest 187(클립 계약: 두 발 클립은 첫 프레임 = 끝 프레임)
- ☑ 깡패판 총·후추 탄 구매 잠금 `TechDeny.GangNoGun` (사용자 추가 요청)
- ☑ 클립 GIF 도구 깡패판 — `Tools/CTC/Dev/깡패 쥐 클립 GIF 내보내기` → `docs/design-vault/images/gang/*.gif`
- ☑ MPPM QA `qa_v58` 7/7(⑥ 구매 잠금 추가) — 1페이즈엔 안 드러남 · 2페이즈 남의 화면에서 켜짐 · 휘두르기+ClubSmash · 총구 섬광 0 · 고양이 HP 1000→982
- ☑ 인게임 촬영 — 서기·걷기(BipedWalk)·휘두르기·모자·몽둥이·화난 눈
- ☑ 코드 다이어그램 · CLAUDE.md

## 진행 기록
- 2026-09-14 계획 작성.
- 2026-09-14 전 항목 구현·검증.
  - 첫 촬영이 전부 「MICE WIN」 패널에 가렸다 — `TestForceGangPhase`가 페이즈만 세우고 **시계를 0으로 둬** 다음 판정 틱에 시간 초과로 판이 끝났다. 훅이 시계도 `GangPhaseSeconds`로 채우게 고쳤다(시간 초과를 재는 `qa_v52`는 원래 `TestBurnPhaseClock`을 부른다).
  - 모자가 크기(28cm, `TestGetGangPropTransform`으로 실측)는 맞는데 화면엔 크라운만 삐죽 보였다 — 네 발 자세 기준으로 수평으로 얹어서 두 발(몸 −48도)에서 머리 속으로 누웠다. 굽기 때 BipedIdle을 샘플해 그 자세의 정수리 방향으로 얹고 되돌린다. 크라운을 높이고(0.30→0.55) 색을 순흑에서 짙은 갈회색으로.
