# 능력 바(스킬 쿨다운 슬롯 UI) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 좌하단 글자 칩(ControlsHud)을 없애고, 상점 핫바 왼쪽에 슬롯형 능력 바(쥐: Space 대시·F 임명 / 고양이: Q 스킬)를 붙인다. 쿨다운은 방사형 덮개+남은 초, 지속 중은 발광+잔여 바, 미보유는 흐림+얻는 곳. 살금 키 LCtrl→LShift, 대시 LShift→Space.

**Architecture:** 순수 `AbilityBarLogic`(슬롯 목록·상태·비율·글자, EditMode)이 판단하고, `AbilityBar`(View)가 R3 스트림(직업·보유·전직)으로 슬롯을 재구성하며 쿨다운이 살아 있을 때만 `EveryUpdate`로 채움을 갱신한다. `AbilitySlotView`는 슬롯 1칸의 UGUI 그리기만. 고양이 스킬 쿨다운은 `CatSkill`의 `NetworkVariable<double>`(서버 시각)로 복제해 클라 고양이도 본다.

**Tech Stack:** Unity 6 UGUI(Image Filled Radial360) · TMP · R3 · NGO 2.12 NetworkVariable · NUnit EditMode · wesqa

Spec: `docs/superpowers/specs/2026-08-17-ability-bar-design.md`

**사전 조건:** Unity 플레이 모드 종료(플레이 중 리컴파일 = NGO NRE 폭풍). `u_play(action:stop)`.

---

## 파일 지도

| 파일 | 역할 |
|---|---|
| Modify `Assets/Scripts/Player/Sneak.cs` | LCtrl → LShift |
| Modify `Assets/Scripts/Player/MouseDash.cs` | LShift → Space · `CooldownTotal`·`ActiveRemaining`·`ActiveTotal` 노출 |
| Modify `Assets/Scripts/Player/CatSkill.cs` | 서버 float 쿨/지속 → `NetworkVariable<double> m_ReadyAt`·`m_ActiveUntil` · `CooldownRemaining`·`CooldownTotal`·`ActiveRemaining`·`ActiveTotal` |
| Modify `Assets/Scripts/Player/CatClassLogic.cs` | `ClassWordOf(CatClassId)` 공개(StatusHud에서 이사) |
| Modify `Assets/Scripts/UI/StatusHud.cs` | 사설 `ClassWordOf` 삭제 → `CatClassLogic.ClassWordOf` |
| Create `Assets/Scripts/UI/AbilityBarLogic.cs` | 순수: `AbilitySlot` 구조체 · `Build` · `StateOf` · `FillRatio` · `SecondsText` |
| Create `Assets/Scripts/UI/AbilitySlotView.cs` | 슬롯 1칸 그리기 |
| Create `Assets/Scripts/UI/AbilityBar.cs` | View — 구독·재구성·틱 |
| Delete `Assets/Scripts/Lobby/ControlsHud.cs` (+.meta), `Assets/Scripts/Tests/EditMode/ControlsHudTests.cs` (+.meta) | |
| Create `Assets/Scripts/Tests/EditMode/AbilityBarLogicTests.cs` | |
| Modify `Assets/Scripts/Editor/Setup/LobbyCanvasBuilder.cs` | ControlsText 블록 → `BuildAbilityBar` |
| Modify `Assets/Scripts/Economy/ShopHotbar.cs` | 문구 3곳(LShift→Space, LCtrl→LShift) |
| Modify `Assets/Scripts/Testing/TestManager.Hud.cs` | `TestGetControlsText` → `TestGetAbilityBar` |
| Modify `Assets/Scripts/Lobby/MapInfoHud.cs`, `Assets/Scripts/UI/ChatHud.cs`, `Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs` | 주석의 ControlsHud/LCtrl 언급 |
| Modify `tools/wesqa/tests/qa_v45.py` | §조작 안내 → 능력 바 단언 |
| Modify `docs/design-vault/규칙과-현황.html`, `docs/design-vault/코드 다이어그램.md`, `.claude/handoff-active.md` | |

---

### Task 1: 키 변경 — 살금 LShift · 대시 Space · 문구

**Files:** `Assets/Scripts/Player/Sneak.cs:9,59-61`, `Assets/Scripts/Player/MouseDash.cs:9,68,74`, `Assets/Scripts/Economy/ShopHotbar.cs:42,148,370`, `Assets/Scripts/Editor/Setup/PlayerPrefabFactory.cs:81`

- [ ] **Step 1: Sneak.cs**

```csharp
/// 살금살금(LShift 홀드). 발소리를 완전히 죽이는 대신 이동 속도를 절반 이하로 낮춘다.
...
// 커서가 풀린 동안(로비·인게임 콘솔)은 조작으로 치지 않는다 — 다른 창에서 Shift 조합을 누를 때
bool held = m_DevForced
            || (Cursor.lockState == CursorLockMode.Locked && kb.leftShiftKey.isPressed);
```

- [ ] **Step 2: MouseDash.cs**

```csharp
/// 대시(상점 구매 영구 능력). 소유자가 Space를 누르면 몸 전방으로 짧게 돌진.
...
if (kb == null || !kb.spaceKey.wasPressedThisFrame) return;
...
/// 봇·QA 전용 — 사람의 Space와 같은 입구.
```

