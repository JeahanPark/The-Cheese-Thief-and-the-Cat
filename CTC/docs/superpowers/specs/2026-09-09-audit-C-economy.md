# 전체 검수 — 도메인 C: 경제·테크·UI·데이터 (2026-09-09)

> **한 줄 결론: 데이터·순수 로직은 매우 튼튼한데(EditMode가 CSV 정합성을 이미 기계적으로 지킨다) 「사람 손 → 서버」 입력 경로가 안 덮여 있고, 그 틈에서 고양이는 산 광분을 키로 켤 수 없다.**

## 0. 이 감사의 한계 (반드시 읽을 것)

| 항목 | 내용 |
|---|---|
| 방법 | **정적 분석만.** 유니티 에디터·플레이모드를 한 번도 안 썼다(다른 감사 4개 동시 실행 중) |
| 근거 | 소스 · CSV · 프리팹 YAML(unity-scanner) · 기존 테스트 코드 · 핸드오프 |
| 못 본 것 | 실제 씬 배선(`m_Slots` 배열 길이·SFX 클립 배선), 런타임 값, 프레임 경합 |
| 표기 | **[확인]** = 코드/데이터로 단정 가능 · **[의심]** = 정황상 그렇게 보이나 플레이 검증 필요 |

---

## 1. 책임 지도

| 관심사 | 상태의 주인 | 판정(권위) | 표시(View) | 순수 로직 |
|---|---|---|---|---|
| 지갑 잔액 | `CheeseWallet.m_Cheese` (NetworkVariable, 서버 기록) | `TrySpend` (서버) | `StatusHud` · `ShopHotbar` (R3 구독) | — |
| 누적 획득(전직 문턱) | `CheeseWallet.m_Earned` (환급은 안 섞음) | 서버 | `CatClassPopup` | `CatClassLogic` |
| 테크 칸·레벨 | `TechSlots.m_Entries` (NetworkList, 서버만 쓰기) | `ServerApplyNextLevel` | `ShopHotbar` (`Changed` 구독) | `TechLogic` |
| 구매 판정 | — | `ShopController.BuyRpc` → `CanBuyNow` → `TechLogic.CanBuy` | `ShopHotbar.TryBuy`가 같은 함수로 미리 물어봄 | `TechLogic.CanBuy` |
| 효과 적용 가능성 | 프리팹 컴포넌트 유무 | `CanApplyLevel` (차감 **전**) / `TryApplyLevel` (차감 후) | — | — |
| 사용(액티브) 판정 | — | `ShopController.CanUse` → `UseRpc`/`UseAtRpc` | `ShopHotbar.TryUse` | 부분(`StickyLogic`·`CatFrenzyLogic`) |
| 상점 안/밖 | 위치(월드) | `ShopSpot.AnyContains` (서버 재검증) | `ShopHotbar.Update` 매 프레임 | `ShopSpotLogic` |
| 오늘의 품절 | **복제 안 함** — `MatchRandom.Seed`만 복제, 양쪽이 재계산 | `TechLogic.CanBuy(_soldOut)` | `ShopHotbar.CandidatesNow` | `TechRandomLogic` |
| 핫바 후보 인덱스 | `ShopHotbar.m_CandidateIndex` (**로컬 전용**) | — | 자기 자신 | — |
| 창고 수입·상한·환급 | `WarehouseProduction` NetworkVariable(레벨을 **스폰 시 굽는다**) | 서버 `Update` 틱 | `WarehouseTick` SFX | `WarehouseLogic` |
| 요새 HP·반사 | 스폰 시 `Health.ServerSetMaxHp` + `ThornWall` | 서버 | — | — |
| 끈끈이 감속 | `PlayerMovement.m_SlowSources` (서버) → `m_ExternalSpeedMul` 복제 | 서버 0.1초 틱 | — | `StickyLogic.MinMultiplier` |
| 무기 티어 | `MousePopgun.m_Tier` | `ServerSetTier(레벨)` | — | `ShopLogic.WeaponStatsForTier` |
| CSV 4종 | `InfoManager` (싱글턴, `Resources.Load`, 동기) | — | — | — |

**설계상 좋은 점(확인)**: 품절을 목록이 아니라 **시드**로 복제 · 구매 거절 사유를 `TechDeny`/`UseDeny` enum으로 **판정한 쪽이** 말함 · `CanApplyLevel`을 차감 **전에** 둬서 "돈만 먹는 구매"를 막음.

