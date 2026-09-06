# 테크 뼈대 + 상점 장소 Implementation Plan (계획 1/3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기존 상점 8항목을 「테크·레벨·칸 5」 모델로 옮기고, 쥐는 맵의 상점 자리 안에서만 사고 키우게 한다 — 신규 효과 없이 뼈대만.

**Architecture:** 데이터는 `TechInfo.csv`(테크×레벨 한 행씩) → 자동 생성 `TechInfo`. 판정은 순수 `TechLogic`(EditMode TDD), 소유 상태는 플레이어 프리팹의 `TechSlots`(NetworkList), 구매 진입점은 기존 `ShopController`를 `TechId`로 갈아 끼운다. 상점 자리는 `ShopSpot`(트리거 반경) + `ShopSpotSpawner`(서버 스폰, `LootBoxSpawner`와 같은 취급). 핫바는 같은 6칸에 「상점 안=산다·키운다 / 밖=쓴다」 두 모드.

**Tech Stack:** Unity 6000.5 · NGO 2.12(`NetworkList`, `[Rpc]`) · R3 · UniTask · CSV 파이프라인(`Tools/CTC/Convert CSV → Info`) · NUnit EditMode · wesqa(TestManager 리플렉션)

**기준 문서:** `docs/superpowers/specs/2026-09-06-tech-shop-design.md` §2·§3·§6·§6-1·§11. 이 계획은 **기존 8항목만** 테크로 옮긴다. 신규 효과(치즈 방패·숨은 창고·큰 주머니·빠른 발·끈끈이·광분·귀·요새 Lv2/3·총 Lv3·후추 Lv2/3)는 계획 2, 확률·전직 300·봇 상점 이동은 계획 3.

**프로젝트 규칙(반드시):** `m_` 멤버 · `_` 매개변수 · public 필드 금지 · 상수 `UPPER_SNAKE_CASE` · 코루틴 금지(UniTask) · `onClick.AddListener` 금지(R3) · Unity 객체는 `!= null`로만 검사(`?.` 금지) · 밸런스 수치는 CSV · 코드 고치면 `docs/design-vault/코드 다이어그램.md` 갱신 · EditMode 실행은 메뉴 `Tools/CTC/Run EditMode Tests`(MCP `u_editor_menu`) · 에셋 바뀌면 `u_editor_asset(action: refresh)`.

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Resources/CSVInfo/TechInfo.csv` (신규) | 테크 × 레벨 한 행 — 역할·축·값·라벨·힌트 |
| `Assets/Resources/CSVInfo/ShopInfo.csv` (삭제) | `TechInfo`가 대체 |
| `Assets/Resources/CSVInfo/BalanceInfo.csv` (컬럼 추가) | `TechSlotCap.int`(5) · `FortressPlaceCost.int`(80) · `ShopSpotCount.int`(4) · `ShopSpotRadius.float`(3) · `ShopSpotMinGap.float`(25) · `ShopBuySoundMeters.float`(12) |
| `Assets/Scripts/Info/Generator/ConvertInfo/TechInfo.cs` (자동 생성) | 스키마 |
| `Assets/Scripts/Info/InfoCustom.cs` (수정) | `Tech` 조회 헬퍼 |
| `Assets/Scripts/Economy/TechId.cs` (신규) | `TechId`·`TechAxis` enum — **값 고정, 뒤에만 추가** |
| `Assets/Scripts/Economy/TechLogic.cs` (신규) | 순수 판정 — 카탈로그·다음 레벨·칸 상한·구매 가능·액티브 여부 |
| `Assets/Scripts/Economy/TechSlots.cs` (신규) | 플레이어별 보유 테크·레벨(NetworkList) — Model |
| `Assets/Scripts/Economy/ShopController.cs` (수정) | `TechId` 기반 구매·업그레이드·사용 RPC · 상점 안 검증 |
| `Assets/Scripts/Economy/ShopLogic.cs` (수정) | `ShopItemId`·`CATALOG`·`Find`·`RoleCatalogIndices`·`IsPlaceable` 삭제, 밸런스 접근자만 남김 |
| `Assets/Scripts/Economy/ShopHotbar.cs` (수정) | 두 모드 View |
| `Assets/Scripts/Map/ShopSpotLogic.cs` (신규) | 순수 — 방 중심 중 서로 먼 N개 고르기 |
| `Assets/Scripts/Map/ShopSpot.cs` (신규) | 상점 자리 마커 — `AnyContains(pos)` |
| `Assets/Scripts/Map/ShopSpotSpawner.cs` (신규) | 서버가 판 시작에 세우고 로비 복귀에 걷음 |
| `Assets/Scripts/Editor/Setup/ShopSpotPrefabFactory.cs` (신규) | `Assets/Prefabs/ShopSpot.prefab` 생성 |
| `Assets/Scripts/Editor/Setup/SceneComposer.cs` (수정) | 로비 GO에 `ShopSpotSpawner` 부착 |
| `Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs` (수정) | 플레이어 프리팹에 `TechSlots` 부착 |
| `Assets/Scripts/Editor/Setup/LobbyCanvasBuilder.cs` (수정) | 핫바 모드 라벨 텍스트 1개 |
| `Assets/Scripts/Lobby/LobbyController.cs` (수정) | `ShopSpotSpawner` 호출 |
| `Assets/Scripts/Bots/BotShopper.cs` · `MouseBotLogic.cs` (수정) | `TechId`로 이관 |
| `Assets/Scripts/Testing/TestManager.Tech.cs` (신규) | wesqa 훅 |
| `Assets/Scripts/Tests/EditMode/TechLogicTests.cs` · `ShopSpotLogicTests.cs` (신규) · `ShopLogicTests.cs` · `MouseBotLogicTests.cs` (수정) | |
| `docs/design-vault/코드 다이어그램.md` (수정) | 규칙 8 |

**호출 흐름(완성 후):**

```
숫자키 ─ ShopHotbar ─┬─ 상점 안·빈 칸 → 후보 넘김(같은 키 반복) → Enter → ShopController.Buy(id)
                     ├─ 상점 안·있는 칸 → ShopController.Buy(id)           (다음 레벨)
                     └─ 밖·있는 칸·액티브 → ShopController.Use(id) / UseAt(id,pos)
ShopController.BuyRpc(서버) → 역할 · ShopSpot.AnyContains(쥐만) · TechLogic.CanBuy(TechSlots) · Wallet.TrySpend → ApplyLevel → TechSlots 갱신
```

---

### Task 1: TechId · TechAxis enum + TechInfo.csv + 자동 생성

**Files:**
- Create: `Assets/Scripts/Economy/TechId.cs`
- Create: `Assets/Resources/CSVInfo/TechInfo.csv`
- Modify: `Assets/Resources/CSVInfo/BalanceInfo.csv` (컬럼 6개 추가)
- Modify: `Assets/Scripts/Info/InfoCustom.cs`
- Generate: `Assets/Scripts/Info/Generator/ConvertInfo/TechInfo.cs`, `Assets/Scripts/Info/Generator/ConvertInfo/BalanceInfo.cs`, `Assets/Scripts/Info/Generator/InfoLoader.cs` (메뉴가 만든다)
- Test: `Assets/Scripts/Tests/EditMode/TechInfoTests.cs`

- [ ] **Step 1: enum 파일 작성**

```csharp
// Assets/Scripts/Economy/TechId.cs
namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 테크 = 판 안에서 얻는 모든 구매·업그레이드 (테크 v3, 2026-09-07).
    /// **값은 NetworkList·CSV에 직렬화되므로 바꾸지 않는다** — 새 테크는 뒤에만 추가한다.
    /// 계획 1은 기존 8항목만 쓴다. 나머지는 계획 2에서 효과가 붙을 때까지 CSV에 행이 없다.
    /// </summary>
    public enum TechId
    {
        None = 0,
        // 쥐
        Gun = 1,             // 총 — Lv1 새총 · Lv2 기관총 (기존 MousePopgun 티어)
        Pepper = 2,          // 후추 탄 (기존 PepperRounds)
        Fortress = 3,        // 요새 — Lv1 설치 가능, 설치마다 FortressPlaceCost
        CheeseShield = 4,    // 치즈 방패 (계획 2)
        Warehouse = 5,       // 창고 (기존 WarehouseProduction)
        BigPouch = 6,        // 큰 주머니 (계획 2)
        HiddenWarehouse = 7, // 숨은 창고 (계획 2)
        Sticky = 8,          // 끈끈이 (계획 2)
        Dash = 9,            // 대시 (기존 MouseDash)
        FastFeet = 10,       // 빠른 발 (계획 2)
        // 고양이
        CatClaw = 20,        // 발톱 (기존 CatPounce 스택)
        CatHide = 21,        // 두꺼운 가죽 (계획 2)
        CatHunger = 22,      // 배고픔 (계획 2)
        CatFrenzy = 23,      // 광분 (계획 2)
        CatStomp = 24,       // 발 구르기 (기존 PlayerMovement 스택)
        CatEar = 25,         // 예민한 귀 (계획 2)
    }

    /// <summary>5축 (사용자 정의 2026-09-06). CSV `Axis.TechAxis` 컬럼이 이 이름을 쓴다.</summary>
    public enum TechAxis
    {
        Attack = 0,
        Defense = 1,
        Resource = 2,
        Escape = 3,
        Utility = 4,
    }
}
```

- [ ] **Step 2: TechInfo.csv 작성** (UTF-8, 첫 줄 헤더 `필드명.타입`). 값은 기획서 §4·§5 — 계획 1은 **효과가 이미 있는 레벨만**.

```csv
TechId.TechId,Role.RoleId,Axis.TechAxis,Level.int,Cost.int,Label.string,Hint.string
Gun,Mouse,Attack,1,150,새총,한 방 5→12 · 더 멀리
Gun,Mouse,Attack,2,600,기관총,5배 연사 — 고양이 사냥
Pepper,Mouse,Attack,1,120,후추 탄,총알에 매운맛 — 지속피해
Fortress,Mouse,Defense,1,100,요새,즉석 벙커 — 설치마다 80
Warehouse,Mouse,Resource,1,150,창고,자동 치즈 생산 · 소리로 위치가 팔린다
Dash,Mouse,Utility,1,100,대시,Space 순간 가속
CatClaw,Cat,Attack,1,100,발톱,덮치기 +10
CatClaw,Cat,Attack,2,140,발톱,덮치기 +20
CatClaw,Cat,Attack,3,180,발톱,덮치기 +30
CatStomp,Cat,Utility,1,80,발 구르기,이속 +0.2
CatStomp,Cat,Utility,2,120,발 구르기,이속 +0.4
CatStomp,Cat,Utility,3,160,발 구르기,이속 +0.6
```

- [ ] **Step 3: BalanceInfo.csv 헤더·값 행 끝에 6컬럼 추가**

헤더 끝에 `,TechSlotCap.int,FortressPlaceCost.int,ShopSpotCount.int,ShopSpotRadius.float,ShopSpotMinGap.float,ShopBuySoundMeters.float` — 값 행 끝에 `,5,80,4,3,25,12`.

Run(PowerShell): 
```powershell
$p='Assets/Resources/CSVInfo/BalanceInfo.csv'; $l=Get-Content $p -Encoding UTF8
$l[0]+=',TechSlotCap.int,FortressPlaceCost.int,ShopSpotCount.int,ShopSpotRadius.float,ShopSpotMinGap.float,ShopBuySoundMeters.float'
$l[1]+=',5,80,4,3,25,12'
Set-Content $p $l -Encoding UTF8
```
Expected: 두 줄 다 컬럼 수가 같다 (`($l[0] -split ',').Count -eq ($l[1] -split ',').Count` → True).

- [ ] **Step 4: 자동 생성 실행** — MCP `u_editor_asset(action: refresh)` → `u_editor_menu("Tools/CTC/Convert CSV → Info")`.
Expected: 콘솔 `[InfoConvert] 5개 표 생성: BalanceInfo, PlayerInfo, ShopInfo, TechInfo, WeaponInfo`. `TechInfo.cs`가 생기고 `InfoLoader.cs`에 `TechInfoList = LoadInfo<TechInfo>();`가 추가된다.

- [ ] **Step 5: InfoCustom에 조회 헬퍼 추가** (`InfoCustom.cs`의 `public IReadOnlyList<ShopInfo> Shop => ShopInfoList;` 아래)

```csharp
        /// <summary>테크 표 전체 — 역할·레벨 필터는 <see cref="TechLogic"/>이 한다.</summary>
        public IReadOnlyList<TechInfo> Tech => TechInfoList;
