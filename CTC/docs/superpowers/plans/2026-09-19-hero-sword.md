# 용사 쥐 「칼과 무적」 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 용사 표를 밟은 쥐가 총 대신 칼을 들고(연출 1.5초), 3초마다 한 번 휘둘러 0.8초 스턴을 걸며, Space로 3초 무적을 쓴다.

**Architecture:** 공격 버튼은 그대로 하나다 — `MousePopgun.FireRpc`가 직업에 따라 총/몽둥이(깡패)/칼(용사)로 갈린다. 스턴은 새 공통 컴포넌트(`Stun`) 하나가 복제값 `StunUntil`을 들고, 기존 입력 가드들이 그 값을 본다. 겉모습·변신은 깡패(`GangLook`·`GangTransformLogic`)와 같은 형태로 만든다.

**Tech Stack:** Unity 6000.5.2f1 · NGO · UniTask · R3 · Blender 5.2.1(자작 클립) · EditMode(NUnit) + wesqa(Python)

**기획서:** `docs/superpowers/specs/2026-09-19-hero-sword-design.md`
**선행:** 상점 팝업 계획(`2026-09-19-shop-popup.md`)을 먼저 끝낸다 — 둘 다 `ShopController`·`TechLogic`을 건드린다.

---

### Task 1: CSV 수치 7개

**Files:**
- Modify: `Assets/Resources/CSVInfo/BalanceInfo.csv`

- [ ] **Step 1: 헤더와 값에 컬럼 7개를 더한다**

헤더 끝에 이어 붙인다(기존 컬럼 순서는 건드리지 않는다):

```
HeroSwordDamage.int,HeroSwordRange.float,HeroSwordArcDegrees.float,HeroSwordCooldown.float,StunSeconds.float,HeroInvincibleSeconds.float,HeroInvincibleCooldown.float
```

값 줄 끝에: `20,1.8,90,3,0.8,3,10`

- [ ] **Step 2: 생성기를 돌린다**

`u_editor_menu`로 `Tools/CTC/Convert CSV → Info` 실행 →
`Assets/Scripts/Info/Generator/ConvertInfo/BalanceInfo.cs`에 필드 7개가 생겼는지 `grep HeroSword` 로 확인.

- [ ] **Step 3: 커밋**

```bash
git add CTC/Assets/Resources/CSVInfo/BalanceInfo.csv CTC/Assets/Scripts/Info/Generator/ConvertInfo/BalanceInfo.cs
git commit -m "data(hero): 칼·스턴·무적 수치 7개"
```

---

### Task 2: 스턴 판정 (순수 함수)