---

## 2. 데이터 정합성

### 2-1. TechId enum ↔ `TechInfo.csv` ↔ `ShopController.CanApplyLevel` 3자 대조

**[확인] 3자가 완전히 일치한다. 못 사는 테크는 0건.** (스크립트로 전수 대조)

| 집합 | 개수 | 차집합 |
|---|---|---|
| `TechId` enum (None 제외) | 16 | — |
| CSV 고유 TechId | 16 (48행 = 16 × 3레벨) | enum−CSV = ∅ · CSV−enum = ∅ |
| `CanApplyLevel` case | 16 | enum−switch = ∅ |
| `TryApplyLevel` case | 16 | `CanApplyLevel` ↔ `TryApplyLevel` 차집합 = ∅ (같은 목록 규약 지켜짐) |

과거 `BigPouch` 구매 불가와 같은 사고는 **지금 없다**.

### 2-2. 어긋난 것 — 10건

| # | 무엇 | 어긋난 내용 | 근거 | 심각도 |
|---|---|---|---|---|
| D-1 | **빠른 발(FastFeet) 값이 기획서와 다르다** | 기획서 §4-5: `+0.3 / +0.7 / +1.2` → 이속 7.8, "고양이 8.6에 근접". CSV: `0.2 / 0.45 / 0.7` → 6.6+0.7 = **7.3**, 라벨은 "고양이 7.4에 근접" | `TechInfo.csv:35-37` · `PlayerInfo.csv` Cat MoveSpeed **7.4** · 핸드오프 qa_v47 실측은 **7.8**(옛 값) | 중 — 기획서 stale. **CSV가 정답으로 보이나 확인 필요** |
| D-2 | **창고 Lv1 가격 표기 불일치** | 기획서 §4-3: 창고 Lv1 = 150. CSV = **50**(설치권) + `WarehousePlaceCost` 100 = 150 | `TechInfo.csv:6` · `BalanceInfo.WarehousePlaceCost=100` · 핸드오프에 의도 기록 | 낮 — **의도된 분해**, 기획서만 미갱신 |
| D-3 | **§3 「합 7 380」이 실제 7 280** | D-2에서 100 차이. 축당 범위도 "920~2 280" → 실제 **920~2 180**(자원 축) | 계산: 쥐 30행 합 = 7 280 | 낮 — 문서 수치 |
| D-4 | **긴 총열 DPS 배율 1.3 vs 1.4** | 기획서 §4-1 "피해 ×1.3". `WeaponInfo` Tier3 = 7/0.15 = 46.7 vs Tier2 33.3 → **×1.4** (CSV Note·Hint도 ×1.4) | `WeaponInfo.csv:5` · `TechInfo.csv:14` | 낮 — 구현·CSV 일치, 기획서만 다름 |
| D-5 | **고양이 이속 기준값 불일치** | 기획서 여러 곳이 고양이 8.6 전제. 실제 `PlayerInfo` Cat = **7.4** (+발 구르기 Lv3 0.6 = 8.0) | `PlayerInfo.csv:2` | 중 — §0-1 「발 싸움은 고양이」의 여유가 기획서보다 작다 |
| D-6 | `BalanceInfo.WarehouseLimitPerMouse` **읽는 곳 0** | 상한의 주인이 `WarehouseLogic.LimitOf`(TechInfo P1)로 이동 | `ShopLogic.cs:48`에 사유 주석 | 낮 — **의도적 잔존** |
| D-7 | **단조성 예외는 Gun 하나뿐** | Gun 150/600/450. 나머지 15종 전부 단조 증가 | 전수 확인 + `TechInfoTests`가 이미 핀 고정 | — 정상 |
| D-8 | `CatClaw`/`CatStomp`는 **레벨=스택**인데 상한이 두 곳 | `BalanceInfo.MaxUpgradeStacks`(3) ↔ CSV 최대 레벨(3). 지금 일치하나 **한쪽만 4로 올리면 치즈는 나가고 스택은 안 오른다** | `PlayerMovement.ServerAddSpeedStack` · `CatPounce.ServerAddDamageStack` | 중(잠재) — 테스트 없음 |
| D-9 | `BalanceInfo` 94컬럼 중 **코드가 읽는데 CSV에 없는 키 = 0** | 전수 확인 | — | — 정상 |
| D-10 | `InfoManager.LoadInfo`는 **CSV를 `,`로 단순 split** | Label/Hint에 쉼표가 들어오면 컬럼이 밀린다. `TechInfoTests`가 **TechInfo만** 컬럼 수를 센다 — `WeaponInfo.Note`는 안 센다 | `InfoManager.cs:80` · `TechInfoTests.cs:139-152` | 낮(잠재) |