- [ ] **Step 3: ShopHotbar.cs 문구**

```csharp
"더 세고 멀리", "연사 — 고양이 사냥", "Space 순간 가속",
...
Say($"WASD 이동 · 마우스 시점 · 좌클릭 {attack} · LShift 살금살금 · 숫자키 구매",
...
case ShopItemId.Dash: return "대시 습득 — Space로 순간 가속해 거리를 벌린다";
```

- [ ] **Step 4: PlayerPrefabFactory.cs:81 주석** `// LCtrl 잠행` → `// LShift 잠행`

- [ ] **Step 5: Commit** `feat(input): 살금 LCtrl→LShift(쥐·고양이 공통) · 대시 LShift→Space`

---

### Task 2: CatSkill 쿨다운·지속 복제 + ClassWordOf 이사

**Files:** `Assets/Scripts/Player/CatSkill.cs`, `Assets/Scripts/Player/CatClassLogic.cs`, `Assets/Scripts/UI/StatusHud.cs:194-204`

- [ ] **Step 1: CatClassLogic에 추가**

```csharp
/// <summary>전직 이름 한 글자(HUD 배지·능력 슬롯 공용).</summary>
public static string ClassWordOf(CatClassId _class)
{
    switch (_class)
    {
        case CatClassId.Fire: return "불";
        case CatClassId.Wind: return "바람";
        case CatClassId.Light: return "빛";
        case CatClassId.Dark: return "어둠";
        default: return "";
    }
}
```

- [ ] **Step 2: StatusHud** 사설 `ClassWordOf` 메서드 삭제, 호출부를 `Player.CatClassLogic.ClassWordOf(...)`로.

- [ ] **Step 3: CatSkill 재작성 (필드·틱·사용)**

```csharp
private readonly NetworkVariable<bool> m_Active = new NetworkVariable<bool>();
// 서버 시각(NetworkManager.ServerTime) 기준. 소유자 HUD가 남은 초를 스스로 계산한다 —
// 서버 float였을 때는 클라 고양이에게 쿨다운이 항상 0으로 보였다(2026-08-17).
private readonly NetworkVariable<double> m_ReadyAt = new NetworkVariable<double>();
private readonly NetworkVariable<double> m_ActiveUntil = new NetworkVariable<double>();

private CatClass m_Class;

public bool Active => m_Active.Value;
public Observable<bool> ActiveChanged => m_Active.AsObservableWithCurrent();

/// <summary>남은 쿨다운(초) — 전 클라에서 같은 값(서버 시각 기준).</summary>
public float CooldownRemaining => RemainingUntil(m_ReadyAt.Value);
public float CooldownTotal => CatClassLogic.SkillCooldown;
/// <summary>남은 지속(초). 꺼져 있으면 0.</summary>
public float ActiveRemaining => m_Active.Value ? RemainingUntil(m_ActiveUntil.Value) : 0f;
public float ActiveTotal => CatClassLogic.SkillSeconds;

private float RemainingUntil(double _serverTime)
{
    if (!IsSpawned) return 0f;
    return Mathf.Max(0f, (float)(_serverTime - NetworkManager.ServerTime.Time));
}

private void TickServer()
{
    if (m_Active.Value && NetworkManager.ServerTime.Time >= m_ActiveUntil.Value)
        m_Active.Value = false;
}

[Rpc(SendTo.Server, RequireOwnership = true)]
private void UseRpc()
{
    double now = NetworkManager.ServerTime.Time;
    float cooldown = (float)Math.Max(0.0, m_ReadyAt.Value - now);
    if (!CatClassLogic.CanUseSkill(m_Class.Value, cooldown, m_Active.Value)) return;

    m_Active.Value = true;
    m_ActiveUntil.Value = now + CatClassLogic.SkillSeconds;
    m_ReadyAt.Value = now + CatClassLogic.SkillCooldown;

    SkillFxRpc();
}
```
`Update`: `if (IsServer) TickServer(); if (IsOwner) ReadInput();`. `using System;` 추가. `m_Remaining`·`m_Cooldown` 삭제.

- [ ] **Step 4: TestManager.CatClass.cs** 에서 `m_Cooldown`/`m_Remaining` 리플렉션 쓰는 곳이 있으면 `CooldownRemaining`으로 (grep `CatSkill` in `Assets/Scripts/Testing/`).

- [ ] **Step 5: Commit** `feat(skill): 고양이 스킬 쿨다운·지속을 서버 시각 NetworkVariable로 복제 — 클라 고양이도 남은 초를 본다`

---

### Task 3: MouseDash 노출

**Files:** `Assets/Scripts/Player/MouseDash.cs:38`

- [ ] **Step 1**

```csharp
public float CooldownRemaining => !m_Owned.Value ? 0f
    : Mathf.Max(0f, (float)(COOLDOWN_SECONDS - (Time.timeAsDouble - m_LastUseTime)));
public float CooldownTotal => COOLDOWN_SECONDS;
/// <summary>대시 진행 중 남은 시간(초) — 슬롯 '지속 중' 표시용.</summary>
public float ActiveRemaining => Mathf.Max(0f, m_DashRemainingSeconds);
public float ActiveTotal => DASH_DURATION_SECONDS;
```

- [ ] **Step 2: Commit** (Task 4와 묶어도 됨)

---

### Task 4: AbilityBarLogic (TDD)