**Files:**
- Create: `Assets/Scripts/Combat/StunLogic.cs`
- Test: `Assets/Scripts/Tests/EditMode/StunLogicTests.cs`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using CTC.Runtime.Combat;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>스턴(2026-09-19) — 겹침은 더하지 않고 갱신한다.</summary>
    public class StunLogicTests
    {
        [NUnit.Framework.Property("CaseId", "B-28")]
        [Test]
        public void Stunned_UntilTheStampPasses()
        {
            Assert.IsTrue(StunLogic.Stunned(_until: 10.5f, _now: 10.0f));
            Assert.IsFalse(StunLogic.Stunned(10.5f, 10.5f), "끝나는 순간은 풀린 것이다");
            Assert.IsFalse(StunLogic.Stunned(0f, 0f), "한 번도 안 맞았으면 0이다");
        }

        [NUnit.Framework.Property("CaseId", "B-28")]
        [Test]
        public void NextUntil_RefreshesNeverAdds()
        {
            // 남은 0.5초에 0.8초를 또 맞으면 1.3이 아니라 0.8이다 — 연쇄 스턴을 막는다.
            Assert.AreEqual(10.8f, StunLogic.NextUntil(_current: 10.5f, _now: 10.0f, _seconds: 0.8f), 1e-4f);
            // 남은 시간이 더 길면 줄이지 않는다.
            Assert.AreEqual(11.5f, StunLogic.NextUntil(11.5f, 10.0f, 0.8f), 1e-4f);
        }
    }
}
```

- [ ] **Step 2: 실패를 본다** — `Tools/CTC/Run EditMode Tests`, 컴파일 에러(`StunLogic` 없음).

- [ ] **Step 3: 구현한다**

```csharp
namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 스턴 판정 (2026-09-19, 용사 칼). **더하지 않고 갱신한다** —
    /// 여러 명이 동시에 때리면 스턴이 쌓여 고양이가 영영 못 움직인다.
    /// </summary>
    public static class StunLogic
    {
        /// <summary>지금 굳어 있는가. <paramref name="_until"/>은 서버 시간 도장이다.</summary>
        public static bool Stunned(float _until, float _now) => _now < _until;

        /// <summary>새로 맞았을 때의 도장 — 지금보다 뒤로만 민다.</summary>
        public static float NextUntil(float _current, float _now, float _seconds)
        {
            float fresh = _now + _seconds;
            return fresh > _current ? fresh : _current;
        }
    }
}
```

- [ ] **Step 4: 통과를 본다** — Passed 수가 2 늘고 Failed 0.

- [ ] **Step 5: 커밋**

```bash
git add CTC/Assets/Scripts/Combat/StunLogic.cs CTC/Assets/Scripts/Tests/EditMode/StunLogicTests.cs
git commit -m "feat(stun): 스턴 판정 — 겹치면 갱신, 쌓이지 않는다"
```

---

### Task 3: `Stun` 컴포넌트와 입력 가드

**Files:**
- Create: `Assets/Scripts/Combat/Stun.cs`
- Modify: `Assets/Scripts/Player/PlayerMovement.cs` · `Combat/MousePopgun.cs` · `Combat/CatPounce.cs` · `Player/MouseDash.cs` · `Economy/ShopController.cs` · `Match/GangRecruit.cs`

- [ ] **Step 1: 컴포넌트를 쓴다**

```csharp
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 굳어 있는 상태 (2026-09-19). 서버가 도장을 찍고 전 클라가 같은 값을 본다 —
    /// 입력 가드는 **소유자 쪽에서** 이 값을 보고 끊고, 서버 Rpc 입구도 같은 값을 본다
    /// (`GangTransformLock`과 같은 규약 — 클라가 속여도 서버가 다시 본다).
    /// </summary>
    public class Stun : NetworkBehaviour
    {
        private readonly NetworkVariable<float> m_Until = new NetworkVariable<float>();

        /// <summary>지금 굳어 있는가.</summary>
        public bool Active => StunLogic.Stunned(m_Until.Value, (float)NetworkManager.ServerTime.Time);

        /// <summary>서버 전용 — 스턴을 건다(겹치면 갱신).</summary>
        public void ServerApply(float _seconds)
        {
            if (!IsServer) return;
            float now = (float)NetworkManager.ServerTime.Time;
            m_Until.Value = StunLogic.NextUntil(m_Until.Value, now, _seconds);
        }

        /// <summary>그 몸이 굳어 있는가 — 컴포넌트가 없으면 false.</summary>
        public static bool Locked(Component _body)
        {
            if (_body == null) return false;
            var stun = _body.GetComponent<Stun>();
            return stun != null && stun.Active;
        }
    }
}
```

- [ ] **Step 2: 가드를 여섯 군데에 넣는다**

각 파일의 입력·Rpc 입구에서 `GangTransformLock.Locked(this)`를 검사하는 **바로 옆**에 한 줄을 더한다:

```csharp
            if (Combat.Stun.Locked(this)) return;   // 스턴 — 굳어 있는 동안 아무것도 못 한다