### 2-3. `WeaponInfo` 티어 ↔ `TechInfo` Gun 레벨

**[확인] 연결은 `gun.ServerSetTier(_level)` 한 줄이다.** Gun Lv1→Tier1, Lv2→Tier2, Lv3→Tier3, 미구매=Tier0(딱총).
`InfoCustom.WeaponOf`가 `Clamp(0, Count-1)`이라 **표에서 티어가 빠져도 조용히 이웃 티어를 준다** — [의심] 회귀 위험.

---

## 3. 이미 검증되는 것

| 무엇 | 어디서 | 비고 |
|---|---|---|
| CSV 48행·중복 없음·레벨 1..N 연속 | `TechInfoTests.TableIsWellFormed` | 강함 |
| 비용 단조 증가 (Gun만 예외, 150/600/450 핀) | 동상 | 새 테크가 내려가면 잡힌다 |
| Label/Hint 비어 있지 않음 · TechInfo.csv 쉼표 안전 | 동상 | |
| 같은 테크의 Role/Axis 일관 | 동상 | |
| 카탈로그 순서·개수(쥐 10 · 고양이 6) | `TechLogicTests` | |
| `TechDeny` 8종 중 7종 (None·SlotsFull·MaxLevel·WrongRole·NotEnough·Unknown·SoldOut) | `TechLogicTests` · `TechRandomLogicTests` | **NotInShop만 EditMode 밖** |
| `UseDenyText` 문구 6종 | `TechLogicTests` | **판정(`CanUse`)은 미검증** |
| 품절 추첨 결정성·면제 3종·카탈로그 순 정렬 | `TechRandomLogicTests` | |
| 요새 HP/반사 사다리 · 고양이 테크 사다리 · Balance 테크 컬럼 8개 | `TechInfoTests` | |
| 창고·숨은 창고 수학(상한·배율·소리·HP·환급) | `WarehouseTechLogicTests` | |
| 치즈 방패·후추 도트·끈끈이 수학(겹침 최솟값) | `CheeseShieldLogicTests`·`PepperDotLogicTests`·`StickyLogicTests` | |
| 광분×전직 불 = max(곱 아님) | `TechLogicTests` | |
| 무기 티어 0~3 + 범위 밖 클램프 | `ShopLogicTests` | |
| 상점 밖 구매 거절(차감 0) · 상점 안 구매/업글 · 칸 5 · 요새 설치비 · 고양이 상점 면제 · 역할 불일치 | **qa_v46** (MPPM 3인) | |
| 쥐 테크 효과 9종 실측 | **qa_v47** | 숨은 창고 무음 SKIP |
| 고양이 테크 5종 실측 · 칸 5 상한 6번째 거절 | **qa_v48** | **광분은 QA 훅으로 켬** |
| 시드·상점 자리·품절·직업권 셔플·전직 300·봇 상점 이동 | **qa_v50** (판 3회 비교) | |
| SFX 클립 배선 누락 전수 | `TestGetUnwiredSfxIds` | 클립 **유무**만, 재생 여부 아님 |

---

## 4. 검증 공백