**Files:** Create `Assets/Scripts/UI/AbilityBarLogic.cs`, Create `Assets/Scripts/Tests/EditMode/AbilityBarLogicTests.cs`, Delete `Assets/Scripts/Tests/EditMode/ControlsHudTests.cs`

- [ ] **Step 1: 테스트**

```csharp
using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using CTC.Runtime.Player;
using CTC.Runtime.UI;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>능력 바(2026-08-17) — 슬롯 목록·상태·채움 비율·초 글자. 그림(View)은 이 값을 그대로 그린다.</summary>
    public class AbilityBarLogicTests
    {
        [Test]
        public void Mouse_HasDashAndAppointSlots_InOrder()
        {
            var slots = AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.None, _dashOwned: false, CatClassId.None, _dashShopSlot: 3);
            Assert.AreEqual(2, slots.Length);
            Assert.AreEqual("Space", slots[0].Key); Assert.AreEqual("대시", slots[0].Name);
            Assert.AreEqual("F", slots[1].Key);     Assert.AreEqual("임명", slots[1].Name);
        }

        [Test]
        public void Mouse_DashLockedTellsShopSlot_UntilOwned()
        {
            var locked = AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.None, false, CatClassId.None, 3)[0];
            Assert.IsTrue(locked.Locked); Assert.AreEqual("상점 3", locked.LockReason);
            var owned = AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.None, true, CatClassId.None, 3)[0];
            Assert.IsFalse(owned.Locked); Assert.AreEqual("", owned.LockReason);
        }

        [Test]
        public void Mouse_AppointLockedUnlessGang()
        {
            Assert.IsTrue(AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.None, true, CatClassId.None, 3)[1].Locked);
            Assert.IsTrue(AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.Minion, true, CatClassId.None, 3)[1].Locked);
            Assert.AreEqual("깡패만", AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.None, true, CatClassId.None, 3)[1].LockReason);
            Assert.IsFalse(AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.Gang, true, CatClassId.None, 3)[1].Locked);
        }

        [Test]
        public void Cat_HasSkillSlot_NamedByClass()
        {
            var none = AbilityBarLogic.Build(RoleId.Cat, MouseJobId.None, false, CatClassId.None, 0);
            Assert.AreEqual(1, none.Length);
            Assert.AreEqual("Q", none[0].Key); Assert.AreEqual("스킬", none[0].Name);
            Assert.IsTrue(none[0].Locked); Assert.AreEqual("전직 필요", none[0].LockReason);

            var dark = AbilityBarLogic.Build(RoleId.Cat, MouseJobId.None, false, CatClassId.Dark, 0)[0];
            Assert.AreEqual("어둠", dark.Name); Assert.IsFalse(dark.Locked);
        }

        [Test]
        public void Spectator_HasNoSlots()
        {
            Assert.AreEqual(0, AbilityBarLogic.Build(RoleId.None, MouseJobId.None, false, CatClassId.None, 0).Length);
        }

        [Test]
        public void StateOf_PrefersLocked_ThenActive_ThenCooldown()
        {
            Assert.AreEqual(AbilityState.Locked,   AbilityBarLogic.StateOf(true,  2f, 1f));
            Assert.AreEqual(AbilityState.Active,   AbilityBarLogic.StateOf(false, 2f, 1f));
            Assert.AreEqual(AbilityState.Cooldown, AbilityBarLogic.StateOf(false, 0f, 1f));
            Assert.AreEqual(AbilityState.Ready,    AbilityBarLogic.StateOf(false, 0f, 0f));
        }

        [Test]
        public void FillRatio_ClampsAndHandlesZeroTotal()
        {
            Assert.AreEqual(0.5f, AbilityBarLogic.FillRatio(1f, 2f), 1e-4f);
            Assert.AreEqual(1f,   AbilityBarLogic.FillRatio(5f, 2f), 1e-4f);
            Assert.AreEqual(0f,   AbilityBarLogic.FillRatio(-1f, 2f), 1e-4f);
            Assert.AreEqual(0f,   AbilityBarLogic.FillRatio(1f, 0f), 1e-4f);
        }

        [Test]
        public void SecondsText_CeilsAndBlanksAtZero()
        {
            Assert.AreEqual("3", AbilityBarLogic.SecondsText(2.01f));
            Assert.AreEqual("1", AbilityBarLogic.SecondsText(0.2f));
            Assert.AreEqual("",  AbilityBarLogic.SecondsText(0f));
        }
    }
}
```

- [ ] **Step 2: 로직**