```

`PlayerMovement`는 이동 입력을 0으로 만들고(`Move`를 안 부른다), 나머지는 조기 반환한다.

- [ ] **Step 3: 프리팹에 붙인다**

`Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs`에서 `Health`를 붙이는 줄 옆에
`root.AddComponent<CTC.Runtime.Combat.Stun>();`을 더하고, 강아지 프리팹(`CreateDogPrefab`)에도 같이 붙인다.

- [ ] **Step 4: 씬·프리팹을 다시 굽는다** — `Tools/CTC/Setup Networking Scene`, 에러 0.

- [ ] **Step 5: 커밋**

```bash
git add CTC/Assets/Scripts/Combat/Stun.cs CTC/Assets/Scripts/Player CTC/Assets/Scripts/Combat CTC/Assets/Scripts/Economy/ShopController.cs CTC/Assets/Scripts/Match/GangRecruit.cs CTC/Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs CTC/Assets/Prefabs
git commit -m "feat(stun): 굳는 상태를 몸에 붙이고 입력·Rpc 여섯 군데를 막는다"
```

---

### Task 4: 칼 판정과 공격 갈래

**Files:**
- Create: `Assets/Scripts/Combat/HeroSwordLogic.cs`
- Modify: `Assets/Scripts/Combat/MousePopgun.cs`
- Test: `Assets/Scripts/Tests/EditMode/HeroSwordLogicTests.cs`

- [ ] **Step 1: 테스트를 쓴다**

```csharp
using CTC.Runtime.Combat;
using CTC.Runtime.Match;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>용사 칼(2026-09-19) — 언제 칼이 되고, 누구를 벨 수 있는가.</summary>
    public class HeroSwordLogicTests
    {
        [NUnit.Framework.Property("CaseId", "B-29")]
        [Test]
        public void SwordActive_OnlyForHero_RegardlessOfPhase()
        {
            Assert.IsTrue(HeroSwordLogic.Active(MouseJobId.Hero, MatchPhase.Hunt), "표를 밟는 순간부터다");
            Assert.IsTrue(HeroSwordLogic.Active(MouseJobId.Hero, MatchPhase.Gang));
            Assert.IsFalse(HeroSwordLogic.Active(MouseJobId.Gang, MatchPhase.Gang), "그쪽은 몽둥이다");
            Assert.IsFalse(HeroSwordLogic.Active(MouseJobId.None, MatchPhase.Hunt));
        }

        [NUnit.Framework.Property("CaseId", "B-29")]
        [Test]
        public void CanCut_EveryoneButHimself()
        {
            Assert.IsTrue(HeroSwordLogic.CanCut(MouseJobId.Hero, MouseJobId.Gang), "2단계의 깡패도 벤다");
            Assert.IsTrue(HeroSwordLogic.CanCut(MouseJobId.Hero, MouseJobId.None));
            Assert.IsFalse(HeroSwordLogic.CanCut(MouseJobId.Hero, MouseJobId.Hero), "용사는 하나뿐이지만 규칙으로 못 박는다");
        }

        [NUnit.Framework.Property("CaseId", "B-29")]
        [Test]
        public void HeroCannotBuyGunOrDash()
        {
            Assert.IsTrue(HeroSwordLogic.Denied(CTC.Runtime.Economy.TechId.Gun));
            Assert.IsTrue(HeroSwordLogic.Denied(CTC.Runtime.Economy.TechId.Pepper));
            Assert.IsTrue(HeroSwordLogic.Denied(CTC.Runtime.Economy.TechId.Dash), "Space가 무적으로 찼다");
            Assert.IsFalse(HeroSwordLogic.Denied(CTC.Runtime.Economy.TechId.Fortress));
        }
    }
}
```

- [ ] **Step 2: 실패를 본다.**

- [ ] **Step 3: 구현한다**

```csharp
using CTC.Runtime.Economy;
using CTC.Runtime.Match;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 용사 칼 (2026-09-19). 깡패 몽둥이와 **같은 구조**지만 조건이 다르다 —
    /// 깡패는 2단계에서만 몽둥이를 들고, 용사는 **표를 밟는 순간부터** 칼을 든다(정체를 숨기지 않는다).
    /// 부채꼴 판정은 `GangLookLogic.InArc`를 그대로 쓴다 — 같은 규칙을 두 벌 두지 않는다.
    /// </summary>
    public static class HeroSwordLogic
    {
        public static bool Active(MouseJobId _job, MatchPhase _phase) => _job == MouseJobId.Hero;

        /// <summary>벨 수 있는 상대인가. 자기 자신(같은 직업)만 못 벤다.</summary>
        public static bool CanCut(MouseJobId _me, MouseJobId _target) => _me != _target;

        /// <summary>용사가 못 사는 테크 — 총알(총·후추)과 대시(Space가 무적이다).</summary>
        public static bool Denied(TechId _id) => TechLogic.IsGunTech(_id) || _id == TechId.Dash;
    }
}
```

- [ ] **Step 4: `MousePopgun`에 갈래를 더한다**

깡패 몽둥이 갈래(`ClubActive`) 바로 옆에 칼 갈래를 만든다 — 사거리·각도·피해·쿨다운은 CSV에서 읽고,
맞은 대상에 `Stun.ServerApply(Balance.StunSeconds)`를 건다. 발사 애니메이션은 `AnimationStateLogic.SwordSwing`.

- [ ] **Step 5: 구매 거절을 잇는다**

`ShopController.CanBuyNow`에서 지금 깡패를 막는 자리(`ClubActive && IsGunTech`) 옆에
`HeroSwordLogic.Denied` 갈래를 더하고, `TechLogic.DenyText`에 문구를 넣는다:
`case TechDeny.HeroNoGun: return "용사는 칼을 쓴다 — 총·대시는 못 산다";`

- [ ] **Step 6: 테스트 + 커밋**

```bash
git add CTC/Assets/Scripts/Combat CTC/Assets/Scripts/Economy CTC/Assets/Scripts/Tests/EditMode/HeroSwordLogicTests.cs
git commit -m "feat(hero): 칼 판정과 공격 갈래 — 맞으면 0.8초 굳는다"
```

---

### Task 5: 무적 (Space)

**Files:**
- Create: `Assets/Scripts/Player/HeroInvincible.cs`
- Modify: `Assets/Scripts/Combat/Health.cs` · `Assets/Scripts/Player/MouseDash.cs`
- Test: `Assets/Scripts/Tests/EditMode/HeroInvincibleLogicTests.cs`

- [ ] **Step 1: 테스트를 쓴다** — 쿨다운 계산과 「무적 중 피해 0」 규칙.

```csharp
        [NUnit.Framework.Property("CaseId", "B-30")]
        [Test]
        public void Absorb_EverythingWhileOn_NothingAfter()
        {
            Assert.AreEqual(0, HeroInvincibleLogic.AfterAbsorb(_damage: 90, _on: true));
            Assert.AreEqual(90, HeroInvincibleLogic.AfterAbsorb(90, false));
        }

        [NUnit.Framework.Property("CaseId", "B-30")]
        [Test]
        public void Ready_OnlyAfterCooldown()
        {
            Assert.IsTrue(HeroInvincibleLogic.Ready(_lastUsed: 0f, _now: 10f, _cooldown: 10f));
            Assert.IsFalse(HeroInvincibleLogic.Ready(0f, 9.9f, 10f));
        }
