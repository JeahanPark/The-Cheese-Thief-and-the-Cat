# 빌드 심화 Phase 0~2 — 노선 뼈대 + 후추 노선 완전체 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 쥐에게 "노선"이 생긴다 — 후추 탄을 사는 순간 후추 노선이 확정되고, 핫바가 후추 전용 카탈로그(기관총②/산탄③ 택1)로 갈아입는다. 첫 배타적 선택이 게임에 들어간다.

**Architecture:** 카탈로그는 지금처럼 ShopInfo.csv가 원본 — `Branch` 컬럼 하나로 노선 소속을 표기하고, 노선 개시템(①)은 "그 노선의 첫 CSV 행"이라는 규약으로 정한다(컬럼 추가 없이). 확정 상태는 쥐 프리팹의 `MouseBranch`(NetworkVariable)가 든다. 필터 규칙은 순수 로직(`RoleCatalogIndices` 확장)으로 EditMode가 덮는다.

**Tech Stack:** 기존 상점 파이프라인(CSV→CATALOG→핫바) · NGO NetworkVariable · R3 구독.

**근거:** [기획안 - 빌드 심화 v1](../../design-vault/기획안%20-%20빌드%20심화%20v1.md) P7(기관총=후추 전용·새총=공용) · [전개 계획](2026-08-31-build-depth-rollout.md) Phase 0~2.

**핵심 산수 (6칸 안 넘침):**
- 확정 전: 공용 4(새총·대시·요새·창고) + 노선① 1(후추 탄) = **5칸** (꿀①·밀가루①이 오면 7 → 그때 창고 이관 P8로 6)
- 확정 후(후추): 공용 4 + 기관총② + 산탄③ = **6칸**. ①(후추 탄)은 확정 후 핫바에서 사라진다(1회성 개시템 — 자리를 ②③에 내준다)
- ②③ 택1은 티어 규칙으로 성립: 둘 다 `Tier < 2`가 조건, 기관총=티어2·산탄=티어3 → 하나 사면 다른 쪽 영구 잠금. ChoiceGroup 컬럼은 꿀 노선까지 보류(YAGNI)

**검토 ①(핫바 동적 교체) 해소가 이 계획 자체다.** 검토 ③(트리거 볼륨)·④(부채꼴 광역)는 꿀·발톱 계획으로 이월.

---

### Task 1: BranchId + 카탈로그 필터 (TDD)

**Files:**
- Create: `Assets/Scripts/Economy/BranchId.cs`
- Modify: `Assets/Scripts/Economy/ShopLogic.cs` (ShopItem에 Branch 추가 · RoleCatalogIndices 확장)
- Test: `Assets/Scripts/Tests/EditMode/ShopBranchLogicTests.cs`

- [ ] BranchId enum: `None=0, Pepper=1, Honey=2, Flour=3` (Economy 네임스페이스 — 생성 CSV 파일이 이미 using)
- [ ] 실패 테스트 먼저:

```csharp
[Test] public void PreBranch_Mouse_Is_Common4_Plus_Openers()
{
    var idx = ShopLogic.RoleCatalogIndices(RoleId.Mouse, BranchId.None);
    var ids = idx.ConvertAll(_i => ShopLogic.CATALOG[_i].Id);
    CollectionAssert.AreEqual(new[] { ShopItemId.Slingshot, ShopItemId.Dash,
        ShopItemId.Fortress, ShopItemId.Warehouse, ShopItemId.PepperShot }, ids);
}
[Test] public void PepperBranch_Swaps_Opener_For_Tier_Choice()
{
    var idx = ShopLogic.RoleCatalogIndices(RoleId.Mouse, BranchId.Pepper);
    var ids = idx.ConvertAll(_i => ShopLogic.CATALOG[_i].Id);
    CollectionAssert.AreEqual(new[] { ShopItemId.Slingshot, ShopItemId.Dash,
        ShopItemId.Fortress, ShopItemId.Warehouse, ShopItemId.MachineGun, ShopItemId.SaltShot }, ids);
}
[Test] public void Cat_Catalog_Ignores_Branch() { /* Cat 2칸 불변 */ }
```