```csharp
using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using CTC.Runtime.Player;

namespace CTC.Runtime.UI
{
    public enum AbilityState { Ready, Cooldown, Active, Locked }

    /// <summary>능력 슬롯 1칸의 정적 정보(키·이름·잠금). 시간값(쿨·지속)은 View가 매 프레임 컴포넌트에서 읽는다.</summary>
    public readonly struct AbilitySlot
    {
        public readonly string Key;
        public readonly string Name;
        public readonly bool Locked;
        public readonly string LockReason;
        public AbilitySlot(string _key, string _name, bool _locked, string _lockReason)
        { Key = _key; Name = _name; Locked = _locked; LockReason = _lockReason; }
    }

    /// <summary>
    /// 능력 바 규칙(2026-08-17) — 순수 함수. 좌하단 글자 칩(ControlsHud)을 대체한다.
    /// 슬롯은 역할별 고정(쥐: Space 대시·F 임명 / 고양이: Q 스킬). 기본 공격·살금은 슬롯이 없다.
    /// 미보유는 숨기지 않고 흐리게 + 얻는 곳을 적는다 — "뭐가 있는지"를 첫 판에 알게.
    /// </summary>
    public static class AbilityBarLogic
    {
        public const string KEY_DASH = "Space";
        public const string KEY_APPOINT = "F";
        public const string KEY_SKILL = "Q";

        public static AbilitySlot[] Build(RoleId _role, MouseJobId _job, bool _dashOwned, CatClassId _class, int _dashShopSlot)
        {
            switch (_role)
            {
                case RoleId.Mouse:
                    return new[]
                    {
                        new AbilitySlot(KEY_DASH, "대시", !_dashOwned, _dashOwned ? "" : $"상점 {_dashShopSlot}"),
                        new AbilitySlot(KEY_APPOINT, "임명", _job != MouseJobId.Gang, _job == MouseJobId.Gang ? "" : "깡패만"),
                    };
                case RoleId.Cat:
                    bool advanced = _class != CatClassId.None;
                    return new[]
                    {
                        new AbilitySlot(KEY_SKILL, advanced ? CatClassLogic.ClassWordOf(_class) : "스킬",
                            !advanced, advanced ? "" : "전직 필요"),
                    };
                default:
                    return System.Array.Empty<AbilitySlot>();
            }
        }

        /// <summary>잠금 &gt; 지속 중 &gt; 쿨다운 &gt; 준비.</summary>
        public static AbilityState StateOf(bool _locked, float _activeRemaining, float _cooldownRemaining)
        {
            if (_locked) return AbilityState.Locked;
            if (_activeRemaining > 0f) return AbilityState.Active;
            if (_cooldownRemaining > 0f) return AbilityState.Cooldown;
            return AbilityState.Ready;
        }

        /// <summary>남은/전체 → 0~1. 전체가 0이면 0.</summary>
        public static float FillRatio(float _remaining, float _total)
        {
            if (_total <= 0f) return 0f;
            return UnityEngine.Mathf.Clamp01(_remaining / _total);
        }

        /// <summary>남은 초 글자 — 올림 정수, 0이면 빈 칸.</summary>
        public static string SecondsText(float _remaining)
        {
            if (_remaining <= 0f) return "";
            return UnityEngine.Mathf.CeilToInt(_remaining).ToString();
        }
    }
}
```

- [ ] **Step 3:** ControlsHudTests.cs(+meta) 삭제. `Tools/CTC/Run EditMode Tests` → 전부 PASS.
- [ ] **Step 4: Commit** `feat(ui): AbilityBarLogic — 능력 슬롯 목록·상태·비율·초(순수, 테스트 8)`

---

### Task 5: AbilitySlotView + AbilityBar (View)

**Files:** Create `Assets/Scripts/UI/AbilitySlotView.cs`, `Assets/Scripts/UI/AbilityBar.cs`

