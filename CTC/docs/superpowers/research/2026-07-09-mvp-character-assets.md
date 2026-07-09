# MVP 캐릭터 모델·애니메이션 조사

**작성일:** 2026-07-09
**근거 스펙:** [../specs/2026-07-06-cheese-thief-and-cat-design.md](../specs/2026-07-06-cheese-thief-and-cat-design.md) §11(스코프)·§12(아트)
**관련 계획:** 로드맵 계획 3 (플레이어 & 카메라 — 쥐/고양이 2종 프리팹), 계획 9 (아트 패스)

---

## 1. 필요 리소스 (기획서 기준)

MVP 스코프 = **쥐·고양이 각 1종**, 3D 스타일라이즈드(귀엽고 코믹), URP.

### 모델

| 모델 | 근거 |
|------|------|
| 쥐 (치즈 도둑) | 스코프 "쥐 1종" |
| 고양이 (술래) | 스코프 "고양이 1종" |
| 소품 변장용 메시 | 변장 = 쥐 메시 숨기고 소품 메시로 교체 — 별도 캐릭터 불필요, 맵 소품(계획 9) 재사용 |

### 애니메이션

| 대상 | 클립 | 근거 메커닉 |
|------|------|------------|
| 공통 | Idle / Walk / Run | 기본 이동 (계획 3) |
| 쥐 | **Eat (치즈 먹기 루프)** | 그 자리 먹기·채널링·무방비 — 코어 루프 핵심 (계획 4) |
| 쥐 | Captured (잡힘/탈락) | 포획 → 탈락 → 관전 (계획 5) |
| 쥐 | Dash | 도주 아이템 (계획 7) |
| 쥐 | 약올리기 이모트 | 밀당 수단 (§6) |
| 고양이 | **Pounce (덮치기)** | 핵심 능력, 쿨다운 스킬 (계획 5) |
| 고양이 | Sniff (후각 탐지) | 후각 추적 표현 (계획 6) |
| 고양이 | Catch (포획 성공) | 포획 연출 |

**최소 검증 세트:** Idle · Run · Eat · Pounce · Captured 5개면 코어 루프(먹기→덮치기→탈락) 검증 가능.

생략 가능: 점프(기획서에 없음), 사망 래그돌(탈락=관전 전환), 감정표현 확장.

---

## 2. 추천 에셋 — Quirky Series (Omabuarts Studio)

쥐·고양이가 **같은 시리즈** → 스타일 통일. 합계 **$12**.

| 에셋 | 가격 | URL |
|------|------|-----|
| Cat - Quirky Series | $6 | https://assetstore.unity.com/packages/3d/characters/animals/mammals/cat-quirky-series-119971 |
| Mouse - Quirky Series | $6 | https://assetstore.unity.com/packages/3d/characters/animals/mammals/mouse-quirky-series-180848 |

- 스타일: 로우폴리 스타일라이즈드 카툰 — 기획서 §12 톤 일치
- 렌더: Built-in·**URP 호환** (HDRP 미지원 — 무관)
- Cat 최근 업데이트 2026-03, 지원 2019.4/2022.3
- 공통 애니메이션 **18종**: Attack · Bounce · Clicked · Death · Eat · Fear · Fly · Hit · Idle_A/B/C · Jump · Roll · Run · Sit · Spin/Splash · Swim · Walk

### MVP 요구 ↔ Quirky 클립 매핑

| MVP 필요 | Quirky 클립 | 판정 |
|----------|------------|------|
| Idle / Walk / Run | Idle_A·B·C, Walk, Run | ✅ |
| 쥐 Eat | Eat | ✅ |
| 쥐 Captured | Death, Hit, Fear | ✅ |
| 쥐 Dash | Run 재생속도↑ | ✅ |
| 쥐 약올리기 이모트 | Spin, Clicked, Sit, Bounce | ✅ |
| 쥐 변장 | 애니 불필요 (메시 교체 방식) | ✅ |
| 고양이 Pounce | Attack + Jump 조합 | ✅ |
| 고양이 Catch | Attack 재사용 | ✅ |
| 고양이 Sniff | ✗ 없음 → Idle 변형 + VFX 대체 (계획 6) | ⚠️ |
| 쥐 벽타기 (아이템) | ✗ 없음 → 도주 아이템을 대시+순간은신으로 선택하면 회피 | ⚠️ |

