# 장난기(Fun) 재화 구현 계획 (스펙 v3.2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 고양이 재화를 킬 보상 치즈(+50)에서 실데미지 기반 장난기(1dmg=1pt, 킬 +20)로 교체하고 HUD에 `Fun:` 표시.

**Architecture:** 순수 적립 수학은 신규 `FunLogic`(EditMode TDD). `Health.TakeDamage`가 실데미지(과잉타격 제외)를 반환하도록 확장, `CatPounce`가 반환값으로 지갑 적립. 지갑은 기존 `CheeseWallet` 범용 재사용(신규 NetworkVariable 없음 — NGO 직렬화 불변). HUD는 역할별 라벨 분기.

**Tech Stack:** Unity 6000.5.2f1 / NGO 2.12 / NUnit EditMode. 프리팹·씬은 `Tools/CTC/Setup Networking Scene`이 재생성 — 수동 편집 금지.

**참고 문서:** 스펙 §6 v3.2 (`docs/superpowers/specs/2026-07-06-cheese-thief-and-cat-design.md`)

**전제 사실 (2026-07-23 HEAD `bd76575` 기준):**
- `Health.TakeDamage`는 `void`, 호출자는 `CatPounce.cs:77`·`MousePopgun.cs:96`·`TestManager.cs:191` (전부 반환 미사용 — 시그니처 확장 안전)
- `CatPounce.m_KillReward = 50` 직렬화 필드 — 프리팹 재생성 시 코드 기본값이 다시 구워짐
- 쥐→고양이 데미지는 적립 없음(장난기는 고양이 전용) — `MousePopgun` 무변경
- 요새·미끼는 `RoleTag.Mouse`라 PounceRpc 대상에 이미 포함 — 같은 경로로 1:1 적립, `isPlayerKill` 가드로 킬 보너스 제외
- EditMode 테스트 실행: 메뉴 `Tools/CTC/Run EditMode Tests` → `%USERPROFILE%\AppData\LocalLow\DefaultCompany\CTC\TestResults.xml` (실행 전 rm, 현재 95개)

---

### Task 1: FunLogic 순수 함수 + 테스트 (TDD)

**Files:**
- Create: `Assets/Scripts/Economy/FunLogic.cs`
- Create: `Assets/Scripts/Tests/EditMode/FunLogicTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/Scripts/Tests/EditMode/FunLogicTests.cs`:

```csharp
using CTC.Runtime.Economy;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    public class FunLogicTests
    {
        [Test]
        public void AccruedFun_DamageOnly_OneToOne()
        {
            Assert.AreEqual(60, FunLogic.AccruedFun(60, false));
        }

        [Test]
        public void AccruedFun_PlayerKill_AddsBonus20()
        {
            Assert.AreEqual(60 + 20, FunLogic.AccruedFun(60, true));
        }

        [Test]
        public void AccruedFun_ZeroDamage_NoAccrual()
        {
            Assert.AreEqual(0, FunLogic.AccruedFun(0, false));
        }

        [Test]
        public void AccruedFun_NegativeDamage_ClampedToZero()
        {
            Assert.AreEqual(0, FunLogic.AccruedFun(-5, false));
        }

        [Test]
        public void AccruedFun_StructureKill_NoBonus() // 요새·미끼 파괴: killedPlayer=false로 호출됨
        {
            Assert.AreEqual(40, FunLogic.AccruedFun(40, false));
        }
    }
}
```

- [ ] **Step 2: 컴파일 실패 확인** — `u_editor_asset(refresh)` 후 `u_console(error)`에 `FunLogic` 미정의 CS 에러 확인
- [ ] **Step 3: 최소 구현**

`Assets/Scripts/Economy/FunLogic.cs`:

```csharp
namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 고양이 장난기(Fun) 적립 수학 — 스펙 v3.2. 순수 함수(EditMode TDD).
    /// 실데미지 1 = 1pt(과잉타격은 호출자가 실데미지로 이미 제외), 플레이어 킬만 보너스.
    /// </summary>
    public static class FunLogic
    {
        public const int KILL_BONUS = 20;

        public static int AccruedFun(int _dealtDamage, bool _killedPlayer)
        {
            int dealt = UnityEngine.Mathf.Max(0, _dealtDamage);
            return dealt + (_killedPlayer ? KILL_BONUS : 0);
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인** — rm TestResults.xml → 메뉴 `Tools/CTC/Run EditMode Tests` → `passed="100" failed="0"` (95+5)
- [ ] **Step 5: Commit**

```bash
# 저장소 루트(c:/GitFork/The-Cheese-Thief-and-the-Cat)에서, .meta 포함
git add CTC/Assets/Scripts/Economy/FunLogic.cs CTC/Assets/Scripts/Economy/FunLogic.cs.meta \
        CTC/Assets/Scripts/Tests/EditMode/FunLogicTests.cs CTC/Assets/Scripts/Tests/EditMode/FunLogicTests.cs.meta