- [ ] **Step 1: AbilitySlotView**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.UI
{
    /// <summary>능력 슬롯 1칸 — 그리기만. 판단은 AbilityBarLogic, 시간은 AbilityBar가 넘긴다.</summary>
    public class AbilitySlotView : MonoBehaviour
    {
        private const float FLASH_SECONDS = 0.25f;

        [SerializeField] private Image m_Frame;       // 테두리(발광 색)
        [SerializeField] private Image m_Background;
        [SerializeField] private Image m_Cover;       // Filled Radial360 — 쿨다운 덮개
        [SerializeField] private Image m_ActiveBar;   // Filled Horizontal — 지속 잔여
        [SerializeField] private TMP_Text m_KeyText;
        [SerializeField] private TMP_Text m_NameText;
        [SerializeField] private TMP_Text m_SecondsText;
        [SerializeField] private TMP_Text m_SubText;  // "6초" · "상점 3" · "전직 필요"
        [SerializeField] private CanvasGroup m_Group;

        private static readonly Color FRAME_IDLE = new Color(0.42f, 0.35f, 0.25f, 1f);
        private static readonly Color FRAME_ACTIVE = new Color(1f, 0.82f, 0.48f, 1f);
        private static readonly Color BG_IDLE = new Color(0.055f, 0.047f, 0.035f, 0.85f);
        private static readonly Color BG_FLASH = new Color(0.55f, 0.45f, 0.25f, 0.95f);

        private float m_FlashLeft;

        public string Key => m_KeyText != null ? m_KeyText.text : "";
        public string Name => m_NameText != null ? m_NameText.text : "";
        public AbilityState State { get; private set; }
        public float SecondsShown { get; private set; }

        public void SetSlot(AbilitySlot _slot)
        {
            m_KeyText.text = _slot.Key;
            m_NameText.text = _slot.Name;
            m_SubText.text = _slot.LockReason;
        }

        /// <summary>매 프레임(살아 있을 때) 또는 상태 바뀔 때.</summary>
        public void Apply(AbilitySlot _slot, float _cooldownRemaining, float _cooldownTotal,
            float _activeRemaining, float _activeTotal, float _dt)
        {
            AbilityState next = AbilityBarLogic.StateOf(_slot.Locked, _activeRemaining, _cooldownRemaining);
            if (State == AbilityState.Cooldown && next == AbilityState.Ready) m_FlashLeft = FLASH_SECONDS;
            State = next;
            SecondsShown = _cooldownRemaining;

            m_Group.alpha = _slot.Locked ? 0.45f : 1f;
            m_Cover.fillAmount = next == AbilityState.Cooldown ? AbilityBarLogic.FillRatio(_cooldownRemaining, _cooldownTotal) : 0f;
            m_SecondsText.text = next == AbilityState.Cooldown ? AbilityBarLogic.SecondsText(_cooldownRemaining) : "";
            m_NameText.gameObject.SetActive(next != AbilityState.Cooldown);
            m_ActiveBar.gameObject.SetActive(next == AbilityState.Active);
            if (next == AbilityState.Active) m_ActiveBar.fillAmount = AbilityBarLogic.FillRatio(_activeRemaining, _activeTotal);
            m_SubText.text = next == AbilityState.Active ? $"{AbilityBarLogic.SecondsText(_activeRemaining)}초"
                           : _slot.Locked ? _slot.LockReason : "";
            m_Frame.color = next == AbilityState.Active ? FRAME_ACTIVE : FRAME_IDLE;

            m_FlashLeft = Mathf.Max(0f, m_FlashLeft - _dt);
            m_Background.color = Color.Lerp(BG_IDLE, BG_FLASH, m_FlashLeft / FLASH_SECONDS);
        }

        public bool IsAnimating => m_FlashLeft > 0f;
    }
}
```

- [ ] **Step 2: AbilityBar**

```csharp
using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using CTC.Runtime.Player;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 능력 바(2026-08-17) — 핫바 왼쪽 슬롯 묶음. 좌하단 글자 칩(ControlsHud)을 대체한다.
    /// 슬롯 구성은 스트림(직업·대시 보유·전직)으로, 채움(쿨·지속)은 시간 파생값이라
    /// **살아 있는 동안만** EveryUpdate로 읽는다 — 아무 쿨도 없으면 구독 자체가 없다.
    /// </summary>
    public class AbilityBar : MonoBehaviour
    {
        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private GameObject m_Root;
        [SerializeField] private AbilitySlotView[] m_Slots;   // 빌더가 2칸 — 남는 칸은 끈다

        private readonly SerialDisposable m_JobSub = new SerialDisposable();
        private readonly SerialDisposable m_DashSub = new SerialDisposable();
        private readonly SerialDisposable m_ClassSub = new SerialDisposable();
        private readonly SerialDisposable m_Tick = new SerialDisposable();

        private bool m_GameStarted;
        private RoleId m_Role = RoleId.None;
        private MouseJobId m_Job;
        private bool m_DashOwned;
        private CatClassId m_Class;
        private MouseDash m_Dash;
        private CatSkill m_Skill;
        private AbilitySlot[] m_Built = System.Array.Empty<AbilitySlot>();

        public AbilitySlot[] Built => m_Built;

        private void Awake() { m_Root.SetActive(false); }

        private void Start()
        {
            m_JobSub.AddTo(this); m_DashSub.AddTo(this); m_ClassSub.AddTo(this); m_Tick.AddTo(this);
            m_Lobby.GameStartedChanged.Subscribe(s => { m_GameStarted = s; Rebuild(); }).AddTo(this);
            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
        }

        private void OnLocalPlayerChanged(NetworkObject _player)
        {
            RoleTag tag = _player != null ? _player.GetComponent<RoleTag>() : null;
            m_Role = tag != null ? tag.Role : RoleId.None;

            var job = _player != null ? _player.GetComponent<MouseJob>() : null;
            m_Job = MouseJobId.None;
            m_JobSub.Disposable = job != null ? job.JobChanged.Subscribe(j => { m_Job = j; Rebuild(); }) : null;

            m_Dash = _player != null ? _player.GetComponent<MouseDash>() : null;
            m_DashOwned = false;
            m_DashSub.Disposable = m_Dash != null ? m_Dash.OwnedChanged.Subscribe(o => { m_DashOwned = o; Rebuild(); }) : null;

            var catClass = _player != null ? _player.GetComponent<CatClass>() : null;
            m_Skill = _player != null ? _player.GetComponent<CatSkill>() : null;
            m_Class = CatClassId.None;
            m_ClassSub.Disposable = catClass != null ? catClass.ClassChanged.Subscribe(c => { m_Class = c; Rebuild(); }) : null;

            Rebuild();
        }

        private void Rebuild()
        {
            bool show = m_GameStarted && m_Role != RoleId.None;
            if (m_Root.activeSelf != show) m_Root.SetActive(show);
            if (!show) { m_Built = System.Array.Empty<AbilitySlot>(); m_Tick.Disposable = null; return; }

            m_Built = AbilityBarLogic.Build(m_Role, m_Job, m_DashOwned, m_Class, DashShopSlot());
            for (int i = 0; i < m_Slots.Length; i++)
            {
                bool used = i < m_Built.Length;
                if (m_Slots[i].gameObject.activeSelf != used) m_Slots[i].gameObject.SetActive(used);
                if (used) m_Slots[i].SetSlot(m_Built[i]);
            }
            Tick(0f);
            EnsureTicker();
        }

        // 핫바에서 대시가 몇 번 칸인지 — 카탈로그 순서가 바뀌어도 안내가 안 틀리게
        private static int DashShopSlot()
        {
            var indices = ShopLogic.RoleCatalogIndices(RoleId.Mouse);
            for (int i = 0; i < indices.Count; i++)
                if (ShopLogic.CATALOG[indices[i]].Id == ShopItemId.Dash) return i + 1;
            return 0;
        }

        private void EnsureTicker()
        {
            if (m_Tick.Disposable != null) return;
            m_Tick.Disposable = Observable.EveryUpdate().Subscribe(_ =>
            {
                Tick(Time.deltaTime);
                if (!AnyAlive()) m_Tick.Disposable = null;
            });
        }

        private bool AnyAlive()
        {
            bool alive = false;
            for (int i = 0; i < m_Built.Length; i++) alive |= m_Slots[i].IsAnimating;
            if (m_Dash != null && m_DashOwned) alive |= m_Dash.CooldownRemaining > 0f || m_Dash.ActiveRemaining > 0f;
            if (m_Skill != null && m_Class != CatClassId.None) alive |= m_Skill.CooldownRemaining > 0f || m_Skill.ActiveRemaining > 0f;
            return alive;
        }

        private void Tick(float _dt)
        {
            for (int i = 0; i < m_Built.Length; i++)
            {
                AbilitySlot slot = m_Built[i];
                float cd = 0f, cdTotal = 0f, act = 0f, actTotal = 0f;
                if (slot.Key == AbilityBarLogic.KEY_DASH && m_Dash != null)
                { cd = m_Dash.CooldownRemaining; cdTotal = m_Dash.CooldownTotal; act = m_Dash.ActiveRemaining; actTotal = m_Dash.ActiveTotal; }
                else if (slot.Key == AbilityBarLogic.KEY_SKILL && m_Skill != null)
                { cd = m_Skill.CooldownRemaining; cdTotal = m_Skill.CooldownTotal; act = m_Skill.ActiveRemaining; actTotal = m_Skill.ActiveTotal; }
                m_Slots[i].Apply(slot, cd, cdTotal, act, actTotal, _dt);
            }
        }

        /// <summary>플레이어 입력(대시·스킬 사용)은 컴포넌트가 처리한다 — 여기서 쿨이 새로 돌기 시작하면 틱을 켠다.</summary>
        private void Update()
        {
            if (m_Built.Length > 0 && m_Tick.Disposable == null && AnyAlive()) EnsureTicker();
        }
    }
}
```
> `Update`는 폴링이 아니라 "틱 켤 필요가 생겼나"만 본다 — 쿨다운 시작은 컴포넌트 내부 값이라 스트림이 없다(ControlsHud와 같은 사정). 규칙 3 예외로 주석에 남긴다.

- [ ] **Step 3: Commit** `feat(ui): AbilityBar·AbilitySlotView — 슬롯 재구성은 스트림, 채움은 살아 있을 때만 틱`

---

### Task 6: 빌더 · ControlsHud 삭제 · TestManager

**Files:** `Assets/Scripts/Editor/Setup/LobbyCanvasBuilder.cs:196-215`, delete `Assets/Scripts/Lobby/ControlsHud.cs`(+meta), `Assets/Scripts/Testing/TestManager.Hud.cs:27-28`, `Assets/Scripts/Lobby/MapInfoHud.cs:15`, `Assets/Scripts/UI/ChatHud.cs:48`

- [ ] **Step 1: 빌더** — ControlsText 블록(196-215)을 `BuildAbilityBar(canvasGo, _lobby);` 호출로 바꾸고 메서드 추가:

```csharp
private const float ABILITY_SLOT = 84f;
private const float ABILITY_GAP = 8f;
private const float ABILITY_TO_HOTBAR = 32f;
private const int ABILITY_SLOTS = 2;