```

- [ ] **Step 2~4: 구현 → 통과.**

`HeroInvincible`은 `IDamageAbsorber`(치즈 방패가 쓰는 기존 인터페이스)를 구현해 `Health.TakeDamage`가
부르는 길에 그대로 올라탄다 — **새 훅을 만들지 않는다.** Space 입력은 `MouseDash`가 읽던 자리에서
직업을 보고 갈라진다(용사면 무적, 아니면 대시).

- [ ] **Step 5: 커밋**

```bash
git add CTC/Assets/Scripts/Player/HeroInvincible.cs CTC/Assets/Scripts/Combat/Health.cs CTC/Assets/Scripts/Player/MouseDash.cs CTC/Assets/Scripts/Tests/EditMode/HeroInvincibleLogicTests.cs
git commit -m "feat(hero): 무적 3초 · 쿨 10초 — Space가 용사에게는 무적이다"
```

---

### Task 6: 칼 소품과 클립 3종

**Files:**
- Create: `tools/blender/clips/sword_swing.py` · `hero_draw.py` · `stunned.py`
- Create: `Assets/Scripts/Editor/Dev/HeroPropsBaker.cs`
- Modify: `Assets/Scripts/Player/AnimationStateLogic.cs`

- [ ] **Step 1: 칼 메시를 굽는다**

`Tools/CTC/Dev/Bake Hero Props` 신규 메뉴 — `GangPropsBaker`를 본떠 손잡이+날을 코드로 만들고
오른 앞발 뼈에 붙인다. **반투명 머티리얼**로 굽는다(알파 등장).

- [ ] **Step 2: 클립 3종을 만든다**

```bash
blender -b art/blender/mouse.blend -P tools/blender/clips/sword_swing.py
blender -b art/blender/mouse.blend -P tools/blender/clips/hero_draw.py
blender -b art/blender/mouse.blend -P tools/blender/clips/stunned.py
blender -b art/blender/mouse.blend -P tools/blender/export_clips.py -- Assets/Art/Clips/CTC_Mouse_Clips.fbx
echo $?   # 0이어야 한다 — 출력이 아니라 종료 코드로 판정한다(함정 15)
```

- [ ] **Step 3: 바닥을 안 뚫는지 잰다**

```bash
python tools/blender/measure_clip.py SwordSwing HeroDraw Stunned
```
최저점이 `Walk`의 −9.1mm보다 위여야 한다.

- [ ] **Step 4: 상태를 굽는다**

`AnimationStateLogic.MouseOnly`에 이름 셋을 더하고 `Tools/CTC/Dev/Bake Mouse Gun` 실행 →
`Tools/CTC/Dev/Probe Clip Parity`로 검증(보고서 첫 절이 병명이다).

- [ ] **Step 5: 커밋**

```bash
git add CTC/tools/blender CTC/Assets/Art/Clips CTC/Assets/Scripts/Player/AnimationStateLogic.cs CTC/Assets/Scripts/Editor/Dev/HeroPropsBaker.cs
git commit -m "art(hero): 칼 소품과 클립 3종 — 휘두르기·뽑기·굳음"
```

---

### Task 7: 변신 연출 1.5초

**Files:**
- Create: `Assets/Scripts/Match/HeroTransformLogic.cs` · `Assets/Scripts/Player/HeroLook.cs`
- Test: `Assets/Scripts/Tests/EditMode/HeroTransformLogicTests.cs`

- [ ] **Step 1: 시간표를 순수 함수로 쓴다** — `GangTransformLogic`과 같은 형태.

| 구간 | 값 |
|---|---|
| 0.0~0.4 | 총 알파 1→0 |
| 0.5~1.1 | 칼 알파 0→1 |
| 0.0~1.5 | 잠금(`HeroTransformLock`) |

테스트는 경계값 전수(0 · 0.39 · 0.5 · 1.1 · 1.5)로 알파와 잠금을 확인한다.

- [ ] **Step 2~4: `HeroLook` 구현 → 테스트 통과.**

`GangLook`과 같은 규약 — 복제값(직업)을 구독하고, **늦게 들어온 사람에게는 연출 없이 결과만** 준다.

- [ ] **Step 5: GIF로 눈으로 본다**

`Tools/CTC/Dev/용사 클립 GIF 내보내기`(깡패 GIF 메뉴를 본떠 추가) → 알파 시간표까지 구워 확인.

- [ ] **Step 6: 커밋**

```bash
git add CTC/Assets/Scripts/Match/HeroTransformLogic.cs CTC/Assets/Scripts/Player/HeroLook.cs CTC/Assets/Scripts/Tests/EditMode/HeroTransformLogicTests.cs CTC/Assets/Scripts/Editor
git commit -m "feat(hero): 표를 밟으면 총을 넣고 칼을 뽑는다 — 1.5초 연출·잠금"
```

---

### Task 8: 이펙트 2종

**Files:**
- Modify: `Assets/Scripts/Editor/Fx/SkillEffectBuilder.cs` · `Assets/Scripts/Fx/FxId.cs`

- [ ] **Step 1: `FxId`에 둘을 더한다** — `HeroInvincible`(흰 빛 확산) · `Stunned`(머리 위 별).
- [ ] **Step 2: `Tools/CTC/Fx/Build Skill Effects` 실행** → `Resources/Fx/FxCatalog`에 22 → 24.
- [ ] **Step 3: 붙이는 자리** — 무적은 켜질 때 `*FxRpc`에서, 스턴 별은 `Stun.ServerApply`가 아니라 **복제값이 바뀌는 자리**(전 클라가 도는 길)에서 띄운다.
- [ ] **Step 4: 느린 배속으로 확인** — `TestSetTimeScale 0.1` + `TestCaptureGameView`.
- [ ] **Step 5: 커밋**

```bash
git add CTC/Assets/Scripts/Fx CTC/Assets/Scripts/Editor/Fx CTC/Assets/Resources/Fx
git commit -m "feat(fx): 무적 빛과 스턴 별"
```

---

### Task 9: QA 스위트 `qa_v60`과 대장

**Files:**
- Create: `tools/wesqa/tests/qa_v60.py`
- Modify: `Assets/Scripts/Testing/TestManager.MouseJob.cs` · `Assets/Scripts/Tests/EditMode/QaCases.cs`

- [ ] **Step 1: 훅을 더한다** — `TestGetHeroLook`(칼이 손에 있나) · `TestGetStunRemaining(clientId)` · `TestUseInvincible`.
  **먼저 `grep -rn "TestGetHeroLook" Assets/Scripts/Testing/`으로 이름 중복을 본다**(함정 17).

- [ ] **Step 2: 스위트를 쓴다** (호스트=쥐, 클론 1=고양이)

```
① 표를 밟으면 1.5초 뒤 칼이 손에 있다
② 변신 중에는 이동·공격이 막힌다
③ 칼로 때린 고양이가 0.8초 동안 좌표가 안 변한다
④ 스턴이 겹쳐도 0.8초를 넘지 않는다
⑤ 무적 중에는 HP가 안 깎인다
⑥ 무적은 10초 쿨이다
⑦ 용사는 총·대시를 못 산다
```

- [ ] **Step 3: 돌린다** — `python qa_v60.py` → `PASS 7 · FAIL 0`, rc=0.

- [ ] **Step 4: 대장에 B-28~B-30, C-36을 등록**하고 `Tools/CTC/Generate QA Case Ledger` 실행.

- [ ] **Step 5: 코드 다이어그램 갱신(규칙 8)** — `Stun`·`HeroSword`·`HeroLook`·`HeroInvincible` 추가.

- [ ] **Step 6: 커밋**

```bash
git add CTC/tools/wesqa/tests/qa_v60.py CTC/Assets/Scripts/Testing CTC/Assets/Scripts/Tests/EditMode/QaCases.cs "CTC/docs/design-vault"
git commit -m "test(qa): 용사 칼·스턴·무적 실플레이와 대장"
```

---

## 완료 기준

- 표를 밟으면 1.5초 연출 뒤 칼을 들고, 총은 못 쏘고 못 산다
- 칼이 맞으면 상대가 0.8초 굳고(겹쳐도 0.8), 머리 위에 별이 돈다
- Space로 3초 무적, 쿨 10초, 무적 중 피해 0이고 칼은 휘두를 수 있다
- EditMode Failed 0 · pytest 통과 · `qa_v60` rc=0 · 클립 3종이 바닥을 안 뚫는다
