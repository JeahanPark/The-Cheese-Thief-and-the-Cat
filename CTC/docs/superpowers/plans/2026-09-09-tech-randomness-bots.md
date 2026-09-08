# 테크 v3 계획 3 — 무작위 3 + 전직 300 + 봇 상점 이동 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기획서 §7의 무작위 3곳(상점 자리·오늘의 품절·직업권)을 판마다 다르게 만들고, 전직 문턱을 실효 780 → **300 장난기**로 내리고, **봇이 사람과 똑같이 상점까지 걸어가게** 한다.

**Architecture:** 무작위 셋의 원본은 **매치 시드 하나**(`MatchRandom`, `NetworkVariable<int>`)다. 시드가 복제되므로 서버가 뽑은 답을 클라가 **다시 계산**한다 — 품절 목록을 NetworkList로 실어 보내거나 RPC로 알리지 않는다. 판정은 전부 순수 static(`ShopSpotLogic`·`TechRandomLogic`·`TicketShuffleLogic`)이고 MonoBehaviour는 시드를 먹이고 결과를 쓰기만 한다. 봇 면제(`ShopController.IsBotExempt`)를 없애면 봇도 사람과 **같은 서버 판정**을 통과해야 하므로, 봇 쥐의 행동 트리에 「상점으로 간다」 가지가 하나 는다.

**Tech Stack:** Unity 6000.5 · NGO 2.12 · R3 · UniTask · CSV 파이프라인 · NUnit EditMode · wesqa

**기준:** 기획서 `docs/superpowers/specs/2026-09-06-tech-shop-design.md` §7(확률)·§5(전직 300)·§9(제약). HEAD `cadafc3`, EditMode **519/519**, qa_v46~49 초록.

**규칙(반드시):** `m_`/`_` · public 필드 금지 · UPPER_SNAKE_CASE · 코루틴 금지(UniTask) · R3 · Unity 객체는 `!= null`만(`?.`·`??` 금지) · 밸런스는 CSV · 생성 파일 손대지 않음(메뉴 `Tools/CTC/Convert CSV → Info`) · 코드 고치면 `docs/design-vault/코드 다이어그램.md` 갱신 · EditMode = `Tools/CTC/Run EditMode Tests` · 에셋 바뀌면 `u_editor_asset(refresh)` · 노이즈(`Assets/Materials/M_*.mat`·폰트 SDF·fileID만 바뀐 프리팹/씬) 스테이징 금지.

---

## 0. 이 계획이 내린 판단 (먼저 읽는다)

| # | 판단 | 이유 |
|---|---|---|
| 1 | **상점 개수를 못 채우면 간격을 반으로 줄여 재시도**한다 (맵 크기 비례 간격 **기각**) | 25 m는 "고양이 하나가 두 상점을 동시에 못 지키는 거리"라 **절대값에 뜻이 있다**. 맵 크기로 곱하면 30×30에서 6 m가 되어 상점 둘이 나란히 서고, 그러면 상점을 넷 만든 이유(§3 시간 결핍)가 조용히 사라진다. 재시도는 25를 **목표**로 두고 이 맵이 감당하는 만큼만 양보한다 — 대형 맵에선 한 번도 안 걸린다 |
| 2 | 목표 **개수**도 맵에서 뽑는다 (`ShopSpotCountSmall` 2 · `ShopSpotLargeHalfExtent` 40) | 기획서 §6이 이미 "대형 4곳 · 기본·확장 2곳"이라고 답했는데 CSV엔 전역 `ShopSpotCount` 하나뿐이다. 재시도만 넣으면 30×30에 상점 넷이 몰린다 — 1번이 지키려던 것을 개수가 깬다 |
| 3 | 무작위 셋의 원본은 **매치 시드 하나**, 용도별로 `SubSeed`로 갈라 쓴다 | 시드를 공유하면 "상점이 왼쪽에 서는 판은 늘 후추가 품절"처럼 **상관이 생긴다**. 갈라 쓰면 판마다 독립이고, 시드 하나만 복제하면 클라도 같은 답을 계산한다(대역폭 4바이트) |
| 4 | **전직 문턱은 누적 획득 장난기 300** — 잔액도, 구매 이력도 아니다 | 지금 조건(발톱·발 구르기 만렙)이 실효 **780**(420+360)이다. 이건 "칸 5 중 무엇을 고르나"를 정면으로 부순다 — 전직하려면 정해진 두 테크를 사야 하니까. 누적 획득이면 **무엇을 샀든** 판을 오래 산 고양이에게 열리고, 쓴 돈이 문턱을 되돌리지 않는다 |
| 5 | 광분과 전직 「불」은 **곱하지 않고 큰 쪽만** 쓴다 | 계획 2b가 남긴 미결(×2.5 × ×2 = ×5.0)이 전직 300으로 **흔해진다**. 기획서 §5의 "불을 고르면 광분이 흡수된다"를 가장 싸게 구현한 것이 max다 |
| 6 | 봇 면제 제거는 **계측 기준선을 바꾼다** — 전/후를 같이 남긴다 | 지금 봇 수치엔 상점 왕복 시간이 통째로 빠져 있다. 안 재고 갈아 끼우면 이전 계측이 전부 거짓말이 된다 |

**제약 확인 (기획서 §9)** — 새 모션 0 · VFX 0 · 프리팹 0 · 새 화면 0 · 커서 해제 0. **하나도 안 깬다.**
`MatchRandom`은 프리팹이 아니라 기존 `LobbyController` 오브젝트에 동승하는 컴포넌트고, 품절 알림은 기존 `ShopHotbar` 토스트를 쓴다.