// ===== 능력 바 (2026-08-17) — 핫바 왼쪽. 좌하단 글자 칩(ControlsHud)을 대체 =====
private static void BuildAbilityBar(GameObject _canvasGo, LobbyController _lobby)
{
    float hotbarWidth = SLOT_WIDTH * ShopLogic.HOTBAR_SLOTS + SLOT_GAP * (ShopLogic.HOTBAR_SLOTS - 1);
    var root = new GameObject("AbilityBar", typeof(RectTransform));
    root.transform.SetParent(_canvasGo.transform, false);
    var rt = (RectTransform)root.transform;
    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
    rt.pivot = new Vector2(1f, 0f);
    rt.anchoredPosition = new Vector2(-hotbarWidth * 0.5f - ABILITY_TO_HOTBAR, 28f);
    rt.sizeDelta = new Vector2(ABILITY_SLOT * ABILITY_SLOTS + ABILITY_GAP, SLOT_HEIGHT);

    var views = new Object[ABILITY_SLOTS];
    for (int i = 0; i < ABILITY_SLOTS; i++)
    {
        views[i] = BuildAbilitySlot(root.transform, i);
    }
    root.SetActive(false);

    var bar = _canvasGo.AddComponent<CTC.Runtime.UI.AbilityBar>();
    SetRef(bar, "m_Lobby", _lobby);
    SetRef(bar, "m_Root", root);
    SetRefArray(bar, "m_Slots", views);
}

