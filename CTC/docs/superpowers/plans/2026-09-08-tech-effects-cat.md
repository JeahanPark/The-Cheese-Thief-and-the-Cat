# 테크 v3 계획 2b — 고양이 테크 + 동작이 보이게 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 고양이 테크 4종(두꺼운 가죽·배고픔·광분·예민한 귀)을 작동시키고, **쓰는 순간이 화면에 보이게** 팩 클립을 원샷으로 매핑한다. 봇도 신규 테크를 산다.

**Architecture:** 고양이 테크도 쥐와 같은 길을 쓴다 — `TechInfo` P1~P3 + `TechSlots` 레벨 + `ShopController.TryApplyLevel`. 세 가지만 새 길이다: ① **두꺼운 가죽**은 이미 스폰된 몸의 MaxHp를 올려야 해서 `Health`에 상승 전용 API가 필요하다. ② **광분**은 `CatSkill`의 「액티브 지속+쿨」 패턴을 복제하되 전직과 무관한 별도 컴포넌트다. ③ **예민한 귀**는 서버가 아니라 **듣는 클라이언트**가 가청 거리를 곱한다(소리는 각 클라가 로컬 재생하므로). 동작 표현은 **새 클립 0개** — 팩에 이미 있는 미사용 클립 6종을 `PlayerAnimationDriver.PlayOneShot`로 붙인다.

**Tech Stack:** Unity 6000.5 · NGO 2.12 · R3 · UniTask · CSV 파이프라인 · NUnit EditMode · wesqa

**기준:** 기획서 `docs/superpowers/specs/2026-09-06-tech-shop-design.md` §5(고양이)·§9(제약). 계획 2a 결과(HEAD `c1be653`): `TechLogic.ParamOf`·`TechOwner`·`TechSlots.LevelOf`(무할당)·`ShopController.CanApplyLevel/TryApplyLevel`·`UseDeny`·`FunLogic.StructureFun`·`PlayerMovement.ServerAddSlowSource`. EditMode 476/476.

**규칙(반드시):** `m_`/`_` · public 필드 금지 · UPPER_SNAKE_CASE · 코루틴 금지(UniTask) · R3 · Unity 객체는 `!= null`만 · 밸런스는 CSV · 생성 파일 손대지 않음(메뉴 `Tools/CTC/Convert CSV → Info`) · 코드 고치면 `docs/design-vault/코드 다이어그램.md` 갱신 · EditMode = `Tools/CTC/Run EditMode Tests` · 에셋 바뀌면 `u_editor_asset(refresh)` · 노이즈(`Assets/Materials/M_*.mat`·`Assets/Fonts/*`·fileID만 바뀐 프리팹/씬) 스테이징 금지.