```

- [ ] **Step 6: 실패하는 테스트 작성**

```csharp
// Assets/Scripts/Tests/EditMode/TechInfoTests.cs
using CTC.Runtime.Economy;
using CTC.Runtime.Info;
using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests
{
    public class TechInfoTests
    {
        [Test]
        public void TechInfo_LoadsTwelveRows()
        {
            // 계획 1: 기존 8항목 = 쥐 6행(총 2레벨) + 고양이 6행(스택 2종 × 3레벨)
            Assert.AreEqual(12, InfoManager.Instance.Tech.Count);
        }

        [Test]
        public void TechInfo_GunLevel2_IsMachineGun600()
        {
            TechInfo found = null;
            foreach (TechInfo row in InfoManager.Instance.Tech)
                if (row.TechId == TechId.Gun && row.Level == 2) found = row;
            Assert.IsNotNull(found);
            Assert.AreEqual(600, found.Cost);
            Assert.AreEqual(RoleId.Mouse, found.Role);
            Assert.AreEqual(TechAxis.Attack, found.Axis);
        }

        [Test]
        public void Balance_HasTechColumns()
        {
            BalanceInfo b = InfoManager.Instance.Balance;
            Assert.AreEqual(5, b.TechSlotCap);
            Assert.AreEqual(80, b.FortressPlaceCost);
            Assert.AreEqual(4, b.ShopSpotCount);
            Assert.AreEqual(3f, b.ShopSpotRadius, 1e-4f);
            Assert.AreEqual(25f, b.ShopSpotMinGap, 1e-4f);
            Assert.AreEqual(12f, b.ShopBuySoundMeters, 1e-4f);
        }
    }
}
```

- [ ] **Step 7: 테스트 실행** — `u_editor_asset(action: refresh)` → `u_editor_menu("Tools/CTC/Run EditMode Tests")`.
Expected: `TechInfoTests` 3개 PASS (컴파일이 먼저 통과해야 한다 — 생성 파일이 없으면 Step 4를 다시).

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Economy/TechId.cs Assets/Scripts/Economy/TechId.cs.meta Assets/Resources/CSVInfo/TechInfo.csv Assets/Resources/CSVInfo/TechInfo.csv.meta Assets/Resources/CSVInfo/BalanceInfo.csv Assets/Scripts/Info/Generator/ConvertInfo/TechInfo.cs Assets/Scripts/Info/Generator/ConvertInfo/TechInfo.cs.meta Assets/Scripts/Info/Generator/ConvertInfo/BalanceInfo.cs Assets/Scripts/Info/Generator/InfoLoader.cs Assets/Scripts/Info/InfoCustom.cs Assets/Scripts/Tests/EditMode/TechInfoTests.cs Assets/Scripts/Tests/EditMode/TechInfoTests.cs.meta
git commit -m "feat(tech): TechId·TechAxis enum + TechInfo.csv — 기존 8항목을 테크·레벨 행으로

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: TechLogic — 순수 판정 (TDD)

**Files:**
- Create: `Assets/Scripts/Economy/TechLogic.cs`
- Test: `Assets/Scripts/Tests/EditMode/TechLogicTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
// Assets/Scripts/Tests/EditMode/TechLogicTests.cs
using System.Collections.Generic;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests
{
    public class TechLogicTests
    {
        private static List<TechEntry> Owned(params (TechId, int)[] _e)
        {
            var list = new List<TechEntry>();
            foreach (var (id, lv) in _e) list.Add(new TechEntry(id, lv));
            return list;
        }

        [Test]
        public void Catalog_Mouse_IsCsvOrderDistinct()
        {
            IReadOnlyList<TechId> c = TechLogic.Catalog(RoleId.Mouse);
            CollectionAssert.AreEqual(
                new[] { TechId.Gun, TechId.Pepper, TechId.Fortress, TechId.Warehouse, TechId.Dash }, c);
        }

        [Test]
        public void MaxLevel_Gun2_Dash1_Unknown0()
        {
            Assert.AreEqual(2, TechLogic.MaxLevel(TechId.Gun));
            Assert.AreEqual(1, TechLogic.MaxLevel(TechId.Dash));
            Assert.AreEqual(0, TechLogic.MaxLevel(TechId.CheeseShield)); // 계획 2 전엔 행이 없다
        }

        [Test]
        public void LevelOf_NotOwned_IsZero()
        {
            Assert.AreEqual(0, TechLogic.LevelOf(Owned(), TechId.Gun));
            Assert.AreEqual(2, TechLogic.LevelOf(Owned((TechId.Gun, 2)), TechId.Gun));
        }

        [Test]
        public void NextRow_NotOwned_IsLevel1()
        {
            TechRow row = TechLogic.NextRow(Owned(), TechId.Gun);
            Assert.AreEqual(1, row.Level);
            Assert.AreEqual(150, row.Cost);
        }

        [Test]
        public void NextRow_AtMax_IsEmpty()
        {
            Assert.IsFalse(TechLogic.NextRow(Owned((TechId.Gun, 2)), TechId.Gun).IsValid);
        }

        [Test]
        public void CanBuy_NewTech_WithinCap_Ok()
        {
            Assert.AreEqual(TechDeny.None,
                TechLogic.CanBuy(Owned(), TechId.Gun, RoleId.Mouse, 150, 5));
        }

        [Test]
        public void CanBuy_NewTech_SlotsFull_Denied()
        {
            var owned = Owned((TechId.Gun, 1), (TechId.Pepper, 1), (TechId.Fortress, 1),
                              (TechId.Warehouse, 1), (TechId.Dash, 1));
            // 5칸이 찼다 — 6번째 테크는 못 산다(기획 규칙 1). 계획 2에서 테크가 늘면 의미가 생긴다.
            Assert.AreEqual(TechDeny.SlotsFull,
                TechLogic.CanBuy(owned, TechId.CheeseShield, RoleId.Mouse, 9999, 5));
        }

        [Test]
        public void CanBuy_Upgrade_WhenSlotsFull_StillOk()
        {
            var owned = Owned((TechId.Gun, 1), (TechId.Pepper, 1), (TechId.Fortress, 1),
                              (TechId.Warehouse, 1), (TechId.Dash, 1));
            Assert.AreEqual(TechDeny.None,
                TechLogic.CanBuy(owned, TechId.Gun, RoleId.Mouse, 600, 5));
        }

        [Test]
        public void CanBuy_AtMaxLevel_Denied()
        {
            Assert.AreEqual(TechDeny.MaxLevel,
                TechLogic.CanBuy(Owned((TechId.Gun, 2)), TechId.Gun, RoleId.Mouse, 9999, 5));
        }

        [Test]
        public void CanBuy_WrongRole_Denied()
        {
            Assert.AreEqual(TechDeny.WrongRole,
                TechLogic.CanBuy(Owned(), TechId.CatClaw, RoleId.Mouse, 9999, 5));
        }

        [Test]
        public void CanBuy_Poor_Denied()
        {
            Assert.AreEqual(TechDeny.NotEnough,
                TechLogic.CanBuy(Owned(), TechId.Gun, RoleId.Mouse, 149, 5));
        }

        [Test]
        public void CanBuy_Unknown_Denied()
        {
            Assert.AreEqual(TechDeny.Unknown,
                TechLogic.CanBuy(Owned(), TechId.None, RoleId.Mouse, 9999, 5));
        }

        [Test]
        public void IsActive_FortressStickyDash_Only()
        {
            Assert.IsTrue(TechLogic.IsActive(TechId.Fortress));
            Assert.IsTrue(TechLogic.IsActive(TechId.Dash));
            Assert.IsTrue(TechLogic.IsActive(TechId.Sticky));
            Assert.IsTrue(TechLogic.IsActive(TechId.Warehouse)); // 조준 설치 — 밖에서 숫자키로 놓는다
            Assert.IsFalse(TechLogic.IsActive(TechId.Gun));
            Assert.IsFalse(TechLogic.IsActive(TechId.CatClaw));
        }

        [Test]
        public void Candidates_ExcludesOwned_KeepsOrder()
        {
            IReadOnlyList<TechId> c = TechLogic.Candidates(RoleId.Mouse, Owned((TechId.Gun, 1), (TechId.Dash, 1)));
            CollectionAssert.AreEqual(new[] { TechId.Pepper, TechId.Fortress, TechId.Warehouse }, c);
        }

        [Test]
        public void NeedsShopSpot_MouseOnly()
        {
            // 기획 규칙 3·5 — 쥐는 상점 안에서만, 고양이는 핫바에서 바로
            Assert.IsTrue(TechLogic.NeedsShopSpot(RoleId.Mouse));
            Assert.IsFalse(TechLogic.NeedsShopSpot(RoleId.Cat));
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 → 컴파일 실패 확인** (`TechEntry`·`TechLogic`·`TechRow`·`TechDeny` 없음).

- [ ] **Step 3: 구현**

```csharp
// Assets/Scripts/Economy/TechLogic.cs
using System.Collections.Generic;
using CTC.Runtime.Lobby;
using Unity.Netcode;

namespace CTC.Runtime.Economy
{
    /// <summary>보유 테크 한 칸 — NetworkList 원소라 INetworkSerializable·IEquatable.</summary>
    public struct TechEntry : INetworkSerializable, System.IEquatable<TechEntry>
    {
        public TechId Id;
        public int Level;

        public TechEntry(TechId _id, int _level)
        {
            Id = _id;
            Level = _level;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> _s) where T : IReaderWriter
        {
            int id = (int)Id;
            _s.SerializeValue(ref id);
            _s.SerializeValue(ref Level);
            Id = (TechId)id;
        }

        public bool Equals(TechEntry _o) => Id == _o.Id && Level == _o.Level;
    }

    /// <summary>CSV 한 행의 값 복사 — 행이 없으면 <see cref="IsValid"/>가 false.</summary>
    public readonly struct TechRow
    {
        public readonly TechId Id;
        public readonly RoleId Role;
        public readonly TechAxis Axis;
        public readonly int Level;
        public readonly int Cost;
        public readonly string Label;
        public readonly string Hint;

        public bool IsValid => Id != TechId.None;

        public TechRow(Info.TechInfo _r)
        {
            Id = _r.TechId; Role = _r.Role; Axis = _r.Axis; Level = _r.Level;
            Cost = _r.Cost; Label = _r.Label; Hint = _r.Hint;
        }
    }

    public enum TechDeny { None, Unknown, WrongRole, MaxLevel, SlotsFull, NotEnough, NotInShop }

    /// <summary>
    /// 테크 판정 순수 로직 (테크 v3, 2026-09-07). MonoBehaviour와 분리 — EditMode TDD.
    /// 규칙(기획서 §2): 칸 5 · 레벨은 순서대로 · 쥐는 상점 안에서만 · 되팔기 없음.
    /// </summary>
    public static class TechLogic
    {
        private static IReadOnlyList<Info.TechInfo> Rows => Info.InfoManager.Instance.Tech;

        /// <summary>역할의 테크 목록 — CSV 등장 순서, 중복 없음.</summary>
        public static IReadOnlyList<TechId> Catalog(RoleId _role)
        {
            var list = new List<TechId>();
            foreach (Info.TechInfo r in Rows)
            {
                if (r.Role != _role || list.Contains(r.TechId)) continue;
                list.Add(r.TechId);
            }
            return list;
        }

        /// <summary>아직 안 산 테크 — 상점 빈 칸 후보(순서 유지).</summary>
        public static IReadOnlyList<TechId> Candidates(RoleId _role, IReadOnlyList<TechEntry> _owned)
        {
            var list = new List<TechId>();
            foreach (TechId id in Catalog(_role))
            {
                if (LevelOf(_owned, id) == 0) list.Add(id);
            }
            return list;
        }

        public static int MaxLevel(TechId _id)
        {
            int max = 0;
            foreach (Info.TechInfo r in Rows)
            {
                if (r.TechId == _id && r.Level > max) max = r.Level;
            }
            return max;
        }

        public static int LevelOf(IReadOnlyList<TechEntry> _owned, TechId _id)
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i].Id == _id) return _owned[i].Level;
            }
            return 0;
        }

        public static TechRow RowOf(TechId _id, int _level)
        {
            foreach (Info.TechInfo r in Rows)
            {
                if (r.TechId == _id && r.Level == _level) return new TechRow(r);
            }
            return default;
        }

        /// <summary>다음에 살 행 — 미보유면 Lv1, 만렙이면 무효.</summary>
        public static TechRow NextRow(IReadOnlyList<TechEntry> _owned, TechId _id)
        {
            return RowOf(_id, LevelOf(_owned, _id) + 1);
        }

        /// <summary>
        /// 구매·업그레이드 가능 판정. 상점 안 여부는 위치라 여기 없다 — 호출부가 <see cref="NeedsShopSpot"/>와
        /// <c>ShopSpot.AnyContains</c>로 따로 본다.
        /// </summary>
        public static TechDeny CanBuy(IReadOnlyList<TechEntry> _owned, TechId _id, RoleId _role, int _cheese, int _slotCap)
        {
            TechRow next = NextRow(_owned, _id);
            if (!next.IsValid)
            {
                return MaxLevel(_id) == 0 ? TechDeny.Unknown : TechDeny.MaxLevel;
            }
            if (next.Role != _role) return TechDeny.WrongRole;
            if (LevelOf(_owned, _id) == 0 && _owned.Count >= _slotCap) return TechDeny.SlotsFull;
            if (_cheese < next.Cost) return TechDeny.NotEnough;
            return TechDeny.None;
        }

        /// <summary>상점 밖에서 숫자키로 "쓰는" 테크 — 나머지는 패시브라 누를 게 없다.</summary>
        public static bool IsActive(TechId _id)
        {
            return _id == TechId.Fortress || _id == TechId.Warehouse || _id == TechId.Sticky || _id == TechId.Dash;
        }

        /// <summary>기획 규칙 3·5 — 쥐만 상점에 가야 한다.</summary>
        public static bool NeedsShopSpot(RoleId _role) => _role == RoleId.Mouse;

        /// <summary>거절 사유 → 토스트 문구. 재화 이름은 역할별.</summary>
        public static string DenyText(TechDeny _deny, RoleId _role, int _short)
        {
            switch (_deny)
            {
                case TechDeny.MaxLevel: return "이미 만렙";
                case TechDeny.SlotsFull: return "칸이 다 찼다 — 5개까지";
                case TechDeny.NotEnough: return $"{ShopLogic.CurrencyNameOf(_role)} 부족 · {_short} 모자람";
                case TechDeny.NotInShop: return "상점에서만 살 수 있다";
                case TechDeny.WrongRole: return "이 역할은 못 산다";
                default: return "살 수 없다";
            }
        }
    }
}
```

- [ ] **Step 4: 테스트 실행** — `Tools/CTC/Run EditMode Tests`. Expected: `TechLogicTests` 15개 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Economy/TechLogic.cs Assets/Scripts/Economy/TechLogic.cs.meta Assets/Scripts/Tests/EditMode/TechLogicTests.cs Assets/Scripts/Tests/EditMode/TechLogicTests.cs.meta
git commit -m "feat(tech): TechLogic 순수 판정 — 카탈로그·다음 레벨·칸 5·거절 사유 (EditMode 15)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: TechSlots — 보유 상태 Model (NetworkList)

**Files:**
- Create: `Assets/Scripts/Economy/TechSlots.cs`
- Modify: `Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs:75` 근처 (`ShopController` 추가 직후)

- [ ] **Step 1: 컴포넌트 작성**

```csharp
// Assets/Scripts/Economy/TechSlots.cs
using System.Collections.Generic;
using R3;
using Unity.Netcode;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 플레이어가 이번 판에 산 테크와 레벨 (테크 v3). 서버만 쓰고 전원 읽는다.
    /// MVC의 Model — View(ShopHotbar)는 <see cref="Changed"/>를 구독한다.
    /// 되팔기·교체가 없으므로 지우는 API가 없다. 판이 끝나면 프리팹째 사라진다.
    /// </summary>
    public class TechSlots : NetworkBehaviour
    {
        private readonly NetworkList<TechEntry> m_Entries = new NetworkList<TechEntry>();
        private readonly Subject<Unit> m_Changed = new Subject<Unit>();
        private readonly List<TechEntry> m_Snapshot = new List<TechEntry>();

        /// <summary>보유 목록 스냅샷(구매 순 = 칸 순). 호출마다 새로 복사한다 — 순수 로직에 넘길 때 쓴다.</summary>
        public IReadOnlyList<TechEntry> Owned
        {
            get
            {
                m_Snapshot.Clear();
                for (int i = 0; i < m_Entries.Count; i++) m_Snapshot.Add(m_Entries[i]);
                return m_Snapshot;
            }
        }

        public int Count => m_Entries.Count;

        /// <summary>변경 스트림 — 구독 즉시 한 번 흐른다(현재 상태를 그리라고).</summary>
        public Observable<Unit> Changed => m_Changed.Prepend(Unit.Default);

        public int LevelOf(TechId _id) => TechLogic.LevelOf(Owned, _id);

        public override void OnNetworkSpawn()
        {
            m_Entries.OnListChanged += OnListChanged;
        }

        public override void OnNetworkDespawn()
        {
            m_Entries.OnListChanged -= OnListChanged;
        }

        private void OnListChanged(NetworkListEvent<TechEntry> _e)
        {
            m_Changed.OnNext(Unit.Default);
        }

        /// <summary>서버 전용(ShopController). 미보유면 Lv1로 추가, 보유면 레벨 +1. 검증은 호출부가 끝냈다.</summary>
        public void ServerApplyNextLevel(TechId _id)
        {
            if (!IsServer) return;
            for (int i = 0; i < m_Entries.Count; i++)
            {
                if (m_Entries[i].Id != _id) continue;
                m_Entries[i] = new TechEntry(_id, m_Entries[i].Level + 1);
                return;
            }
            m_Entries.Add(new TechEntry(_id, 1));
        }
    }
}
```

- [ ] **Step 2: 프리팹 팩토리에 부착** — `PlayerPrefabFactory.cs` 75행 `var shop = root.AddComponent<ShopController>();` 바로 아래에:

```csharp
                root.AddComponent<TechSlots>();   // 테크 v3 — 보유 테크·레벨(NetworkList)
```

- [ ] **Step 3: 컴파일 확인** — `u_editor_asset(action: refresh)` → `u_console`에 에러 0.

- [ ] **Step 4: 프리팹 재생성** — `u_editor_menu("Tools/CTC/Setup Networking Scene")`. Expected: `PlayerMouse.prefab`·`PlayerCat.prefab`에 `TechSlots` 컴포넌트 (`unity-scanner read -p . Assets/Prefabs/PlayerMouse.prefab`으로 확인).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Economy/TechSlots.cs Assets/Scripts/Economy/TechSlots.cs.meta Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs Assets/Prefabs/PlayerMouse.prefab Assets/Prefabs/PlayerCat.prefab
git commit -m "feat(tech): TechSlots — 플레이어별 보유 테크·레벨 NetworkList (Model)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: ShopSpotLogic(순수) + ShopSpot + ShopSpotSpawner + 프리팹

**Files:**
- Create: `Assets/Scripts/Map/ShopSpotLogic.cs`, `Assets/Scripts/Map/ShopSpot.cs`, `Assets/Scripts/Map/ShopSpotSpawner.cs`
- Create: `Assets/Scripts/Editor/Setup/ShopSpotPrefabFactory.cs`
- Modify: `Assets/Scripts/Editor/Setup/SceneComposer.cs:133` 근처, `Assets/Scripts/Lobby/LobbyController.cs:29-37, 112-128, 186-187`
- Test: `Assets/Scripts/Tests/EditMode/ShopSpotLogicTests.cs`

- [ ] **Step 1: 실패하는 테스트**

```csharp
// Assets/Scripts/Tests/EditMode/ShopSpotLogicTests.cs
using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class ShopSpotLogicTests
    {
        private static List<Vector2> Grid(int _n, float _step)
        {
            var list = new List<Vector2>();
            for (int i = 0; i < _n; i++) for (int j = 0; j < _n; j++) list.Add(new Vector2(i * _step, j * _step));
            return list;
        }

        [Test]
        public void Pick_ReturnsRequestedCount_WhenRoomsSuffice()
        {
            List<Vector2> picked = ShopSpotLogic.Pick(Grid(7, 20f), 4, 25f, 1);
            Assert.AreEqual(4, picked.Count);
        }

        [Test]
        public void Pick_RespectsMinGap()
        {
            List<Vector2> picked = ShopSpotLogic.Pick(Grid(7, 20f), 4, 25f, 7);
            for (int a = 0; a < picked.Count; a++)
                for (int b = a + 1; b < picked.Count; b++)
                    Assert.GreaterOrEqual(Vector2.Distance(picked[a], picked[b]), 25f);
        }

        [Test]
        public void Pick_TooFewRooms_ReturnsWhatFits()
        {
            // 기본 맵(30×30, 방 6)에선 4곳이 안 들어갈 수 있다 — 조용히 줄인다, 예외 없음
            List<Vector2> picked = ShopSpotLogic.Pick(Grid(2, 10f), 4, 25f, 1);
            Assert.LessOrEqual(picked.Count, 1);
        }

        [Test]
        public void Pick_SameSeed_SameResult()
        {
            CollectionAssert.AreEqual(ShopSpotLogic.Pick(Grid(7, 20f), 4, 25f, 42),
                                      ShopSpotLogic.Pick(Grid(7, 20f), 4, 25f, 42));
        }

        [Test]
        public void Contains_UsesRadius()
        {
            Assert.IsTrue(ShopSpotLogic.Contains(new Vector3(0, 0, 0), new Vector3(2.9f, 5f, 0), 3f)); // y 무시
            Assert.IsFalse(ShopSpotLogic.Contains(new Vector3(0, 0, 0), new Vector3(3.1f, 0, 0), 3f));
        }
    }
}
```

- [ ] **Step 2: 실행 → 컴파일 실패 확인.**

- [ ] **Step 3: 순수 로직 구현**

```csharp
// Assets/Scripts/Map/ShopSpotLogic.cs
using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 상점 자리 고르기 (테크 v3). 방 중심 후보 중 서로 <c>_minGap</c> 이상 떨어진 N개를 시드로 고른다.
    /// 계획 1은 시드 = 맵 시드라 판마다 같다. 계획 3에서 판 시드로 바꿔 무작위가 된다.
    /// </summary>
    public static class ShopSpotLogic
    {
        public static List<Vector2> Pick(IReadOnlyList<Vector2> _candidates, int _count, float _minGap, int _seed)
        {
            var picked = new List<Vector2>();
            if (_candidates == null || _candidates.Count == 0 || _count <= 0) return picked;

            var order = new List<int>(_candidates.Count);
            for (int i = 0; i < _candidates.Count; i++) order.Add(i);
            var rng = new System.Random(_seed);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            float gapSqr = _minGap * _minGap;
            foreach (int idx in order)
            {
                Vector2 p = _candidates[idx];
                bool ok = true;
                foreach (Vector2 q in picked)
                {
                    if ((p - q).sqrMagnitude < gapSqr) { ok = false; break; }
                }
                if (!ok) continue;
                picked.Add(p);
                if (picked.Count >= _count) break;
            }
            return picked;
        }

        /// <summary>수평 반경 판정 — 높이는 무시(평면 맵).</summary>
        public static bool Contains(Vector3 _center, Vector3 _point, float _radius)
        {
            float dx = _point.x - _center.x, dz = _point.z - _center.z;
            return dx * dx + dz * dz <= _radius * _radius;
        }
    }
}
```

- [ ] **Step 4: 테스트 실행** → `ShopSpotLogicTests` 5개 PASS.

- [ ] **Step 5: ShopSpot 컴포넌트**

```csharp
// Assets/Scripts/Map/ShopSpot.cs
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 상점 자리 마커 (테크 v3). 쥐는 이 반경 안에서만 테크를 사고 키운다 — 시간 결핍의 실체.
    /// 부술 수 없다(Health 없음). 반경은 BalanceInfo.ShopSpotRadius.
    /// </summary>
    public class ShopSpot : NetworkBehaviour
    {
        private static float Radius => Info.InfoManager.Instance.Balance.ShopSpotRadius;

        /// <summary>이 지점이 어느 상점 안인가 — 서버 검증·핫바 모드 공용.</summary>
        public static bool AnyContains(Vector3 _point)
        {
            foreach (ShopSpot spot in FindObjectsByType<ShopSpot>(FindObjectsSortMode.None))
            {
                if (!spot.IsSpawned) continue;
                if (ShopSpotLogic.Contains(spot.transform.position, _point, Radius)) return true;
            }
            return false;
        }
    }
}
```

- [ ] **Step 6: ShopSpotSpawner**

```csharp
// Assets/Scripts/Map/ShopSpotSpawner.cs
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 판 시작에 상점 자리를 세우는 Worker (테크 v3). `LootBoxSpawner`와 같은 취급 —
    /// NetworkObject라 서버가 스폰한다. 자리는 <see cref="ShopSpotLogic.Pick"/>.
    /// </summary>
    public class ShopSpotSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject m_SpotPrefab;

        private readonly List<NetworkObject> m_Spawned = new List<NetworkObject>();

        /// <summary>서버 전용. 이전 판 것을 걷어내고 새로 세운다.</summary>
        public void ServerSpawnAll(MapLayout _layout)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            ServerDespawnAll();
            if (_layout == null || m_SpotPrefab == null) return;

            var centers = new List<Vector2>(_layout.Rooms.Count);
            foreach (RoomRect room in _layout.Rooms) centers.Add(room.Center);

            Info.BalanceInfo b = Info.InfoManager.Instance.Balance;
            List<Vector2> picked = ShopSpotLogic.Pick(centers, b.ShopSpotCount, b.ShopSpotMinGap, _layout.Seed);

            foreach (Vector2 p in picked)
            {
                GameObject go = Instantiate(m_SpotPrefab, new Vector3(p.x, 0f, p.y), Quaternion.identity);
                var no = go.GetComponent<NetworkObject>();
                no.Spawn(true);
                m_Spawned.Add(no);
            }
            Core.GameDebug.Log($"[ShopSpotSpawner] 상점 {picked.Count}곳 (요청 {b.ShopSpotCount})");
        }

        /// <summary>서버 전용. 로비 복귀 시 걷는다.</summary>
        public void ServerDespawnAll()
        {
            foreach (NetworkObject no in m_Spawned)
            {
                if (no != null && no.IsSpawned) no.Despawn(true);
            }
            m_Spawned.Clear();
        }
    }
}
```

- [ ] **Step 7: 프리팹 팩토리** — 기존 팩토리(`Assets/Scripts/Editor/Setup/DogPrefabFactory.cs`)의 저장 방식을 따른다. 모델은 **새로 만들지 않는다**: `Assets/Poly Universal/…` 아래 카운터·선반 프롭이 있으면 그 메시를 자식으로, 없으면 원통 프리미티브(임시).

```csharp
// Assets/Scripts/Editor/Setup/ShopSpotPrefabFactory.cs
using CTC.Runtime.Map;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor.Setup
{
    /// <summary>상점 자리 프리팹 (테크 v3). 부수지 못하므로 Health·RoleTag가 없다.</summary>
    public static class ShopSpotPrefabFactory
    {
        public const string PATH = "Assets/Prefabs/ShopSpot.prefab";
        private const string PROP_SEARCH = "t:Model counter";   // Poly Universal 프롭 후보

        public static GameObject Build()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PATH);
            if (existing != null) return existing;   // 자리·개수의 주인은 프리팹 — 있으면 안 덮어쓴다(CLAUDE.md 함정 2)

            var root = new GameObject("ShopSpot");
            root.AddComponent<NetworkObject>();
            root.AddComponent<ShopSpot>();

            GameObject visual = LoadPackProp();
            if (visual == null)
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visual.transform.localScale = new Vector3(1.6f, 0.5f, 1.6f);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
            }
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject LoadPackProp()
        {
            string[] guids = AssetDatabase.FindAssets(PROP_SEARCH, new[] { "Assets/Poly Universal" });
            if (guids.Length == 0) return null;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return model != null ? (GameObject)PrefabUtility.InstantiatePrefab(model) : null;
        }
    }
}
```

- [ ] **Step 8: SceneComposer 배선** — 133행 `var boxSpawner = lobbyGo.AddComponent<CTC.Runtime.Map.LootBoxSpawner>();` 블록을 따라 그 아래에:

```csharp
            var shopSpawner = lobbyGo.AddComponent<CTC.Runtime.Map.ShopSpotSpawner>();
            SetRef(shopSpawner, "m_SpotPrefab", ShopSpotPrefabFactory.Build());
```
(`SetRef`는 같은 파일에서 `boxSpawner`의 `m_BoxPrefab`을 넣는 데 쓴 헬퍼와 같은 이름을 쓴다 — 없으면 `SerializedObject`로 `FindProperty("m_SpotPrefab").objectReferenceValue = …; ApplyModifiedPropertiesWithoutUndo();`.)

- [ ] **Step 9: LobbyController 배선**

29행 `private Map.LootBoxSpawner m_BoxSpawner;` 아래:
```csharp
        private Map.ShopSpotSpawner m_ShopSpawner;
```
37행 `m_BoxSpawner = GetComponent<Map.LootBoxSpawner>();` 아래:
```csharp
            m_ShopSpawner = GetComponent<Map.ShopSpotSpawner>();
```
115행·128행 `if (m_BoxSpawner != null) m_BoxSpawner.ServerSpawnAll(ActiveLayout);` 각각 아래:
```csharp
            if (m_ShopSpawner != null) m_ShopSpawner.ServerSpawnAll(ActiveLayout);   // 상점 자리(테크 v3)
```
187행 `if (m_BoxSpawner != null) m_BoxSpawner.ServerDespawnAll();` 아래:
```csharp
            if (m_ShopSpawner != null) m_ShopSpawner.ServerDespawnAll();
```

- [ ] **Step 10: 씬 재생성 + 플레이 확인** — `u_editor_menu("Tools/CTC/Setup Networking Scene")` → `u_play(start)` → 호스트로 판 시작 → `u_console`에 `[ShopSpotSpawner] 상점 4곳`(대형 맵) 또는 더 적은 수. `u_editor_gameobject(find "ShopSpot")`로 개수 확인.

- [ ] **Step 11: Commit**

```bash
git add Assets/Scripts/Map/ShopSpotLogic.cs* Assets/Scripts/Map/ShopSpot.cs* Assets/Scripts/Map/ShopSpotSpawner.cs* Assets/Scripts/Editor/Setup/ShopSpotPrefabFactory.cs* Assets/Scripts/Editor/Setup/SceneComposer.cs Assets/Scripts/Lobby/LobbyController.cs Assets/Prefabs/ShopSpot.prefab* Assets/Scenes Assets/Scripts/Tests/EditMode/ShopSpotLogicTests.cs*
git commit -m "feat(shop): 상점 자리 — ShopSpotLogic(순수)·ShopSpot·ShopSpotSpawner·프리팹, 판 시작에 서버가 세운다

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: ShopController를 TechId로 — 구매·업그레이드·사용

**Files:**
- Modify: `Assets/Scripts/Economy/ShopController.cs` (전면)
- Modify: `Assets/Scripts/Economy/ShopLogic.cs` (`ShopItemId`·`ShopItem`·`CATALOG`·`Find`·`RoleCatalogIndices`·`IsPlaceable`·`HOTBAR_SLOTS` 삭제; 밸런스 접근자·`CanBuy`·`CanAddStack`·`PounceDamage`·`MoveSpeed`·`CurrencyNameOf`·`WeaponStatsForTier` 유지)

이 태스크가 끝나면 `ShopHotbar`·`BotShopper`·`MouseBotLogic`·`ShopLogicTests`가 컴파일되지 않는다 — **Task 6·7·8에서 차례로 고친다.** 그때까지 커밋은 하되 EditMode는 안 돈다(컴파일 에러). 한 커밋에 5~8을 묶고 싶으면 그래도 된다.

- [ ] **Step 1: ShopLogic에서 카탈로그 관련 삭제** — `ShopItemId` enum, `ShopItem` struct, `CATALOG`, `Find`, `HOTBAR_SLOTS`, `RoleCatalogIndices`, `IsPlaceable`를 지운다. `WeaponStats`·`ShopLogic`의 나머지는 그대로. 파일 머리 주석의 "정적 카탈로그"를 "밸런스 접근자"로 고친다.

- [ ] **Step 2: ShopController 전면 교체**

```csharp
// Assets/Scripts/Economy/ShopController.cs
using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 테크 구매·업그레이드·사용 진입점(플레이어 프리팹) — 테크 v3(2026-09-07).
    /// 소유자 Buy → ServerRpc → 서버가 역할·상점 안(쥐)·TechLogic·TrySpend 검증 → 레벨 적용.
    /// 사용(Use)은 상점 밖 숫자키 — 요새 즉발·창고 조준·대시.
    /// </summary>
    public class ShopController : NetworkBehaviour
    {
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;
        private static float PLACE_FORWARD_METERS => Balance.PlaceForwardMeters;

        [SerializeField] private GameObject m_FortressPrefab;
        [SerializeField] private GameObject m_WarehousePrefab;

        public static float MAX_PLACE_DISTANCE => Balance.PlaceMaxDistanceMeters;

        private TechSlots m_Slots;
        private RoleTag m_Tag;
        private CheeseWallet m_Wallet;

        public override void OnNetworkSpawn()
        {
            m_Slots = GetComponent<TechSlots>();
            m_Tag = GetComponent<RoleTag>();
            m_Wallet = GetComponent<CheeseWallet>();
        }

        // ===== 로컬 진입점 =====

        /// <summary>산다(미보유) 또는 키운다(보유). 로컬 UI·봇·QA 공용.</summary>
        public void Buy(TechId _id) => BuyRpc((int)_id);

        /// <summary>즉발 액티브 — 요새·대시.</summary>
        public void Use(TechId _id) => UseRpc((int)_id);

        /// <summary>조준 액티브 — 창고. 고스트 확정 위치를 서버가 재검증한다.</summary>
        public void UseAt(TechId _id, Vector3 _position) => UseAtRpc((int)_id, _position);

        /// <summary>로컬 표시용 판정(권위는 서버). 상점 안 여부까지 본다.</summary>
        public TechDeny CanBuyNow(TechId _id)
        {
            if (m_Slots == null || m_Tag == null || m_Wallet == null) return TechDeny.Unknown;
            if (TechLogic.NeedsShopSpot(m_Tag.Role) && !Map.ShopSpot.AnyContains(transform.position))
                return TechDeny.NotInShop;
            return TechLogic.CanBuy(m_Slots.Owned, _id, m_Tag.Role, m_Wallet.CheeseValue, Balance.TechSlotCap);
        }

        /// <summary>쓸 수 있는가 — 보유·상황(요새 겹침·창고 상한).</summary>
        public bool CanUse(TechId _id)
        {
            if (m_Slots == null || m_Slots.LevelOf(_id) == 0 || !TechLogic.IsActive(_id)) return false;
            switch (_id)
            {
                case TechId.Fortress:
                    return m_FortressPrefab != null && !FortressShield.AnyContains(transform.position)
                           && m_Wallet != null && m_Wallet.CheeseValue >= Balance.FortressPlaceCost;
                case TechId.Warehouse:
                    return m_WarehousePrefab != null
                           && WarehouseProduction.CountOwnedBy(OwnerClientId) < ShopLogic.WAREHOUSE_LIMIT_PER_MOUSE;
                case TechId.Dash:
                    return true;   // 쿨다운은 MouseDash가 본다
                default:
                    return false;
            }
        }

        // ===== 서버 =====

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void BuyRpc(int _id)
        {
            var id = (TechId)_id;
            TechDeny deny = CanBuyNow(id);
            if (deny != TechDeny.None) { ResultRpc(false); return; }

            TechRow row = TechLogic.NextRow(m_Slots.Owned, id);
            if (!m_Wallet.TrySpend(row.Cost)) { ResultRpc(false); return; }

            m_Slots.ServerApplyNextLevel(id);
            ApplyLevel(id, row.Level);
            BuySoundRpc(transform.position);
            Core.GameDebug.Log($"[ShopController] {OwnerClientId} {id} Lv{row.Level} (-{row.Cost})");
            ResultRpc(true);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void UseRpc(int _id)
        {
            var id = (TechId)_id;
            if (!CanUse(id)) { ResultRpc(false); return; }
            switch (id)
            {
                case TechId.Fortress:
                    if (!m_Wallet.TrySpend(Balance.FortressPlaceCost)) { ResultRpc(false); return; }
                    SpawnFortressAround(m_FortressPrefab);
                    break;
                case TechId.Dash:
                    GetComponent<Player.MouseDash>().DevDash();
                    break;
                default:
                    ResultRpc(false);
                    return;
            }
            ResultRpc(true);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void UseAtRpc(int _id, Vector3 _position)
        {
            var id = (TechId)_id;
            if (id != TechId.Warehouse || !CanUse(id)) { ResultRpc(false); return; }

            Vector3 flat = _position - transform.position;
            flat.y = 0f;
            if (flat.magnitude > MAX_PLACE_DISTANCE || !PlacementFits(id, _position)) { ResultRpc(false); return; }

            GameObject go = Instantiate(m_WarehousePrefab, _position, Quaternion.identity);
            go.GetComponent<NetworkObject>().Spawn(true);
            BindWarehouseOwner(go);
            ResultRpc(true);
        }

        /// <summary>레벨이 오른 순간 기존 컴포넌트에 반영. 계획 2가 신규 테크를 여기에 붙인다.</summary>
        private void ApplyLevel(TechId _id, int _level)
        {
            switch (_id)
            {
                case TechId.Gun:
                    GetComponent<MousePopgun>().ServerSetTier(_level);          // Lv1 새총 · Lv2 기관총
                    break;
                case TechId.Pepper:
                    GetComponent<PepperRounds>().ServerSetOwned();
                    break;
                case TechId.Dash:
                    GetComponent<Player.MouseDash>().ServerSetOwned();
                    break;
                case TechId.Fortress:
                case TechId.Warehouse:
                    break;                                                      // 설치권만 — 실제 설치는 Use
                case TechId.CatClaw:
                    GetComponent<CatPounce>().ServerAddDamageStack();           // 레벨 = 스택
                    break;
                case TechId.CatStomp:
                    GetComponent<Player.PlayerMovement>().ServerAddSpeedStack();
                    break;
            }
        }

        // 구매음은 12m로 퍼진다(정보=소리) — 상점 근처 고양이가 듣는다. 실패음은 구매자만.
        [Rpc(SendTo.Everyone)]
        private void BuySoundRpc(Vector3 _at)
        {
            Audio.SfxPlayer.PlayAt(Audio.SfxId.ShopBuy, _at, 1f, Balance.ShopBuySoundMeters);   // (id, pos, volume, maxDistance)
        }

        [Rpc(SendTo.Owner)]
        private void ResultRpc(bool _success)
        {
            if (!_success) Audio.SfxPlayer.PlayUi(Audio.SfxId.ShopDeny);
        }

        // ===== 설치 공용 (기존 유지) =====

        private const int PLACE_OVERLAP_BUFFER = 16;
        private static readonly Collider[] s_PlaceOverlap = new Collider[PLACE_OVERLAP_BUFFER];

        public static bool PlacementFits(TechId _id, Vector3 _position)
        {
            Vector3 half = HalfExtentsOf(_id);
            Vector3 center = _position + Vector3.up * half.y;
            int count = Physics.OverlapBoxNonAlloc(center, half * 0.95f, s_PlaceOverlap,
                Quaternion.identity, ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (s_PlaceOverlap[i].transform.root.GetComponent<CharacterController>() != null) continue;
                return false;
            }
            return true;
        }

        public static Vector3 HalfExtentsOf(TechId _id)
        {
            switch (_id)
            {
                case TechId.Fortress: return new Vector3(1.2f, 0.6f, 1.2f);
                case TechId.Warehouse: return new Vector3(0.5f, 0.4f, 0.5f);
                default: return new Vector3(0.25f, 0.25f, 0.25f);
            }
        }

        private void BindWarehouseOwner(GameObject _spawned)
        {
            var production = _spawned.GetComponent<WarehouseProduction>();
            if (production != null) production.ServerSetOwner(OwnerClientId);
        }

        private void SpawnFortressAround(GameObject _prefab)
        {
            GameObject go = Instantiate(_prefab, transform.position, Quaternion.LookRotation(FlatForward()));
            go.GetComponent<NetworkObject>().Spawn(true);
        }

        private Vector3 FlatForward()
        {
            return new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        }
    }
}
```

`SfxPlayer.PlayAt(SfxId, Vector3, float _volume = 1f, float _maxDistance = …)`는 이미 있다(`SfxPlayer.cs:100`) — 네 번째 인자가 가청 거리다.

- [ ] **Step 3: 컴파일** — 이 시점엔 `ShopHotbar`·`BotShopper`·`MouseBotLogic`·`ShopLogicTests`·`MouseBotLogicTests`에서 `ShopItemId` 에러가 난다. 예상된 것 — Task 6~8로.

- [ ] **Step 4: Commit(중간)**

```bash
git add Assets/Scripts/Economy/ShopController.cs Assets/Scripts/Economy/ShopLogic.cs
git commit -m "refactor(shop): ShopController를 TechId로 — 구매·업그레이드·사용 RPC, 상점 안 검증, 구매음 12m (WIP: 핫바·봇 미이관)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: ShopHotbar — 두 모드 View

**Files:**
- Modify: `Assets/Scripts/Economy/ShopHotbar.cs` (전면)
- Modify: `Assets/Scripts/Editor/Setup/LobbyCanvasBuilder.cs:121-168` (모드 라벨 1개 추가)

- [ ] **Step 1: LobbyCanvasBuilder에 모드 라벨** — 121행 `hotbarRoot` 생성 뒤, 슬롯 루프 앞에 텍스트 하나. 기존 슬롯 라벨을 만드는 코드와 같은 방식(`TMP_Text` 생성 헬퍼)으로:

```csharp
            // 테크 v3 — 핫바 위 한 줄: "상점 · 숫자키 = 산다 / 키운다" 또는 "숫자키 = 쓴다"  ·  "보유 3/5"
            TMP_Text modeLabel = MakeLabel(hotbarRoot.transform, "ModeLabel", 13f, UiPalette.TextDim);
            modeLabel.rectTransform.anchoredPosition = new Vector2(0f, 34f);   // 슬롯 줄 바로 위
```
그리고 168행 `SetRefArray(hotbar, "m_SlotCosts", slotCosts);` 아래에 `SetRef(hotbar, "m_ModeLabel", modeLabel);`. (`MakeLabel`·`SetRef` 이름은 같은 파일에서 슬롯 라벨을 만들 때 쓴 헬퍼를 그대로 쓴다 — 이름이 다르면 그 이름으로.)

- [ ] **Step 2: ShopHotbar 전면 교체**

```csharp
// Assets/Scripts/Economy/ShopHotbar.cs
using System.Collections.Generic;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 하단 상시 핫바 — 테크 v3(2026-09-07) 두 모드.
    /// **상점 안**(쥐: ShopSpot 반경 / 고양이: 항상): 빈 칸 숫자키 = 후보 넘김, Enter = 확정 구매 / 있는 칸 = 다음 레벨.
    /// **밖**: 있는 칸 숫자키 = 쓴다(요새 즉발·창고 조준·대시). 패시브는 누를 것 없음.
    /// 커서는 끝까지 잠겨 있다 — 3인칭에서 커서가 곧 시야다.
    /// </summary>
    public class ShopHotbar : MonoBehaviour
    {
        private const float TOAST_SECONDS = 1.4f;
        private const float TOAST_LONG_SECONDS = 3f;
        private const float INTRO_TOAST_SECONDS = 4f;
        private const float GHOST_MIN_DISTANCE = 1.5f;
        private const int VISIBLE_SLOTS = 5;   // 칸 5 = TechSlotCap. 6번째 슬롯 오브젝트는 숨긴다.

        private static float GHOST_MAX_DISTANCE => Info.InfoManager.Instance.Balance.PlaceGhostMaxMeters;
        private static int SLOT_CAP => Info.InfoManager.Instance.Balance.TechSlotCap;

        private static readonly Color COLOR_HINT = UI.UiPalette.WithAlpha(UI.UiPalette.TextDim, 0.9f);
        private static readonly Color COLOR_READY = UI.UiPalette.TextOn;
        private static readonly Color COLOR_POOR = UI.UiPalette.Bad;
        private static readonly Color COLOR_OWNED = UI.UiPalette.Good;
        private static readonly Color COLOR_EMPTY = UI.UiPalette.WithAlpha(UI.UiPalette.TextDim, 0.5f);

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private GameObject m_Root;
        [SerializeField] private GameObject[] m_Slots;
        [SerializeField] private TMP_Text[] m_SlotLabels;
        [SerializeField] private TMP_Text[] m_SlotHints;
        [SerializeField] private TMP_Text[] m_SlotCosts;
        [SerializeField] private TMP_Text m_ModeLabel;
        [SerializeField] private TMP_Text m_Toast;
        [SerializeField] private Material m_GhostMaterialSource;

        public bool IsPlacing => m_PlacingId != TechId.None;

        private readonly SerialDisposable m_WalletSub = new SerialDisposable();
        private readonly SerialDisposable m_SlotsSub = new SerialDisposable();

        private ShopController m_Shop;
        private TechSlots m_TechSlots;
        private RoleId m_Role;
        private int m_Cheese;
        private bool m_GameStarted;
        private bool m_SlotsDirty;
        private bool m_IntroShown;
        private bool m_InShop;

        // 빈 칸마다 "지금 보여 주는 후보" 인덱스 — 같은 숫자키를 반복하면 넘어간다
        private readonly int[] m_CandidateIndex = new int[VISIBLE_SLOTS];

        private float m_ToastUntil;
        private TechId m_PlacingId = TechId.None;
        private GameObject m_Ghost;
        private Material m_GhostMaterial;
        private bool m_GhostValid;

        private void Awake()
        {
            m_Root.SetActive(false);
            m_Toast.gameObject.SetActive(false);
        }

        private void Start()
        {
            m_WalletSub.AddTo(this);
            m_SlotsSub.AddTo(this);
            m_Lobby.GameStartedChanged.Subscribe(OnGameStartedChanged).AddTo(this);
            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
        }

        private void OnDestroy()
        {
            CancelPlacement();
            if (m_GhostMaterial != null) Destroy(m_GhostMaterial);
        }

        private void OnGameStartedChanged(bool _started)
        {
            m_GameStarted = _started;
            if (!_started) m_IntroShown = false;
            ApplyActive();
        }

        private void OnLocalPlayerChanged(NetworkObject _player)
        {
            ShopController shop = _player != null ? _player.GetComponent<ShopController>() : null;
            TechSlots slots = _player != null ? _player.GetComponent<TechSlots>() : null;
            RoleTag tag = _player != null ? _player.GetComponent<RoleTag>() : null;
            CheeseWallet wallet = _player != null ? _player.GetComponent<CheeseWallet>() : null;

            if (shop == null || slots == null || tag == null || wallet == null)
            {
                m_Shop = null;
                m_TechSlots = null;
                m_WalletSub.Disposable = null;
                m_SlotsSub.Disposable = null;
                ApplyActive();
                return;
            }

            m_Shop = shop;
            m_TechSlots = slots;
            m_Role = tag.Role;
            for (int i = 0; i < m_CandidateIndex.Length; i++) m_CandidateIndex[i] = 0;
            m_WalletSub.Disposable = wallet.Cheese.Subscribe(c => { m_Cheese = c; m_SlotsDirty = true; });
            m_SlotsSub.Disposable = slots.Changed.Subscribe(_ => m_SlotsDirty = true);
            ApplyActive();
        }

        private void ApplyActive()
        {
            bool inGame = m_GameStarted && m_Shop != null;
            if (m_Root.activeSelf != inGame) m_Root.SetActive(inGame);
            if (inGame) m_SlotsDirty = true;
            else CancelPlacement();

            if (inGame && !m_IntroShown)
            {
                m_IntroShown = true;
                string attack = m_Role == RoleId.Cat ? "덮치기" : "발사";
                string shop = m_Role == RoleId.Cat ? "숫자키 구매" : "상점에서 숫자키 구매";
                Say($"WASD 이동 · 마우스 시점 · 좌클릭 {attack} · LShift 살금살금 · {shop}", INTRO_TOAST_SECONDS);
            }
        }

        private void Update()
        {
            if (!m_GameStarted || m_Shop == null) return;

            // 상점 안/밖은 위치라 매 프레임 본다(상점 4곳, FindObjectsByType 비용은 미미)
            bool inShop = !TechLogic.NeedsShopSpot(m_Role) || Map.ShopSpot.AnyContains(m_Shop.transform.position);
            if (inShop != m_InShop) { m_InShop = inShop; m_SlotsDirty = true; }

            if (m_SlotsDirty) { m_SlotsDirty = false; RefreshSlots(); }

            if (m_PlacingId != TechId.None) UpdatePlacement();
            else HandleHotkeys();

            if (m_Toast.gameObject.activeSelf && Time.time >= m_ToastUntil) m_Toast.gameObject.SetActive(false);
        }

        // ===== 그리기 =====

        private void RefreshSlots()
        {
            IReadOnlyList<TechEntry> owned = m_TechSlots.Owned;
            IReadOnlyList<TechId> candidates = TechLogic.Candidates(m_Role, owned);

            for (int slot = 0; slot < m_Slots.Length; slot++)
            {
                bool visible = slot < VISIBLE_SLOTS;
                if (m_Slots[slot].activeSelf != visible) m_Slots[slot].SetActive(visible);
                if (!visible) continue;

                if (slot < owned.Count) DrawOwned(slot, owned[slot], owned);
                else DrawEmpty(slot, candidates);
            }

            string mode = m_InShop ? "상점 · 숫자키 = 산다 / 키운다 · Enter = 확정" : "숫자키 = 쓴다";
            if (m_ModeLabel != null) m_ModeLabel.text = $"{mode}    보유 {owned.Count}/{SLOT_CAP}";
        }

        private void DrawOwned(int _slot, TechEntry _e, IReadOnlyList<TechEntry> _owned)
        {
            TechRow cur = TechLogic.RowOf(_e.Id, _e.Level);
            TechRow next = TechLogic.NextRow(_owned, _e.Id);
            m_SlotLabels[_slot].text = $"{_slot + 1}  {cur.Label} Lv{_e.Level}";
            SetHint(_slot, cur.Hint);

            if (m_InShop)
            {
                bool affordable = next.IsValid && m_Cheese >= next.Cost;
                m_SlotCosts[_slot].text = next.IsValid ? next.Cost.ToString() : "MAX";
                Color c = !next.IsValid ? COLOR_OWNED : affordable ? COLOR_READY : COLOR_POOR;
                m_SlotLabels[_slot].color = c; m_SlotCosts[_slot].color = c;
            }
            else
            {
                bool usable = TechLogic.IsActive(_e.Id);
                m_SlotCosts[_slot].text = usable ? "쓰기" : "";
                Color c = usable ? COLOR_READY : COLOR_OWNED;
                m_SlotLabels[_slot].color = c; m_SlotCosts[_slot].color = c;
            }
        }

        private void DrawEmpty(int _slot, IReadOnlyList<TechId> _candidates)
        {
            if (!m_InShop || _candidates.Count == 0)
            {
                m_SlotLabels[_slot].text = $"{_slot + 1}  —";
                m_SlotCosts[_slot].text = "";
                SetHint(_slot, m_InShop ? "살 게 없다" : "");
                m_SlotLabels[_slot].color = COLOR_EMPTY;
                return;
            }
            int idx = m_CandidateIndex[_slot] % _candidates.Count;
            TechRow row = TechLogic.RowOf(_candidates[idx], 1);
            bool affordable = m_Cheese >= row.Cost;
            m_SlotLabels[_slot].text = $"{_slot + 1}  {row.Label}  ({idx + 1}/{_candidates.Count})";
            m_SlotCosts[_slot].text = row.Cost.ToString();
            SetHint(_slot, row.Hint);
            Color c = affordable ? COLOR_READY : COLOR_POOR;
            m_SlotLabels[_slot].color = c; m_SlotCosts[_slot].color = c;
        }

        private void SetHint(int _slot, string _text)
        {
            if (m_SlotHints == null || _slot >= m_SlotHints.Length || m_SlotHints[_slot] == null) return;
            m_SlotHints[_slot].text = _text;
            m_SlotHints[_slot].color = COLOR_HINT;
        }

        // ===== 입력 =====

        private void HandleHotkeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || Core.UiFocus.BlocksGameplay) return;

            IReadOnlyList<TechEntry> owned = m_TechSlots.Owned;
            IReadOnlyList<TechId> candidates = TechLogic.Candidates(m_Role, owned);

            // Enter = 첫 빈 칸의 후보를 확정 구매 (상점 안에서만)
            if (m_InShop && kb.enterKey.wasPressedThisFrame && owned.Count < SLOT_CAP && candidates.Count > 0)
            {
                int slot = owned.Count;
                TryBuy(candidates[m_CandidateIndex[slot] % candidates.Count]);
                return;
            }

            for (int slot = 0; slot < VISIBLE_SLOTS; slot++)
            {
                if (!kb[SlotKey(slot)].wasPressedThisFrame) continue;

                if (slot < owned.Count)
                {
                    if (m_InShop) TryBuy(owned[slot].Id);       // 키운다
                    else TryUse(owned[slot].Id);                 // 쓴다
                }
                else if (m_InShop && slot == owned.Count && candidates.Count > 0)
                {
                    m_CandidateIndex[slot] = (m_CandidateIndex[slot] + 1) % candidates.Count;   // 후보 넘김
                    m_SlotsDirty = true;
                }
                else if (!m_InShop)
                {
                    Say("상점에서만 살 수 있다");
                }
                return;
            }
        }

        private void TryBuy(TechId _id)
        {
            TechDeny deny = m_Shop.CanBuyNow(_id);
            if (deny != TechDeny.None)
            {
                TechRow next = TechLogic.NextRow(m_TechSlots.Owned, _id);
                Say(TechLogic.DenyText(deny, m_Role, next.IsValid ? next.Cost - m_Cheese : 0));
                return;
            }
            TechRow row = TechLogic.NextRow(m_TechSlots.Owned, _id);
            m_Shop.Buy(_id);
            Say($"{row.Label} Lv{row.Level} — {row.Hint}", TOAST_LONG_SECONDS);
        }

        private void TryUse(TechId _id)
        {
            if (!TechLogic.IsActive(_id)) { Say("패시브 — 늘 켜져 있다"); return; }
            if (!m_Shop.CanUse(_id))
            {
                Say(_id == TechId.Fortress ? "요새 안에서는 못 세운다 — 밖으로" :
                    _id == TechId.Warehouse ? "창고는 하나까지" : "지금은 못 쓴다");
                return;
            }
            if (_id == TechId.Warehouse) { BeginPlacement(_id, "창고"); return; }
            m_Shop.Use(_id);
        }

        private static Key SlotKey(int _slot)
        {
            switch (_slot)
            {
                case 0: return Key.Digit1;
                case 1: return Key.Digit2;
                case 2: return Key.Digit3;
                case 3: return Key.Digit4;
                default: return Key.Digit5;
            }
        }

        // ===== 설치물 고스트 (기존 유지, ShopItemId → TechId) =====

        private void BeginPlacement(TechId _id, string _name)
        {
            CancelPlacement();
            if (m_GhostMaterial == null) m_GhostMaterial = CreateGhostMaterial();
            if (m_GhostMaterial == null) { Say("설치 미리보기를 만들 수 없다"); return; }

            m_PlacingId = _id;
            Vector3 half = ShopController.HalfExtentsOf(_id);
            m_Ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_Ghost.name = "PlacementGhost";
            Destroy(m_Ghost.GetComponent<Collider>());
            m_Ghost.transform.localScale = half * 2f;
            m_Ghost.GetComponent<Renderer>().sharedMaterial = m_GhostMaterial;
            Say($"{_name} 배치 — 좌클릭 설치 / 우클릭 취소");
        }

        private void UpdatePlacement()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) { CancelPlacement(); Say("배치 취소"); return; }

            Vector3 pos = GhostPosition(m_Shop.transform);
            m_GhostValid = ShopController.PlacementFits(m_PlacingId, pos);
            Vector3 half = ShopController.HalfExtentsOf(m_PlacingId);
            m_Ghost.transform.position = pos + Vector3.up * half.y;
            m_GhostMaterial.color = m_GhostValid
                ? UI.UiPalette.WithAlpha(UI.UiPalette.Good, 0.35f)
                : UI.UiPalette.WithAlpha(UI.UiPalette.Bad, 0.35f);

            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (!m_GhostValid) { Say("여기엔 설치할 수 없다"); return; }

            m_Shop.UseAt(m_PlacingId, pos);
            CancelPlacement();
            Say("창고 설치 — 자리를 비워도 치즈가 쌓인다 · 소리로 위치가 팔린다", TOAST_LONG_SECONDS);
        }

        private static Vector3 GhostPosition(Transform _player)
        {
            Camera cam = Camera.main;
            Vector3 dir = cam != null ? cam.transform.forward : _player.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = _player.forward;
            dir.Normalize();
            Vector3 origin = _player.position + Vector3.up * 1.5f;
            float distance = GHOST_MAX_DISTANCE;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, GHOST_MAX_DISTANCE, ~(1 << 2), QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(GHOST_MIN_DISTANCE, hit.distance - 0.5f);
            Vector3 target = _player.position + dir * distance;
            target.y = _player.position.y;
            return target;
        }

        public void CancelPlacementFromEscape()
        {
            CancelPlacement();
            Say("배치 취소");
        }

        private void CancelPlacement()
        {
            m_PlacingId = TechId.None;
            if (m_Ghost != null) Destroy(m_Ghost);
            m_Ghost = null;
        }

        private Material CreateGhostMaterial()
        {
            if (m_GhostMaterialSource == null)
            {
                Debug.LogError("[ShopHotbar] 고스트 머티리얼 미배선 — Setup Networking Scene 재실행 필요");
                return null;
            }
            return new Material(m_GhostMaterialSource);
        }

        private void Say(string _message, float _seconds = TOAST_SECONDS)
        {
            m_Toast.text = _message;
            m_Toast.gameObject.SetActive(true);
            m_ToastUntil = Time.time + _seconds;
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인** — 남은 에러는 `BotShopper`·`MouseBotLogic`·테스트 2개뿐이어야 한다.

- [ ] **Step 4: Commit(중간)**

```bash
git add Assets/Scripts/Economy/ShopHotbar.cs Assets/Scripts/Editor/Setup/LobbyCanvasBuilder.cs
git commit -m "feat(ui): ShopHotbar 두 모드 — 상점 안 산다·키운다(후보 넘김·Enter) / 밖 쓴다, 보유 n/5 표시

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: 봇 이관 — BotShopper · MouseBotLogic (TDD)

**Files:**
- Modify: `Assets/Scripts/Bots/MouseBotLogic.cs:88-112`
- Modify: `Assets/Scripts/Bots/BotShopper.cs`
- Modify: `Assets/Scripts/Tests/EditMode/MouseBotLogicTests.cs` (`NextPurchase` 관련 테스트)
- Modify: `Assets/Scripts/Economy/ShopController.cs` (봇 상점 면제 1줄)

계획 1의 봇은 **상점까지 가지 않는다**(계획 3). 대신 서버 검증에서 봇만 상점 안 조건을 면제한다 — 봇 계측이 "상점까지 가는 비용"을 빼고 잰다는 뜻이니 결과에 그 주석을 남긴다.

- [ ] **Step 1: MouseBotLogicTests의 `NextPurchase` 테스트를 TechId로** — 파일에서 `ShopItemId`를 쓰는 테스트들을 다음으로 바꾼다(기존 의도 유지: 대시 → 요새 → 창고, 비상 벽 값 남기기):

```csharp
        [Test]
        public void NextPurchase_Dash_First()
        {
            Assert.AreEqual(TechId.Dash, MouseBotLogic.NextPurchase(100, false, false, false, false));
        }

        [Test]
        public void NextPurchase_Fortress_BeforeWarehouse()
        {
            Assert.AreEqual(TechId.Fortress, MouseBotLogic.NextPurchase(100, true, false, false, false));
        }

        [Test]
        public void NextPurchase_Warehouse_KeepsFortressPlaceCost()
        {
            // 창고 150 + 설치비 80 = 230 있어야 창고
            Assert.AreEqual(TechId.None, MouseBotLogic.NextPurchase(229, true, false, false, true));
            Assert.AreEqual(TechId.Warehouse, MouseBotLogic.NextPurchase(230, true, false, false, true));
        }

        [Test]
        public void NextPurchase_AllBought_None()
        {
            Assert.AreEqual(TechId.None, MouseBotLogic.NextPurchase(9999, true, true, true, true));
        }
```
(`using CTC.Runtime.Economy;` 추가.)

- [ ] **Step 2: 실행 → 컴파일 실패 확인.**

- [ ] **Step 3: MouseBotLogic.NextPurchase 교체** (88~112행)

```csharp
        public static Economy.TechId NextPurchase(
            int _cheese, bool _hasDash, bool _hasGun, bool _hasWarehouse, bool _hasFortress)
        {
            int placeCost = Info.InfoManager.Instance.Balance.FortressPlaceCost;
            int CostOf(Economy.TechId id) => Economy.TechLogic.RowOf(id, 1).Cost;

            // ① 대시 — 눈앞에서 쫓길 때 거리를 벌리는 유일한 수단
            if (!_hasDash && _cheese >= CostOf(Economy.TechId.Dash)) return Economy.TechId.Dash;

            // ② 요새 설치권 — 창고보다 먼저(2026-08-18 계측). 테크 v3에선 Lv1 100 + 설치마다 80.
            if (!_hasFortress && _cheese >= CostOf(Economy.TechId.Fortress)) return Economy.TechId.Fortress;

            // ③ 창고 — 비상 벽 설치비를 남기고 산다
            if (!_hasWarehouse && _cheese >= CostOf(Economy.TechId.Warehouse) + placeCost)
                return Economy.TechId.Warehouse;

            return Economy.TechId.None;
        }
```

- [ ] **Step 4: BotShopper 교체** (해당 부분)

```csharp
        /// <summary>도주 중 요새 — 설치권이 있으면 즉발. 지갑이 모자라면 서버가 거른다.</summary>
        public void BuyFortressShield()
        {
            if (m_Shop != null && m_Shop.CanUse(TechId.Fortress)) m_Shop.Use(TechId.Fortress);
        }

        /// <summary>쥐 — 살 수 있으면 산다. 창고는 산 직후 제자리에 놓는다.</summary>
        public void BuyForMouse()
        {
            if (m_Shop == null || m_Wallet == null) return;

            TechId next = MouseBotLogic.NextPurchase(
                m_Wallet.CheeseValue, m_BoughtDash, m_BoughtGun, m_BoughtWarehouse, m_BoughtFortress);
            if (next == TechId.None) return;

            m_Shop.Buy(next);

            if (next == TechId.Dash) m_BoughtDash = true;
            else if (next == TechId.Gun) m_BoughtGun = true;
            else if (next == TechId.Fortress) m_BoughtFortress = true;
            else if (next == TechId.Warehouse)
            {
                m_BoughtWarehouse = true;
                m_PendingWarehousePlace = true;   // 서버가 레벨을 올린 다음 틱에 놓는다
            }

            if (m_PendingWarehousePlace && m_Shop.CanUse(TechId.Warehouse))
            {
                m_Shop.UseAt(TechId.Warehouse, m_Body.position);
                m_PendingWarehousePlace = false;
            }
        }
```
필드 추가: `private bool m_PendingWarehousePlace;`. 파일 안의 다른 `ShopItemId` 참조(고양이 구매 `CatAttack`/`CatSpeed`)는 `TechId.CatClaw`/`TechId.CatStomp`로, `m_Shop.Buy(...)` 그대로.

- [ ] **Step 5: 봇 상점 면제** — `ShopController.CanBuyNow`의 상점 조건을 다음으로:

```csharp
            if (TechLogic.NeedsShopSpot(m_Tag.Role) && !IsBotExempt() && !Map.ShopSpot.AnyContains(transform.position))
                return TechDeny.NotInShop;
```
와 메서드:
```csharp
        // 계획 1 한정: 봇은 상점까지 가지 않는다(경로는 계획 3). 계측치에 "상점 이동 비용 제외"를 적는다.
        private bool IsBotExempt()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return GetComponent<Bots.BotAgent>() != null;
#else
            return false;
#endif
        }
```
(`Bots.BotAgent`가 플레이어 루트에 붙는 컴포넌트 이름인지 `Assets/Scripts/Bots/`에서 확인 — 다르면 그 이름으로.)

- [ ] **Step 6: 테스트 실행** → `MouseBotLogicTests` 전부 PASS. 컴파일 에러는 `ShopLogicTests`만 남는다.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Bots/MouseBotLogic.cs Assets/Scripts/Bots/BotShopper.cs Assets/Scripts/Tests/EditMode/MouseBotLogicTests.cs Assets/Scripts/Economy/ShopController.cs
git commit -m "refactor(bots): 봇 구매를 TechId로 — 요새 설치권+설치비, 창고는 산 뒤 제자리 설치, 상점 면제(계획 3까지)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: 옛 ShopInfo 제거 · ShopLogicTests 정리 · QA 훅

**Files:**
- Delete: `Assets/Resources/CSVInfo/ShopInfo.csv`(+.meta), `Assets/Scripts/Info/Generator/ConvertInfo/ShopInfo.cs`(+.meta)
- Modify: `Assets/Scripts/Info/InfoCustom.cs` (`Shop` 프로퍼티 삭제), `Assets/Scripts/Info/Generator/InfoLoader.cs` (메뉴 재실행으로 재생성)
- Modify: `Assets/Scripts/Tests/EditMode/ShopLogicTests.cs`
- Create: `Assets/Scripts/Testing/TestManager.Tech.cs`

- [ ] **Step 1: ShopInfo 삭제 후 `Tools/CTC/Convert CSV → Info` 재실행** — `InfoLoader.cs`에서 `ShopInfoList` 줄이 사라진다. `InfoCustom.cs`의 `public IReadOnlyList<ShopInfo> Shop => ShopInfoList;` 줄 삭제.

- [ ] **Step 2: ShopLogicTests에서 카탈로그 테스트 삭제·이관** — `Find_*`, `RoleCatalogIndices_*`, `IsPlaceable_OnlyWarehouse`, `Warehouse_IsMouseItemCosting150`, `Balance_CatAdvancementIsReachableWithinOneMatch`(전직 조건은 계획 3에서 300으로 바뀌므로 그때 다시 쓴다)를 지우고, 아래 둘을 `TechLogicTests`에 옮겨 넣는다:

```csharp
        [Test]
        public void Warehouse_PaysBackWithinMatch()
        {
            float payback = TechLogic.RowOf(TechId.Warehouse, 1).Cost / (float)ShopLogic.WAREHOUSE_INCOME_PER_SECOND;
            Assert.AreEqual(30f, payback, 1e-4f);
            Assert.Less(payback, CTC.Runtime.Match.MatchFlowLogic.MATCH_DURATION_SECONDS * 0.5f);
        }

        [Test]
        public void Fortress_PlaceCost_IsCheapInstantShield()
        {
            // 즉발 방패 — 쫓기는 순간 살 수 있는 값이어야 한다(2026-08-15). 설치권 100은 따로.
            Assert.AreEqual(80, CTC.Runtime.Info.InfoManager.Instance.Balance.FortressPlaceCost);
            Assert.AreEqual(100, TechLogic.RowOf(TechId.Fortress, 1).Cost);
        }
```
`ShopLogicTests`에는 `CanBuy_*`, `PounceDamage_*`, `WarehouseTick_*`, `CurrencyName_*`, `MoveSpeed_*`, `CanAddStack_*`, `WeaponStats_*`, `Balance_MaxUpgradeStillTakesTwoPouncesToDropAMouse`가 남는다. 이 마지막 것의 `MAX_UPGRADE_STACKS`는 5인데 테크 v3 발톱은 3레벨이다 — 기대치를 `60 + 10 * TechLogic.MaxLevel(TechId.CatClaw)`(=90)로 고치고 `Assert.Greater(maxPounce, 80, …)`로 낮춘다.

- [ ] **Step 3: QA 훅** — 기존 `TestManager.Balance.cs`의 partial class·네임스페이스·`_clientId`로 플레이어 찾는 방식을 그대로 따른다(그 파일의 `TestGetFootstepRange(long)`를 보고 같은 헬퍼로 플레이어 오브젝트를 얻는다).

```csharp
// Assets/Scripts/Testing/TestManager.Tech.cs
using CTC.Runtime.Economy;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    /// <summary>테크 v3 QA 훅 — wesqa가 리플렉션으로 부른다. 타입을 쪼개지 않는다(CLAUDE.md).</summary>
    public partial class TestManager
    {
        // 기존 partial들과 같은 방식 — NetUtil.FindPlayerObject(clientId)는 NetworkObject를 준다
        private static GameObject FindPlayer(long _clientId)
        {
            Unity.Netcode.NetworkObject no = Core.NetUtil.FindPlayerObject((ulong)_clientId);
            return no != null ? no.gameObject : null;
        }

        /// <summary>특정 클라이언트의 지갑 — 기존 TestGetCheese()는 로컬 전용이라 따로 둔다.</summary>
        public int TestGetCheeseOf(long _clientId)
        {
            CheeseWallet w = FindPlayer(_clientId) != null ? FindPlayer(_clientId).GetComponent<CheeseWallet>() : null;
            return w != null ? w.CheeseValue : -1;
        }

        /// <summary>QA: 즉발 액티브 사용(요새·대시) — 사람의 숫자키와 같은 입구.</summary>
        public void TestUseTech(long _clientId, string _techId)
        {
            ShopController shop = FindPlayer(_clientId) != null ? FindPlayer(_clientId).GetComponent<ShopController>() : null;
            if (shop != null && System.Enum.TryParse(_techId, out TechId id)) shop.Use(id);
        }

        /// <summary>보유 레벨(0 = 미보유).</summary>
        public int TestGetTechLevel(long _clientId, string _techId)
        {
            TechSlots slots = FindPlayer(_clientId) != null ? FindPlayer(_clientId).GetComponent<TechSlots>() : null;
            if (slots == null || !System.Enum.TryParse(_techId, out TechId id)) return -1;
            return slots.LevelOf(id);
        }

        public int TestGetTechSlotCount(long _clientId)
        {
            TechSlots slots = FindPlayer(_clientId) != null ? FindPlayer(_clientId).GetComponent<TechSlots>() : null;
            return slots != null ? slots.Count : -1;
        }

        public bool TestIsInShop(long _clientId)
        {
            GameObject p = FindPlayer(_clientId);
            return p != null && Map.ShopSpot.AnyContains(p.transform.position);
        }

        public int TestGetShopSpotCount()
        {
            return Object.FindObjectsByType<Map.ShopSpot>(FindObjectsSortMode.None).Length;
        }

        /// <summary>QA: 상점 안에서 사는 것과 같은 입구. 판정은 서버가 한다.</summary>
        public void TestBuyTech(long _clientId, string _techId)
        {
            ShopController shop = FindPlayer(_clientId) != null ? FindPlayer(_clientId).GetComponent<ShopController>() : null;
            if (shop != null && System.Enum.TryParse(_techId, out TechId id)) shop.Buy(id);
        }

        /// <summary>QA: 플레이어를 가장 가까운 상점 자리로 옮긴다(시간 결핍을 건너뛰고 구매만 볼 때).</summary>
        public bool TestWarpToNearestShop(long _clientId)
        {
            GameObject p = FindPlayer(_clientId);
            if (p == null) return false;
            Map.ShopSpot best = null; float bestSqr = float.MaxValue;
            foreach (Map.ShopSpot s in Object.FindObjectsByType<Map.ShopSpot>(FindObjectsSortMode.None))
            {
                float d = (s.transform.position - p.transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = s; }
            }
            if (best == null) return false;
            var push = p.GetComponent<Player.BodyPush>();
            if (push == null) return false;
            push.ServerPush(best.transform.position - p.transform.position);   // CC.Move 경유(직접 transform 세팅 금지)
            return true;
        }
    }
}
```
`TestManager`의 네임스페이스·클래스 선언은 `TestManager.Balance.cs` 첫 줄들과 똑같이 맞춘다(다르면 partial이 안 붙는다).

- [ ] **Step 4: 전체 컴파일 + EditMode** — `u_editor_asset(action: refresh)` → `Tools/CTC/Run EditMode Tests`. Expected: 전부 PASS(기존 310 − 삭제분 + 신규 ≈ 330).

- [ ] **Step 5: Commit**

```bash
git add -A Assets/Resources/CSVInfo Assets/Scripts/Info Assets/Scripts/Tests/EditMode/ShopLogicTests.cs Assets/Scripts/Tests/EditMode/TechLogicTests.cs Assets/Scripts/Testing
git commit -m "refactor(tech): ShopInfo 제거 — TechInfo가 단일 원본, ShopLogic은 밸런스 접근자만; QA 훅 TestManager.Tech

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: 플레이모드 QA (wesqa) + 코드 다이어그램

**Files:**
- Create: `tools/wesqa/qa_v46.py`
- Modify: `docs/design-vault/코드 다이어그램.md` (상점 클래스 블록 662~700 · 핫바 780~825 · CSV 표 1242)

- [ ] **Step 1: MPPM 3인(호스트=쥐) 시나리오 작성** — 기존 `tools/wesqa/qa_v37.py`(호스트=쥐)의 접속·역할 지정·판 시작 뼈대를 복사해 항목만 바꾼다. 스팀이 켜져 있으면 클론이 못 붙는다(CLAUDE.md).

```python
# tools/wesqa/qa_v46.py — 테크 뼈대 + 상점 자리 (계획 1)
# 항목: ①상점 자리 개수 ②밖에서 구매 거절 ③상점 안 구매 성공(Gun Lv1) ④업그레이드(Gun Lv2) ⑤칸 상한 ⑥요새 설치권+설치비 ⑦고양이는 밖에서 구매
from wesqa import Session, expect   # qa_v37이 쓰는 것과 같은 임포트

def run(host, mouse, cat):
    host.start_match(roles={host: "Mouse", mouse: "Mouse", cat: "Cat"})
    m = host.client_id_of(mouse)          # MPPM 인스턴스 ≠ clientId — 매 판 다시 읽는다
    c = host.client_id_of(cat)

    n = host.call("TestGetShopSpotCount")
    expect(n >= 1, f"① 상점 자리 {n}곳")

    host.call("TestBuyTech", m, "Gun")
    expect(host.call("TestGetTechLevel", m, "Gun") == 0, "② 밖에서 산 것은 거절")

    expect(host.call("TestWarpToNearestShop", m), "상점으로 이동")
    expect(host.call("TestIsInShop", m), "③-a 상점 안")
    host.call("TestGrantCheeseAll"); host.call("TestGrantCheeseAll")   # 전 지갑 +2000 (기존 훅)
    host.call("TestBuyTech", m, "Gun")
    expect(host.call("TestGetTechLevel", m, "Gun") == 1, "③-b Gun Lv1")
    host.call("TestBuyTech", m, "Gun")
    expect(host.call("TestGetTechLevel", m, "Gun") == 2, "④ Gun Lv2 (같은 칸)")
    expect(host.call("TestGetTechSlotCount", m) == 1, "④ 칸은 1")

    for t in ("Pepper", "Fortress", "Warehouse", "Dash"):
        host.call("TestBuyTech", m, t)
    expect(host.call("TestGetTechSlotCount", m) == 5, "⑤ 칸 5")
    # 6번째 테크는 계획 2 전엔 CSV에 없다 — 상한은 EditMode(TechLogicTests)가 덮는다

    before = host.call("TestGetCheeseOf", m)
    host.call("TestUseTech", m, "Fortress")
    expect(host.call("TestGetCheeseOf", m) == before - 80, "⑥ 요새 설치비 80")

    host.call("TestBuyTech", c, "CatClaw")   # 고양이 지갑도 위에서 +2000 받았다
    expect(host.call("TestGetTechLevel", c, "CatClaw") == 1, "⑦ 고양이는 상점 없이 구매")
```
훅 5개(`TestGetTechLevel`·`TestGetTechSlotCount`·`TestIsInShop`·`TestGetShopSpotCount`·`TestBuyTech`)와 `TestGetCheeseOf`·`TestUseTech`·`TestWarpToNearestShop`는 Task 8에서 만들었다.

- [ ] **Step 2: 실행** — 스팀 종료 → 에디터 + MPPM 클론 2 → `python tools/wesqa/qa_v46.py`. Expected: 7항목 PASS. 실패하면 `Player.log`·`u_console`로 원인을 잡고 고친 뒤 재실행(자동 수정 규칙).

- [ ] **Step 3: 코드 다이어그램 갱신** — 상점 클래스 블록(662행 근처)의 `ShopController`를 아래로, 핫바 블록(780행 근처)에 `TechSlots`·`ShopSpot` 관계 추가, CSV 표(1242행)의 `ShopInfo` 행을 `TechInfo`로:

```mermaid
    class TechSlots {
        NetworkList~TechEntry~ 보유 테크·레벨
        +Owned · LevelOf(id) · Changed
        +ServerApplyNextLevel(id)
    }
    class TechLogic {
        <<static · 순수>>
        Catalog · Candidates · NextRow · CanBuy · IsActive
    }
    class ShopController {
        +Buy(id) 산다·키운다
        +Use(id) 요새·대시 / UseAt(id,pos) 창고
        +CanBuyNow(id) · CanUse(id)
    }
    class ShopSpot {
        상점 자리 · 반경 3m
        +AnyContains(pos)$
    }
    ShopController --> TechLogic
    ShopController --> TechSlots : 레벨 적용
    ShopController --> ShopSpot : 쥐는 안에서만
    ShopController --> CheeseWallet : TrySpend
    ShopHotbar ..> TechSlots : Changed 구독
    ShopHotbar ..> ShopSpot : 모드(안/밖)
    ShopSpotSpawner --> ShopSpot : 판 시작 스폰
```
CSV 표 행: `| \`TechInfo\` | 테크 × 레벨 12행(계획 1) — 역할·축·값·라벨·힌트 | 런타임 |`.

- [ ] **Step 4: Commit**

```bash
git add tools/wesqa/qa_v46.py "docs/design-vault/코드 다이어그램.md" Assets/Scripts/Testing
git commit -m "test(qa): qa_v46 테크 뼈대·상점 자리 7항목 + 코드 다이어그램 갱신

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## 자가 검토

| 기획 §2 규칙 | 태스크 |
|---|---|
| 1 칸 5 · 차면 못 삼 · 업그레이드는 계속 | T2 `CanBuy(SlotsFull)` · T6 표시 |
| 2 Lv1→2→3 순서 | T2 `NextRow` · T3 `ServerApplyNextLevel` |
| 3 쥐는 상점 안에서만 · 안에서 숫자키 = 산다/키운다 | T4 `ShopSpot` · T5 `CanBuyNow(NotInShop)` · T6 |
| 4 밖 숫자키 = 쓴다 | T5 `Use/UseAt` · T6 `TryUse` |
| 5 고양이는 상점 안 감 | T2 `NeedsShopSpot` |
| §3 구매음 12m | T5 `BuySoundRpc` |
| §6 상점 4곳·최소 25m·반경 3m·못 부순다 | T1 컬럼 · T4 |
| §6-1 빈 칸 후보 넘김 = 같은 키 반복 · Enter | T6 |
| 규칙 8 다이어그램 | T9 |

**미포함(다음 계획):** 신규 효과 10종·요새 Lv2/3·총 Lv3·후추 Lv2/3(계획 2) · 상점 위치 무작위·품절·직업권 셔플·전직 300·봇 상점 이동·고양이 테크 전원 토스트(계획 3).

**타입 일관성:** `TechEntry(Id, Level)` · `TechRow.IsValid` · `TechDeny` 7종 · `ShopController.Buy/Use/UseAt/CanBuyNow/CanUse` · `TechSlots.Owned/LevelOf/Count/Changed/ServerApplyNextLevel` · `ShopSpot.AnyContains` · `ShopSpotLogic.Pick/Contains` — 태스크 간 동일.