⚠️ 2건 모두 MVP 내 대체 가능 — 구매 차단 사유 아님.

### 차선 후보 (탈락)

| 후보 | 탈락 사유 |
|------|----------|
| Cartoon Rat ($15) · Mouse Rat Rodent Brown ($21.99) | 고양이와 스타일 불일치, 더 비쌈 |
| Fully Animated Cats · Cat Kitten Animated | 쥐 짝이 없음 |

무료 사전 검증: Quirky FREE Animals (https://omabuarts.itch.io/quirky-series-free-animals) — 리그·애니 품질 확인용, 고양이/쥐 미포함이라 실개발엔 불필요.

---

## 3. 액션

- [x] Cat + Mouse Quirky Series 구매 ($12)
- [x] 프로젝트 임포트 (2026-07-09 완료, 검증됨 — §4)
- [ ] 계획 3 플레이어 프리팹에 모델 연결
- [ ] Animator Controller: 최소 검증 세트 5클립부터 (Idle·Run·Eat·Attack·Death)
- [ ] Sniff 대체 VFX·변장 메시 교체는 계획 6 시점에 결정

---

## 4. 임포트 완료 상태 (다른 세션 작업용)

**임포트 검증 완료 — 아래 경로 그대로 사용 가능.**

베이스 경로: `Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/`

| 리소스 | 경로 (베이스 기준) |
|--------|-------------------|
| 고양이 프리팹 | `Prefabs/Cat.prefab` (LOD 통합) · `Prefabs/Single LODs/Cat_LOD0~3.prefab` |
| 쥐 프리팹 | `Prefabs/Mouse.prefab` · `Prefabs/Single LODs/Mouse_LOD0~3.prefab` |
| 애니메이션 FBX | `Animations/Cat_Animations.fbx` · `Animations/Mouse_Animations.fbx` |
| Animator Controller | `Animations/AC_Cat.controller` · `Animations/AC_Mouse.controller` (에셋 기본 제공) |
| 머티리얼 | `Materials/M_Cat.mat` · `Materials/M_Mouse.mat` |
| 데모 씬 (동작 확인용) | `Demo/Demo_Cat.unity` · `Demo/Demo_Mouse.unity` |

**애니 클립 19종** (Cat·Mouse 동일, FBX 서브에셋):
Attack · Bounce · Clicked · Death · Eat · Fear · Fly · Hit · Idle_A/B/C · Jump · Lay · Roll · Run · Sit · Spin · Swim · Walk

### 통합 시 주의

1. **URP 셰이더**: 머티리얼이 핑크로 보이면 `Assets/Quirky Series/_Shader/URP (Unity 2019.3.0f3 and above)/SoftSurfaceGraph.shadergraph`로 셰이더 교체.
2. **네트워크 프리팹 연결**: 기존 Player 프리팹(계획 3)의 비주얼 자식으로 Cat/Mouse 모델을 넣는 방식 권장 — NetworkObject는 기존 Player 루트 유지.
3. **Animator**: 기본 제공 AC_Cat/AC_Mouse는 데모용 — 게임 상태기계(Idle/Run/Eat/Attack/Death)는 새 Controller를 만들고 FBX 클립만 참조.
4. **역할 매핑**: 고양이 Pounce = Attack(+Jump 조합), 쥐 Captured = Death, 쥐 Dash = Run 재생속도↑.
5. 눈 표정 Shapekey 애니 별도 존재: `Assets/Quirky Series/_Animations/Shapekey/` (Eyes_*, 이후 폴리싱용).