private static CTC.Runtime.UI.AbilitySlotView BuildAbilitySlot(Transform _parent, int _index)
{
    // 오른쪽 끝(핫바 쪽)부터 채운다 — 슬롯 수가 달라도 핫바와의 간격이 같게
    var go = new GameObject($"Ability{_index + 1}", typeof(RectTransform));
    go.transform.SetParent(_parent, false);
    var rt = (RectTransform)go.transform;
    rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
    rt.pivot = new Vector2(1f, 0f);
    rt.anchoredPosition = new Vector2(-(ABILITY_SLOT + ABILITY_GAP) * (ABILITY_SLOTS - 1 - _index), 0f);
    rt.sizeDelta = new Vector2(ABILITY_SLOT, ABILITY_SLOT);
    var group = go.AddComponent<CanvasGroup>();

    var frame = go.AddComponent<Image>();
    frame.color = new Color(0.42f, 0.35f, 0.25f, 1f);
    frame.raycastTarget = false;

    Image bg = FullRectImage(go.transform, "Background", 2f, new Color(0.055f, 0.047f, 0.035f, 0.85f));
    Image cover = FullRectImage(go.transform, "Cover", 2f, new Color(0f, 0f, 0f, 0.78f));
    cover.type = Image.Type.Filled;
    cover.fillMethod = Image.FillMethod.Radial360;
    cover.fillOrigin = (int)Image.Origin360.Top;
    cover.fillClockwise = true;
    cover.fillAmount = 0f;

    TMP_Text name = CreateTmpText("Name", go.transform, "", 20f, TextAlignmentOptions.Center,
        new Vector2(0f, -28f), new Vector2(ABILITY_SLOT - 8f, 28f));
    name.fontStyle = FontStyles.Bold;
    TMP_Text seconds = CreateTmpText("Seconds", go.transform, "", 40f, TextAlignmentOptions.Center,
        new Vector2(0f, -18f), new Vector2(ABILITY_SLOT, 50f));
    seconds.fontStyle = FontStyles.Bold;
    TMP_Text sub = CreateTmpText("Sub", go.transform, "", 13f, TextAlignmentOptions.Center,
        new Vector2(0f, -(ABILITY_SLOT - 24f)), new Vector2(ABILITY_SLOT - 8f, 18f));
    sub.color = new Color(0.79f, 0.74f, 0.65f, 1f);

    // 키캡 — 좌상단 모서리에 걸친 밝은 칩
    var keyGo = new GameObject("KeyCap", typeof(RectTransform));
    keyGo.transform.SetParent(go.transform, false);
    var keyRt = (RectTransform)keyGo.transform;
    keyRt.anchorMin = keyRt.anchorMax = new Vector2(0f, 1f);
    keyRt.pivot = new Vector2(0f, 1f);
    keyRt.anchoredPosition = new Vector2(-6f, 9f);
    keyRt.sizeDelta = new Vector2(52f, 20f);
    var keyBg = keyGo.AddComponent<Image>();
    keyBg.color = new Color(0.91f, 0.86f, 0.76f, 1f);
    keyBg.raycastTarget = false;
    TMP_Text key = CreateTmpText("KeyText", keyGo.transform, "", 13f, TextAlignmentOptions.Center,
        Vector2.zero, new Vector2(52f, 20f));
    key.color = new Color(0.13f, 0.12f, 0.1f, 1f);
    key.fontStyle = FontStyles.Bold;

    // 지속 잔여 바 — 하단
    var barGo = new GameObject("ActiveBar", typeof(RectTransform));
    barGo.transform.SetParent(go.transform, false);
    var barRt = (RectTransform)barGo.transform;
    barRt.anchorMin = new Vector2(0f, 0f); barRt.anchorMax = new Vector2(1f, 0f);
    barRt.pivot = new Vector2(0.5f, 0f);
    barRt.anchoredPosition = new Vector2(0f, 6f);
    barRt.sizeDelta = new Vector2(-12f, 5f);
    var bar = barGo.AddComponent<Image>();
    bar.color = new Color(1f, 0.82f, 0.48f, 1f);
    bar.type = Image.Type.Filled; bar.fillMethod = Image.FillMethod.Horizontal; bar.fillOrigin = 0;
    bar.raycastTarget = false;
    barGo.SetActive(false);

    var view = go.AddComponent<CTC.Runtime.UI.AbilitySlotView>();
    SetRef(view, "m_Frame", frame);
    SetRef(view, "m_Background", bg);
    SetRef(view, "m_Cover", cover);
    SetRef(view, "m_ActiveBar", bar);
    SetRef(view, "m_KeyText", key);
    SetRef(view, "m_NameText", name);
    SetRef(view, "m_SecondsText", seconds);
    SetRef(view, "m_SubText", sub);
    SetRef(view, "m_Group", group);
    return view;
}