- [ ] 구현 — 필터 규칙(순수):

```csharp
// 확정 전: 공용 + 각 노선의 개시템(그 노선의 첫 CSV 행).
// 확정 후: 공용 + 내 노선의 비개시템. 개시템은 1회성이라 자리를 내준다.
public static List<int> RoleCatalogIndices(RoleId _role, BranchId _branch)
{
    var list = new List<int>();
    var seenBranch = new HashSet<BranchId>();
    for (int i = 0; i < CATALOG.Length && list.Count < HOTBAR_SLOTS; i++)
    {
        if (CATALOG[i].Role != _role) continue;
        BranchId b = CATALOG[i].Branch;
        bool opener = b != BranchId.None && seenBranch.Add(b);
        bool include = b == BranchId.None
            || (_branch == BranchId.None ? opener : b == _branch && !opener);
        if (include) list.Add(i);
    }
    return list;
}
// 기존 RoleCatalogIndices(RoleId)는 (role, None) 위임으로 유지 — 호출부 호환.
```

기존 `RoleCatalogIndices_MouseSixInCatalogOrder` 테스트는 새 현실(확정 전 5칸)로 재작성.

### Task 2: CSV + 컨버터

**Files:** `ShopInfo.csv` · `WeaponInfo.csv` · (메뉴 재생성) `Info/Generator/ConvertInfo/*.cs`

- [ ] ShopInfo: `Branch.BranchId` 컬럼. 행 순서가 규약이다(노선 첫 행 = 개시템):

```
ItemId.ShopItemId,Role.RoleId,Cost.int,Branch.BranchId
Slingshot,Mouse,150,None
Dash,Mouse,100,None
Fortress,Mouse,80,None
Warehouse,Mouse,150,None
PepperShot,Mouse,200,Pepper      ← 후추 개시(①)
MachineGun,Mouse,600,Pepper      ← ② (공용에서 이관, P7)
SaltShot,Mouse,520,Pepper        ← ③ 신규
CatAttack,Cat,120,None
CatSpeed,Cat,90,None
```

- [ ] WeaponInfo: `PelletCount.int`·`SpreadHalfAngleDeg.float` 컬럼(기존 행 1·0) + Tier 3 산탄 행:
  `3,14,1.2,6,굵은 소금 산탄,3,12` (발당 14 — 3발 전탄 명중 42, 기획 40±)
- [ ] `Tools/CTC/Convert CSV → Info` — 생성 파일에 Branch·Pellet 필드 확인. (컴파일 선행이 필요하면 생성 파일 선패치 후 메뉴로 정식 재생성 — 후추 탄 때 검증된 순서)
- [ ] `ShopItemId.SaltShot = 10` · `ShopItem`에 `Branch` 필드 · `CATALOG` 채우기

### Task 3: MouseBranch + ShopController

**Files:** Create `Assets/Scripts/Economy/MouseBranch.cs` · Modify `ShopController.cs` · `PlayerPrefabFactory.cs`

- [ ] MouseBranch(쥐 프리팹, PepperRounds 패턴): `NetworkVariable<int>` + `Branch`/`BranchChanged`/`ServerSetBranch(단방향: None→만 허용)`
- [ ] ShopController.Apply(PepperShot): 기존 PepperRounds.ServerSetOwned() + `GetComponent<MouseBranch>().ServerSetBranch(BranchId.Pepper)`
- [ ] CanApply 개정:
  - `MachineGun`: `branch.Branch == BranchId.Pepper && gun.Tier < 2` (노선 게이트 추가)
  - `SaltShot`: 동일 조건 · Apply → `ServerSetTier(3)`
  - `PepperShot`: `!pepper.Owned` (기존 유지 — 구매가 곧 확정)
- [ ] PlayerPrefabFactory 쥐 분기: `AddComponent<MouseBranch>()`

### Task 4: 산탄 발사 (MousePopgun)

**Files:** `ShopLogic.cs`(WeaponStats 확장) · `MousePopgun.cs`