**깨는 것은 아니지만 알아 둘 것 하나**: 손 배치 창고 맵(`Layout_Warehouse01`)은 상점 후보가 **정확히 4곳**이라
아무리 잘 섞어도 매 판 같은 자리다. Task 2에서 후보를 **8곳으로 늘려** 그중 4곳을 뽑는다 —
이건 레벨디자인 자산을 건드리므로 `ctc-8b` 세션과 충돌 위험이 있다(Task 2 Step 5에 절차를 적어 뒀다).

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Match/MatchRandom.cs` (신규) | 매치 시드 Model — 서버가 굴리고 전원이 읽는다 · `SubSeed` |
| `Assets/Scripts/Editor/Setup/SceneComposer.cs` (수정) | `LobbyController` 오브젝트에 `MatchRandom` 동승 |
| `Assets/Scripts/Lobby/LobbyController.cs` (수정) | 판 시작에 시드를 **스포너보다 먼저** 굴린다 |
| `Assets/Scripts/Map/ShopSpotLogic.cs` (수정) | `CountFor` · 간격 완화 재시도 · 손 배치 자리도 뽑는다 |
| `Assets/Scripts/Map/ShopSpotSpawner.cs` (수정) | 맵 크기·매치 시드를 먹인다 |
| `Assets/Scripts/Editor/WarehouseLayoutBuilder.cs` (수정) | 상점 후보 4 → 8 |
| `Assets/Scripts/Economy/TechRandomLogic.cs` (신규) | 오늘의 품절 추첨(순수) |
| `Assets/Scripts/Economy/TechLogic.cs` (수정) | `TechDeny.SoldOut` · `CanBuy`에 품절 인자 |
| `Assets/Scripts/Economy/ShopController.cs` (수정) | 품절 전달 · **`IsBotExempt` 제거** |
| `Assets/Scripts/Economy/ShopHotbar.cs` (수정) | 후보 목록에서 품절 제외 · 판 시작 품절 토스트 |
| `Assets/Scripts/Map/TicketShuffleLogic.cs` (신규) | 직업권 자리 셔플(순수) |
| `Assets/Scripts/Map/LootBoxSpawner.cs` · `AlcoveSpawner.cs` (수정) | 스폰 직전 내용물 **통째 교환** |
| `Assets/Scripts/Economy/CheeseWallet.cs` (수정) | 누적 획득(`EarnedValue`) |
| `Assets/Scripts/Player/CatClassLogic.cs` · `CatClass.cs` (수정) | 전직 문턱을 누적 장난기로 |
| `Assets/Scripts/Combat/CatPounce.cs` (수정) | 광분 × 불 → **max** |
| `Assets/Scripts/Map/ShopSpot.cs` (수정) | `Nearest(point)` |
| `Assets/Scripts/Bots/MouseBotLogic.cs` (수정) | 품절 건너뛰기 · `WantsShopping` |
| `Assets/Scripts/Bots/BotAgent.cs` · `BotBrainTrees.cs` · `BotShopper.cs` (수정) | 「상점으로 간다」 가지 |
| `Assets/Resources/CSVInfo/BalanceInfo.csv` (수정) | 5키 추가 |
| `Assets/Scripts/Testing/TestManager.Tech.cs` · `TestManager.CatClass.cs` (수정) | QA 훅 |
| `Assets/Scripts/Tests/EditMode/*` | 신규 4 + 갱신 4 |
| `tools/wesqa/tests/qa_v50.py` (신규) | MPPM 3인 E2E |
| `docs/design-vault/코드 다이어그램.md` | 규칙 8 |

**CSV 신규 5키** (`BalanceInfo`)

| 키 | 값 | 뜻 |
|---|---|---|
| `ShopSpotCountSmall.int` | 2 | 작은 맵의 상점 수 |
| `ShopSpotLargeHalfExtent.float` | 40 | 이 반경 이상이면 「대형」 — `ShopSpotCount`(4)를 쓴다 |
| `SoldOutCount.int` | 2 | 판마다 못 사는 쥐 테크 수 |
| `CatAdvanceMischief.int` | 300 | 전직 문턱(누적 획득 장난기) |
| `BotShopTravelMinCheese.int` | 0 | 0 = 다음에 살 것의 값을 그대로 쓴다(봇이 헛걸음하지 않게 하는 여유분) |

---

### Task 1: 매치 시드 `MatchRandom`

**Files:** Create `Assets/Scripts/Match/MatchRandom.cs`, `Assets/Scripts/Tests/EditMode/MatchRandomLogicTests.cs` · Modify `Assets/Scripts/Editor/Setup/SceneComposer.cs`, `Assets/Scripts/Lobby/LobbyController.cs`

- [x] **Step 1: `MatchRandom` (Model, NetworkBehaviour)**
  - `NetworkVariable<int> m_Seed` · `int Seed => m_Seed.Value` · `Observable<int> SeedChanged => m_Seed.AsObservableWithCurrent()`
  - `static MatchRandom Current` — `OnNetworkSpawn`에 등록, `OnNetworkDespawn`에 해제(`ShopSpot.s_All`과 같은 명부 방식). **`?.` 금지**, 읽는 쪽은 `if (MatchRandom.Current != null)`.
  - `public void ServerRoll()` — 서버 전용. `m_Seed.Value = 0`이 나오지 않게 한다(0은 "아직 안 굴렸다"로 읽힌다).
  - `public static int SubSeed(int _seed, int _salt)` — **순수 static**. 용도(상점·품절·직업권)마다 다른 소금을 넣어 수열을 가른다. 곱셈 해시 한 줄이면 된다.
  - 용도 상수는 `enum`이 아니라 `const int SALT_SHOP = 1` 식으로 둔다 — 복제되는 값이 아니라 코드 안에서만 쓴다.
- [x] **Step 2: EditMode `MatchRandomLogicTests`** — 같은 시드·같은 소금이면 같은 값 · 소금이 다르면 다른 값 · 시드가 다르면 다른 값 · `SubSeed`는 0을 그대로 통과시키지 않는다(시드 0이 들어와도 쓸 수 있는 수).
- [x] **Step 3: 씬 배선** — `SceneComposer`의 `lobbyGo`에 `AddComponent<MatchRandom>()`. **`LobbySession` 바로 다음**(상태끼리 모아 둔다).
- [x] **Step 4: 판 시작에 굴린다** — `LobbyController.RequestStartGame`·`DevForceStartGame` 둘 다에서 `ServerBeginGame()` 성공 직후, **스포너 호출보다 먼저**:
```csharp
            MatchRandom random = GetComponent<Match.MatchRandom>();
            if (random != null) random.ServerRoll();   // 스포너가 이 시드를 읽는다 — 순서가 중요
```
  두 경로에 똑같이 넣는다. **`DevForceStartGame`을 빠뜨리면 봇 계측 판만 시드가 0**이 되어 무작위가 통째로 죽는다.
- [x] **Step 5: 메뉴 `Tools/CTC/Setup Networking Scene` 실행** → `Game.unity` 갱신 · 콘솔 에러 0. `u_editor_asset(refresh)`.
- [x] **Step 6: EditMode 전체 PASS 확인 → Commit**
```bash
git add Assets/Scripts/Match/MatchRandom.cs Assets/Scripts/Match/MatchRandom.cs.meta Assets/Scripts/Editor/Setup/SceneComposer.cs Assets/Scripts/Lobby/LobbyController.cs Assets/Scripts/Tests/EditMode/MatchRandomLogicTests.cs Assets/Scripts/Tests/EditMode/MatchRandomLogicTests.cs.meta Assets/Scenes/Game.unity
git commit -m "feat(match): 매치 시드 MatchRandom — 무작위 셋의 단일 원본, SubSeed로 용도 분리

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: 상점 자리 — 개수·간격 규칙 + 매 판 무작위

**Files:** Modify `Assets/Scripts/Map/ShopSpotLogic.cs`, `Assets/Scripts/Map/ShopSpotSpawner.cs`, `Assets/Resources/CSVInfo/BalanceInfo.csv`, `Assets/Scripts/Editor/WarehouseLayoutBuilder.cs`, `Assets/Scripts/Tests/EditMode/ShopSpotLogicTests.cs`

- [x] **Step 1: CSV 2키** — `ShopSpotCountSmall.int` 2 · `ShopSpotLargeHalfExtent.float` 40 → 메뉴 `Tools/CTC/Convert CSV → Info`.
- [x] **Step 2: `ShopSpotLogic.CountFor` (순수)**
```csharp
        /// <summary>
        /// 이 맵에 상점 몇 곳을 세울까 (기획서 §6 — 대형 4 · 기본·확장 2).
        /// **전역 상수 하나로는 안 된다**: 30×30에 넷을 세우면 서로 붙어 서서
        /// "한 곳을 지키는 동안 나머지가 열린다"가 성립하지 않는다.
        /// </summary>
        public static int CountFor(float _halfExtent, int _largeCount, int _smallCount, float _largeHalfExtent)
```
  테스트: 반경 60 → 4 · 21 → 2 · 15 → 2 · 경계값 40 → 4 · 음수·0은 최소 1.
- [x] **Step 3: `Pick`에 간격 완화 재시도** — 지금의 선택 루프를 그대로 두고 **바깥에 재시도**를 씌운다. 섞은 순서(`order`)는 **한 번만** 만들고 시도마다 재사용한다 — 안 그러면 같은 시드에서 결과가 달라진다.
```csharp
        /// <summary>간격을 몇 번까지 반으로 줄여 볼까 — 25 → 12.5 → 6.25 → 3.125.</summary>
        public const int GAP_RELAX_STEPS = 3;
```
  - 바닥: `_radius * 2f`(상점 원반끼리 겹치면 "다른 곳"이 아니다). 완화값이 바닥 밑으로 내려가면 바닥에서 멈춘다.
  - 개수를 채웠으면 즉시 반환. 끝까지 못 채우면 **가장 많이 채운 결과**를 쓴다.
  - `Pick`이 몇 번째 시도에서 멈췄는지 `GameDebug.Log`로 남긴다 — 대형 맵에서 이게 찍히면 방 배치가 나빠진 것이다.
  - 테스트: 넉넉한 맵은 완화 없이 목표 개수 · 좁은 맵은 완화 후 목표 개수 · 완화해도 못 채우면 최대치 · 바닥 밑으로는 안 내려간다 · **같은 시드는 같은 결과**(완화가 걸린 경우에도).
- [x] **Step 4: `Resolve`가 손 배치 자리도 뽑는다** — 지금은 `_placed`를 통째로 돌려준다. 후보가 목표보다 많으면 **시드로 골라야** 판마다 달라진다.
```csharp
        /// 손 배치 맵도 후보가 목표보다 많으면 시드로 고른다 — 자리는 레벨디자이너 것이고,
        /// **오늘 어느 넷이 서는지**만 판이 정한다(기획서 §7-1).
```
  간격 규칙은 손 배치에도 그대로 적용한다(레벨디자이너가 붙여 찍었어도 넷이 뭉치지 않게). 후보 ≤ 목표면 지금처럼 전부 쓴다.
  테스트: 후보 8·목표 4 → 4개, 시드가 다르면 조합이 다르다 · 후보 4·목표 4 → 그대로 4개 · 후보 2·목표 4 → 2개.
- [x] **Step 5: 창고 맵 상점 후보 4 → 8** — `WarehouseLayoutBuilder`의 `SHOPS` 배열에 4곳을 더한다. 지금 넷은 네 모서리 안쪽(`±36~40`)이니 **존 사이 통로 쪽**으로 네 곳을 더해 "오늘은 가운데 상점이 섰다"가 나오게 한다. 새 자리도 `ShopSpotLogic.RoomHoldsShop` 취지대로 **원반(반경 3 m)이 들어가는 빈 바닥**이어야 한다.
  - **`ctc-8b` 세션이 같은 파일·같은 레이아웃 에셋을 만지고 있다.** 이 Step에 들어가기 전에 `git log --oneline -5 -- Assets/Scripts/Editor/WarehouseLayoutBuilder.cs`로 그쪽 커밋이 있는지 보고, 있으면 **rebase 후에** 손댄다. 충돌하면 좌표만 옮겨 붙인다(배열 한 줄이다).
  - 메뉴로 창고 레이아웃 재생성 → `Layout_Warehouse01.asset`의 `m_Shops` 8개 확인. **자리 확인은 `TestGetShopSpotPos`**로 하고 눈대중하지 않는다.
- [x] **Step 6: `ShopSpotSpawner`가 새 인자를 먹인다**
```csharp
            int count = ShopSpotLogic.CountFor(_layout.HalfExtent, b.ShopSpotCount,
                b.ShopSpotCountSmall, b.ShopSpotLargeHalfExtent);
            int seed = MatchRandom.Current != null
                ? MatchRandom.SubSeed(MatchRandom.Current.Seed, MatchRandom.SALT_SHOP)
                : _layout.Seed;   // 시드가 없으면(실험실 등) 맵 시드 — 판마다 같지만 서기는 한다
```
  로그를 `상점 N곳 (목표 M · 간격 G)`로 늘린다.
- [x] **Step 7: EditMode PASS → Commit**
```bash
git add Assets/Resources/CSVInfo/BalanceInfo.csv Assets/Scripts/Info/Generator/ConvertInfo/BalanceInfo.cs Assets/Scripts/Map/ShopSpotLogic.cs Assets/Scripts/Map/ShopSpotSpawner.cs Assets/Scripts/Editor/WarehouseLayoutBuilder.cs Assets/Prefabs/Maps/Layout_Warehouse01.asset Assets/Scripts/Tests/EditMode/ShopSpotLogicTests.cs
git commit -m "feat(shop): 상점 자리가 판마다 바뀐다 — 맵 크기로 개수, 못 채우면 간격 완화, 창고 후보 8곳

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: 오늘의 품절

**Files:** Create `Assets/Scripts/Economy/TechRandomLogic.cs`, `Assets/Scripts/Tests/EditMode/TechRandomLogicTests.cs` · Modify `Assets/Scripts/Economy/TechLogic.cs`, `Assets/Scripts/Economy/ShopController.cs`, `Assets/Scripts/Economy/ShopHotbar.cs`, `Assets/Scripts/Bots/MouseBotLogic.cs`, `Assets/Resources/CSVInfo/BalanceInfo.csv`, `Assets/Scripts/Tests/EditMode/TechLogicTests.cs`

- [x] **Step 1: CSV `SoldOutCount.int` 2** → `Convert CSV → Info`.
- [x] **Step 2: `TechRandomLogic` (순수)**
```csharp
        /// <summary>
        /// 품절에서 **빼는** 테크 — 요새·대시·창고(기획서 §7). 생존과 째기의 바닥이라
        /// 이게 없는 판은 쥐가 할 줄 아는 것이 통째로 사라진다.
        /// </summary>
        public static readonly IReadOnlyList<TechId> NEVER_SOLD_OUT = new[]
            { TechId.Fortress, TechId.Dash, TechId.Warehouse };

        /// <summary>그 판에 못 사는 테크. 시드가 같으면 결과도 같다 — 서버가 뽑고 클라가 다시 계산한다.</summary>
        public static IReadOnlyList<TechId> SoldOut(IReadOnlyList<TechId> _catalog, int _count, int _seed)

        public static bool IsSoldOut(IReadOnlyList<TechId> _soldOut, TechId _id)
```
  - 후보 = `_catalog` − `NEVER_SOLD_OUT`. 쥐 카탈로그 10 − 3 = **7 중 2**.
  - 결과는 **카탈로그 순서**로 정렬해 돌려준다 — 토스트 문구가 시드에 따라 순서까지 바뀌면 읽는 쪽이 헷갈린다.
  - 고양이 카탈로그를 넣어도 동작해야 하지만 **부르지 않는다**(§7은 쥐 테크만). 고양이는 상점을 안 가므로 품절이 곧 "못 큰다"가 된다.
  - 테스트: 같은 시드 같은 답 · 다른 시드 다른 답(여러 시드 중 하나 이상은 달라야 한다) · 면제 3종은 절대 안 나온다 · 중복 없음 · 후보보다 많이 요청하면 후보 전부 · `_count` 0이면 빈 목록 · 카탈로그 순 정렬.
- [x] **Step 3: `TechDeny.SoldOut` + `CanBuy` 인자**
  - `enum TechDeny`에 `SoldOut` 추가(**뒤에 추가만** — 값이 직렬화되진 않지만 QA 문자열이 붙어 있다).
  - `CanBuy(..., IReadOnlyList<TechId> _soldOut)` — `_soldOut`이 null이면 품절 없음. 판정 순서는 **`WrongRole` 다음, `SlotsFull` 앞**: 품절은 "칸이 없어서"가 아니라 "오늘은 없어서"다. 다만 **이미 가진 테크의 업그레이드는 막지 않는다** — 품절은 판 시작에 정해지고 그전엔 아무도 못 샀으므로 실제로는 Lv0만 걸리지만, 규칙으로 못 박아 둔다(`LevelOf(_owned, _id) == 0`일 때만 SoldOut).
  - `DenyText`에 `case TechDeny.SoldOut: return "오늘은 품절";`
  - 기존 `CanBuy` 호출부·테스트를 전부 갱신한다(`grep -rn "CanBuy(" Assets/Scripts`).
- [x] **Step 4: `ShopController`가 품절을 안다**
```csharp
        /// <summary>오늘 못 사는 테크 — 매치 시드에서 다시 계산한다(서버·클라 같은 답).</summary>
        public IReadOnlyList<TechId> SoldOutToday
```
  `MatchRandom.Current`가 null이거나 `Seed == 0`이면 **빈 목록**(실험실·로비). 매 프레임 새로 뽑지 않게 시드가 바뀔 때만 다시 계산해 캐시한다(R3 `SeedChanged` 구독, 규칙 3).
  `CanBuyNow`가 이 목록을 `TechLogic.CanBuy`에 넘긴다 — **서버 `BuyRpc`도 같은 `CanBuyNow`를 지나므로 권위는 서버에 그대로 있다.**
- [x] **Step 5: `ShopHotbar` — 후보에서 빼고, 판 시작에 알린다**
  - 빈 칸 후보 순환 목록에서 품절을 **제외**한다. 후보에 남겨 두고 누를 때 거절하면 5키를 헛돌리게 된다.
  - 판 시작 토스트: 기존 `INTRO_TOAST_SECONDS`(4초) 경로에 이어 `오늘 품절: 후추 탄 · 큰 주머니`를 띄운다. **새 UI 0** — 같은 `m_Toast`다. 이름은 `TechLogic.RowOf(id, 1).Label`.
  - 기획서 §7은 "전원 동일·공개"라 **고양이에게도 띄운다**. 고양이가 쥐의 선택지를 읽는 힌트가 되지만(§0-2의 정보 비대칭과 살짝 어긋난다) 숨은 주사위를 만들지 않는 쪽이 기획 기조다.
  - 시드가 늦게 도착해 토스트를 놓치지 않게, `GameStarted`와 `SeedChanged`가 **둘 다** 준비된 뒤 한 번만 띄운다(`m_IntroShown`과 같은 1회 플래그).
- [x] **Step 6: 봇도 품절을 안다** — `MouseBotLogic.NextPurchase`에 `IReadOnlyList<TechId> _soldOut`을 넘겨 품절이면 그 줄을 건너뛴다. 안 넘기면 봇이 매 틱 죽은 구매를 시도한다(창고 상한과 같은 이유, 그 함수 주석 참조). `BotShopper.BuyForMouse`가 `m_Shop.SoldOutToday`를 넘긴다.
- [x] **Step 7: EditMode PASS → Commit**
```bash
git add Assets/Resources/CSVInfo/BalanceInfo.csv Assets/Scripts/Info/Generator/ConvertInfo/BalanceInfo.cs Assets/Scripts/Economy/TechRandomLogic.cs Assets/Scripts/Economy/TechRandomLogic.cs.meta Assets/Scripts/Economy/TechLogic.cs Assets/Scripts/Economy/ShopController.cs Assets/Scripts/Economy/ShopHotbar.cs Assets/Scripts/Bots/MouseBotLogic.cs Assets/Scripts/Tests/EditMode/TechRandomLogicTests.cs Assets/Scripts/Tests/EditMode/TechRandomLogicTests.cs.meta Assets/Scripts/Tests/EditMode/TechLogicTests.cs
git commit -m "feat(tech): 오늘의 품절 — 쥐 테크 7 중 2, 요새·대시·창고 면제, 판 시작 토스트

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: 직업권 셔플

**Files:** Create `Assets/Scripts/Map/TicketShuffleLogic.cs`, `Assets/Scripts/Tests/EditMode/TicketShuffleLogicTests.cs` · Modify `Assets/Scripts/Map/LootBoxSpawner.cs`, `Assets/Scripts/Map/AlcoveSpawner.cs`

- [x] **Step 1: 지금 어디에 박혀 있는지 확인** — 직업권은 **에디터에서 자리에 굳어 있다**: `LootBoxFactory`가 상자 0번=깡패권·1번=용사권, `AlcoveFactory`가 가장 먼 두 벽감에 각각 하나씩. 창고 맵은 상자 0개·벽감 6개라 **벽감이 실제 경로**고, 절차 맵은 상자다. **둘 다 고친다.**
- [x] **Step 2: `TicketShuffleLogic` (순수)**
```csharp
        /// <summary>
        /// 내용물 자리를 판마다 섞는다 — **통째로 교환**한다. 항목을 새로 만들지 않는 이유:
        /// 맵 총 치즈량(LootBoxLogic.TotalCheese)이 지점 매장량에서 이미 떼어져 있어서,
        /// 하나라도 늘거나 줄면 고갈 시점이 밀려 후반 압축이 무너진다(벽감과 같은 이유).
        /// </summary>
        public static int[] Permutation(int _count, int _seed)
```
  테스트: 같은 시드 같은 순열 · 다른 시드 다른 순열 · **순열이다**(0..n−1이 정확히 한 번씩) · 0·1개는 항등 · 음수는 빈 배열.
- [x] **Step 3: 스포너 두 곳에 적용** — `ServerSpawnAll`에서 자리 목록을 순회하기 **전에** 순열을 만들고, `i`번째 자리에 `perm[i]`번째 내용물을 넣는다. 좌표·회전·페이로드(강아지 벽감 여부)는 **안 섞는다** — 강아지가 든 벽감이 판마다 옮겨 다니면 소리 단서가 거짓말이 된다. 섞는 것은 `Contents`/`BoxContents`뿐이다.
  시드는 `MatchRandom.SubSeed(seed, SALT_TICKET)`, 없으면 순열을 만들지 않는다(지금 동작 그대로).
- [x] **Step 4: 셔플이 진짜 도는지 훅으로 확인** — `TestManager.Map.cs`에 `TestGetJobTicketPositions()`(깡패권·용사권이 든 벽감/상자의 좌표를 돌려준다)를 추가한다. 눈으로 벽을 부숴 확인하지 않는다.
- [x] **Step 5: EditMode PASS → Commit**
```bash
git add Assets/Scripts/Map/TicketShuffleLogic.cs Assets/Scripts/Map/TicketShuffleLogic.cs.meta Assets/Scripts/Map/LootBoxSpawner.cs Assets/Scripts/Map/AlcoveSpawner.cs Assets/Scripts/Testing/TestManager.Map.cs Assets/Scripts/Tests/EditMode/TicketShuffleLogicTests.cs Assets/Scripts/Tests/EditMode/TicketShuffleLogicTests.cs.meta
git commit -m "feat(map): 직업권 자리 셔플 — 내용물 통째 교환이라 총 치즈량 불변

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: 전직 300 + 광분×불 곱연산 해소

**Files:** Modify `Assets/Scripts/Economy/CheeseWallet.cs`, `Assets/Scripts/Player/CatClassLogic.cs`, `Assets/Scripts/Player/CatClass.cs`, `Assets/Scripts/Combat/CatPounce.cs`, `Assets/Scripts/Bots/BotShopper.cs`, `Assets/Scripts/Testing/TestManager.CatClass.cs`, `Assets/Resources/CSVInfo/BalanceInfo.csv`, `Assets/Scripts/Tests/EditMode/CatClassLogicTests.cs`

- [x] **Step 1: 왜 780인지 커밋 메시지에 남긴다** — `CatClass.CanAdvance`가 `CatClassLogic.CanAdvance(pounce.DamageStacks, move.SpeedStacks, MAX_UPGRADE_STACKS, ...)`다. 테크 v3에서 스택 = 테크 레벨이므로 조건은 **발톱 Lv3 + 발 구르기 Lv3**이고, 값은 CSV로 `100+140+180` + `80+120+160` = **780 장난기**다. 문턱이 아니라 **정해진 두 테크를 사라는 강제**였다.
- [x] **Step 2: CSV `CatAdvanceMischief.int` 300** → `Convert CSV → Info`.
- [x] **Step 3: 누적 획득을 지갑이 센다** — `CheeseWallet`에:
```csharp
        // 전직 문턱은 **번 총액**을 본다(잔액이 아니다). 잔액으로 재면 테크를 사는 순간
        // 문턱이 되돌아가 "전직하려면 아무것도 사지 마라"가 된다.
        private readonly NetworkVariable<int> m_Earned = new NetworkVariable<int>();
        public int EarnedValue => m_Earned.Value;
        public Observable<int> Earned => m_Earned.AsObservableWithCurrent();
```
  `AddCheese`에서만 올린다(`TrySpend`는 안 건드린다). **환급(창고 부서짐)도 `AddCheese`를 지난다** — 자기 창고를 부수고 다시 짓는 것으로 누적을 부풀릴 수 있는지 확인하고, 부풀 수 있으면 환급 경로는 `AddCheese`를 안 지나게 분리한다(판단해서 하고 이유를 커밋에 적는다).
- [x] **Step 4: `CatClassLogic.CanAdvance` 교체**
```csharp
        /// <summary>
        /// 전직할 수 있는가 — **누적 획득 장난기**가 문턱을 넘었나(2026-09-09, 기획서 §5).
        /// 전에는 발톱·발 구르기 만렙(실효 780)이었다. 테크 v3의 칸 5는 "무엇을 고르나"가 전부인데
        /// 전직이 두 테크를 강제하면 그 선택이 사라진다 — 문턱은 **얼마나 벌었나**여야 한다.
        /// </summary>
        public static bool CanAdvance(int _earnedMischief, int _threshold, CatClassId _current)
```
  `MischiefThreshold => Balance.CatAdvanceMischief`도 같이 뺀다. 테스트: 문턱 미만 false · 정확히 문턱 true · 이미 전직했으면 false · 문턱 0이면 즉시 true(설정 실수를 드러내는 값).
- [x] **Step 5: `CatClass.CanAdvance` 호출부** — `CatPounce`·`PlayerMovement` 조회를 **`CheeseWallet` 하나로 바꾼다**. 전직 창(`CatClassPopup`)은 이미 스택 스트림을 구독하고 있을 수 있으니 `grep -rn "DamageStacksChanged\|CanAdvance" Assets/Scripts`로 훑어 **지갑 스트림 구독으로 갈아 끼운다**(규칙 3 — 폴링 금지).
- [x] **Step 6: 광분 × 불 → max** — `CatPounce`가 두 배율을 곱하는 자리(파일 110행 근처, "둘은 서로를 모른다" 주석)를 큰 쪽만 쓰게 고친다.
```csharp
        /// <summary>
        /// 광분과 전직 「불」은 **곱하지 않는다** — 기획서 §5의 "불을 고르면 광분이 흡수된다"다.
        /// 전직이 300으로 앞당겨지면서 ×2.5 × ×2 = ×5.0이 흔해졌다(계획 2b가 남긴 미결).
        /// </summary>
        public static float PounceMultiplier(float _frenzy, float _fire) => Mathf.Max(_frenzy, _fire);
```
  순수 함수로 뽑아 EditMode로 덮는다(둘 다 1이면 1 · 한쪽만 · 둘 다면 큰 쪽).
- [x] **Step 7: 봇·QA 훅** — `BotShopper.BuyForCat`의 전직 분기는 `m_Class.CanAdvance`를 그대로 쓰므로 **코드 변경 없이 조건만 바뀐다**(주석의 "둘 다 만렙이면 전직"을 고친다). `TestManager.CatClass.cs`의 "스택을 만렙으로 채워 전직을 연다"(51·62행)는 **지갑 누적을 채우는 방식**으로 바꾼다 — 안 바꾸면 QA가 조용히 통과하며 아무것도 안 잰다.
- [x] **Step 8: EditMode PASS → Commit**
```bash
git add Assets/Resources/CSVInfo/BalanceInfo.csv Assets/Scripts/Info/Generator/ConvertInfo/BalanceInfo.cs Assets/Scripts/Economy/CheeseWallet.cs Assets/Scripts/Player/CatClassLogic.cs Assets/Scripts/Player/CatClass.cs Assets/Scripts/Combat/CatPounce.cs Assets/Scripts/Bots/BotShopper.cs Assets/Scripts/Testing/TestManager.CatClass.cs Assets/Scripts/Tests/EditMode/CatClassLogicTests.cs
git commit -m "feat(cat): 전직 문턱 누적 장난기 300 (실효 780 = 발톱420+발구르기360 강제 해소) · 광분×불은 max

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: 봇이 상점까지 걸어간다

**Files:** Modify `Assets/Scripts/Economy/ShopController.cs`, `Assets/Scripts/Map/ShopSpot.cs`, `Assets/Scripts/Bots/MouseBotLogic.cs`, `Assets/Scripts/Bots/BotAgent.cs`, `Assets/Scripts/Bots/BotBrainTrees.cs`, `Assets/Scripts/Bots/BotShopper.cs`, `Assets/Scripts/Tests/EditMode/MouseBotLogicTests.cs`

- [x] **Step 1: 계측 기준선을 먼저 뜬다** — 고치기 **전에** 봇 2v8을 돌려 지금 값을 적어 둔다(쥐 승률·평균 구매 수·평균 이동 거리). 판단 6번의 이유다. `Tools/CTC/Dev`의 봇전 경로 + `TestManager.BotMatch` 훅을 쓴다. **플레이모드를 쓰므로 이 Step은 단독 실행.**
- [x] **Step 2: `IsBotExempt` 제거** — `ShopController`에서 메서드와 두 호출부(`CanBuyNow`)를 지운다. `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 블록째 사라진다. `BotShopper` 클래스 주석의 "계획 1: 봇은 상점까지 가지 않는다" 두 줄도 고친다.
- [x] **Step 3: `ShopSpot.Nearest`**
```csharp
        /// <summary>가장 가까운 상점(없으면 false). 봇 목적지·QA 워프 공용 — 명부를 쓰므로 탐색이 없다.</summary>
        public static bool Nearest(Vector3 _from, out Vector3 _center)
```
  `TestManager.TestWarpToNearestShop`의 `FindObjectsByType` 루프도 이걸로 갈아 끼운다(같은 질의는 한 군데서).
- [x] **Step 4: 「살 게 있으면 상점으로」 순수 판단**
```csharp
        /// <summary>
        /// 지금 상점에 갈 값어치가 있나. **살 것이 정해졌고 돈이 있을 때만** 간다 —
        /// 빈손으로 가면 봇이 상점 주위를 맴돌아 잰 승률이 봇의 길찾기 실력이 된다.
        /// </summary>
        public static bool WantsShopping(Economy.TechId _next, int _cheese, int _cost, float _distanceToShop)
```
  - `_next == None`이면 false. 돈이 모자라면 false(가서 기다리지 않는다 — 파밍이 먼저다).
  - 거리 상한을 둔다: 너무 먼 상점을 향해 맵을 가로지르면 그 판을 통째로 걷기만 한다. 상한은 `MouseBotLogic`의 기존 상수 관례를 따라 `const float SHOP_TRAVEL_MAX`로 둔다.
  - 테스트: 살 것 없음 false · 돈 부족 false · 가깝고 살 수 있으면 true · 상한 밖 false · 경계값.
- [x] **Step 5: 행동 트리에 가지 하나** — `BotBrainTrees.BuildMouse`에서 **위협 가지 다음, 파밍 가지 앞**에 넣는다. 순서가 곧 우선순위다: 도망 > 쇼핑 > 파밍 > 배회. 파밍 뒤에 두면 치즈가 마르기 전엔 영영 안 산다.
```
                new BtSequence(
                    new BtCondition(() => _self.TryGetShop(out _)),
                    BtAction.Once(() =>
                    {
                        // 도착했으면 사고, 아니면 걸어간다. 사는 것 자체는 BuyForMouse가 한다 —
                        // 서버가 상점 안인지 다시 보므로 도착 전 호출은 그냥 거절된다.
                    })),
```
  `BotAgent.TryGetShop(out Vector3)`는 `ShopSpot.Nearest` + `MouseBotLogic.WantsShopping` 조합이다. 도착 판정은 **`ShopSpotLogic.Contains`**(반경)로 한다 — `ARRIVE_DISTANCE`를 쓰면 반경 밖에 서서 영원히 구매에 실패한다.
  `Activity`에 `"Shop"`을 넣는다(실험실 오버레이 F1이 읽는다).
- [x] **Step 6: 봇 실험실로 눈으로 본다** — `Tools/CTC/Setup BotLab Scene` → 플레이 → F2 경로선으로 봇이 상점으로 꺾는지 본다. 길이 안 나오면 추측하지 말고 `TestTracePath`·`TestGetNavStats`(CLAUDE.md 길찾기 절). **플레이모드 — 단독 실행.**
- [x] **Step 7: 계측 재측정** — Step 1과 같은 조건으로 다시 돌려 **전/후 표**를 만든다. 밸런스 게이트(기획서 §11: 봇 쥐 승률 20~50%)를 벗어나면 **수치를 고치지 말고 표만 남긴다** — 이 계획은 무작위·이동을 넣는 것이고, 밸런스 조정은 그다음 판단이다.
- [x] **Step 8: EditMode PASS → Commit**
```bash
git add Assets/Scripts/Economy/ShopController.cs Assets/Scripts/Map/ShopSpot.cs Assets/Scripts/Bots/MouseBotLogic.cs Assets/Scripts/Bots/BotAgent.cs Assets/Scripts/Bots/BotBrainTrees.cs Assets/Scripts/Bots/BotShopper.cs Assets/Scripts/Testing/TestManager.Tech.cs Assets/Scripts/Tests/EditMode/MouseBotLogicTests.cs
git commit -m "feat(bots): 봇도 상점까지 걸어간다 — IsBotExempt 제거, 행동 트리에 쇼핑 가지

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: QA 훅 · qa_v50 · 다이어그램

**Files:** Modify `Assets/Scripts/Testing/TestManager.Tech.cs` · Create `tools/wesqa/tests/qa_v50.py` · Modify `docs/design-vault/코드 다이어그램.md`

- [x] **Step 1: 훅 4** (전부 **읽기 전용**, `TestManager` 안에 둔다 — 타입을 쪼개면 리플렉션이 못 찾는다)
  - `TestGetMatchSeed()` — 판마다 다른지 본다.
  - `TestGetSoldOutTechs()` — 오늘 품절 목록(테크 id 문자열).
  - `TestGetShopSpotPositions()` — 좌표 목록. **기존 `TestGetShopSpotPos`·`TestGetShopSpotCount`로는 "판마다 달라졌나"를 못 잰다.**
  - `TestGetEarnedOf(clientId)` — 누적 획득 장난기(전직 문턱 확인).
- [x] **Step 2: `qa_v50.py` — MPPM 3인, 호스트=고양이.** 판을 **두 번** 연다(무작위는 한 판만 봐선 못 잰다). 항목:

| # | 확인 | 어떻게 |
|---|---|---|
| ① | 매치 시드가 판마다 다르다 | `TestGetMatchSeed` 1판 ≠ 2판 |
| ② | 상점 자리가 판마다 다르다 | `TestGetShopSpotPositions` 두 판 비교 — 최소 1곳 이동 |
| ③ | 상점 개수가 맵 규칙과 맞다 | 창고(반경 60) → 4곳 |
| ④ | 상점끼리 붙어 서지 않는다 | 모든 쌍의 거리 ≥ 원반 지름 |
| ⑤ | 품절 2개가 뜬다 · 면제 3종은 안 뜬다 | `TestGetSoldOutTechs` |
| ⑥ | **품절 테크는 상점 안에서도 못 산다** | 워프 → `TestBuyTech` → 레벨 0 유지 · **치즈 차감 0** |
| ⑦ | 품절이 아닌 테크는 같은 자리에서 사진다 | 대조군 — ⑥이 "그냥 다 안 사지는 것"이 아님을 보인다 |
| ⑧ | 직업권 자리가 판마다 다르다 | `TestGetJobTicketPositions` 두 판 비교 |
| ⑨ | 고양이가 300 벌면 전직이 열린다 | `TestGetEarnedOf` ≥ 300 → 전직 시도 성공 · 299에선 실패 |
| ⑩ | 봇 쥐가 상점 반경 안에 들어간다 | 봇전에서 `TestIsInShop`이 참인 봇이 1 이상 |

  ⑥은 **차감 0까지** 본다 — 값만 보는 QA가 배선 누락을 놓친 전례(2026-08-09)를 반복하지 않는다.
- [x] **Step 3: 규칙 8** — `docs/design-vault/코드 다이어그램.md`에 `MatchRandom` → `ShopSpotSpawner`·`ShopController`·`LootBoxSpawner`/`AlcoveSpawner` 세 갈래와, 봇 쥐 행동 트리의 새 가지를 넣는다.
- [x] **Step 4: EditMode 전체 + qa_v50 통과 확인 후 Commit**
```bash
git add Assets/Scripts/Testing/TestManager.Tech.cs tools/wesqa/tests/qa_v50.py "docs/design-vault/코드 다이어그램.md"
git commit -m "test(tech): QA 훅 4 + qa_v50(두 판 비교) · 코드 다이어그램 갱신

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## 순서 · 병렬

```
Task 1 (시드)  ──┬── Task 2 (상점 자리)   [에디터 단독: Step 5·6]
                 ├── Task 3 (품절)        [순수 + View]
                 └── Task 4 (직업권)      [순수 + 스포너]
Task 5 (전직 300) — 앞의 것과 독립, 언제든
Task 6 (봇 상점)  — Task 3 뒤(품절을 알아야 죽은 구매를 안 한다) · 계측이라 반드시 마지막에서 둘째
Task 7 (QA)      — 전부 끝난 뒤
```

| 병렬 가능 | 반드시 단독 |
|---|---|
| Task 3 · 4 · 5 (셋 다 순수 로직 중심) | Task 1 Step 5 · Task 2 Step 5·6 · **Task 6 Step 1·6·7** · Task 7 전부 |

**에디터는 하나다.** 메뉴 실행(`Setup Networking Scene`·`Convert CSV → Info`·레이아웃 재생성)과 플레이모드는 서로를 끊는다 — 계획 2b에서 실제로 EditMode 실행이 두 번 끊겼다. 위 오른쪽 칸은 **한 번에 하나만** 돈다.

## 검증 기준선

| 항목 | 지금 | 이 계획 뒤 |
|---|---|---|
| EditMode | **519** | 신규 4파일(시드·품절·순열·봇 쇼핑) + 갱신 4파일 → **545 안팎**. 숫자보다 **0 FAIL**이 기준 |
| wesqa | v46·47·48·49 초록 | + **v50**. v46~49도 다시 돌린다 — `CanBuy` 시그니처와 전직 조건이 바뀌어 **기존 QA가 조용히 깨질 수 있다** |
| 봇 계측 | 상점 이동 비용 **제외** | 포함. Task 6 Step 1·7의 전/후 표가 곧 근거 |

### 실측 (2026-09-08 · Task 7 종료)

| 항목 | 결과 |
|---|---|
| EditMode | **609 Passed · 0 Failed · 0 Skipped** (계획 예상 545를 넘겼다 — 앞 태스크들이 순수 로직 테스트를 더 촘촘히 깔았다) |
| qa_v50 | **PASS 12 · FAIL 0 · NOTE 4** (①~⑩ 전부 초록 · 첫 실행에서 통과) |
| qa_v46 | 11/11 |
| qa_v47 | **처음엔 FAIL 2** → 원인은 **낡은 기대값**(품절이 그 판에 막은 숨은 창고·빠른 발). `TestClearSoldOut`을 태운 뒤 **PASS 13 · FAIL 0 · SKIP 1**(SKIP은 예전부터 훅이 없는 항목) |
| qa_v48 | PASS 9 · FAIL 0 · SKIP 1 |
| qa_v49 | PASS 7 · FAIL 0 · NOTE 1 |

**계획에서 벗어난 판단 하나** — Task 7이 `TestManager.Tech.cs`만 고치기로 돼 있었는데
`MatchRandom.DevClearSeed()`(QA 전용, `#if UNITY_EDITOR || DEVELOPMENT_BUILD`)를 더했다.
이유: 품절이 매 판 쥐 테크 둘을 막으므로 **효과 스위트(qa_v46·v47)는 한 판으로 열 종류를 다 살 수
구조적으로 없다.** 시드를 0(로비·실험실이 늘 쓰는 상태)으로 되돌려 주사위만 치운다 —
`MatchManager.DevSetRemaining`과 같은 취급이고, 품절 자체는 qa_v50이 따로 본다.

## 이 계획이 손대지 않는 것

| 항목 | 왜 |
|---|---|
| 계획 2c(쥐 사격 클립 신규) | 「모션 0」을 깨므로 사용자 승인 대기 중 |
| 밸런스 수치 재조정 | 무작위·이동을 넣는 것이 이 계획이다. Task 6의 계측 표를 보고 **다음에** 판단한다 |
| 미니맵에 상점 마커 갱신 | `MinimapMarkers`가 `ShopSpot.All`을 0.2초마다 읽으므로 자리가 바뀌어도 **저절로 따라온다**. 확인만 한다(qa_v50 ②에서 같이 본다) |
| 숨은 창고 무음 검증 훅 · 끈끈이 겹침 QA | 계획 2a가 남긴 부채. 별건 |