private static Image FullRectImage(Transform _parent, string _name, float _inset, Color _color)
{
    var go = new GameObject(_name, typeof(RectTransform));
    go.transform.SetParent(_parent, false);
    var rt = (RectTransform)go.transform;
    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
    rt.offsetMin = new Vector2(_inset, _inset); rt.offsetMax = new Vector2(-_inset, -_inset);
    var img = go.AddComponent<Image>();
    img.color = _color; img.raycastTarget = false;
    return img;
}
```
> `키캡`을 슬롯 밖으로 6px 내밀기 위해 앵커 좌상단·anchoredPosition(-6, 9). Cover의 `Origin360.Top`+clockwise: fillAmount가 줄어들며 시계방향으로 걷힌다(시안).

- [ ] **Step 2: ControlsHud.cs + .meta 삭제.** MapInfoHud.cs:15 주석 `ControlsHud가 조작키로` → `능력 바(AbilityBar)·핫바가`. ChatHud.cs:48 주석 `LobbyUI·ControlsHud` → `LobbyUI·AbilityBar`.

- [ ] **Step 3: TestManager.Hud.cs**

```csharp
/// <summary>QA: 능력 바 — 슬롯마다 "키:이름:상태:남은초(올림)"를 '|'로 잇는다. 바가 꺼져 있으면 빈 문자열.</summary>
public string TestGetAbilityBar()
{
    var bar = FindFirstObjectByType<AbilityBar>();
    if (bar == null) return "";
    var views = bar.GetComponentsInChildren<AbilitySlotView>(false);
    var sb = new System.Text.StringBuilder();
    foreach (var v in views)
    {
        if (sb.Length > 0) sb.Append('|');
        sb.Append(v.Key).Append(':').Append(v.Name).Append(':').Append(v.State)
          .Append(':').Append(Mathf.CeilToInt(v.SecondsShown));
    }
    return sb.ToString();
}
```
`TestGetControlsText` 삭제.

- [ ] **Step 4:** `u_editor_asset refresh` → 컴파일 0 에러 → `Tools/CTC/Setup Networking Scene` → `Tools/CTC/Run EditMode Tests` PASS.
- [ ] **Step 5: Commit** `feat(hud): 능력 바 빌더 — 핫바 왼쪽 84px 슬롯 2칸 · ControlsHud 삭제 · TestGetAbilityBar`

---

### Task 7: 실플레이 QA (qa_v45 갱신 + 스크린샷)

**Files:** `tools/wesqa/tests/qa_v45.py:40-59`

- [ ] **Step 1: qa_v45 §조작 안내 교체**

```python
    # ---------- 능력 바 (2026-08-17: 좌하단 칩 → 핫바 왼쪽 슬롯) ----------
    mouse_bar = m1.invoke("TestGetAbilityBar")
    cat_bar = cat.invoke("TestGetAbilityBar")
    check("능력 바 — 쥐는 대시·임명 두 칸", mouse_bar.count("|") == 1 and "Space:대시" in mouse_bar and "F:임명" in mouse_bar, mouse_bar)
    check("능력 바 — 대시 미보유는 Locked", "Space:대시:Locked" in mouse_bar, mouse_bar)
    check("능력 바 — 깡패가 아니면 임명 Locked", "F:임명:Locked" in mouse_bar, mouse_bar)
    check("능력 바 — 고양이는 스킬 한 칸", "|" not in cat_bar and "Q:" in cat_bar, cat_bar)
```
직업 HUD 절의 `"안내 — 깡패가 되면 F 안내가 붙는다"` 줄을
```python
    check("능력 바 — 깡패가 되면 임명이 열린다", "F:임명:Ready" in m1.invoke("TestGetAbilityBar"), m1.invoke("TestGetAbilityBar"))
```
qa_v45 뒤쪽에 스킬 복제 단언(고양이가 **호스트가 아닌** 스위트가 있으면 거기, 아니면 v37에 추가):
```python
    # 클라 고양이도 스킬 쿨다운을 본다(2026-08-17 복제)
    cat.invoke("TestSetCatClass", _class="Dark")   # 기존 훅 이름 확인 후 사용
    cat.invoke("TestUseCatSkill")                   # 기존 훅 이름 확인 후 사용
    time.sleep(0.5)
    bar = cat.invoke("TestGetAbilityBar")
    check("능력 바 — 스킬 사용 직후 Active", ":Active:" in bar, bar)
```

- [ ] **Step 2:** 클론 재시작 → 씬 로드 로그 확인 → 플레이 → `python tools/wesqa/tests/qa_v45.py` 전 항목 PASS. `qa_v36.py`도 회귀(대시 키 훅은 DevDash라 영향 없음).
- [ ] **Step 3:** 스크린샷(쥐·고양이 각 1장) — 핫바 왼쪽 정렬, 겹침 없음, 글자 크기. `u_screenshot`.
- [ ] **Step 4: Commit** `test(qa): qa_v45 능력 바 단언 — 대시·임명·스킬 슬롯 상태`

---

### Task 8: 문서

- [ ] `docs/design-vault/규칙과-현황.html`: 키 표 `LCtrl`→`LShift`(잠행), `LShift`→`Space`(대시); 대시 행 `<kbd>Space</kbd>`; 잠행 문단 `<kbd>LShift</kbd>`; HUD 절에 "능력 바(핫바 왼쪽) — 쿨다운 덮개·남은 초·지속 바" 한 줄.
- [ ] `docs/design-vault/코드 다이어그램.md`: `ControlsHud` 클래스 → `AbilityBar`·`AbilitySlotView`·`AbilityBarLogic`; 관계 `AbilityBar --> MouseDash/CatSkill/MouseJob/CatClass`.
- [ ] `.claude/handoff-active.md`: 표에 "능력 바" 행 + 키 변경.
- [ ] Commit `docs: 능력 바·키 변경 반영(규칙 페이지·코드 다이어그램·핸드오프)` · push.