git commit -m "feat: FunLogic — damage-based fun accrual math (spec v3.2)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Health.TakeDamage가 실데미지 반환

**Files:**
- Modify: `Assets/Scripts/Combat/Health.cs:39-45` (`TakeDamage`)

- [ ] **Step 1: 시그니처 변경**

기존:

```csharp
public void TakeDamage(int _damage)
{
    if (!IsServer || IsDead) return;

    m_Hp.Value = CombatLogic.ApplyDamage(m_Hp.Value, _damage);
    if (IsDead) Died?.Invoke(this);
}
```

변경:

```csharp
/// <summary>서버 전용. 실제 깎인 HP(과잉타격 제외)를 반환 — 장난기 적립용(스펙 v3.2).</summary>
public int TakeDamage(int _damage)
{
    if (!IsServer || IsDead) return 0;

    int before = m_Hp.Value;
    m_Hp.Value = CombatLogic.ApplyDamage(before, _damage);
    int dealt = before - m_Hp.Value;
    if (IsDead) Died?.Invoke(this);
    return dealt;
}
```

- [ ] **Step 2: 컴파일 확인** — refresh 후 `u_console(error)` 0건 (기존 호출자 3곳은 반환 미사용이라 무변경)
- [ ] **Step 3: Commit**

```bash
git add CTC/Assets/Scripts/Combat/Health.cs
git commit -m "feat: Health.TakeDamage returns dealt damage (overkill excluded)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: CatPounce 장난기 적립 (킬 +50 → 실데미지 + 킬 +20)

**Files:**
- Modify: `Assets/Scripts/Combat/CatPounce.cs` — `m_KillReward` 필드 삭제, PounceRpc 적립부 교체

- [ ] **Step 1: 필드 삭제**

삭제할 줄 (`CatPounce.cs:19`):

```csharp
        [SerializeField] private int m_KillReward = 50;
```

- [ ] **Step 2: PounceRpc 적립부 교체**

기존 (PounceRpc 루프 내):

```csharp
                bool isPlayerKill = target.NetworkObject.IsPlayerObject;

                target.TakeDamage(ShopLogic.PounceDamage(m_Damage, m_DamageStacks.Value));
                // 보상은 플레이어 처치만 — 요새/미끼(RoleTag.Mouse 구조물) 파괴가 돈줄이 되면 안 됨
                if (target.IsDead && myWallet != null && isPlayerKill)
                {
                    myWallet.AddCheese(m_KillReward);
                }
```

변경:

```csharp
                bool isPlayerKill = target.NetworkObject.IsPlayerObject;

                int dealt = target.TakeDamage(ShopLogic.PounceDamage(m_Damage, m_DamageStacks.Value));
                // 장난기 적립(스펙 v3.2): 실데미지 1:1 — 요새·미끼도 동일. 킬 보너스는 플레이어만
                if (myWallet != null)
                {
                    myWallet.AddCheese(FunLogic.AccruedFun(dealt, target.IsDead && isPlayerKill));
                }
```

(`CTC.Runtime.Economy`는 이미 using — `CheeseWallet` 사용 중이라 추가 using 불필요)

- [ ] **Step 3: 컴파일 확인** — refresh 후 `u_console(error)` 0건
- [ ] **Step 4: Commit**

```bash
git add CTC/Assets/Scripts/Combat/CatPounce.cs
git commit -m "feat: cat accrues Fun from dealt damage, kill bonus 50 -> 20 (spec v3.2)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: WalletHud 역할별 라벨 (Cat=Fun / Mouse=Cheese)

**Files:**
- Modify: `Assets/Scripts/Economy/WalletHud.cs`

- [ ] **Step 1: 쥐 전용 필터 → 역할별 라벨로 교체**

기존 (`WalletHud.cs:32-41`):