- [ ] WeaponStats에 `PelletCount`·`SpreadHalfAngleDeg` 추가, `WeaponStatsForTier` 매핑
- [ ] FireRpc: 단발 경로를 `FireOne(원점, 방향, stats)`로 추출 → 펠릿 수만큼 부채꼴 호출:

```csharp
int pellets = Mathf.Max(1, stats.PelletCount);
for (int p = 0; p < pellets; p++)
{
    float t = pellets == 1 ? 0f : (p / (float)(pellets - 1)) * 2f - 1f;   // -1..+1
    Vector3 dir = Quaternion.Euler(0f, t * stats.SpreadHalfAngleDeg, 0f) * _direction;
    FireOne(origin, dir, stats);   // 레이캐스트+데미지+도트+트레이서(펠릿당)
}
```

  쿨다운·발사음·애니는 1회(펠릿 밖). 후추 도트는 펠릿마다 — 전탄 명중 시 3스택 즉시(후추+산탄 시너지, 기획 의도).

### Task 5: 핫바 동적 교체 (검토 ① 해소)

**Files:** `ShopHotbar.cs`

- [ ] ITEM_LABELS/HINTS: CSV 새 순서로 재배열 + `"굵은 소금 산탄" / "부채꼴 3발 — 붙었을 때"` 추가
- [ ] 노선 구독: 로컬 쥐 바인드 시 `MouseBranch.BranchChanged.Subscribe(_ => { m_Indices = ShopLogic.RoleCatalogIndices(m_Role, branch); m_SlotsDirty = true; }).AddTo(this)` — 확정 순간 카탈로그가 즉시 갈린다
- [ ] 확정 토스트: "후추 노선 확정 — 기관총/산탄 중 하나만 고를 수 있다"

### Task 6: QA 훅 + 실검증

- [ ] TestManager: `TestBuySaltShot` · `TestGetBranch`(문자열) · `TestGetWeaponTier`
- [ ] EditMode 전체(388+/0)
- [ ] 씬 재생성(`Setup Networking Scene` — MouseBranch 프리팹 굽기) 후 플레이 검증(솔로, wesqa):
  1. 확정 전 핫바 5칸(산탄·기관총 없음) — `TestGetAbilityBar`류로 텍스트 확인
  2. `TestBuyPepperShot` → `TestGetBranch`=Pepper → 핫바 6칸(기관총·산탄 등장, 후추탄 소멸)
  3. `TestBuyMachineGun` 성공 → `TestBuySaltShot` 거부(택1 잠금) — 지갑 불변 확인
  4. (재시작) 산탄 먼저 → 티어 3 → `TestShootAtCat` 트레이서 3줄·강아지 도트 3스택 가속 확인
- [ ] wesqa v36 1회(MonoBehaviour 재배치 필수 규정)

### Task 7: 문서 + 커밋

- [ ] `코드 다이어그램.md` — MouseBranch·노선 필터 규약(개시템=첫 행) 반영
- [ ] `기획안 - 빌드 심화 v1.md` 상태 갱신: P7 채택·산탄 구현·검토① 해소
- [ ] 커밋(논리 단위 2~3개) + `handoff-active.md` 갱신

---

## 주의 (이 판의 함정)

1. **CSV 컬럼 추가 → 생성 파일 선패치 → Convert 정식 재생성** 순서(후추 탄 때 실증 — 안 하면 컴파일이 막혀 메뉴를 못 돌린다)
2. MouseBranch는 NetworkBehaviour — **프리팹 굽기(씬 재생성) 전에는 플레이 검증 불가**
3. MPPM 클론이 포커스 때 자동 재기동해 파이프를 훔친다 — 검증 전 `Get-Process Unity` 확인
4. 기관총이 공용에서 빠진다 — **qa_v36의 기관총 구매 항목이 노선 미확정 상태면 실패**할 수 있다. 스위트가 깨지면 setup에 TestBuyPepperShot 선행 추가가 맞는 수정이다(스위트 오염 아님, 명세 변경)