| 심각도 | # | 무엇 | 왜 위험한가 | 근거 |
|:---:|---|---|---|---|
| **높음** | G-1 | **고양이는 광분(액티브)을 키로 켤 수 없다** | `ShopHotbar.Update`가 `inShop = !NeedsShopSpot(role) \|\| ...` 라 **고양이는 항상 `m_InShop = true`**. `HandleHotkeys`는 `m_InShop`이면 무조건 `TryBuy`로 간다 → 고양이 숫자키는 영원히 "산다/키운다". `CatFrenzy`에 다른 입력 경로가 **없다**(`MouseDash`의 Space 같은 것이 없고 `AbilityBar`에도 칸이 없다). `CooldownOf`도 Sticky·Dash만 다뤄 쿨다운조차 안 보인다. qa_v48 ③이 통과한 건 `TestUseTech` **훅으로 직접 RPC를 쐈기 때문** | `ShopHotbar.cs:291` · `:431` · `:369-375` · `CatFrenzy.cs`(입력 0) · `TechLogic.cs:184`가 CatFrenzy를 액티브로 선언 |
| **높음** | G-2 | **액티브 사용 경로에 사람-입력 QA가 하나도 없다** | 요새·끈끈이·대시·광분·창고 전부 wesqa는 `TestUseTech*` 훅으로만 검증. G-1이 정확히 이 사각지대에서 나왔다. 「핫바 키 → `CanUse` → RPC」 사슬이 **한 번도** 실제 키로 검증된 적 없다 | `qa_v47`·`qa_v48` 전 항목 · `TestManager.Tech.cs:174-183` |
| **높음** | G-3 | **`UseDeny` 7종의 *판정*이 전혀 테스트되지 않는다** | 문구(`UseDenyText`)만 EditMode에 있고 `ShopController.CanUse`는 MonoBehaviour라 EditMode 밖, wesqa에도 없다. `InsideFortress`·`LimitReached`·`Cooldown`·`NotEnough`(설치비)·`Unavailable` 전부 미검증. 2026-09-07에 「치즈 부족을 창고 상한이라 거짓말한」 사고가 난 바로 그 자리 | `ShopController.cs:140-190` · `TechLogicTests.cs:265-289`(문구만) |
| 중 | G-4 | **상점 안에서는 액티브를 못 쓴다**(쥐 포함) | §2 규칙 3·4의 결과지만 「쫓겨서 상점에 뛰어든 쥐가 요새·끈끈이를 못 쓴다」는 밸런스 사건. 의도 확인·검증 필요 | `ShopHotbar.cs:431` |
| 중 | G-5 | **대시만 입력이 둘(Space + 숫자키)** | §4-6 "액티브 3개뿐이라 키 충돌 없다 — 칸이 곧 키". 대시는 **상점 안에서도 Space로 된다**, 다른 액티브는 안 된다 → 비대칭 | `MouseDash.cs:100` · `ShopHotbar.cs:431` |
| 중 | G-6 | **`TechDeny.NotInShop` EditMode 없음** | 상점 판정이 `CanBuyNow`(MonoBehaviour)에 있어 순수 로직 밖. qa_v46 ②가 유일한 방어선 | `ShopController.cs:126-134` |
| 중 | G-7 | **CSV ↔ 기획서 대조 테스트 없음** | D-1(빠른 발)·D-5(고양이 이속)가 **아무 경보 없이** 생겼다. 기획서를 못 읽는 테스트라 자동화는 어렵지만, 「구현 확정값」을 EditMode 핀으로 고정하면 다음 드리프트를 잡는다 | §2-2 D-1·D-5 |
| 중 | G-8 | **`CanApplyLevel` 완전성 테스트 없음** | 지금은 16/16 일치하나, 새 TechId를 enum+CSV에만 넣으면 **런타임 경고 한 줄과 구매 거절**로만 드러난다(과거 BigPouch) | `ShopController.cs:280-320` |
| 중 | G-9 | **잔액 부족 경계값 실플레이 없음** | EditMode는 `149 vs 150`만 본다. 실플레이에서 `TrySpend` 실패 시 레벨이 안 오르고 치즈도 안 빠지는지 미확인 | `ShopController.BuyRpc` |
| 중 | G-10 | **`TryApplyLevel`의 반환값을 아무도 안 본다** | `CanApplyLevel` 통과 → `TrySpend` 성공 → `TryApplyLevel` false여도 치즈는 이미 나갔고 `ResultRpc(true)`가 간다. 두 switch가 같은 목록이라 지금은 안 나지만 **규약이 코드로 강제돼 있지 않다** | `ShopController.BuyRpc` 호출부 |
| 중 | G-11 | **숨은 창고 Lv3 「무음」 검증 훅 없음** | qa_v47 ⑦-b SKIP 그대로. `SfxPlayer.Played`에 붙는 `SoundLog`가 이미 있어 `TestGetSfxPlayCount` 신설이 쉽다 | 핸드오프 · `Bots/SoundLog.cs:39` |
| 중 | G-12 | **원샷 애니메이션 재생 여부 훅 없음** | 계획 2b Task 5(동작 6종)는 **스크린샷 육안**으로만 검증. 상태 이름 오타는 조용히 죽는다 | 핸드오프 · qa_v48 ⑦ SKIP |
| 낮 | G-13 | **끈끈이 겹침은 EditMode(`MinMultiplier`)로만** | 장판 2장을 겹쳐 깔고 하나만 만료시키는 훅이 없다 | 핸드오프 |
| 낮 | G-14 | **`StickyPatch`는 스폰 시 고양이 명단을 굳힌다** | 판 도중 들어온 고양이는 이미 깔린 장판에 안 걸린다(수명 10초라 감수) — 문서화만 됐고 테스트 없음 | `StickyPatch.cs:36-41` |
| 낮 | G-15 | **인트로가 "표 4종 불러옴"이라 말하고 `Tech`만 본다** | `BalanceInfo.csv`를 못 읽어도 인트로는 초록. 밸런스가 전부 0인 채로 판이 선다 | `IntroDirector.cs:134` |
| 낮 | G-16 | **`WeaponInfo`·`BalanceInfo`·`PlayerInfo` 컬럼 수 검사 없음** | 쉼표가 들어가면 조용히 밀린다(TechInfo에만 검사가 있다) | `TechInfoTests.cs:141` |
| 낮 | G-17 | **핫바 슬롯 배열 길이 ≠ `TechSlotCap`은 경고만** | `Awake`에서 `LogWarning` 후 그대로 진행. 배포 빌드에선 `GameDebug`가 지워져 아무도 모른다 | `ShopHotbar.cs` Awake |