```csharp
            Combat.RoleTag tag = m_LocalWallet != null ? m_LocalWallet.GetComponent<Combat.RoleTag>() : null;
            show = show && tag != null && tag.Role == RoleId.Mouse;

            if (m_Text.gameObject.activeSelf != show)
            {
                m_Text.gameObject.SetActive(show);
            }
            if (!show) return;

            m_Text.text = $"Cheese: {m_LocalWallet.Cheese}";
```

변경:

```csharp
            Combat.RoleTag tag = m_LocalWallet != null ? m_LocalWallet.GetComponent<Combat.RoleTag>() : null;
            show = show && tag != null; // 관전자(로컬 플레이어 없음)만 숨김

            if (m_Text.gameObject.activeSelf != show)
            {
                m_Text.gameObject.SetActive(show);
            }
            if (!show) return;

            // 스펙 v3.2: 고양이 재화 = 장난기(Fun) — 같은 지갑, 라벨만 역할별
            string label = tag.Role == RoleId.Cat ? "Fun" : "Cheese";
            m_Text.text = $"{label}: {m_LocalWallet.Cheese}";
```

클래스 XML 주석도 갱신: "게임 시작 후 쥐에게만 표시 — 고양이는 ... 노출 안 함(기획 변경 2026-07-22...)" → "게임 시작 후 표시. 쥐 `Cheese:` / 고양이 `Fun:`(스펙 v3.2) — 관전자는 숨김."

- [ ] **Step 2: 컴파일 확인** — refresh 후 `u_console(error)` 0건
- [ ] **Step 3: Commit**

```bash
git add CTC/Assets/Scripts/Economy/WalletHud.cs
git commit -m "feat: wallet HUD shows Fun for cat, Cheese for mouse (spec v3.2)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5: 프리팹 재생성 + 전체 테스트 + 커밋

- [ ] **Step 1: 씬·프리팹 재생성** — 메뉴 `Tools/CTC/Setup Networking Scene` (PlayerCat 프리팹에서 stale `m_KillReward` 직렬화 제거)
- [ ] **Step 2: 에러 확인** — `u_console(error)` 0건
- [ ] **Step 3: 전체 EditMode** — rm TestResults.xml → 메뉴 실행 → `passed="100" failed="0"`
- [ ] **Step 4: Commit** (재생성 산출물 — `.claude/*`·`ProjectSettings/*`·`Assets/Settings/*` 제외)

```bash
git add CTC/Assets/Prefabs/PlayerCat.prefab CTC/Assets/Prefabs/PlayerMouse.prefab \
        CTC/Assets/Prefabs/CheeseFortress.prefab CTC/Assets/Prefabs/MouseDecoy.prefab \
        CTC/Assets/Scenes/Game.unity CTC/Assets/Materials/M_Cat_URP.mat CTC/Assets/Materials/M_Mouse_URP.mat
git commit -m "chore: regenerate scene/prefabs for Fun currency (drop stale m_KillReward)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 6: MPPM 2인 런타임 QA

핸드오프 표준 절차(`.claude/handoff-active.md` §검증 방법) 사용. 호스트=고양이, 클론=쥐.

- [ ] **Step 1: 진입** — `u_play` enter → wesqa `TestStartHost` (CTC/tools/wesqa) → 클론 probe `objects>0` (0이면 `Tools/CTC/Dev/MPPM Deactivate→Activate Player 2`)
- [ ] **Step 2: 역할·시작** — 호스트 `DevSelectCat`, 클론 cmd_clone.json `TestMpSelectRoleMouse`(seq+1) → `DevStartGame`
- [ ] **Step 3: 데미지 적립 검증** — 호스트 wesqa `TestTeleportLocalPlayer(_x,_z)`로 클론 쥐 옆 이동 → `TestPounce` → probe/`mppm_collect`로 **고양이 cheese(=Fun) 증가 = 실데미지값(60)** 확인, 쥐 HP 100→40
- [ ] **Step 4: 킬 보너스 검증** — `TestPounce` 재시도(쿨 2.5s 대기)로 쥐 처치 → 고양이 지갑 = 60 + 40(잔여HP만 적립) + 20(킬) = **120** 확인 (과잉타격 미적립 동시 검증)
- [ ] **Step 5: HUD 확인** — 호스트 화면 `u_screenshot` → 우상단 `Fun: 120` 표기
- [ ] **Step 6: 종료** — `u_play` exit → `netstat -ano -p UDP | Select-String ":778"` 잔존 0

QA 통과 시 완료. 실패 시 superpowers-systematic-debugging으로 원인 규명 후 해당 Task로 복귀.