**계획 2c로 넘기는 것:** 쥐 **사격 클립 신규 제작**(팩에 총 쏘는 모션이 없다 — `Attack`은 앞발 휘두르기다). 2b는 있는 클립만 쓴다.

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Resources/CSVInfo/TechInfo.csv` (행 12 추가) | 고양이 신규 4종 × 3레벨 |
| `Assets/Scripts/Combat/Health.cs` (수정) | `ServerRaiseMaxHp` — 스폰 뒤 최대 HP 상승(늘어난 만큼 현재 HP도) |
| `Assets/Scripts/Player/CatHide.cs` (신규) | 두꺼운 가죽 — 구매 시 HP 상승 |
| `Assets/Scripts/Player/CatFrenzy.cs` · `CatFrenzyLogic.cs` (신규) | 광분 — 액티브 지속+쿨 |
| `Assets/Scripts/Combat/CatPounce.cs` (수정) | 광분 배율 · 배고픔 배율 |
| `Assets/Scripts/Economy/FunLogic.cs` (수정) | `WithHunger(fun, mul)` |
| `Assets/Scripts/Audio/ListenerRange.cs` (신규) | 예민한 귀 — 내 클라의 가청 배율 |
| `Assets/Scripts/Audio/SfxPlayer.cs` · `FootstepPlayer.cs` (수정) | 배율 적용 |
| `Assets/Scripts/Player/AnimationStateLogic.cs` (수정) | 팩 클립 상태 이름 6개 |
| `Assets/Scripts/Player/MouseDash.cs` · `Economy/ShopController.cs` · `Economy/StickyPatch.cs`(호출부) (수정) | 원샷 재생 |
| `Assets/Scripts/Economy/ShopController.cs` (수정) | 고양이 4종 Apply · `Use(CatFrenzy)` |
| `Assets/Scripts/Economy/TechLogic.cs` (수정) | `IsActive`에 `CatFrenzy` |
| `Assets/Scripts/Bots/BotShopper.cs` · `Bots/MouseBotLogic.cs` · `CatBotLogic.cs` (수정) | 봇이 신규 테크도 산다 |
| `Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs` (수정) | 고양이 컴포넌트 3종 |
| `Assets/Scripts/Testing/TestManager.Tech.cs` (수정) | QA 훅 |
| `Assets/Scripts/Tests/EditMode/*` | `CatFrenzyLogicTests`·`FunLogicTests`·`TechInfoTests`·`AnimationStateLogicTests` |
| `tools/wesqa/tests/qa_v48.py` (신규) | MPPM 3인 E2E |
| `docs/design-vault/코드 다이어그램.md` | 규칙 8 |

---

### Task 1: 고양이 12행 + Health.ServerRaiseMaxHp

**Files:** `Assets/Resources/CSVInfo/TechInfo.csv` · `Assets/Scripts/Combat/Health.cs` · `Assets/Scripts/Tests/EditMode/TechInfoTests.cs`

- [ ] **Step 1: CSV 12행 추가** (기획서 §5 값. P 의미: CatHide P1 = 최대 HP · CatHunger P1 = 장난기 배율 · CatFrenzy P1 = 덮치기 배율, P2 = 지속 초, P3 = 쿨 초 · CatEar P1 = 가청 배율)

```csv
CatHide,Cat,Defense,1,120,두꺼운 가죽,HP 1150,1150,0,0
CatHide,Cat,Defense,2,200,두꺼운 가죽,HP 1300,1300,0,0
CatHide,Cat,Defense,3,280,두꺼운 가죽,HP 1450,1450,0,0
CatHunger,Cat,Resource,1,80,배고픔,장난기 ×1.2,1.2,0,0
CatHunger,Cat,Resource,2,120,배고픔,장난기 ×1.4,1.4,0,0
CatHunger,Cat,Resource,3,160,배고픔,장난기 ×1.6,1.6,0,0
CatFrenzy,Cat,Escape,1,200,광분,10초 덮치기 ×1.5 · 쿨 40,1.5,10,40
CatFrenzy,Cat,Escape,2,300,광분,10초 덮치기 ×2 · 쿨 40,2,10,40
CatFrenzy,Cat,Escape,3,400,광분,10초 덮치기 ×2.5 · 쿨 20,2.5,10,20
CatEar,Cat,Utility,1,160,예민한 귀,소리가 1.3배 멀리 들린다,1.3,0,0
CatEar,Cat,Utility,2,260,예민한 귀,소리가 1.6배,1.6,0,0
CatEar,Cat,Utility,3,380,예민한 귀,소리가 2배 — 창고 48m,2,0,0
```
총 36 + 12 = **48행**. Hint에 쉼표 금지. `TechInfoTests`의 행 수·고양이 카탈로그 수(2 → 6) 단언을 갱신하고, `TechInfo_TableIsWellFormed`가 그대로 통과하는지 본다(레벨 연속·비용 단조 — 위 값은 전부 증가).

- [ ] **Step 2: `Tools/CTC/Convert CSV → Info` 실행** 후 refresh, 에러 0.

- [ ] **Step 3: Health에 상승 API** — 기존 `ServerSetMaxHp`(스폰 전용) 아래에:

```csharp
        /// <summary>
        /// 서버 전용 — **이미 스폰된 몸**의 최대 HP를 올린다. 늘어난 만큼 현재 HP도 함께 오른다
        /// (두꺼운 가죽을 사자마자 체력이 비어 있으면 산 값을 못 느낀다). 내리지는 않는다.
        /// </summary>
        public void ServerRaiseMaxHp(int _newMax)
        {
            if (!IsServer || _newMax <= m_MaxHp) return;
            int gain = _newMax - m_MaxHp;
            m_MaxHp = _newMax;
            m_Hp.Value = Mathf.Min(m_MaxHp, m_Hp.Value + gain);
        }
```
`m_MaxHp`가 `[SerializeField]` 평범한 필드라 **클라에는 안 간다** — HUD가 최대 HP를 그리면 어긋난다. 지금 읽는 곳이 서버뿐인지 `grep -rn "\.MaxHp" Assets/Scripts`로 확인하고, 클라가 읽는 곳이 있으면 이 태스크에서 `NetworkVariable<int>`로 바꾼다(그 편이 옳다 — 판단해서 하고 이유를 커밋에 적는다).

- [ ] **Step 4: EditMode PASS 확인 → Commit**
```bash
git add Assets/Resources/CSVInfo/TechInfo.csv Assets/Scripts/Info/Generator/ConvertInfo/TechInfo.cs Assets/Scripts/Combat/Health.cs Assets/Scripts/Tests/EditMode/TechInfoTests.cs
git commit -m "feat(tech): 고양이 테크 12행 + Health.ServerRaiseMaxHp — 스폰 뒤 최대 HP 상승

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: 두꺼운 가죽 · 배고픔

**Files:** Create `Assets/Scripts/Player/CatHide.cs` · Modify `Assets/Scripts/Economy/FunLogic.cs`, `Assets/Scripts/Combat/CatPounce.cs`, `Assets/Scripts/Economy/ShopController.cs`, `Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs`, `Assets/Scripts/Tests/EditMode/FunLogicTests.cs`

- [ ] **Step 1: 배고픔 순수 함수** — `FunLogic`에:

```csharp
        /// <summary>배고픔(고양이 자원 테크) — 적립 장난기에 배율. 0은 미보유라 1배.</summary>
        public static int WithHunger(int _fun, float _multiplier)
        {
            if (_multiplier <= 0f) return _fun;
            return UnityEngine.Mathf.RoundToInt(_fun * _multiplier);
        }
```
테스트 3건: 미보유(0) 1배 · ×1.6 반올림 · 0 장난기는 0.

- [ ] **Step 2: CatPounce 적용** — `myWallet.AddCheese(...)` 두 곳을 감싼다. 배율은 루프 밖에서 한 번 읽는다:
```csharp
            float hunger = m_TechSlots != null
                ? TechLogic.ParamOf(TechId.CatHunger, m_TechSlots.LevelOf(TechId.CatHunger), 1) : 0f;
```
(`m_TechSlots`는 `Awake` 캐시 — 파일에 이미 캐시 패턴이 있으면 그것을 따른다.) 두 호출을 `FunLogic.WithHunger(..., hunger)`로 감싼다.

- [ ] **Step 3: CatHide 컴포넌트**

```csharp
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using Unity.Netcode;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 두꺼운 가죽(고양이 방어 테크) — 살 때마다 최대 HP를 그 레벨 값으로 올린다.
    /// 레벨은 `TechSlots`가 들고 있고, 여기는 "샀을 때 몸에 반영"만 한다.
    /// </summary>
    public class CatHide : NetworkBehaviour
    {
        /// <summary>서버 전용(ShopController). 레벨의 최대 HP를 몸에 반영한다.</summary>
        public void ServerApplyLevel(int _level)
        {
            if (!IsServer) return;
            int max = UnityEngine.Mathf.RoundToInt(TechLogic.ParamOf(TechId.CatHide, _level, 1));
            if (max <= 0) return;
            Health health = GetComponent<Health>();
            if (health != null) health.ServerRaiseMaxHp(max);
        }
    }
}
```

- [ ] **Step 4: ShopController** — `CanApplyLevel`: `CatHide → GetComponent<Player.CatHide>() != null`, `CatHunger → true`(패시브, `CatPounce`가 읽는다). `TryApplyLevel`: `CatHide → { GetComponent<Player.CatHide>().ServerApplyLevel(_level); return true; }`, `CatHunger → true`.

- [ ] **Step 5: PlayerPrefabFactory** — 고양이 분기에 `root.AddComponent<CatHide>();`.

- [ ] **Step 6: refresh → EditMode PASS → `Setup Networking Scene` → unity-scanner로 `PlayerCat.prefab`에 `CatHide` 확인 → Commit** (`PlayerCat.prefab` 포함, 씬은 fileID만 바뀌면 제외)

---

### Task 3: 광분 (TDD)

**Files:** Create `Assets/Scripts/Player/CatFrenzyLogic.cs`, `Assets/Scripts/Player/CatFrenzy.cs`, `Assets/Scripts/Tests/EditMode/CatFrenzyLogicTests.cs` · Modify `Assets/Scripts/Combat/CatPounce.cs`, `Assets/Scripts/Economy/ShopController.cs`, `Assets/Scripts/Economy/TechLogic.cs`, `Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs`

- [ ] **Step 1: 테스트**

```csharp
using CTC.Runtime.Player;
using NUnit.Framework;

namespace CTC.Tests
{
    public class CatFrenzyLogicTests
    {
        [Test]
        public void CanUse_NeedsLevelAndCooldown()
        {
            Assert.IsFalse(CatFrenzyLogic.CanUse(0, readyAt: 0, now: 100, active: false));   // 미보유
            Assert.IsFalse(CatFrenzyLogic.CanUse(1, readyAt: 101, now: 100, active: false)); // 쿨
            Assert.IsFalse(CatFrenzyLogic.CanUse(1, readyAt: 0, now: 100, active: true));    // 이미 켜짐
            Assert.IsTrue(CatFrenzyLogic.CanUse(1, readyAt: 100, now: 100, active: false));
        }

        [Test]
        public void Multiplier_OffIsOne()
        {
            Assert.AreEqual(1f, CatFrenzyLogic.PounceMultiplier(false, 2.5f), 1e-4f);
            Assert.AreEqual(2.5f, CatFrenzyLogic.PounceMultiplier(true, 2.5f), 1e-4f);
            Assert.AreEqual(1f, CatFrenzyLogic.PounceMultiplier(true, 0f), 1e-4f);   // 데이터 사고
        }
    }
}
```

- [ ] **Step 2: 순수 로직**

```csharp
namespace CTC.Runtime.Player
{
    /// <summary>광분(고양이 모면 테크) 판정 — `CatSkill`과 같은 「액티브 지속+쿨」 규칙이되 전직과 무관하다.</summary>
    public static class CatFrenzyLogic
    {
        public static bool CanUse(int _level, double _readyAt, double _now, bool _active)
        {
            return _level > 0 && !_active && _now >= _readyAt;
        }

        /// <summary>켜져 있을 때만 곱한다. 배율이 0이면(데이터 사고) 1배.</summary>
        public static float PounceMultiplier(bool _active, float _levelMultiplier)
        {
            return _active && _levelMultiplier > 0f ? _levelMultiplier : 1f;
        }
    }
}
```

- [ ] **Step 3: CatFrenzy 컴포넌트** — `CatSkill.cs`를 그대로 본떠서 쓴다(서버 시각 `NetworkVariable<double>` 두 개 + `NetworkVariable<bool>`, `TickServer`로 만료, `ActiveChanged`/`CooldownRemaining`/`CooldownTotal`/`ActiveRemaining`/`ActiveTotal` 노출 — 능력 바·핫바가 읽는다). **다른 점 셋**: ① 입력은 Q가 아니라 핫바 숫자키 → 입력 읽기 없음, `ShopController.Use`가 부르는 `public void ServerTryUse()`만 둔다. ② 값은 `TechLogic.ParamOf(CatFrenzy, level, 1/2/3)`. ③ 켤 때 `SfxPlayer.PlayAt(SfxId.CatSkill, …, Balance.CatSkillSoundMeters)` + `PlayerAnimationDriver.PlayOneShot(AnimationStateLogic.Spin, …)`(Task 5에서 상태 이름이 생긴 뒤 붙인다 — 이 태스크에서는 소리만).

- [ ] **Step 4: CatPounce 배율** — 기존 `fireMultiplier`(전직 불) 옆에 광분 배율을 곱한다. 둘 다 켜져 있으면 곱연산(불 ×2 · 광분 ×2.5 = ×5) — **기획서에 없는 조합**이라 주석으로 남기고, `TechLogicTests`에 "광분 최대 × 불 최대여도 요새 Lv3(1200)을 한 번에 못 부순다"는 판정 테스트를 넣는다(못 넘으면 값이 아니라 테스트를 고치지 말고 보고).

- [ ] **Step 5: ShopController · TechLogic** — `TechLogic.IsActive`에 `TechId.CatFrenzy` 추가(핫바 밖 숫자키 = 쓴다). `ShopController.CanUse(CatFrenzy)`: 미보유 `NotOwned` / 쿨 중 `Cooldown` / 아니면 `None`. `UseRpc case TechId.CatFrenzy: GetComponent<Player.CatFrenzy>().ServerTryUse(); break;`. `CanApplyLevel/TryApplyLevel`: 컴포넌트 존재 확인 / 패시브 no-op(레벨은 `TechSlots`).

- [ ] **Step 6: PlayerPrefabFactory** 고양이 분기에 `CatFrenzy` 추가 → refresh → EditMode → `Setup Networking Scene` → unity-scanner 확인 → Commit.

---

### Task 4: 예민한 귀 — 듣는 쪽 배율

**Files:** Create `Assets/Scripts/Audio/ListenerRange.cs` · Modify `Assets/Scripts/Audio/SfxPlayer.cs`, `Assets/Scripts/Audio/FootstepPlayer.cs`, `Assets/Scripts/Economy/ShopController.cs`, `Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs`

**왜 이렇게 하나:** 소리는 `[Rpc(SendTo.Everyone)]` 안에서 **각 클라이언트가 자기 프로세스에서** `SfxPlayer.PlayAt`을 부른다. 발소리도 `FootstepPlayer.Update`가 전 클라에서 돈다. 그러니 **듣는 쪽이 `maxDistance`를 곱하면** 그 사람만 멀리 듣는다 — 서버·복제가 필요 없다. (신뢰 경계: 클라가 제 오디오를 조작하는 건 원래 막을 수 없다. 주석으로 남긴다.)

- [ ] **Step 1: ListenerRange**

```csharp
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 예민한 귀(고양이 유틸 테크) — **듣는 쪽**의 가청 거리 배율 (테크 v3 계획 2b).
    /// 소리는 각 클라가 자기 프로세스에서 재생하므로(3D AudioSource maxDistance),
    /// 내 화면의 배율만 올리면 나만 멀리 듣는다. 서버 판정도 복제도 필요 없다.
    /// 클라가 제 오디오를 조작하는 것은 원래 막을 수 없는 영역이라 신뢰 경계 밖이다.
    /// </summary>
    public static class ListenerRange
    {
        /// <summary>1 = 기본. 로컬 플레이어가 바뀌거나 테크를 사면 갱신된다.</summary>
        public static float Multiplier { get; private set; } = 1f;

        public static void Set(float _multiplier)
        {
            Multiplier = _multiplier > 0f ? _multiplier : 1f;
        }

        public static void Reset() => Multiplier = 1f;
    }
}
```
그리고 로컬 플레이어의 `TechSlots.Changed`를 구독해 값을 세우는 얇은 MonoBehaviour(예: `ListenerRangeBinder`) — `LocalPlayerBinder.LocalPlayer` 구독은 `ShopHotbar`가 쓰는 패턴을 그대로 따른다. **어디에 붙일지**: 씬의 HUD 루트(핫바와 같은 캔버스)에 붙이는 게 자연스럽다(`LobbyCanvasBuilder`). 로컬 플레이어가 없어지면 `Reset()`.

- [ ] **Step 2: 적용 두 곳**
  - `SfxPlayer.PlayAt`: `source.maxDistance = _maxDistance * Audio.ListenerRange.Multiplier;` (`Played?.Invoke`에 넘기는 값은 원본 유지 — QA 훅이 설계값을 봐야 한다).
  - `FootstepPlayer.Update`: `float range = FootstepLogic.AudibleRange(...) * ListenerRange.Multiplier;` — 단, `CurrentRange`는 **원본**을 남긴다(QA `TestGetFootstepRange`가 설계값을 재고 있다. 확인하고, 배율 적용값도 필요하면 별도 프로퍼티로).

- [ ] **Step 3: ShopController** — `CatEar`: `CanApplyLevel → true`(순수 표시용 패시브), `TryApplyLevel → true`. 실제 반영은 Binder가 `TechSlots.Changed`에서 한다.

- [ ] **Step 4: 확인** — EditMode(순수 `ListenerRange` 테스트 2건: 0·음수는 1로, 값 세팅) → 플레이: 고양이가 `CatEar` Lv3을 사면 멀리 있는 창고 틱 소리가 들린다. QA는 Task 6.

---

### Task 5: 동작이 보이게 — 팩 클립 원샷 매핑

**Files:** Modify `Assets/Scripts/Player/AnimationStateLogic.cs`, `Assets/Scripts/Player/MouseDash.cs`, `Assets/Scripts/Economy/ShopController.cs`, `Assets/Scripts/Economy/StickyPatch.cs`(또는 `ShopController`의 Sticky 사용부), `Assets/Scripts/Player/CatFrenzy.cs`, `Assets/Scripts/Tests/EditMode/AnimationStateLogicTests.cs`

**팩에 이미 있는 미사용 클립**(`Mouse_Animations.fbx`/`Cat_Animations.fbx` 공통): `Bounce`·`Clicked`·`Eat`·`Fear`·`Idle_B`·`Idle_C`·`Jump`·`Lay`·`Roll`·`Sit`·`Spin`·`Swim`·`Fly`·`Death`. 새 클립은 만들지 않는다.

- [ ] **Step 1: 상태 이름 상수** — `AnimationStateLogic`에:
```csharp
        public const string Roll = "Roll";       // 대시 — 구르기
        public const string Bounce = "Bounce";   // 끈끈이 — 발밑에 내려찍기
        public const string Jump = "Jump";       // 요새 — 뛰어올랐다 내려오며 벽이 선다
        public const string Spin = "Spin";       // 광분 — 한 바퀴
        public const string Clicked = "Clicked"; // 구매 — 짧은 끄덕임
        public const string Eat = "Eat";         // 채굴 — 갉아먹기(반복)
```
테스트: `FromSpeed`는 그대로고, 새 상수가 팩 상태 이름과 정확히 같은지 문자열 단언(오타 방지).

- [ ] **Step 2: 원샷 재생 6곳** — 전부 **전 클라에서 보여야** 하므로 이미 `[Rpc(SendTo.Everyone)]`인 지점(구매음·설치음·스킬음)에 얹는다. 서버 전용 경로면 기존 Rpc에 한 줄 더한다.

| 동작 | 어디 | 상태 · 초 |
|---|---|---|
| 대시 | `MouseDash` 대시 시작(이미 전 클라가 보는 이동) | `Roll` 0.45 |
| 끈끈이 | `ShopController` Sticky 사용 Rpc | `Bounce` 0.5 |
| 요새 | 요새 설치음 Rpc(`FortressShield`의 `PushSfxRpc` 또는 `PlaceSfxOnSpawn`) | `Jump` 0.5 |
| 광분 | `CatFrenzy` 켜질 때 Rpc | `Spin` 0.6 |
| 구매 | `ShopController.BuySoundRpc` | `Clicked` 0.35 |
| 채굴 | `CheeseSpot`이 채굴자를 알고 있다 — 채굴 중 0.8초마다 | `Eat` 0.5 |

`PlayerAnimationDriver.PlayOneShot(state, seconds)`는 `_oneShotUntil` 동안 이동 전이를 잠근다 — **채굴은 반복 재생이라 짧게(0.5초) 걸고 0.8초마다 갱신**해서 걷기 전이를 막지 않게 한다. 각 호출부에서 `GetComponentInChildren<PlayerAnimationDriver>()`를 매번 부르지 말고 캐시한다.

- [ ] **Step 3: 확인** — 플레이모드에서 여섯 동작을 눈으로 본다. `u_screenshot`으로 대시(Roll)·광분(Spin) 프레임을 찍어 보고서에 첨부. 어색하면 초를 조절한다(클립 자체는 안 고친다).

- [ ] **Step 4: Commit** — 커밋 메시지에 "새 클립 0 — 팩 미사용 클립 6종 재사용"을 남긴다.

---

### Task 6: 봇 · QA 훅 · qa_v48 · 다이어그램

**Files:** Modify `Assets/Scripts/Bots/BotShopper.cs`, `Assets/Scripts/Bots/CatBotLogic.cs`(또는 고양이 구매 순서가 있는 곳), `Assets/Scripts/Testing/TestManager.Tech.cs` · Create `tools/wesqa/tests/qa_v48.py` · Modify `docs/design-vault/코드 다이어그램.md`

- [ ] **Step 1: 봇 구매 확장** — 봇은 지금 옛 5개만 산다. 순수 로직에 신규를 넣는다:
  - 쥐(`MouseBotLogic.NextPurchase`): 대시 → 요새 → 창고 **→ 치즈 방패 Lv1 → 큰 주머니 Lv1**(둘 다 값이 싸고 효과가 즉시 난다). 칸 5 상한을 넘지 않게 `TechSlots.Count`를 인자로 받아 판단한다(지금 시그니처에 인자 추가 — 테스트도 갱신).
  - 고양이: 발톱 → 두꺼운 가죽 → 배고픔 순으로. 광분·예민한 귀는 **안 산다**(액티브 판단·청각은 봇이 흉내 낼 수 없다 — 흉내 내면 재는 게 밸런스가 아니라 봇의 실력이 된다. 주석으로 남긴다).
  - EditMode 테스트로 순서·칸 상한을 고정.

- [ ] **Step 2: QA 훅** — `TestGetCatFrenzyActive(long)`·`TestGetCatFrenzyCooldown(long)`·`TestGetListenerMultiplier()`(로컬)·`TestUseTech`는 이미 있다(광분에 그대로 쓴다).

- [ ] **Step 3: qa_v48.py** (MPPM 3인, 호스트=**고양이**. `qa_v46`/`qa_v47` 뼈대 재사용, 비용은 `TestGetTechCost`로):
① 두꺼운 가죽 Lv3 → `TestGetMaxHpOf(c) == 1450`, 현재 HP도 올라감
② 배고픔 Lv3 → 쥐를 같은 피해로 때렸을 때 장난기 적립이 ×1.6
③ 광분 Lv3 → `TestUseTech(c,"CatFrenzy")` → `Active` true · 덮치기 피해가 ×2.5 · 10초 뒤 false · 쿨 20
④ 예민한 귀 Lv3 → `TestGetListenerMultiplier() == 2`
⑤ 고양이 칸 5 상한 — 6번째 거절
⑥ 광분 + 전직 불 동시 → 피해가 곱연산인지(설계 확인용, 실패해도 값만 보고)
⑦ 원샷 애니메이션 6종이 재생되는지 — 훅이 없으면 스크린샷 2장으로 대체하고 SKIP 명시

- [ ] **Step 4: 다이어그램** — `CatHide`·`CatFrenzy`·`CatFrenzyLogic`·`ListenerRange`·`FunLogic.WithHunger`·`Health.ServerRaiseMaxHp`·애니메이션 원샷 매핑 표 한 줄. EditMode 기준선 갱신.

- [ ] **Step 5: Commit**

---

## 자가 검토

| 기획서 §5 | 태스크 |
|---|---|
| 두꺼운 가죽 · 배고픔 | T2 |
| 광분 | T3 |
| 예민한 귀 | T4 |
| 고양이 칸 5 / 테크 6 → 하나 포기 | 데이터로 성립(T1) · qa_v48 ⑤ |
| 전직은 칸을 안 쓴다(Q 그대로) | 손대지 않음 — `CatSkill`은 그대로 |
| 제약(새 클립 0) | T5 — 팩 미사용 클립만 |
| 봇·QA·다이어그램 | T6 |

**타입 일관성:** `Health.ServerRaiseMaxHp` · `CatHide.ServerApplyLevel` · `CatFrenzyLogic.CanUse/PounceMultiplier` · `CatFrenzy.ServerTryUse/Active/CooldownRemaining` · `FunLogic.WithHunger` · `ListenerRange.Multiplier/Set/Reset` · `AnimationStateLogic.Roll/Bounce/Jump/Spin/Clicked/Eat`.

**계획 2c(별건):** 쥐 사격 클립 신규 제작 — 팩 rig(본 11개: root·body·head·ear.L/R·leg.F.L/R·leg.B.L/R·tail.01/02)에 에디터 스크립트로 커브를 찍고, 임시 카메라 렌더로 프레임을 확인하는 루프. 기획서 제약의 "모션 0"을 처음으로 깨는 작업이라 사용자 승인 후 착수.
