# 기술 이펙트 패스 — 쥐·고양이 모든 액티브 (2026-09-14)

사용자 요청: "이제 모든 기술들의 이펙트를 넣자 쥐, 고양이 둘다". 방식은 추천안(사용자 부재 중 지시).
상태: ☑ 완료(검증까지) · ☐ 할 일

## 기획 제약과의 관계

- 기획서 §13 "VFX 0 · 프리팹 0"은 **사용자 요청으로 이번에 풀린다**(모션 0이 풀린 것과 같은 방식) — §13에 기록.
- §11 "없던 정보를 만들지 않는다"는 **그대로 지킨다**:
  - **한 번 터지고 끝난다.** 불·바람·광분이 켜져 있는 동안 도는 오라는 없다("아직 켜져 있다"를 흘린다).
  - 패시브 전부 제외 — 큰 주머니·빠른 발·숨은 창고·요새/창고 Lv2~3·고양이 잠복/허기/발 구르기/예민한 귀/발톱 중첩.
  - 후추 도트 틱(테크 레벨)·치즈 방패 파편(지갑)은 제외.

## 목록 (22개)

| 역할 | 기술 | 이펙트 | 붙인 자리(이미 전 클라가 도는 곳) |
|---|---|---|---|
| 쥐 | 사격 | `MuzzleFlash` + `BulletImpact` | `MousePopgun.TracerRpc` |
| 쥐 | 요새·창고 설치 | `PlaceDust` | `PlaceSfxOnSpawn.OnNetworkSpawn` |
| 쥐 | 요새 밀어내기 | `FortressPush` | `FortressShield.PushSfxRpc` |
| 쥐 | 가시 벽 반사 | `ThornSpark` | `CatPounce.ThornFxRpc` **(새 Rpc — 반사는 서버 전용 판정)** |
| 쥐 | 끈끈이 | `StickySplat` | `StickyPatch.OnNetworkSpawn` |
| 쥐 | 대시 | `DashDust` | `MouseDash.DashSfxRpc` |
| 쥐·고양이 | 구매 | `BuyCoins` | `ShopController.BuySoundRpc` |
| 쥐 | 채굴 | `EatCrumbs` | `CheeseSpot` 갉기 원샷 자리 |
| 쥐 | 치즈 줍기 | `CheesePickup` | `CheeseDrop.PickupSfxRpc` |
| 쥐 | 용사 / 깡패(영입 포함) | `HeroBurst` / `GangSmoke` | `MouseJob.JobFxRpc` |
| 고양이 | 덮치기 | `ClawSlash` | `CatPounce.AttackAnimRpc` |
| 고양이 | 전직 불·바람·빛·어둠 | `Advance*` | `CatClass.AdvanceFxRpc(id)` |
| 고양이 | 전직 스킬 불·바람·빛·어둠 | `Skill*` | `CatSkill.SkillFxRpc` |
| 고양이 | 광분 | `FrenzyShock` | `CatFrenzy.FrenzyFxRpc` |

## 구조

- `Fx/FxId` · `FxCatalog`(Resources/Fx) · `FxOneShot`(풀에서 다시 켤 때 입자 처음부터) · `FxPlayer.PlayAt`(PoolManager) · `FxCatalogLogic`(전직→이펙트)
- 에디터 `FxPrefabBuilder` — 입자 텍스처 6종(점·링·불꽃줄·연기·별·할퀴기)을 코드로 그리고 프리팹 22개를 굽는다 (`Tools/CTC/Fx/Build Skill Effects`)
- QA 훅 `TestPlayFxInFront` · `TestResetFxCounts` · `TestGetFxCounts` · `TestCaptureGameView`

## 고친 버그 (패스 중 발견)

- **숨은 창고 Lv3이 치즈 입자를 뿜었다** — 소리만 끄고 입자는 틀어 "숨은" 창고가 눈으로 들켰다(`WarehouseProduction.ProduceFxRpc`). 입자도 같은 조건으로.
- **전직 빛이 아무 화면에도 안 떴다** — 같은 틱에 바꾼 복제값보다 Rpc가 먼저 도착해 받는 쪽이 아직 None. 전직을 Rpc 인자로 넘긴다(qa_v57 ③).

## 검증

- ☑ EditMode(FxCatalogLogicTests 2개 추가)
- ☑ `qa_v57` 11/11 — **행동한 인스턴스가 아닌 다른 인스턴스에서** 이펙트를 센다
- ☑ `qa_v53` 12/12 · wesqa pytest 182
- ☑ 갤러리 스크린샷(22개, 시간 0.1배로 멈춰 찍음) — 검은 연기가 밤 바닥에서 안 보여 보랏빛 연기+불씨+링으로 고침, 할퀴기 수명·크기 늘림

## 남은 것

- 사용자 눈 검수(마지막에 한 번에).
- 총구 섬광은 0.08초라 캡처로는 안 잡힌다 — 실제 사격에서 확인 필요.
