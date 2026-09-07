# 테크 v3 계획 2a — 쥐 테크 효과 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 쥐 테크 10개를 기획서 값대로 전부 작동시킨다 — 기존 5개의 상위 레벨(총 Lv3·후추 Lv2/3·요새 Lv2/3·창고 Lv2/3·대시 Lv2/3) + 신규 5개(치즈 방패·큰 주머니·숨은 창고·끈끈이·빠른 발).

**Architecture:** 효과 수치는 `TechInfo.csv`에 **레벨별 파라미터 3칸(P1·P2·P3)**을 붙여 한 표에서 읽는다(컬럼 폭발 방지). 효과는 "그 테크를 가진 플레이어의 `TechSlots` 레벨"을 읽는 **패시브 컴포넌트/훅**으로 붙인다 — 새 화면·모션·프리팹 없음(끈끈이만 쿼드+트리거 프리팹 1개). 판정 수학은 전부 `*Logic` 순수 클래스(EditMode).

**Tech Stack:** Unity 6000.5 · NGO 2.12 · R3 · UniTask · CSV 파이프라인 · NUnit EditMode · wesqa

**기준:** 기획서 `docs/superpowers/specs/2026-09-06-tech-shop-design.md` §4(쥐), §9(제약). 계획 1 결과(HEAD `6530afd`+): `TechId`/`TechLogic`/`TechSlots`/`ShopController.Buy/Use/UseAt`/`TryApplyLevel`/`UseDeny`. 고양이 테크 4개·클립 4개·원샷 매핑은 **계획 2b**.