---

## 5. 규칙·제약 위반

| 규칙 | 판정 | 내용 |
|---|:---:|---|
| 1 코루틴 금지 | ✅ | `StartCoroutine`/`IEnumerator` **전 프로젝트 0건**. 전부 UniTask |
| 2 `onClick.AddListener` 금지 | ✅ | **0건**. R3로 통일 |
| 3 `Update` 폴링·오브젝트 탐색 금지 | ⚠️ | **`UI/CatSkillView.cs:49` — 매 프레임 `GetLocalPlayerObject()`+`GetComponent`, 0.5초마다 `FindObjectsByType<RoleTag>` 전수. 명백한 위반**(테크 v3 이전 코드) · `Audio/MatchSfx.cs:19` — `GameStarted`·`Result`·`RemainingSeconds`를 매 프레임 엣지 감지(셋 다 R3 스트림이 있다) [의심] · `ShopHotbar.Update`는 위치 판정이라 **정당**(주석에 사유), `AbilityBar.Update`는 주석으로 예외 선언 |
| 4 `OnClick~` 이름 | ✅ | 해당 없음(핫바는 키보드) |
| 5 MVC — View가 판정 금지 | ✅ | `ShopHotbar`가 `CanBuyNow`/`CanUse`/`UseCostOf`로 **전부 위임**. 모범 사례 |
| 6 Worker 분리 | ✅ | `LocalPlayerBinder`·`ShopSpotSpawner`·`MinimapMarkers` |
| 7 컴포넌트 조합 | ⚠️ | `ShopController` 563줄이 구매·사용·설치·FX·배치검증을 모두 갖는다. `TechId` 추가마다 switch 4곳이 늘어난다 [의심: 리팩터 후보] |
| 8 코드 다이어그램 갱신 | — | 이번 감사 범위 밖 |
| 9 Unity 객체 `!= null` | ✅ | `?.`/`??` 위반 **0건**. `CheeseSpot.cs:177`의 `m_Mpb ??=`는 `MaterialPropertyBlock`(순수 C#)이라 허용 |
| 입력 가드 `UiFocus.BlocksGameplay` | ✅ | 게임플레이 입력 9곳 전부 가드 |
| ESC/Enter 단독 라우팅 | ✅ | `EscapeRouter`가 단독 |
| **§9 새 화면 0** | ✅ | 새 Canvas/팝업 없음. 상점은 기존 핫바 라벨 교체 |
| **§9 커서 해제 0** | ✅ | `Cursor.lockState` 해제는 로비·결과·방 화면뿐. **상점·핫바 경로에 없다** |
| §9 프리팹 0 | ⚠️ | `ShopSpot.prefab`·`StickyPatch.prefab` **2개 신규**. 팩 프롭 + 쿼드 조립이라 취지(새 모델 0)에는 부합 |

---

## 6. 소리 없는 동작 목록

계획 2b가 원샷 모션을 붙인 6종 + 테크 v3 전체를 `SfxId` enum과 호출부로 대조했다.

| 동작 | SFX | 상태 | 근거 |
|---|---|:---:|---|
| 대시 | `MouseDash` | ✅ | `MouseDash.cs:130` |
| **끈끈이 설치** | **없음** | ❌ **무음** | `StickyPatch.prefab`에 `PlaceSfxOnSpawn` 없음(unity-scanner 확인) · `StickyPatch.cs`·`StickyUser.cs`에 `SfxPlayer` 호출 0. **기획서 §11 「끈끈이 … SFX 1」 미이행** |
| 요새 설치 | `FortressPlace` | ✅ | `PlaceSfxOnSpawn` (m_Sfx=10) |
| **창고 설치** | `FortressPlace` **재사용** | ⚠️ **요새와 구분 불가** | `CheeseWarehouse.prefab`의 `m_Sfx: 10`. 저장소 자신의 원칙("소리가 정보인 게임이라 겹치면 안 된다", `SfxPlayer.cs:32`)과 충돌 — 고양이가 「벽인가 돈줄인가」를 못 가린다 |
| **광분** | `CatSkill` **재사용** | ⚠️ **전직 스킬과 구분 불가** | `CatFrenzy.cs:121` = `CatSkill.cs:102`와 같은 id. 쥐가 「불이 켜졌나 광분인가」를 못 가린다 |
| 구매·업그레이드 | `ShopBuy` (12 m) | ✅ | `ShopController.cs:400` |
| 채굴 | `CheeseTick` | ✅ | `FarmingSfx.cs:41` |
| **치즈 방패 발동** | **없음** | ❌ 무음 | `CheeseShield.cs` — 치즈가 줄어드는데 아무 신호가 없다. 「째서 번 돈이 목숨」의 피드백 없음 |
| **가시 벽 반사(25)** | 없음(고양이 피격음에 묻힘) | ⚠️ | `ThornWall` — 고양이가 「왜 깎였지」를 모른다 |
| 구매 거절 | `ShopDeny` | ✅ | 구매자에게만(2D) |
| 오늘의 품절 안내 | 없음(토스트만) | ⚠️ | `ShopHotbar.TrySaySoldOut` |
| 숨은 창고 Lv3 | 의도적 무음 | ✅ 의도 | `WarehouseProduction.ProduceFxRpc` |

**무음/모호 합계: 확실한 무음 2(끈끈이·치즈 방패) + 소리 겹침 2(창고·광분) + 신호 없음 2(가시 벽 반사·품절) = 6.**

### 6-b. 소리는 나는데 **봇 고양이가 못 듣는 것** [확인]

`BotSenseLogic.IsMouseMade`에 **`ShopBuy`가 없다**(`BotSenseLogic.cs:67-90`).
→ 기획 §3의 시간 결핍(「구매음 12 m가 퍼져 고양이가 상점을 지킨다」)이 **봇 계측에 통째로 빠져 있다.**
「쥐 승률 0%」를 봇으로 재는 동안 고양이는 이 정보 채널을 전혀 쓰지 않는다.

### 6-c. 기획 §0-2 미이행 [확인]

기획서 §0-2는 「고양이가 사고 키우면 **전원 토스트**」를 요구한다(가위바위보가 성립하려면 쥐가 고양이의 축을 봐야 한다).
**전 소스에서 그런 브로드캐스트 토스트를 찾지 못했다.** 지금 쥐가 고양이 테크를 아는 길은 맞아 보는 것뿐이다.

---

## 7. 제안 테스트 케이스 (30건)

우선순위: **P0** = 이번 라운드 필수 · **P1** = 다음 · **P2** = 여유 있을 때.

| ID | 무엇을 검증 | 종류 | 필요한 훅 | 우선 |
|---|---|---|---|:--:|
| **C-01** | **고양이가 광분을 산 뒤 그 슬롯 숫자키를 누르면 켜진다** — 실제 키(또는 `HandleHotkeys` 진입점) → `TestGetCatFrenzyActive == true`. **지금은 실패할 것으로 예상**(G-1) | wesqa | 키 주입 or `TestPressSlotKey` (**신규**) + `TestGetCatFrenzyActive`(있음) | **P0** |
| **C-02** | 쥐가 상점 **밖에서** 요새 슬롯 숫자키 → 요새가 서고 치즈 −80 | wesqa | `TestPressSlotKey`(신규) · `TestGetFortressPos`(있음) · `TestGetCheeseOf`(있음) | **P0** |
| **C-03** | 쥐가 상점 **안에서** 요새 슬롯 숫자키 → 요새가 **안 서고 업그레이드가 일어난다**(§2 규칙 3의 실제 결과 확정) | wesqa | 위 + `TestGetTechLevel`(있음) | **P0** |
| **C-04** | `CanUse` 6종 사유가 각각 정확히 나온다 (`InsideFortress`/`LimitReached`/`Cooldown`/`NotEnough`/`NotOwned`/`NotActive`) | wesqa | **신규 `TestGetUseDeny(clientId, techId)`** | **P0** |
| **C-05** | 요새 안에 선 채 요새 재설치 → `InsideFortress` 거절 · **치즈 차감 0** | wesqa | C-04 훅 + `TestGetCheeseOf` | **P0** |
| **C-06** | 창고 Lv1로 2번째 창고 → `LimitReached`·차감 0. Lv2로 올린 뒤 2번째는 성공 | wesqa | `TestGetWarehouseCountOf`(있음) | **P0** |
| **C-07** | 끈끈이 쿨다운 중 재사용 → `Cooldown` · 장판 개수 불변 | wesqa | `TestGetStickyPatchCount`(있음) + C-04 훅 | P1 |
| **C-08** | 설치비에 딱 1 모자란 잔액(79)으로 요새 사용 → `NotEnough` · 차감 0 · 요새 0개 | wesqa | **신규 `TestSetCheese`** | **P0** |
| **C-09** | 구매 경계값: `cost−1`로 구매 → 레벨 0·차감 0. `cost` 정확히 → 레벨 1·잔액 0 | wesqa | **신규 `TestSetCheese`** | **P0** |
| **C-10** | **`CanApplyLevel` 전수 완전성** — 모든 `TechId`(None 제외)가 switch에 있고 `TryApplyLevel`과 **같은 집합** | EditMode | switch를 `static IReadOnlyCollection<TechId>`로 노출(소규모 리팩터) | **P0** |
| **C-11** | **enum ↔ CSV 완전성** — `TechId` 전 멤버가 CSV Lv1~3 행을 갖는다(카탈로그 **개수 상수**가 아니라 **enum 열거**로) | EditMode | 없음 | **P0** |
| **C-12** | **기획 확정값 핀** — 빠른 발 P1 = 0.2/0.45/0.7 · 고양이 MoveSpeed 7.4 · 창고 Lv1 Cost 50 (**D-1·D-5를 사용자에게 확정받은 뒤**) | EditMode | 없음 | **P0**(값 확정 후) |
| **C-13** | **쥐 이속 상한 < 고양이 이속 하한** — `Mouse.MoveSpeed + FastFeet Lv3 < Cat.MoveSpeed`. 두 CSV가 따로 움직여도 「발 싸움은 고양이」가 안 뒤집히게 | EditMode | 없음 | **P0** |
| **C-14** | **스택 상한 ≥ 최대 테크 레벨** — `MaxUpgradeStacks ≥ MaxLevel(CatClaw)`·`MaxLevel(CatStomp)` (D-8) | EditMode | 없음 | **P0** |
| **C-15** | 쥐 **칸 5 상한** — 여섯째 쥐 테크 구매 거절·차감 0 (qa_v48 ⑤의 쥐 대응판) | wesqa | 있음 | P1 |
| **C-16** | 품절 테크는 **핫바 후보에 아예 안 뜬다** | wesqa | **신규 `TestGetHotbarCandidates`** | P1 |
| **C-17** | 품절 토스트가 **기본 조작 토스트에 안 덮인다** — 판 시작 5초 안에 "오늘 품절:"이 화면에 남는다 | wesqa | **신규 `TestGetToastText`** | P1 |
| **C-18** | **끈끈이 설치음이 1회 난다** — 지금은 SFX 자체가 없다(§6) → `SfxId.StickyPlace` 신설이 선행 | wesqa | **신규 `TestGetSfxPlayCount(id)`** + 신규 SfxId | P1 |
| **C-19** | **숨은 창고 Lv3 무음** — 창고 틱이 도는데 `WarehouseTick` 재생 0 (qa_v47 ⑦-b SKIP 해소) | wesqa | **신규 `TestGetSfxPlayCount`** | P1 |
| **C-20** | **창고 설치음 ≠ 요새 설치음** — 두 프리팹의 `PlaceSfxOnSpawn.m_Sfx`가 다르다 | EditMode | `AssetDatabase` 로드 | P1 |
| **C-21** | **광분 소리 ≠ 전직 스킬 소리** | EditMode | SfxId 분리 선행 | P1 |
| **C-22** | 봇 고양이가 **구매음을 듣는다** — `IsPreySound(ShopBuy, Mouse) == true` (6-b) | EditMode | 없음(1줄) | P1 |
| **C-23** | **원샷 모션 재생 확인** — 요새 설치 후 0.5초간 `Jump` 상태(동작 6종 전부) | wesqa | **신규 `TestGetAnimState`** | P1 |
| **C-24** | 숨은 창고 Lv3 창고가 부서지면 **설치비 절반 환급 + `EarnedValue`는 불변**(전직 펌프 방지) | wesqa | `TestGetCheeseOf`·`TestGetEarnedOf`(둘 다 있음) | P1 |
| **C-25** | 치즈 방패 보유 + **잔액 0** → 피해가 전액 HP로 간다(「지갑이 비면 방패도 없다」, §4-2) | wesqa | **신규 `TestSetCheese`** | P1 |
| **C-26** | 끈끈이 2장을 겹쳐 깔고 **하나만 만료** → 남은 장판 배율 유지 | wesqa | 위치 지정 설치 훅(신규) | P2 |
| **C-27** | 판 도중 접속한 고양이는 **이미 깔린 장판에 안 걸린다**(G-14를 사양으로 못 박거나 고친다) | wesqa | 없음(MPPM 접속 순서 조작) | P2 |
| **C-28** | 인트로 데이터 검사가 **표 4종 전부**를 본다 — `BalanceInfo`를 비우면 Failed | EditMode | `CheckGameData` 분리 | P2 |
| **C-29** | **모든 CSV의 컬럼 수 검사** — `BalanceInfo`·`PlayerInfo`·`WeaponInfo`도 헤더와 같은 컬럼 수 | EditMode | 없음 | P2 |
| **C-30** | 핫바 슬롯 배열 길이 == `TechSlotCap` (씬 배선 회귀) | wesqa | **신규 `TestGetHotbarSlotCount`** | P2 |

### 7-b. 신규 훅 요약 (테스트보다 이것이 먼저)

| 훅 | 쓰는 케이스 | 난이도 |
|---|---|---|
| `TestPressSlotKey(int slot)` — **`ShopHotbar`의 키 분기를 그대로 태운다** | C-01·C-02·C-03 | 낮음 |
| `TestGetUseDeny(long clientId, string techId)` | C-04~C-08 | 낮음(`CanUse` 한 줄 노출) |
| `TestSetCheese(long clientId, int amount)` | C-08·C-09·C-25 | 낮음 |
| `TestGetSfxPlayCount(string id)` — `SfxPlayer.Played` 카운터 | C-18·C-19 | 낮음(`Bots/SoundLog` 패턴 재사용) |
| `TestGetToastText()` / `TestGetHotbarCandidates()` / `TestGetHotbarSlotCount()` | C-16·C-17·C-30 | 낮음 |
| `TestGetAnimState(long clientId)` | C-23 | 중간 |

> **C-01~C-03의 요점**: RPC를 직접 쏘지 말고 **`ShopHotbar.HandleHotkeys`가 실제로 타는 분기**를 지나게 한다. `TestPressSlotKey`가 그 분기와 같은 코드를 부르기만 하면 훅이라도 목적을 달성한다 — **`ShopController.Use`를 직접 부르면 안 된다.** 그게 G-1을 지금까지 놓친 이유다.

---

## 8. 사용자 결정이 필요한 것

| # | 물음 | 왜 지금 |
|---|---|---|
| 1 | **빠른 발 / 고양이 이속** — CSV(0.2/0.45/0.7, 고양이 7.4)가 정답인가, 기획서(0.3/0.7/1.2, 고양이 8.6)가 정답인가 | C-12·C-13이 어느 값을 핀으로 박을지가 여기 달렸다 |
| 2 | **상점 안에서 액티브를 못 쓰는 것**이 의도인가 | C-03의 기대값 |
| 3 | **대시의 Space 키**를 남길 것인가(§4-6과 비대칭) | C-02·G-5 |
| 4 | 끈끈이·창고·광분에 **전용 SFX를 새로 넣을 것인가** | C-18·C-20·C-21 |
| 5 | **고양이 테크 전원 토스트**(§0-2)를 지금 구현할 것인가 | 6-c — 가위바위보 기조의 전제 |