**규칙(반드시):** `m_`/`_` · public 필드 금지 · UPPER_SNAKE_CASE · 코루틴 금지(UniTask) · R3 · Unity 객체는 `!= null`만 · 밸런스는 CSV · 생성 파일 손대지 않음 · 코드 고치면 `docs/design-vault/코드 다이어그램.md` 갱신 · EditMode = `Tools/CTC/Run EditMode Tests` · 에셋 바꾸면 `u_editor_asset(refresh)` · 노이즈 파일(`Assets/Materials/M_*.mat`·`Pretendard SDF.asset`·프리팹 fileID 변동) 스테이징 금지.

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Resources/CSVInfo/TechInfo.csv` (컬럼 3 추가·행 22 추가) | 레벨별 파라미터 `P1.float,P2.float,P3.float` |
| `Assets/Resources/CSVInfo/WeaponInfo.csv` (행 1 추가) | Tier 3 긴 총열 |
| `Assets/Resources/CSVInfo/BalanceInfo.csv` (컬럼 2 추가) | `CheeseShieldRate.int`(1 HP = 3치즈) · `StickySeconds.float`(10) |
| `Assets/Scripts/Economy/TechLogic.cs` (수정) | `TechRow.P1/P2/P3` · `ParamOf(id, level, index)` |
| `Assets/Scripts/Economy/TechOwner.cs` (신규) | 정적 헬퍼 — clientId/GameObject → 테크 레벨 (효과 훅 공용) |
| `Assets/Scripts/Combat/CheeseShield.cs` · `CheeseShieldLogic.cs` (신규) | 피해 일부를 치즈로 |
| `Assets/Scripts/Combat/Health.cs` (수정) | `IDamageAbsorber` 훅 |
| `Assets/Scripts/Combat/ThornWall.cs` (신규) · `CatPounce.cs` (수정) | 요새 Lv3 반사 |
| `Assets/Scripts/Combat/MousePopgun.cs` (수정) | 후추 레벨 파라미터 · 총 Lv3 티어 |
| `Assets/Scripts/Economy/CheeseSpot.cs` · `CheeseSpotLogic.cs` (수정) | 큰 주머니 채굴 배율 |
| `Assets/Scripts/Economy/WarehouseProduction.cs` · `WarehouseLogic.cs`(신규) (수정) | 둘째 창고·바쁜 창고·숨은 창고 |
| `Assets/Scripts/Economy/StickyPatch.cs` · `StickyLogic.cs` · `StickyUser.cs` (신규) · `Assets/Scripts/Editor/Setup/StickyPatchPrefabFactory.cs` (신규) | 끈끈이 |
| `Assets/Scripts/Player/PlayerMovement.cs` (수정) | 외부 감속 배율(끈끈이) · 빠른 발 |
| `Assets/Scripts/Player/MouseDash.cs` (수정) | 대시 Lv2/3 쿨 |
| `Assets/Scripts/Economy/ShopController.cs` (수정) | `TryApplyLevel`/`CanApplyLevel` 신규 5 · `Use(Sticky)` · 요새/창고 스폰 시 레벨 반영 |
| `Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs` · `SceneComposer.cs` (수정) | 컴포넌트 부착 · 끈끈이 프리팹 등록 |
| `Assets/Scripts/Testing/TestManager.Tech.cs` (수정) | QA 훅 추가 |
| `Assets/Scripts/Tests/EditMode/*` | `TechInfoTests`·`TechLogicTests`·`CheeseShieldLogicTests`·`WarehouseLogicTests`·`StickyLogicTests`·`CheeseSpotLogicTests` |
| `tools/wesqa/tests/qa_v47.py` (신규) | MPPM 3인 E2E |
| `docs/design-vault/코드 다이어그램.md` | 규칙 8 |

**효과가 레벨을 읽는 한 가지 길:** `TechOwner.LevelOf(GameObject player, TechId)` / `TechOwner.LevelOf(ulong clientId, TechId)` → `TechSlots.LevelOf`. 파라미터는 `TechLogic.ParamOf(id, level, 1..3)`.

---

### Task 1: TechInfo P1~P3 + 신규 행 22 + WeaponInfo Tier 3 + Balance 컬럼 2

**Files:** CSV 3개 · `Assets/Scripts/Economy/TechLogic.cs` · `Assets/Scripts/Tests/EditMode/TechInfoTests.cs` · `TechLogicTests.cs`

- [ ] **Step 1: TechInfo.csv 헤더에 `,P1.float,P2.float,P3.float` 추가하고 기존 12행 끝에 값을 붙인다.** 의미는 테크별로 다르다(아래 표). 없는 칸은 `0`.

| 테크 | P1 | P2 | P3 |
|---|---|---|---|
| Gun | 무기 티어 | – | – |
| Pepper | 도트 최대 중첩 | 도트 초 | – |
| Fortress | 요새 HP | 반사 피해(0=없음) | – |
| CheeseShield | 치즈로 내는 비율 | – | – |
| Warehouse | 창고 상한 | 수입 배율 | – |
| BigPouch | 채굴 배율 | – | – |
| HiddenWarehouse | 창고 소리 m | 창고 HP | 파괴 환급 비율 |
| Sticky | 반경 m | 감속 비율(0.4 = −40%) | 쿨 초 |
| Dash | 쿨 초 | – | – |
| FastFeet | 이속 가산 | – | – |
| CatClaw · CatStomp | 0 | 0 | 0 (계획 2b) |

기존 12행 값: Gun L1 `1,0,0` · Gun L2 `2,0,0` · Pepper L1 `3,4,0` · Fortress L1 `480,0,0` · Warehouse L1 `1,1,0` · Dash L1 `6,0,0` · CatClaw/CatStomp 6행 `0,0,0`.

- [ ] **Step 2: 신규 행 추가** (기획서 §4 값. Hint에 쉼표 금지)

```csv
Gun,Mouse,Attack,3,450,긴 총열,사거리 14m · 피해 ×1.3,3,0,0
Pepper,Mouse,Attack,2,180,매운 맛,도트 4중첩,4,4,0
Pepper,Mouse,Attack,3,240,오래 감,도트 6초,4,6,0
Fortress,Mouse,Defense,2,260,두꺼운 벽,요새 HP 900,900,0,0
Fortress,Mouse,Defense,3,400,가시 벽,요새 HP 1200 · 치면 25 반사,1200,25,0
CheeseShield,Mouse,Defense,1,100,치즈 방패,피해 30%를 치즈로 낸다,0.3,0,0
CheeseShield,Mouse,Defense,2,160,치즈 방패,피해 45%를 치즈로,0.45,0,0
CheeseShield,Mouse,Defense,3,220,치즈 방패,피해 60%를 치즈로,0.6,0,0
Warehouse,Mouse,Resource,2,240,둘째 창고,창고 상한 2,2,1,0
Warehouse,Mouse,Resource,3,320,바쁜 창고,창고 수입 ×2,2,2,0
BigPouch,Mouse,Resource,1,120,큰 주머니,채굴 ×1.3,1.3,0,0
BigPouch,Mouse,Resource,2,200,큰 주머니,채굴 ×1.6,1.6,0,0
BigPouch,Mouse,Resource,3,280,큰 주머니,채굴 ×1.9,1.9,0,0
HiddenWarehouse,Mouse,Resource,1,200,숨은 창고,창고 소리 24→16m,16,150,0
HiddenWarehouse,Mouse,Resource,2,320,숨은 창고,소리 8m · HP 300,8,300,0
HiddenWarehouse,Mouse,Resource,3,450,숨은 창고,무음 · HP 450 · 부서지면 절반 환급,0,450,0.5
Sticky,Mouse,Escape,1,220,끈끈이,반경 2.5m 10초 고양이 −40% · 쿨 30,2.5,0.4,30
Sticky,Mouse,Escape,2,300,끈끈이,−60% · 반경 3.5m,3.5,0.6,30
Sticky,Mouse,Escape,3,400,끈끈이,−75% · 쿨 12,3.5,0.75,12
Dash,Mouse,Utility,2,140,대시,쿨 4.5초,4.5,0,0
Dash,Mouse,Utility,3,180,대시,쿨 3.5초,3.5,0,0
FastFeet,Mouse,Utility,1,160,빠른 발,이속 +0.3,0.3,0,0
FastFeet,Mouse,Utility,2,260,빠른 발,이속 +0.7,0.7,0,0
FastFeet,Mouse,Utility,3,360,빠른 발,이속 +1.2,1.2,0,0
```
(Warehouse Lv1 행은 계획 1 값 `50` 유지 — 설치권 50 + 설치비 100. 총 행 12 + 24 = **36**.)

- [ ] **Step 3: WeaponInfo.csv에 Tier 3 행** `3,7,0.15,14,긴 총열(기관총 ×1.3·14m)`. BalanceInfo.csv 헤더 끝 `,CheeseShieldRate.int,StickySeconds.float` 값 `,3,10`(BOM·컬럼 수 유지).

- [ ] **Step 4: 실패하는 테스트** — `TechInfoTests`: `TechInfo_LoadsTwelveRows` → 36으로 이름·값 갱신(`TechInfo_LoadsThirtySixRows`), `TechInfo_TableIsWellFormed`는 그대로(연속 레벨·단조 증가 검증 — 신규 행이 통과해야 한다). 추가:

```csharp
        [Test]
        public void TechInfo_Params_ReadableByLevel()
        {
            Assert.AreEqual(4f, TechLogic.ParamOf(TechId.Pepper, 2, 1), 1e-4f);   // 중첩
            Assert.AreEqual(6f, TechLogic.ParamOf(TechId.Pepper, 3, 2), 1e-4f);   // 초
            Assert.AreEqual(1200f, TechLogic.ParamOf(TechId.Fortress, 3, 1), 1e-4f);
            Assert.AreEqual(25f, TechLogic.ParamOf(TechId.Fortress, 3, 2), 1e-4f);
            Assert.AreEqual(0f, TechLogic.ParamOf(TechId.Fortress, 0, 1), 1e-4f);  // 미보유 = 0
            Assert.AreEqual(0f, TechLogic.ParamOf(TechId.CheeseShield, 9, 1), 1e-4f); // 없는 레벨 = 0
        }

        [Test]
        public void TechInfo_MouseCatalog_IsTen()
        {
            Assert.AreEqual(10, TechLogic.Catalog(RoleId.Mouse).Count);
        }
```
`ShopSpotLogicTests`·`MouseBotLogicTests`는 영향 없음. `WeaponStats_OutOfRange_Clamps`(ShopLogicTests)는 티어 2→3으로 최상위가 바뀌므로 `WeaponStatsForTier(3).Damage == WeaponStatsForTier(99).Damage`로 고친다.

- [ ] **Step 5: 구현** — `TechRow`에 `public readonly float P1, P2, P3;` 추가(생성자에서 `_r.P1` 등 복사). `TechLogic`에:

```csharp
        /// <summary>레벨별 효과 파라미터(1~3). 미보유(0)·없는 레벨은 0 — 효과 훅이 "없음"으로 읽게.</summary>
        public static float ParamOf(TechId _id, int _level, int _index)
        {
            if (_level <= 0) return 0f;
            TechRow row = RowOf(_id, _level);
            if (!row.IsValid) return 0f;
            switch (_index)
            {
                case 1: return row.P1;
                case 2: return row.P2;
                case 3: return row.P3;
                default: return 0f;
            }
        }
```

- [ ] **Step 6: `u_editor_asset(refresh)` → `Tools/CTC/Convert CSV → Info` → refresh → `u_console(error)` 0 → EditMode 전부 PASS.** (`TechInfo.cs`에 `P1/P2/P3` float 필드가 생겼는지 확인.)

- [ ] **Step 7: Commit**
```bash
git add Assets/Resources/CSVInfo/TechInfo.csv Assets/Resources/CSVInfo/WeaponInfo.csv Assets/Resources/CSVInfo/BalanceInfo.csv Assets/Scripts/Info/Generator/ConvertInfo/TechInfo.cs Assets/Scripts/Info/Generator/ConvertInfo/BalanceInfo.cs Assets/Scripts/Economy/TechLogic.cs Assets/Scripts/Tests/EditMode/TechInfoTests.cs Assets/Scripts/Tests/EditMode/ShopLogicTests.cs
git commit -m "feat(tech): TechInfo P1~P3 레벨 파라미터 + 쥐 테크 24행·긴 총열 Tier 3 — 효과는 아직 안 붙음

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: TechOwner 헬퍼 + 총 Lv3 + 후추 Lv2/3

**Files:** Create `Assets/Scripts/Economy/TechOwner.cs` · Modify `Assets/Scripts/Combat/MousePopgun.cs`, `Assets/Scripts/Economy/ShopController.cs`

- [ ] **Step 1: TechOwner**

```csharp
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 효과 훅이 "이 플레이어가 그 테크를 몇 레벨 가졌나"를 묻는 한 가지 길 (테크 v3 계획 2a).
    /// 서버·클라 어디서든 TechSlots(NetworkList)를 읽으므로 표시용으로도 같은 값이다.
    /// </summary>
    public static class TechOwner
    {
        public static int LevelOf(GameObject _player, TechId _id)
        {
            if (_player == null) return 0;
            TechSlots slots = _player.GetComponent<TechSlots>();
            return slots != null ? slots.LevelOf(_id) : 0;
        }

        public static int LevelOf(ulong _clientId, TechId _id)
        {
            NetworkObject no = Core.NetUtil.FindPlayerObject(_clientId);
            return no != null ? LevelOf(no.gameObject, _id) : 0;
        }

        /// <summary>레벨의 파라미터 — 미보유면 0.</summary>
        public static float Param(GameObject _player, TechId _id, int _index)
        {
            return TechLogic.ParamOf(_id, LevelOf(_player, _id), _index);
        }
    }
}
```

- [ ] **Step 2: 총 Lv3** — `ShopController.TryApplyLevel`/`CanApplyLevel`의 `Gun` 케이스는 이미 `ServerSetTier(_level)`이라 Lv3 → Tier 3이 그대로 간다. `MousePopgun.ServerSetTier`가 `_tier <= 현재`만 거르는지 확인(3 허용). WeaponInfo Tier 3 행이 `WeaponStatsForTier(3)`로 잡힌다(`WeaponOf`가 `Clamp(0, Count-1)`).

- [ ] **Step 3: 후추 Lv2/3** — `MousePopgun.cs:122` `PepperDot.ServerApply(target, balance.PepperDotMaxStacks, balance.PepperDotSeconds)`를 다음으로:

```csharp
                    // 후추 레벨이 중첩·지속을 정한다(TechInfo P1·P2). Lv1 = 3중첩·4초(옛 BalanceInfo 값과 같다)
                    int pepperLv = TechOwner.LevelOf(gameObject, TechId.Pepper);
                    PepperDot.ServerApply(target,
                        Mathf.RoundToInt(TechLogic.ParamOf(TechId.Pepper, pepperLv, 1)),
                        TechLogic.ParamOf(TechId.Pepper, pepperLv, 2));
```
`PepperRounds.Owned`로 게이트하던 조건은 그대로(레벨 1 이상이면 Owned). `BalanceInfo.PepperDotMaxStacks/Seconds` 컬럼은 이제 **죽은 값** — `PepperDot`의 dps(`PepperDotDamage`)만 남긴다. 컬럼을 CSV에서 지우고 `Convert CSV → Info` 재실행(생성 파일 갱신). `PepperDotLogicTests`가 그 컬럼을 읽으면 `TechLogic.ParamOf(TechId.Pepper,1,…)`로 바꾼다.

- [ ] **Step 4: `ShopController.TryApplyLevel` Pepper 케이스** — Lv2/3에서도 `PepperRounds.ServerSetOwned()`만 호출(이미 true) — 상관없음. `CanApplyLevel`은 컴포넌트 존재만 본다.

- [ ] **Step 5: refresh → 에러 0 → EditMode PASS → 플레이 확인(호스트 쥐: `TestGrantCheeseAll`×2 → 상점 워프 → `TestBuyTechLocal("Gun")`×3 → `u_console`에 `Gun Lv3` 로그, 발사 사거리 14m는 QA_v47에서).**

- [ ] **Step 6: Commit**
```bash
git add Assets/Scripts/Economy/TechOwner.cs Assets/Scripts/Economy/TechOwner.cs.meta Assets/Scripts/Combat/MousePopgun.cs Assets/Resources/CSVInfo/BalanceInfo.csv Assets/Scripts/Info/Generator/ConvertInfo/BalanceInfo.cs Assets/Scripts/Tests/EditMode/PepperDotLogicTests.cs
git commit -m "feat(tech): TechOwner 헬퍼 · 총 Lv3(Tier 3) · 후추 Lv2/3 중첩·지속을 TechInfo에서

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: 치즈 방패 (TDD)

**Files:** Create `Assets/Scripts/Combat/CheeseShieldLogic.cs`, `Assets/Scripts/Combat/CheeseShield.cs`, `Assets/Scripts/Tests/EditMode/CheeseShieldLogicTests.cs` · Modify `Assets/Scripts/Combat/Health.cs`, `Assets/Scripts/Economy/ShopController.cs`, `Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs`

- [ ] **Step 1: 테스트**

```csharp
using CTC.Runtime.Combat;
using NUnit.Framework;

namespace CTC.Tests
{
    public class CheeseShieldLogicTests
    {
        [Test]
        public void Absorb_Ratio30_Rate3_SplitsDamage()
        {
            // 100 피해 · 30% → HP 70, 치즈 30 × 3 = 90
            CheeseShieldLogic.Absorb(100, 0.3f, 3, 1000, out int hp, out int cheese);
            Assert.AreEqual(70, hp);
            Assert.AreEqual(90, cheese);
        }

        [Test]
        public void Absorb_NotEnoughCheese_PaysWhatItCan_RestToHp()
        {
            // 치즈 30뿐 → 10 HP 분만 치즈로, 나머지 90은 HP
            CheeseShieldLogic.Absorb(100, 0.3f, 3, 30, out int hp, out int cheese);
            Assert.AreEqual(90, hp);
            Assert.AreEqual(30, cheese);
        }

        [Test]
        public void Absorb_NoLevel_PassThrough()
        {
            CheeseShieldLogic.Absorb(50, 0f, 3, 999, out int hp, out int cheese);
            Assert.AreEqual(50, hp);
            Assert.AreEqual(0, cheese);
        }

        [Test]
        public void Absorb_ZeroCheese_PassThrough()
        {
            CheeseShieldLogic.Absorb(50, 0.6f, 3, 0, out int hp, out int cheese);
            Assert.AreEqual(50, hp);
            Assert.AreEqual(0, cheese);
        }
    }
}
```

- [ ] **Step 2: 실행 → 컴파일 실패 확인.**

- [ ] **Step 3: 순수 로직**

```csharp
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 치즈 방패 (테크 v3 계획 2a) — 피해의 일부를 치즈로 대신 낸다. "째서 번 돈이 곧 목숨".
    /// 지갑이 모자라면 낼 수 있는 만큼만 치즈로, 나머지는 HP로.
    /// </summary>
    public static class CheeseShieldLogic
    {
        public static void Absorb(int _damage, float _ratio, int _cheesePerHp, int _cheese,
            out int _hpDamage, out int _cheeseCost)
        {
            if (_damage <= 0 || _ratio <= 0f || _cheesePerHp <= 0 || _cheese <= 0)
            {
                _hpDamage = Mathf.Max(0, _damage);
                _cheeseCost = 0;
                return;
            }
            int wantHp = Mathf.RoundToInt(_damage * _ratio);              // 치즈로 대신 낼 HP
            int canHp = Mathf.Min(wantHp, _cheese / _cheesePerHp);         // 지갑이 감당하는 HP
            _cheeseCost = canHp * _cheesePerHp;
            _hpDamage = _damage - canHp;
        }
    }
}
```

- [ ] **Step 4: Health 훅** — `Health.cs`에 인터페이스와 호출 한 줄:

```csharp
    /// <summary>피해를 받기 전에 일부를 다른 자원으로 대신 내는 것(치즈 방패). 서버에서만 불린다.</summary>
    public interface IDamageAbsorber
    {
        int ServerAbsorb(int _damage);   // 돌려주는 값 = HP로 갈 피해
    }
```
`TakeDamage` 안 `int before = m_Hp.Value;` 앞에:
```csharp
            IDamageAbsorber absorber = GetComponent<IDamageAbsorber>();
            if (absorber != null) _damage = absorber.ServerAbsorb(_damage);
```
(`GetComponent<인터페이스>`는 Unity가 지원한다. 매 피격마다 부르지만 피격은 드물다.)

- [ ] **Step 5: 컴포넌트**

```csharp
using CTC.Runtime.Economy;
using Unity.Netcode;

namespace CTC.Runtime.Combat
{
    /// <summary>치즈 방패 — 쥐 프리팹. 레벨은 TechSlots, 비율은 TechInfo P1, 환율은 BalanceInfo.CheeseShieldRate.</summary>
    public class CheeseShield : NetworkBehaviour, IDamageAbsorber
    {
        public int ServerAbsorb(int _damage)
        {
            if (!IsServer) return _damage;
            float ratio = TechOwner.Param(gameObject, TechId.CheeseShield, 1);
            CheeseWallet wallet = GetComponent<CheeseWallet>();
            if (ratio <= 0f || wallet == null) return _damage;

            CheeseShieldLogic.Absorb(_damage, ratio, Info.InfoManager.Instance.Balance.CheeseShieldRate,
                wallet.CheeseValue, out int hpDamage, out int cheeseCost);
            if (cheeseCost > 0 && wallet.TrySpend(cheeseCost)) return hpDamage;
            return _damage;   // 차감 실패(경쟁)면 원래 피해
        }
    }
}
```
`PlayerPrefabFactory`: 쥐 분기에 `root.AddComponent<CheeseShield>();`. `ShopController.CanApplyLevel/TryApplyLevel`: `case TechId.CheeseShield: return GetComponent<CheeseShield>() != null;` / no-op apply(레벨은 TechSlots가 들고 있다).

- [ ] **Step 6: refresh → EditMode PASS → `Setup Networking Scene`(프리팹 재생성) → 플레이: 쥐가 CheeseShield Lv1 구매 후 `TestGetCheeseOf` 확인하며 고양이 봇에게 맞아 치즈가 줄고 HP가 덜 깎이는지(`TestGetHp`류 훅이 있으면 사용, 없으면 `Health.Hp` 로그).**

- [ ] **Step 7: Commit**
```bash
git add Assets/Scripts/Combat/CheeseShieldLogic.cs* Assets/Scripts/Combat/CheeseShield.cs* Assets/Scripts/Combat/Health.cs Assets/Scripts/Economy/ShopController.cs Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs Assets/Prefabs/PlayerMouse.prefab Assets/Scripts/Tests/EditMode/CheeseShieldLogicTests.cs*
git commit -m "feat(tech): 치즈 방패 — 피해 일부를 치즈로 (IDamageAbsorber 훅, EditMode 4)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: 요새 Lv2/3 — HP·가시 벽 반사

**Files:** Create `Assets/Scripts/Combat/ThornWall.cs` · Modify `Assets/Scripts/Economy/ShopController.cs`(`SpawnFortressAround`), `Assets/Scripts/Combat/CatPounce.cs`

- [ ] **Step 1: ThornWall**

```csharp
using Unity.Netcode;

namespace CTC.Runtime.Combat
{
    /// <summary>가시 벽(요새 Lv3) — 이 요새를 치는 고양이가 타격당 반사 피해를 받는다. 값은 서버가 스폰 시 넣는다.</summary>
    public class ThornWall : NetworkBehaviour
    {
        private readonly NetworkVariable<int> m_Reflect = new NetworkVariable<int>();
        public int Reflect => m_Reflect.Value;
        public void ServerSet(int _reflect) { if (IsServer) m_Reflect.Value = _reflect; }
    }
}
```
요새 프리팹(`CheeseFortress.prefab`)에 `ThornWall`을 **항상** 붙인다(`SetupNetworkingScene`의 요새 팩토리에 `AddComponent<ThornWall>()`) — 값 0이면 아무 일 없음(프리팹 종류를 늘리지 않으려고).

- [ ] **Step 2: 스폰 시 레벨 반영** — `ShopController.SpawnFortressAround`:

```csharp
        private void SpawnFortressAround(GameObject _prefab)
        {
            GameObject go = Instantiate(_prefab, transform.position, Quaternion.LookRotation(FlatForward()));
            // 요새 레벨(TechInfo P1 HP · P2 반사) — 스폰 전에 세워야 OnNetworkSpawn이 그 HP로 채운다
            int lv = m_Slots.LevelOf(TechId.Fortress);
            Health hp = go.GetComponent<Health>();
            if (hp != null) hp.ServerSetMaxHp(Mathf.RoundToInt(TechLogic.ParamOf(TechId.Fortress, lv, 1)));
            ThornWall thorn = go.GetComponent<ThornWall>();
            if (thorn != null) thorn.ServerSet(Mathf.RoundToInt(TechLogic.ParamOf(TechId.Fortress, lv, 2)));
            go.GetComponent<NetworkObject>().Spawn(true);
        }
```
(`FortressHp` BalanceInfo 컬럼은 이제 Lv1 P1=480과 중복 — 그대로 두되 `ShopLogic.FORTRESS_HP` 사용처가 프리팹 굽기뿐인지 확인. 중복이면 컬럼을 지우고 팩토리가 `TechLogic.ParamOf(Fortress,1,1)`을 굽는다.)

- [ ] **Step 3: CatPounce 반사** — `int dealt = target.TakeDamage(damage);` 다음에:

```csharp
                // 가시 벽(요새 Lv3) — 벽을 친 고양이가 되맞는다. 사망 판정은 Health가 한다.
                ThornWall thorn = target.GetComponent<ThornWall>();
                if (thorn != null && thorn.Reflect > 0 && dealt > 0)
                {
                    Health self = GetComponent<Health>();
                    if (self != null) self.TakeDamage(thorn.Reflect);
                }
```

- [ ] **Step 4: refresh → EditMode → `Setup Networking Scene` → 플레이: 요새 Lv3 사서 세우면 `Health.MaxHp` 1200(`u_editor_component`로 확인), 고양이 봇이 치면 고양이 HP가 25씩 준다.**

- [ ] **Step 5: Commit**
```bash
git add Assets/Scripts/Combat/ThornWall.cs* Assets/Scripts/Economy/ShopController.cs Assets/Scripts/Combat/CatPounce.cs Assets/Scripts/Editor/SetupNetworkingScene.cs Assets/Prefabs/CheeseFortress.prefab
git commit -m "feat(tech): 요새 Lv2/3 — 레벨별 HP(900·1200) + 가시 벽 반사 25

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: 큰 주머니 — 채굴 배율 (TDD)

**Files:** Modify `Assets/Scripts/Economy/CheeseSpotLogic.cs`, `CheeseSpot.cs`, `Assets/Scripts/Tests/EditMode/CheeseSpotLogicTests.cs`

- [ ] **Step 1: 테스트** — `CheeseSpotLogicTests`에:

```csharp
        [Test]
        public void MinedThisTick_AppliesMultiplier_KeepsReserveBound()
        {
            // 초당 5 · 0.5초 · ×1.9 → 4.75 → 정수 캐리 방식은 기존과 같아야 한다: 여기선 배율 후 정수화
            Assert.AreEqual(4, CheeseSpotLogic.ScaleMined(2, 1.9f));      // 2 × 1.9 = 3.8 → 4
            Assert.AreEqual(2, CheeseSpotLogic.ScaleMined(2, 0f));        // 미보유(0)는 1배
            Assert.AreEqual(2, CheeseSpotLogic.ScaleMined(2, 1f));
        }
```

- [ ] **Step 2: 구현** — `CheeseSpotLogic`:
```csharp
        /// <summary>큰 주머니(테크 v3) — 이번 틱 채굴량에 배율. 0은 "테크 없음"이라 1배.</summary>
        public static int ScaleMined(int _mined, float _multiplier)
        {
            if (_multiplier <= 0f) return _mined;
            return Mathf.RoundToInt(_mined * _multiplier);
        }
```
`CheeseSpot.ServerTick`에서 채굴자 지갑에 `AddCheese(mined)` 하는 지점을 찾아(`grep AddCheese`) `mined = CheeseSpotLogic.ScaleMined(mined, TechOwner.Param(NetUtil.FindPlayerObject(m_Earner)?.gameObject … ))` — Unity 객체에 `?.` 금지: 
```csharp
            NetworkObject earnerNo = Core.NetUtil.FindPlayerObject(m_Earner);
            float mul = earnerNo != null ? TechOwner.Param(earnerNo.gameObject, TechId.BigPouch, 1) : 0f;
            mined = CheeseSpotLogic.ScaleMined(mined, mul);
```
**지점 매장량은 배율을 안 탄다**(지점에서 빼는 양은 원래대로, 지갑에 들어가는 양만 늘린다 — "더 잘 캔다"가 아니라 "더 챙긴다"). 주석으로 남긴다.

- [ ] **Step 3: EditMode PASS → 플레이(BigPouch Lv3 쥐가 지점에서 캘 때 지갑 증가가 1.9배) → Commit**
```bash
git add Assets/Scripts/Economy/CheeseSpotLogic.cs Assets/Scripts/Economy/CheeseSpot.cs Assets/Scripts/Tests/EditMode/CheeseSpotLogicTests.cs
git commit -m "feat(tech): 큰 주머니 — 채굴 지갑 적립 ×1.3/1.6/1.9 (매장량은 그대로)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: 창고 Lv2/3 + 숨은 창고 (TDD)

**Files:** Create `Assets/Scripts/Economy/WarehouseLogic.cs`, `Assets/Scripts/Tests/EditMode/WarehouseLogicTests.cs` · Modify `WarehouseProduction.cs`, `ShopController.cs`(`CanUse`·`UseAtRpc`), `ShopLogic.cs`(`WAREHOUSE_LIMIT_PER_MOUSE` 사용처)

- [ ] **Step 1: 테스트**

```csharp
using CTC.Runtime.Economy;
using NUnit.Framework;

namespace CTC.Tests
{
    public class WarehouseLogicTests
    {
        [Test]
        public void Limit_ByWarehouseLevel()
        {
            Assert.AreEqual(1, WarehouseLogic.LimitOf(0));   // 설치권 없음 → 기본 1(서버가 어차피 막는다)
            Assert.AreEqual(1, WarehouseLogic.LimitOf(1));
            Assert.AreEqual(2, WarehouseLogic.LimitOf(2));
            Assert.AreEqual(2, WarehouseLogic.LimitOf(3));
        }

        [Test]
        public void Income_Lv3_Doubles()
        {
            Assert.AreEqual(10, WarehouseLogic.TickCheese(10, 1));
            Assert.AreEqual(20, WarehouseLogic.TickCheese(10, 3));
        }

        [Test]
        public void Hidden_SoundAndHp_ByLevel()
        {
            Assert.AreEqual(24f, WarehouseLogic.SoundRange(24f, 0), 1e-4f);
            Assert.AreEqual(16f, WarehouseLogic.SoundRange(24f, 1), 1e-4f);
            Assert.AreEqual(0f, WarehouseLogic.SoundRange(24f, 3), 1e-4f);
            Assert.AreEqual(150, WarehouseLogic.MaxHp(150, 0));
            Assert.AreEqual(450, WarehouseLogic.MaxHp(150, 3));
        }

        [Test]
        public void Refund_Lv3_HalfOfPlaceCost()
        {
            Assert.AreEqual(0, WarehouseLogic.Refund(100, 2));
            Assert.AreEqual(50, WarehouseLogic.Refund(100, 3));
        }
    }
}
```

- [ ] **Step 2: 구현**

```csharp
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>창고 테크 수학 (테크 v3 계획 2a) — 값은 TechInfo P열. 순수.</summary>
    public static class WarehouseLogic
    {
        public static int LimitOf(int _warehouseLevel)
        {
            float p = TechLogic.ParamOf(TechId.Warehouse, _warehouseLevel, 1);
            return p > 0f ? Mathf.RoundToInt(p) : 1;
        }

        public static int TickCheese(int _baseTick, int _warehouseLevel)
        {
            float mul = TechLogic.ParamOf(TechId.Warehouse, _warehouseLevel, 2);
            return mul > 0f ? Mathf.RoundToInt(_baseTick * mul) : _baseTick;
        }

        /// <summary>숨은 창고 소리 — Lv3는 0(무음). 미보유는 기본.</summary>
        public static float SoundRange(float _baseRange, int _hiddenLevel)
        {
            return _hiddenLevel <= 0 ? _baseRange : TechLogic.ParamOf(TechId.HiddenWarehouse, _hiddenLevel, 1);
        }

        public static int MaxHp(int _baseHp, int _hiddenLevel)
        {
            float p = TechLogic.ParamOf(TechId.HiddenWarehouse, _hiddenLevel, 2);
            return p > 0f ? Mathf.RoundToInt(p) : _baseHp;
        }

        public static int Refund(int _placeCost, int _hiddenLevel)
        {
            return Mathf.RoundToInt(_placeCost * TechLogic.ParamOf(TechId.HiddenWarehouse, _hiddenLevel, 3));
        }
    }
}
```

- [ ] **Step 3: 배선**
- `ShopController.CanUse(Warehouse)`·`UseAtRpc`: `ShopLogic.WAREHOUSE_LIMIT_PER_MOUSE` → `WarehouseLogic.LimitOf(m_Slots.LevelOf(TechId.Warehouse))`. `BotShopper`의 같은 참조도 `WarehouseLogic.LimitOf(TechOwner.LevelOf(m_Shop.gameObject, TechId.Warehouse))`.
- `UseAtRpc`: Instantiate 뒤 Spawn 전에 `Health.ServerSetMaxHp(WarehouseLogic.MaxHp(ShopLogic.WAREHOUSE_MAX_HP, hiddenLv))`, `WarehouseProduction.ServerSetTech(warehouseLv, hiddenLv, placeCost)`.
- `WarehouseProduction`: `NetworkVariable<int> m_WarehouseLevel, m_HiddenLevel, m_PlaceCost` + `ServerSetTech(...)`. 틱 지급 `wallet.AddCheese(WarehouseLogic.TickCheese(ShopLogic.WAREHOUSE_TICK_CHEESE, m_WarehouseLevel.Value))`. 틱 소리 `PlayAt(..., range: WarehouseLogic.SoundRange(ShopLogic.WAREHOUSE_SOUND_RANGE_METERS, m_HiddenLevel.Value))` — range 0이면 **재생 생략**. `Health.Died` 구독(서버)에서 `Refund > 0`이면 소유자 지갑에 `AddCheese(refund)` + `GameDebug.Log`.
- `ShopLogic.WAREHOUSE_LIMIT_PER_MOUSE`는 남겨 두되 사용처 0이면 삭제(테스트도).

- [ ] **Step 4: EditMode PASS → 플레이(Warehouse Lv2 → 창고 2개 설치 가능 · Hidden Lv3 → 틱 소리 없음·부서지면 +50) → Commit**
```bash
git add Assets/Scripts/Economy/WarehouseLogic.cs* Assets/Scripts/Economy/WarehouseProduction.cs Assets/Scripts/Economy/ShopController.cs Assets/Scripts/Economy/ShopLogic.cs Assets/Scripts/Bots/BotShopper.cs Assets/Scripts/Tests/EditMode/WarehouseLogicTests.cs* Assets/Scripts/Tests/EditMode/ShopLogicTests.cs
git commit -m "feat(tech): 창고 Lv2/3(상한 2·수입 ×2) + 숨은 창고(소리 16/8/0m·HP·파괴 환급)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: 끈끈이 (TDD) — 액티브

**Files:** Create `Assets/Scripts/Economy/StickyLogic.cs`, `StickyPatch.cs`, `StickyUser.cs`, `Assets/Scripts/Editor/Setup/StickyPatchPrefabFactory.cs`, `Assets/Scripts/Tests/EditMode/StickyLogicTests.cs` · Modify `Assets/Scripts/Player/PlayerMovement.cs`, `ShopController.cs`, `SceneComposer.cs`, `PlayerPrefabFactory.cs`

- [ ] **Step 1: 테스트**

```csharp
using CTC.Runtime.Economy;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class StickyLogicTests
    {
        [Test]
        public void SlowMultiplier_FromSlowRatio()
        {
            Assert.AreEqual(0.6f, StickyLogic.SpeedMultiplier(0.4f), 1e-4f);
            Assert.AreEqual(0.25f, StickyLogic.SpeedMultiplier(0.75f), 1e-4f);
            Assert.AreEqual(1f, StickyLogic.SpeedMultiplier(0f), 1e-4f);
        }

        [Test]
        public void Inside_UsesHorizontalRadius()
        {
            Assert.IsTrue(StickyLogic.Inside(Vector3.zero, new Vector3(2f, 3f, 0f), 2.5f));
            Assert.IsFalse(StickyLogic.Inside(Vector3.zero, new Vector3(2.6f, 0f, 0f), 2.5f));
        }

        [Test]
        public void Ready_WhenCooldownElapsed()
        {
            Assert.IsFalse(StickyLogic.Ready(readyAt: 10.0, now: 9.9));
            Assert.IsTrue(StickyLogic.Ready(readyAt: 10.0, now: 10.0));
        }
    }
}
```

- [ ] **Step 2: 순수 로직**

```csharp
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>끈끈이(테크 v3) 수학 — 반경·감속·쿨. 값은 TechInfo P1(반경)·P2(감속 비율)·P3(쿨), 지속은 BalanceInfo.StickySeconds.</summary>
    public static class StickyLogic
    {
        /// <summary>감속 비율 0.4 → 이속 배율 0.6.</summary>
        public static float SpeedMultiplier(float _slowRatio) => Mathf.Clamp01(1f - _slowRatio);

        public static bool Inside(Vector3 _center, Vector3 _point, float _radius)
        {
            float dx = _point.x - _center.x, dz = _point.z - _center.z;
            return dx * dx + dz * dz <= _radius * _radius;
        }

        public static bool Ready(double readyAt, double now) => now >= readyAt;
    }
}
```

- [ ] **Step 3: PlayerMovement 외부 감속** — `NetworkVariable<float> m_ExternalSpeedMul = new(1f)`(서버 쓰기) + `public void ServerSetExternalSpeedMul(float _mul)`; 이동 속도 계산 `float speed = ShopLogic.MoveSpeed(m_MoveSpeed, m_SpeedStacks.Value);` 뒤에 `speed *= m_ExternalSpeedMul.Value;`. (소유자 권위 이동이라 값이 복제되면 소유자가 그 배율로 움직인다.)

- [ ] **Step 4: StickyPatch (서버가 스폰, 트리거 없이 거리 판정 — 콜라이더 불필요)**

```csharp
using System.Collections.Generic;
using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 끈끈이 장판(테크 v3 계획 2a). 바닥 쿼드 1장. 서버가 매 틱 반경 안 고양이에게 감속 배율을 걸고,
    /// 나가면 1로 되돌린다. 지속이 끝나면 despawn. 모션·파티클 없음 — 쿼드 색이 곧 연출.
    /// </summary>
    public class StickyPatch : NetworkBehaviour
    {
        private const float TICK_SECONDS = 0.1f;
        private const string QUAD_NAME = "Quad";

        private readonly NetworkVariable<float> m_Radius = new NetworkVariable<float>(2.5f);
        private float m_SlowRatio;
        private readonly HashSet<Player.PlayerMovement> m_Slowed = new HashSet<Player.PlayerMovement>();

        public void ServerSetup(float _radius, float _slowRatio)
        {
            if (!IsServer) return;
            m_Radius.Value = _radius;
            m_SlowRatio = _slowRatio;
        }

        public override void OnNetworkSpawn()
        {
            Transform quad = transform.Find(QUAD_NAME);
            if (quad != null) quad.localScale = new Vector3(m_Radius.Value * 2f, m_Radius.Value * 2f, 1f);
            if (IsServer) RunAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid RunAsync(System.Threading.CancellationToken _ct)
        {
            float life = Info.InfoManager.Instance.Balance.StickySeconds;
            double endAt = Time.timeAsDouble + life;
            float mul = StickyLogic.SpeedMultiplier(m_SlowRatio);
            while (Time.timeAsDouble < endAt && !_ct.IsCancellationRequested)
            {
                foreach (Player.PlayerMovement body in FindObjectsByType<Player.PlayerMovement>(FindObjectsSortMode.None))
                {
                    RoleTag tag = body.GetComponent<RoleTag>();
                    if (tag == null || tag.Role != RoleId.Cat) continue;   // 쥐는 안 붙는다
                    bool inside = StickyLogic.Inside(transform.position, body.transform.position, m_Radius.Value);
                    if (inside && m_Slowed.Add(body)) body.ServerSetExternalSpeedMul(mul);
                    else if (!inside && m_Slowed.Remove(body)) body.ServerSetExternalSpeedMul(1f);
                }
                await UniTask.Delay(System.TimeSpan.FromSeconds(TICK_SECONDS), cancellationToken: _ct);
            }
            foreach (Player.PlayerMovement body in m_Slowed)
            {
                if (body != null) body.ServerSetExternalSpeedMul(1f);
            }
            m_Slowed.Clear();
            if (IsSpawned) NetworkObject.Despawn(true);
        }
    }
}
```
(`FindObjectsByType` 0.1초마다 — 플레이어 ≤10, 장판 ≤8이라 무해. 겹친 장판 둘이 한 고양이를 다루면 마지막 값이 이긴다 — 허용, 주석.)

- [ ] **Step 5: StickyUser(쥐 프리팹, 쿨다운)**

```csharp
using Unity.Netcode;

namespace CTC.Runtime.Economy
{
    /// <summary>끈끈이 쿨다운(서버 시계). 핫바가 남은 시간을 읽는다.</summary>
    public class StickyUser : NetworkBehaviour
    {
        private readonly NetworkVariable<double> m_ReadyAt = new NetworkVariable<double>();
        public float CooldownRemaining => (float)System.Math.Max(0.0, m_ReadyAt.Value - NetworkManager.ServerTime.Time);
        public bool Ready => StickyLogic.Ready(m_ReadyAt.Value, NetworkManager.ServerTime.Time);
        public void ServerStartCooldown(float _seconds) { if (IsServer) m_ReadyAt.Value = NetworkManager.ServerTime.Time + _seconds; }
    }
}
```

- [ ] **Step 6: 프리팹 팩토리** — `Assets/Prefabs/StickyPatch.prefab`: 루트(`NetworkObject`+`StickyPatch`) + 자식 `Quad`(`PrimitiveType.Quad`, 콜라이더 제거, x축 90° 눕힘, y=0.02, 머티리얼은 `ShopSpotPrefabFactory`가 쓴 주황 프로토타입 머티리얼과 같은 방식으로 **노란 반투명**—없으면 `Prototype_512x512_Orange` 재사용). 있으면 덮어쓰지 않음. `SceneComposer`에서 `RegisterNetworkPrefabs`에 추가 + 해시 베이크(ShopSpot과 같은 코드 경로). `ShopController`에 `[SerializeField] GameObject m_StickyPrefab` + `PlayerPrefabFactory`에서 `SetRef(shop, "m_StickyPrefab", …)`(요새·창고와 같은 방식).

- [ ] **Step 7: ShopController** — `CanUse(Sticky)`: 보유 + `StickyUser.Ready` else `UseDeny.Unavailable`(문구 "아직 쿨다운"을 `UseDenyText`에 `Cooldown` 사유로 추가). `UseRpc` `case TechId.Sticky:` — 레벨 P1/P2/P3 읽어 `Instantiate(m_StickyPrefab, transform.position, identity)` → `ServerSetup(r, slow)` → `Spawn(true)` → `StickyUser.ServerStartCooldown(P3)`. `CanApplyLevel/TryApplyLevel`: `Sticky → GetComponent<StickyUser>() != null` / no-op.

- [ ] **Step 8: refresh → EditMode → `Setup Networking Scene` → 플레이: Sticky 사면 숫자키로 발밑에 노란 원, 고양이 봇이 밟으면 느려짐(`u_editor_component`로 고양이 `PlayerMovement` 배율 확인) → 10초 뒤 사라짐.**

- [ ] **Step 9: Commit**
```bash
git add Assets/Scripts/Economy/StickyLogic.cs* Assets/Scripts/Economy/StickyPatch.cs* Assets/Scripts/Economy/StickyUser.cs* Assets/Scripts/Editor/Setup/StickyPatchPrefabFactory.cs* Assets/Scripts/Editor/Setup/SceneComposer.cs Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs Assets/Scripts/Player/PlayerMovement.cs Assets/Scripts/Economy/ShopController.cs Assets/Scripts/Economy/TechLogic.cs Assets/Prefabs/StickyPatch.prefab* Assets/Prefabs/PlayerMouse.prefab Assets/Scenes/Game.unity Assets/Scenes/BotLab.unity Assets/Scripts/Tests/EditMode/StickyLogicTests.cs*
git commit -m "feat(tech): 끈끈이 — 발밑 장판 10초, 고양이 감속 40/60/75%, 쿨 30/30/12 (쿼드 1장·모션 0)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```
(`DefaultNetworkPrefabs.asset`도 바뀌면 함께.)

---

### Task 8: 대시 Lv2/3 + 빠른 발

**Files:** Modify `Assets/Scripts/Player/MouseDash.cs`, `Assets/Scripts/Player/PlayerMovement.cs`, `Assets/Scripts/Economy/ShopController.cs`, `Assets/Scripts/Tests/EditMode/ShopLogicTests.cs`

- [ ] **Step 1: 대시 쿨** — `MouseDash.COOLDOWN_SECONDS`를 레벨 기준으로: `private float CooldownSeconds => TechLogic.ParamOf(TechId.Dash, TechOwner.LevelOf(gameObject, TechId.Dash), 1)`가 0이면 `Balance.DashCooldownSeconds`로 후퇴. `CooldownRemaining`·`CooldownTotal`·`TryDash`의 세 사용처를 교체. (Lv1 P1=6 = Balance 값과 같다 — 컬럼은 남긴다, 미보유 후퇴용.)

- [ ] **Step 2: 빠른 발** — `PlayerMovement` 속도 식: `float speed = ShopLogic.MoveSpeed(m_MoveSpeed, m_SpeedStacks.Value) + TechOwner.Param(gameObject, TechId.FastFeet, 1); speed *= m_ExternalSpeedMul.Value;`. 매 프레임 `GetComponent<TechSlots>()`를 피하려면 `OnNetworkSpawn`에서 `m_TechSlots = GetComponent<TechSlots>()` 캐시 후 `m_TechSlots != null ? TechLogic.ParamOf(TechId.FastFeet, m_TechSlots.LevelOf(TechId.FastFeet), 1) : 0f`. `ShopController.CanApplyLevel/TryApplyLevel`: `FastFeet → GetComponent<Player.PlayerMovement>() != null` / no-op.

- [ ] **Step 3: 테스트** — `ShopLogicTests`에 `MoveSpeed_FastFeetLv3_ApproachesCat`: `PlayerOf(Mouse).MoveSpeed + ParamOf(FastFeet,3,1)` < `PlayerOf(Cat).MoveSpeed` (7.8 < 8.6 — "고양이보다 느려야 유틸이 발 싸움을 못 이긴다"는 가위바위보 표).

- [ ] **Step 4: EditMode PASS → 플레이(대시 Lv3 쿨 3.5초 — 능력 바 쿨다운 표시로 확인) → Commit**
```bash
git add Assets/Scripts/Player/MouseDash.cs Assets/Scripts/Player/PlayerMovement.cs Assets/Scripts/Economy/ShopController.cs Assets/Scripts/Tests/EditMode/ShopLogicTests.cs
git commit -m "feat(tech): 대시 Lv2/3 쿨 4.5/3.5 + 빠른 발 +0.3/0.7/1.2

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: 핫바 쿨다운 표시 · QA 훅 · qa_v47 · 다이어그램

**Files:** Modify `Assets/Scripts/Economy/ShopHotbar.cs`, `Assets/Scripts/Testing/TestManager.Tech.cs` · Create `tools/wesqa/tests/qa_v47.py` · Modify `docs/design-vault/코드 다이어그램.md`

- [ ] **Step 1: 핫바** — `DrawOwned` 밖 모드에서 끈끈이 칸의 비용 자리에 쿨다운 남은 초(`StickyUser.CooldownRemaining > 0 ? $"{sec:0}s" : "쓰기"`), 대시도 `MouseDash.CooldownRemaining` 같은 방식(둘 다 소유자 로컬 값). `RefreshSlots`는 dirty일 때만 그리므로 **쿨다운 중엔 0.25초마다 dirty** — `Update`에 `if (m_CooldownTicking && Time.time >= m_NextCooldownRedraw) { m_SlotsDirty = true; m_NextCooldownRedraw = Time.time + 0.25f; }`.

- [ ] **Step 2: QA 훅** (`TestManager.Tech.cs`): `TestGetHpOf(long)`, `TestGetMaxHpOf(long)`, `TestGetMoveSpeedOf(long)`(`PlayerMovement` 현재 속도 계산값 — 내부 식을 `public float CurrentSpeed` 프로퍼티로 노출), `TestGetWarehouseCountOf(long)`, `TestGetStickyPatchCount()`, `TestGetNearestFortressMaxHp(long)`.

- [ ] **Step 3: qa_v47.py** (MPPM 3인, 호스트=쥐, qa_v46 뼈대 재사용):
① 총 Lv3 → `TestGetTechLevel(m,"Gun")==3` ② 후추 Lv3 → 고양이 봇 맞히고 도트가 6초 지속(HP 6초간 감소 — `TestGetHpOf(c)` 3회 샘플) ③ 요새 Lv3 → 세우고 `TestGetNearestFortressMaxHp(m)==1200`, 고양이가 치면 `TestGetHpOf(c)` 25 감소 ④ 치즈 방패 Lv3 → 맞을 때 치즈 감소·HP 덜 감소 ⑤ 큰 주머니 Lv3 → 지점 채굴 10초 지갑 증가 ≈ 1.9배 ⑥ 창고 Lv2 → 창고 2개 (`TestGetWarehouseCountOf(m)==2`) ⑦ 숨은 창고 Lv3 → `TestGetAudioVolume`류 훅으로 틱 소리 0 ⑧ 끈끈이 → `TestGetStickyPatchCount()==1`, 고양이 워프해 넣고 `TestGetMoveSpeedOf(c)` 감소 ⑨ 대시 Lv3·빠른 발 Lv3 → `TestGetMoveSpeedOf(m)` ≈ 7.8. 실행 → 전부 PASS 보고.

- [ ] **Step 4: 다이어그램** — 경제 블록에 `CheeseShield/ThornWall/StickyPatch/StickyUser/WarehouseLogic/StickyLogic/TechOwner` + 관계(`Health --> IDamageAbsorber`, `CatPounce --> ThornWall`, `CheeseSpot --> TechOwner`, `WarehouseProduction --> WarehouseLogic`, `ShopController --> StickyPatch`), CSV 표에 `TechInfo P1~P3` 설명 한 줄, EditMode 기준선 갱신.

- [ ] **Step 5: Commit**
```bash
git add Assets/Scripts/Economy/ShopHotbar.cs Assets/Scripts/Player/PlayerMovement.cs Assets/Scripts/Testing/TestManager.Tech.cs tools/wesqa/tests/qa_v47.py "docs/design-vault/코드 다이어그램.md"
git commit -m "test(qa): qa_v47 쥐 테크 효과 9항목 + 핫바 쿨다운 표시 + 코드 다이어그램

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## 자가 검토

| 기획서 §4 | 태스크 |
|---|---|
| 총 Lv3 · 후추 Lv2/3 | T2 |
| 요새 Lv2/3 · 치즈 방패 | T4 · T3 |
| 창고 Lv2/3 · 큰 주머니 · 숨은 창고 | T6 · T5 · T6 |
| 끈끈이 | T7 |
| 대시 Lv2/3 · 빠른 발 | T8 |
| 제약(모션 0·VFX 0·프리팹 0 — 끈끈이 쿼드는 기획서가 허용한 유일 예외) | T7 쿼드 1장 |
| QA·다이어그램 | T9 |

**타입 일관성:** `TechLogic.ParamOf(id, level, index)` · `TechOwner.LevelOf/Param` · `WarehouseLogic.LimitOf/TickCheese/SoundRange/MaxHp/Refund` · `StickyLogic.SpeedMultiplier/Inside/Ready` · `PlayerMovement.ServerSetExternalSpeedMul` · `StickyUser.Ready/CooldownRemaining/ServerStartCooldown` · `StickyPatch.ServerSetup` · `ThornWall.Reflect/ServerSet` · `IDamageAbsorber.ServerAbsorb`.

**계획 2b로 넘긴 것:** 고양이 4테크(가죽·배고픔·광분·예민한 귀) · 쥐 클립 4개(사격·채굴·끈끈이·요새) + 원샷 매핑(대시 Roll·끈끈이 Bounce·요새 Jump·구매 Clicked) · `BotShopper` 신규 테크 우선순위(봇은 아직 옛 5개만 산다 — 계측 시 주석).
