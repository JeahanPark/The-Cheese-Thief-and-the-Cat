# 쥐 상점 팝업 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 쥐가 상점 원 안에서 E를 눌러 파는 물건 10종을 한 화면에 보고 클릭해서 사도록 바꾸고, 핫바에서는 구매를 걷어낸다.

**Architecture:** 판정은 순수 static `ShopPopupLogic`이 하고, `ShopPopup`(View+Controller, `CatClassPopup`과 같은 형태)이 그리고 클릭을 서버로 넘긴다. 서버 재검증은 기존 `ShopController.BuyRpc`가 그대로 한다. 커서 해제는 `UiFocus.SetModalOpen(true)` 한 줄로 기존 `CursorPolicyLogic` 경로를 탄다.

**Tech Stack:** Unity 6000.5.2f1 · UGUI + TextMeshPro · R3(구독) · UniTask · NGO · EditMode(NUnit) + wesqa(Python)

**기획서:** `docs/superpowers/specs/2026-09-19-shop-popup-design.md`

---

## 시작 전 확인

- [ ] **선행 상태 확인**

이 계획은 **단판제 변경(2026-09-15)이 커밋되지 않은 작업 트리 위**에서 시작한다.
`git status`에 `MatchRoomReturn.cs`가 보이면 정상이다. 씬 재생성(`Tools/CTC/Setup Networking Scene`)이
아직 안 돌았으므로 **Task 4에서 한 번에 돌린다**(단판제의 씬 반영도 그때 같이 들어간다).

MCP 브리지가 응답하지 않으면 유니티 에디터를 재시작한다(CLAUDE.md 함정 18).

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Economy/ShopPopupLogic.cs` (신규) | 순수 판정 — 카드 상태, 열림 조건 |
| `Assets/Scripts/UI/ShopPopup.cs` (신규) | View+Controller — 카드를 그리고 클릭을 `ShopController.Buy`로 넘긴다 |
| `Assets/Scripts/Editor/Setup/LobbyCanvasBuilder.cs` (수정) | 팝업 UI를 씬에 굽는다 |
| `Assets/Scripts/Core/EscapeRouter.cs` (수정) | ESC 사슬에 팝업 닫기를 끼우고, Enter 확정 구매를 걷는다 |
| `Assets/Scripts/Economy/ShopHotbar.cs` (수정) | 쥐 핫바에서 구매 경로 제거 — 칸 = 보유 기술 |
| `Assets/Scripts/Economy/TechLogic.cs` (수정) | `NextCandidateIndex` 제거 |
| `Assets/Scripts/Core/EnterRouteLogic.cs` (수정) | Enter = 채팅만 |
| `Assets/Scripts/Testing/TestManager.Tech.cs` (수정) | QA 훅 3종 |
| `Assets/Scripts/Tests/EditMode/ShopPopupLogicTests.cs` (신규) | 판정 전수 |
| `tools/wesqa/tests/qa_v59.py` (신규) | 실플레이 — 열고 사고 맞고 닫힌다 |
| `Assets/Scripts/Tests/EditMode/QaCases.cs` (수정) | 케이스 등록 |

---

### Task 1: 카드 상태 판정 (순수 함수)

**Files:**
- Create: `Assets/Scripts/Economy/ShopPopupLogic.cs`
- Test: `Assets/Scripts/Tests/EditMode/ShopPopupLogicTests.cs`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Scripts/Tests/EditMode/ShopPopupLogicTests.cs`:

```csharp
using System.Collections.Generic;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>상점 팝업(2026-09-19) — 카드 한 장의 상태와 창이 열리는 조건.</summary>
    public class ShopPopupLogicTests
    {
        private static List<TechEntry> Owned(params (TechId id, int level)[] _entries)
        {
            var list = new List<TechEntry>();
            foreach ((TechId id, int level) in _entries) list.Add(new TechEntry { Id = id, Level = level });
            return list;
        }

        [NUnit.Framework.Property("CaseId", "C-33")]
        [Test]
        public void Card_Buyable_WhenAffordableAndSlotLeft()
        {
            ShopPopupCard card = ShopPopupLogic.CardOf(
                Owned(), TechId.Dash, RoleId.Mouse, _cheese: 500, _slotCap: 5, _soldOut: null, _clubActive: false);

            Assert.AreEqual(TechDeny.None, card.Deny);
            Assert.IsTrue(card.CanBuy);
            Assert.AreEqual(1, card.NextLevel);
            Assert.Greater(card.Cost, 0);
        }

        [NUnit.Framework.Property("CaseId", "C-33")]
        [Test]
        public void Card_NotEnough_TellsHowMuchIsMissing()
        {
            ShopPopupCard card = ShopPopupLogic.CardOf(
                Owned(), TechId.Dash, RoleId.Mouse, _cheese: 40, _slotCap: 5, _soldOut: null, _clubActive: false);

            Assert.AreEqual(TechDeny.NotEnough, card.Deny);
            Assert.IsFalse(card.CanBuy);
            Assert.AreEqual(card.Cost - 40, card.Short);
        }

        [NUnit.Framework.Property("CaseId", "C-33")]
        [Test]
        public void Card_SlotsFull_OnlyBlocksNewTechs()
        {
            List<TechEntry> owned = Owned(
                (TechId.Gun, 1), (TechId.Dash, 1), (TechId.Fortress, 1),
                (TechId.Warehouse, 1), (TechId.Sticky, 1));

            ShopPopupCard fresh = ShopPopupLogic.CardOf(
                owned, TechId.FastFeet, RoleId.Mouse, 9999, 5, null, false);
            ShopPopupCard upgrade = ShopPopupLogic.CardOf(
                owned, TechId.Dash, RoleId.Mouse, 9999, 5, null, false);

            Assert.AreEqual(TechDeny.SlotsFull, fresh.Deny);
            Assert.AreEqual(TechDeny.None, upgrade.Deny, "가진 것을 키우는 건 칸을 새로 안 쓴다");
        }

        [NUnit.Framework.Property("CaseId", "C-33")]
        [Test]
        public void Card_SoldOut_AndMaxLevel_AreTheirOwnReasons()
        {
            var soldOut = new List<TechId> { TechId.Sticky };

            Assert.AreEqual(TechDeny.SoldOut, ShopPopupLogic
                .CardOf(Owned(), TechId.Sticky, RoleId.Mouse, 9999, 5, soldOut, false).Deny);
            Assert.AreEqual(TechDeny.MaxLevel, ShopPopupLogic
                .CardOf(Owned((TechId.Dash, 3)), TechId.Dash, RoleId.Mouse, 9999, 5, null, false).Deny);
        }

        [NUnit.Framework.Property("CaseId", "C-33")]
        [Test]
        public void Card_GangCannotBuyGunTech()
        {
            ShopPopupCard gun = ShopPopupLogic.CardOf(
                Owned(), TechId.Gun, RoleId.Mouse, 9999, 5, null, _clubActive: true);
            ShopPopupCard dash = ShopPopupLogic.CardOf(
                Owned(), TechId.Dash, RoleId.Mouse, 9999, 5, null, _clubActive: true);

            Assert.AreEqual(TechDeny.GangNoGun, gun.Deny);
            Assert.AreEqual(TechDeny.None, dash.Deny, "깡패도 대시는 산다");
        }

        [NUnit.Framework.Property("CaseId", "C-34")]
        [Test]
        public void CanOpen_NeedsShopSpotAndAliveAndFreeHands()
        {
            Assert.IsTrue(ShopPopupLogic.CanOpen(
                _role: RoleId.Mouse, _inShop: true, _dead: false, _locked: false, _typing: false));
            Assert.IsFalse(ShopPopupLogic.CanOpen(RoleId.Mouse, false, false, false, false), "원 밖");
            Assert.IsFalse(ShopPopupLogic.CanOpen(RoleId.Mouse, true, true, false, false), "죽었다");
            Assert.IsFalse(ShopPopupLogic.CanOpen(RoleId.Mouse, true, false, true, false), "변신 중");
            Assert.IsFalse(ShopPopupLogic.CanOpen(RoleId.Mouse, true, false, false, true), "채팅 중");
            Assert.IsFalse(ShopPopupLogic.CanOpen(RoleId.Cat, true, false, false, false), "고양이는 핫바로 산다");
        }

        [NUnit.Framework.Property("CaseId", "C-34")]
        [Test]
        public void ShouldClose_OnDamageDeathEndOrLeavingTheCircle()
        {
            Assert.IsTrue(ShopPopupLogic.ShouldClose(_inShop: true, _dead: false, _hurt: true, _locked: false, _matchOver: false));
            Assert.IsTrue(ShopPopupLogic.ShouldClose(true, true, false, false, false), "죽음");
            Assert.IsTrue(ShopPopupLogic.ShouldClose(false, false, false, false, false), "원 밖으로 밀려남");
            Assert.IsTrue(ShopPopupLogic.ShouldClose(true, false, false, true, false), "깡패 변신");
            Assert.IsTrue(ShopPopupLogic.ShouldClose(true, false, false, false, true), "판 종료");
            Assert.IsFalse(ShopPopupLogic.ShouldClose(true, false, false, false, false), "평온하면 안 닫는다");
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

메뉴 `Tools/CTC/Run EditMode Tests` 실행(`u_editor_menu`). `Logs/Editor.log`에 컴파일 에러
`ShopPopupLogic을 찾을 수 없습니다`가 뜨면 정상이다 — 아직 없는 클래스다.

- [ ] **Step 3: 최소 구현을 쓴다**

`Assets/Scripts/Economy/ShopPopupLogic.cs`:

```csharp
using System.Collections.Generic;
using CTC.Runtime.Lobby;

namespace CTC.Runtime.Economy
{
    /// <summary>팝업에 그릴 카드 한 장 — 이름·가격은 View가 CSV에서 읽고, 여기는 상태만 준다.</summary>
    public struct ShopPopupCard
    {
        public TechId Id;
        public int OwnedLevel;
        public int NextLevel;
        public int Cost;
        public TechDeny Deny;
        /// <summary>모자란 치즈(모자라지 않으면 0) — 문구에 그대로 들어간다.</summary>
        public int Short;

        public bool CanBuy => Deny == TechDeny.None;
    }

    /// <summary>
    /// 상점 팝업 판정 (2026-09-19). **표시용**이다 — 진짜 판정은 서버(`ShopController.BuyRpc`)가 한다.
    /// 여기서 막지 않고 회색으로 보여 주기만 하므로, 서버와 어긋나도 치즈가 새지 않는다.
    /// </summary>
    public static class ShopPopupLogic
    {
        /// <summary>카드 한 장의 상태. 거절 사유는 <see cref="TechLogic.CanBuy"/>와 같은 규칙을 쓴다.</summary>
        public static ShopPopupCard CardOf(IReadOnlyList<TechEntry> _owned, TechId _id, RoleId _role,
            int _cheese, int _slotCap, IReadOnlyList<TechId> _soldOut, bool _clubActive)
        {
            int level = TechLogic.LevelOf(_owned, _id);
            TechRow next = TechLogic.NextRow(_owned, _id);

            TechDeny deny = _clubActive && TechLogic.IsGunTech(_id)
                ? TechDeny.GangNoGun
                : TechLogic.CanBuy(_owned, _id, _role, _cheese, _slotCap, _soldOut);

            return new ShopPopupCard
            {
                Id = _id,
                OwnedLevel = level,
                NextLevel = next.IsValid ? next.Level : 0,
                Cost = next.IsValid ? next.Cost : 0,
                Deny = deny,
                Short = deny == TechDeny.NotEnough && next.IsValid ? next.Cost - _cheese : 0,
            };
        }

        /// <summary>창을 열 수 있는가 — 상점 원 안의 살아 있는 쥐만, 손이 비어 있을 때.</summary>
        public static bool CanOpen(RoleId _role, bool _inShop, bool _dead, bool _locked, bool _typing)
        {
            if (!TechLogic.NeedsShopSpot(_role)) return false;   // 고양이는 핫바로 산다
            return _inShop && !_dead && !_locked && !_typing;
        }

        /// <summary>열려 있는 창을 강제로 닫아야 하는가.</summary>
        public static bool ShouldClose(bool _inShop, bool _dead, bool _hurt, bool _locked, bool _matchOver)
        {
            return !_inShop || _dead || _hurt || _locked || _matchOver;
        }
    }
}
```

- [ ] **Step 4: 통과를 확인한다**

`Tools/CTC/Run EditMode Tests` 재실행. `Logs/Editor.log` 마지막 줄이
`[EditModeTests] 완료 — Passed: <이전보다 7 증가>, Failed: 0`이어야 한다.
**통과 수가 늘었는지 반드시 본다**(CLAUDE.md 함정 16 — 컴파일 전 어셈블리로 돌면 옛 결과가 나온다).

- [ ] **Step 5: 커밋**

```bash
git add CTC/Assets/Scripts/Economy/ShopPopupLogic.cs CTC/Assets/Scripts/Tests/EditMode/ShopPopupLogicTests.cs
git commit -m "feat(shop): 상점 팝업 판정 — 카드 상태·열림 조건 순수 함수"
```

---

### Task 2: 팝업 컴포넌트 (View + Controller)

**Files:**
- Create: `Assets/Scripts/UI/ShopPopup.cs`

- [ ] **Step 1: 컴포넌트를 쓴다**

`CatClassPopup`과 같은 형태다 — 캔버스에 구워진 패널 하나를 켜고 끈다(Addressables 아님).

```csharp
using System.Collections.Generic;
using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Map;
using CTC.Runtime.Player;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 쥐 상점 팝업 (2026-09-19). 상점 원 안에서 **E**로 열고, 파는 물건 10종을 한 화면에 깔아
    /// **클릭 한 번**으로 산다. 핫바에서 구매를 걷어낸 자리를 대신한다.
    ///
    /// `CatClassPopup`과 같은 규약이다 — 캔버스에 구워진 패널을 켜고 끄고,
    /// `UiFocus.SetModalOpen`으로 커서를 푼다(`CursorPolicyLogic`이 그걸 보고 잠금을 푼다).
    ///
    /// **맞으면 닫힌다**(기획 §5). 상점은 안전지대가 아니다 — 팝업을 연 동안에도 발소리는 들린다.
    /// </summary>
    public class ShopPopup : MonoBehaviour
    {
        [SerializeField] private GameObject m_Root;
        [SerializeField] private TMP_Text m_Header;
        [SerializeField] private Button[] m_Cards;
        [SerializeField] private TMP_Text[] m_CardTitles;
        [SerializeField] private TMP_Text[] m_CardHints;
        [SerializeField] private Match.MatchManager m_Match;

        private readonly SerialDisposable m_Binding = new SerialDisposable();
        private readonly List<TechId> m_Catalog = new List<TechId>();

        private ShopController m_Shop;
        private TechSlots m_Slots;
        private CheeseWallet m_Wallet;
        private Health m_Health;
        private MousePopgun m_Popgun;
        private int m_LastHp;

        /// <summary>떠 있는가 — QA가 눈이 아니라 값으로 잰다.</summary>
        public bool IsOpen => m_Root != null && m_Root.activeSelf;

        private void Start()
        {
            if (m_Root != null) m_Root.SetActive(false);

            for (int i = 0; i < m_Cards.Length; i++)
            {
                int index = i;
                m_Cards[i].OnClickAsObservable()
                    .Subscribe(_ => OnClickCard(index))
                    .AddTo(this);
            }

            LocalPlayerBinder.LocalPlayer.Subscribe(Rebind).AddTo(this);
            m_Binding.AddTo(this);
        }

        private void Rebind(NetworkObject _player)
        {
            m_Binding.Disposable = null;
            Close();

            m_Shop = _player != null ? _player.GetComponent<ShopController>() : null;
            m_Slots = _player != null ? _player.GetComponent<TechSlots>() : null;
            m_Wallet = _player != null ? _player.GetComponent<CheeseWallet>() : null;
            m_Health = _player != null ? _player.GetComponent<Health>() : null;
            m_Popgun = _player != null ? _player.GetComponent<MousePopgun>() : null;
            m_LastHp = m_Health != null ? m_Health.Hp : 0;

            if (m_Slots == null || m_Wallet == null) return;

            // 보유·지갑이 바뀌면 떠 있는 창을 다시 그린다(규칙 3 — 폴링 금지).
            m_Binding.Disposable = Observable
                .Merge(m_Slots.Changed, m_Wallet.CheeseChanged.Select(_ => Unit.Default))
                .Subscribe(_ => { if (IsOpen) Render(); });
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) OnPressE();

            if (!IsOpen) return;

            // 맞았는가 — HP가 줄어든 그 프레임에 닫는다.
            bool hurt = m_Health != null && m_Health.Hp < m_LastHp;
            if (m_Health != null) m_LastHp = m_Health.Hp;

            if (ShopPopupLogic.ShouldClose(InShop(), Dead(), hurt, Locked(), MatchOver())) Close();
        }

        /// <summary>E 한 번 — 열거나 닫는다. QA도 이 길로 부른다.</summary>
        public void OnPressE()
        {
            if (IsOpen) { Close(); return; }
            if (UiFocus.IsTyping) return;

            RoleId role = m_Shop != null ? m_Shop.GetComponent<RoleTag>().Role : RoleId.None;
            if (!ShopPopupLogic.CanOpen(role, InShop(), Dead(), Locked(), UiFocus.IsTyping)) return;

            Open();
        }

        private void Open()
        {
            if (m_Root == null) return;
            m_Root.SetActive(true);
            UiFocus.SetModalOpen(true);
            Render();
        }

        /// <summary>닫는다. **모달을 반드시 푼다** — 안 풀면 커서가 풀린 채 조작이 굳는다.</summary>
        public void Close()
        {
            if (m_Root == null || !m_Root.activeSelf) return;
            m_Root.SetActive(false);
            UiFocus.SetModalOpen(false);
        }

        private void Render()
        {
            if (m_Slots == null || m_Wallet == null) return;

            RoleId role = m_Shop.GetComponent<RoleTag>().Role;
            int slotCap = TechLogic.SlotCapOf(role);

            m_Catalog.Clear();
            foreach (TechId id in TechLogic.Catalog(role)) m_Catalog.Add(id);

            m_Header.text = $"상점 — {ShopLogic.CurrencyNameOf(role)} {m_Wallet.CheeseValue} · 칸 {m_Slots.Count}/{slotCap}";

            for (int i = 0; i < m_Cards.Length; i++)
            {
                bool used = i < m_Catalog.Count;
                m_Cards[i].gameObject.SetActive(used);
                if (!used) continue;

                ShopPopupCard card = ShopPopupLogic.CardOf(
                    m_Slots.Owned, m_Catalog[i], role, m_Wallet.CheeseValue, slotCap,
                    m_Shop.SoldOutToday, m_Popgun != null && m_Popgun.ClubActive);

                TechRow row = TechLogic.RowOf(card.Id, card.NextLevel > 0 ? card.NextLevel : 1);
                string dots = new string('●', card.OwnedLevel) + new string('○', 3 - card.OwnedLevel);

                m_CardTitles[i].text = card.CanBuy || card.Deny != TechDeny.MaxLevel
                    ? $"{row.Label}  {card.Cost}"
                    : $"{row.Label}  만렙";
                m_CardHints[i].text = card.CanBuy
                    ? $"{dots}  {row.Hint}"
                    : $"{dots}  {TechLogic.DenyText(card.Deny, role, card.Short, slotCap)}";

                // 못 사는 카드는 흐리게 — 클릭은 막지 않는다(서버가 진짜 판정이다).
                m_Cards[i].GetComponent<Image>().color = card.CanBuy ? UiPalette.PanelSolid : UiPalette.PanelDim;
            }
        }

        private void OnClickCard(int _index)
        {
            if (m_Shop == null || _index >= m_Catalog.Count) return;
            m_Shop.Buy(m_Catalog[_index]);
        }

        private bool InShop() => m_Shop != null && ShopSpot.AnyContains(m_Shop.transform.position);
        private bool Dead() => m_Health == null || m_Health.IsDead;
        private bool Locked() => m_Shop != null && GangTransformLock.Locked(m_Shop);
        private bool MatchOver() => m_Match != null && m_Match.Result != Match.MatchResult.None;
    }
}
```

- [ ] **Step 2: 컴파일을 확인한다**

`u_editor_asset(refresh)` 후 `Logs/Editor.log`에 `error CS`가 없어야 한다.
`UiPalette.PanelDim`이 없다면 `Assets/Scripts/UI/UiPalette.cs`에 추가한다:

```csharp
/// <summary>못 사는 카드 — 살 수 있는 카드(PanelSolid)보다 어둡다.</summary>
public static readonly Color PanelDim = new Color(0.12f, 0.13f, 0.16f, 0.75f);
```

- [ ] **Step 3: 커밋**

```bash
git add CTC/Assets/Scripts/UI/ShopPopup.cs CTC/Assets/Scripts/UI/UiPalette.cs
git commit -m "feat(shop): 상점 팝업 컴포넌트 — E로 열고 클릭으로 산다"
```

---

### Task 3: 팝업을 씬에 굽는다

**Files:**
- Modify: `Assets/Scripts/Editor/Setup/LobbyCanvasBuilder.cs` (`BuildCatClassPopup` 아래에 추가)

- [ ] **Step 1: 빌더 함수를 추가한다**

`BuildCatClassPopup(canvasGo);` 바로 다음 줄에 `BuildShopPopup(canvasGo);`를 넣고, 아래 함수를 같은 파일에 추가한다.

```csharp
        // 상점 팝업(2026-09-19) — 카드 10장을 5줄(축)로 깐다. 클릭으로 사므로 버튼이 곧 카드다.
        private static void BuildShopPopup(GameObject _canvasGo)
        {
            var panel = new GameObject("ShopPanel", typeof(RectTransform));
            panel.transform.SetParent(_canvasGo.transform, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(880f, 520f);
            panel.AddComponent<Image>().color = UiPalette.PanelSolid;

            TMP_Text header = CreateTmpText("ShopHeader", panel.transform, "상점",
                28f, TextAlignmentOptions.Center, new Vector2(0f, -18f), new Vector2(840f, 40f));
            header.fontStyle = FontStyles.Bold;

            const int COUNT = 10;
            var cards = new Button[COUNT];
            var titles = new TMP_Text[COUNT];
            var hints = new TMP_Text[COUNT];

            for (int i = 0; i < COUNT; i++)
            {
                float x = -210f + (i % 2) * 420f;
                float y = -110f - (i / 2) * 84f;
                Button button = CreatePanelButton(panel.transform, $"ShopCard{i}", "", new Vector2(x, y), 400f);
                var buttonRect = (RectTransform)button.transform;
                buttonRect.sizeDelta = new Vector2(400f, 72f);

                // 버튼이 기본으로 만든 글자는 제목으로 쓰고, 힌트 줄을 하나 더 얹는다.
                titles[i] = button.GetComponentInChildren<TMP_Text>();
                titles[i].alignment = TextAlignmentOptions.TopLeft;
                titles[i].margin = new Vector4(14f, 6f, 14f, 0f);

                hints[i] = CreateTmpText($"ShopCardHint{i}", button.transform, "",
                    16f, TextAlignmentOptions.BottomLeft, new Vector2(0f, -12f), new Vector2(372f, 30f));
                hints[i].color = UiPalette.TextDim;

                cards[i] = button;
            }

            TMP_Text foot = CreateTmpText("ShopFoot", panel.transform, "E · ESC 닫기",
                16f, TextAlignmentOptions.Center, new Vector2(0f, -486f), new Vector2(840f, 26f));
            foot.color = UiPalette.TextDim;

            var popup = _canvasGo.AddComponent<CTC.Runtime.UI.ShopPopup>();
            SetRef(popup, "m_Root", panel);
            SetRef(popup, "m_Header", header);
            SetRefArray(popup, "m_Cards", cards);
            SetRefArray(popup, "m_CardTitles", titles);
            SetRefArray(popup, "m_CardHints", hints);

            panel.SetActive(false);
        }
```

`BuildLobbyCanvas`가 `MatchManager _match`를 이미 받으므로, `BuildShopPopup(canvasGo);` 대신
`BuildShopPopup(canvasGo, _match);`로 넘기고 함수 시그니처에 `MatchManager _match`를 더한 뒤
`SetRef(popup, "m_Match", _match);` 한 줄을 추가한다.

- [ ] **Step 2: 씬을 다시 굽는다**

`u_editor_menu`로 `Tools/CTC/Setup Networking Scene` 실행 → 끝나면 `Logs/Editor.log`에 `error` 0.
**단판제 변경(MatchRoomReturn)도 이때 씬에 반영된다.**

- [ ] **Step 3: 눈으로 한 번 본다**

`u_play(control, enter)` → `TestFrontDoorHost` 없이 `Game.unity` 단독으로 플레이 →
상점 원 안으로 이동(`TestTeleportLocalPlayer`로 상점 좌표 근처) → `u_play invoke`로
`TestManager.TestOpenShopPopup` (Task 5에서 추가) 대신 이 단계에서는 `u_screenshot`으로
패널이 화면 가운데 뜨는지만 본다. 글자가 겹치거나 카드가 패널 밖으로 나가면 좌표를 고친다.

- [ ] **Step 4: 커밋**

```bash
git add CTC/Assets/Scripts/Editor/Setup/LobbyCanvasBuilder.cs CTC/Assets/Scenes/Game.unity
git commit -m "feat(shop): 상점 팝업 UI를 씬에 굽는다"
```

---

### Task 4: ESC 사슬과 Enter 정리

**Files:**
- Modify: `Assets/Scripts/Core/EscapeRouter.cs`
- Modify: `Assets/Scripts/Core/EnterRouteLogic.cs`
- Test: `Assets/Scripts/Tests/EditMode/EnterRouteLogicTests.cs` (기존 파일 수정)

- [ ] **Step 1: Enter 경로 테스트를 고친다**

`EnterRouteLogicTests.cs`에서 `ConfirmBuy`를 기대하는 테스트를 지우고, 남길 규칙만 둔다:

```csharp
        [Test]
        public void Route_ChatOpen_Submits_OtherwiseOpensChat()
        {
            Assert.AreEqual(EnterRoute.SubmitChat, EnterRouteLogic.Route(_chatOpen: true));
            Assert.AreEqual(EnterRoute.OpenChat, EnterRouteLogic.Route(_chatOpen: false));
        }
```

- [ ] **Step 2: 실패를 확인한다**

`Tools/CTC/Run EditMode Tests` — `EnterRouteLogic.Route`가 인자 2개를 받으므로 컴파일 에러가 난다.

- [ ] **Step 3: 구현을 고친다**

`EnterRouteLogic.cs`: `ConfirmBuy` 열거값과 `_canConfirm` 인자를 지운다.

```csharp
    /// <summary>Enter 한 번이 갈 곳. 확정 구매는 2026-09-19 상점 팝업으로 옮겼다 — 채팅뿐이다.</summary>
    public enum EnterRoute { OpenChat = 0, SubmitChat = 1 }

    public static class EnterRouteLogic
    {
        public static EnterRoute Route(bool _chatOpen) => _chatOpen ? EnterRoute.SubmitChat : EnterRoute.OpenChat;
    }
```

`EscapeRouter.cs`: `HandleEnter`에서 `ConfirmBuy` 갈래를 걷고, `HandleEscape`의 **배치 취소 다음**에
상점 팝업 닫기를 끼운다.

```csharp
        [SerializeField] private UI.ShopPopup m_ShopPopup;
```

```csharp
        private void HandleEscape()
        {
            if (m_Hotbar != null && m_Hotbar.IsPlacing)
            {
                m_Hotbar.CancelPlacementFromEscape();
                return;
            }

            // 상점 팝업 — 채팅보다 앞이다. 창을 연 채로는 채팅을 못 여므로 겹칠 일이 없다.
            if (m_ShopPopup != null && m_ShopPopup.IsOpen)
            {
                m_ShopPopup.Close();
                return;
            }
```

`LobbyCanvasBuilder.BuildOptionsPopup` 근처에서 `EscapeRouter`에 참조를 꽂는다:
`SetRef(escapeRouter, "m_ShopPopup", popup);` (Task 3에서 만든 `ShopPopup` 인스턴스).

- [ ] **Step 4: 통과를 확인한다**

`Tools/CTC/Run EditMode Tests` — Failed: 0.

- [ ] **Step 5: 커밋**

```bash
git add CTC/Assets/Scripts/Core/EscapeRouter.cs CTC/Assets/Scripts/Core/EnterRouteLogic.cs CTC/Assets/Scripts/Tests/EditMode/EnterRouteLogicTests.cs CTC/Assets/Scripts/Editor/Setup/LobbyCanvasBuilder.cs
git commit -m "refactor(shop): ESC 사슬에 상점 팝업 · Enter는 채팅만"
```

---

### Task 5: 핫바에서 구매를 걷어낸다

**Files:**
- Modify: `Assets/Scripts/Economy/ShopHotbar.cs`
- Modify: `Assets/Scripts/Economy/TechLogic.cs`
- Test: `Assets/Scripts/Tests/EditMode/TechLogicTests.cs` · `ShopHotbarLogicTests.cs`(있으면)

- [ ] **Step 1: 사라질 규칙의 테스트를 먼저 지운다**

`NextCandidateIndex`를 검사하는 테스트를 지우고, 대신 「쥐 핫바는 보유 기술만 담는다」를 쓴다:

```csharp
        [NUnit.Framework.Property("CaseId", "C-35")]
        [Test]
        public void MouseHotbar_HoldsOwnedTechsOnly_NoBuyCandidates()
        {
            Assert.IsFalse(TechLogic.FixedSlots(RoleId.Mouse), "쥐 칸은 여전히 보유 순서대로 채워진다");
            Assert.IsTrue(TechLogic.FixedSlots(RoleId.Cat), "고양이는 1키 1기술 고정이다");
        }
```

- [ ] **Step 2: `ShopHotbar`에서 구매 경로를 걷는다**

지울 것: `m_InShop` 분기 중 **구매 쪽**, 후보 계산(`Candidates`·`NextCandidateIndex` 호출),
`CanConfirmBuy`·`TryConfirmBuy`, 모드 문구(`숫자키 = 산다 / 키운다`).
남길 것: 보유 기술 칸 채우기, 숫자키 = 쓴다, 설치 고스트·취소, 고양이의 고정 칸 구매(그대로).

`PressSlot`의 쥐 갈래는 이렇게 단순해진다:

```csharp
            // 쥐: 칸 = 보유 기술. 상점 안팎과 무관하게 **언제나 쓴다**(2026-09-19 — 구매는 팝업으로 갔다).
            if (_slot < owned.Count) Use(owned[_slot].Id);
```

- [ ] **Step 3: `TechLogic.NextCandidateIndex`를 지운다**

쓰는 곳이 `ShopHotbar` 하나뿐이었다. 지우고 `grep -rn "NextCandidateIndex" Assets/`로 0을 확인한다.

- [ ] **Step 4: 테스트를 돌린다**

`Tools/CTC/Run EditMode Tests` — Failed: 0. 통과 수가 줄었다면(지운 테스트만큼) 정상이다.

- [ ] **Step 5: 커밋**

```bash
git add CTC/Assets/Scripts/Economy/ShopHotbar.cs CTC/Assets/Scripts/Economy/TechLogic.cs CTC/Assets/Scripts/Tests/EditMode/
git commit -m "refactor(shop): 쥐 핫바에서 구매를 걷어낸다 — 숫자키는 언제나 쓴다"
```

---

### Task 6: QA 훅 3종

**Files:**
- Modify: `Assets/Scripts/Testing/TestManager.Tech.cs`

- [ ] **Step 1: 이름이 겹치지 않는지 본다**

```bash
grep -rn "TestOpenShopPopup\|TestGetShopPopupCards\|TestClickShopCard" CTC/Assets/Scripts/Testing/
```
아무것도 안 나와야 한다(CLAUDE.md 함정 17 — 같은 이름의 오버로드는 조용히 엉뚱한 쪽이 불린다).

- [ ] **Step 2: 훅을 쓴다**

```csharp
        /// <summary>QA: 상점 팝업을 E처럼 토글한다 → 토글 뒤에 떠 있으면 true. 팝업이 없으면 false.</summary>
        public bool TestOpenShopPopup()
        {
            var popup = FindFirstObjectByType<UI.ShopPopup>();
            if (popup == null)
            {
                GameDebug.LogWarning("[TestManager] ShopPopup 없음 — 씬을 다시 구웠는지 본다");
                return false;
            }
            popup.OnPressE();
            return popup.IsOpen;
        }

        /// <summary>QA: 팝업 카드들 — "이름:가격:살수있나"를 '|'로 잇는다. 안 떠 있으면 빈 문자열.</summary>
        public string TestGetShopPopupCards()
        {
            var popup = FindFirstObjectByType<UI.ShopPopup>();
            if (popup == null || !popup.IsOpen) return "";
            return popup.DevCardSummary();
        }

        /// <summary>QA: 팝업의 그 기술 카드를 누른다(사람의 클릭과 같은 길). 눌렀으면 true.</summary>
        public bool TestClickShopCard(string _tech)
        {
            var popup = FindFirstObjectByType<UI.ShopPopup>();
            if (popup == null || !popup.IsOpen) return false;
            return popup.DevClick(_tech);
        }
```

`ShopPopup`에 QA용 두 메서드를 더한다(클릭 경로를 그대로 탄다 — 훅이 지름길을 만들면 배선 누락을 못 잡는다):

```csharp
        /// <summary>QA 전용 — 지금 그린 카드 요약. 렌더 결과를 읽는다.</summary>
        public string DevCardSummary()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < m_Catalog.Count; i++)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append(m_Catalog[i]).Append(':').Append(m_CardTitles[i].text.Trim())
                  .Append(':').Append(m_Cards[i].GetComponent<Image>().color == UiPalette.PanelSolid);
            }
            return sb.ToString();
        }

        /// <summary>QA 전용 — 그 기술의 카드 버튼을 실제로 누른다.</summary>
        public bool DevClick(string _tech)
        {
            for (int i = 0; i < m_Catalog.Count; i++)
            {
                if (m_Catalog[i].ToString() != _tech) continue;
                m_Cards[i].onClick.Invoke();
                return true;
            }
            return false;
        }
```

- [ ] **Step 3: 파이썬 계약을 맞춘다**

```bash
cd CTC/tools/wesqa && python -m pytest -q
```
`hooks.py`가 훅 이름·인자를 정적으로 검사한다. 실패하면 거기 서명을 더한다.

- [ ] **Step 4: 커밋**

```bash
git add CTC/Assets/Scripts/Testing/TestManager.Tech.cs CTC/Assets/Scripts/UI/ShopPopup.cs CTC/tools/wesqa/
git commit -m "test(qa): 상점 팝업 훅 3종 — 열기·카드 목록·카드 클릭"
```

---

### Task 7: 실플레이 스위트 `qa_v59`

**Files:**
- Create: `tools/wesqa/tests/qa_v59.py`

- [ ] **Step 1: 스위트를 쓴다**

`qa_v58.py`를 형식의 본으로 삼는다(머리말·`EXPECTED`·`check`·`summary`). 재는 것:

```
① 상점 원 밖에서는 E를 눌러도 안 열린다
② 상점 원 안에서 E를 누르면 열리고 카드가 10장 뜬다
③ 살 수 있는 카드와 없는 카드가 갈린다(시작 치즈 65에 100짜리 대시는 회색)
④ 치즈를 채우고 카드를 누르면 실제로 사진다(TestGetTechLevel 증가 · 칸 사용량 증가)
⑤ 맞으면 창이 닫힌다(고양이가 때린 뒤 TestOpenShopPopup 전 상태가 false)
⑥ 창이 닫히면 조작이 돌아온다(이동 훅으로 좌표가 움직인다)
```

전제: 호스트=쥐, 클론 1이 고양이. `setup_match 0 mouse` 뒤 상점 좌표로 순간이동
(`TestGetShopSpots`가 이미 있다 — 없으면 Task 6에 훅을 하나 더 더한다).

- [ ] **Step 2: 돌린다**

```bash
cd CTC/tools/wesqa/tests && python qa_v59.py
```
기대: `PASS 6 · FAIL 0 · SKIP 0 · 미실행 0 / 선언 6`, 종료 코드 0.

- [ ] **Step 3: 커밋**

```bash
git add CTC/tools/wesqa/tests/qa_v59.py
git commit -m "test(qa): qa_v59 — 상점 팝업 실플레이"
```

---

### Task 8: 대장·문서 갱신

**Files:**
- Modify: `Assets/Scripts/Tests/EditMode/QaCases.cs`
- Modify: `docs/design-vault/코드 다이어그램.md`
- Modify: `docs/superpowers/plans/BACKLOG.md`

- [ ] **Step 1: 케이스를 등록한다**

C 도메인 끝에 셋을 더한다(번호는 이어서 — 재사용·밀기 금지).

```csharp
            QaCase.Of("C-33",
                "상점 팝업의 카드 상태가 잔액·칸·품절·만렙·깡패 총을 전부 구분한다",
                "못 사는 이유를 한 가지로 뭉개 사람이 왜 안 사지는지 모른다",
                QaLayer.EditMode, QaStatus.Implemented),
            QaCase.Of("C-34",
                "상점 팝업은 상점 원 안의 살아 있는 쥐만 열 수 있고, 맞으면 닫힌다",
                "커서가 풀린 채 굳거나, 상점이 무적 지대가 된다",
                QaLayer.EditMode, QaStatus.Implemented),
            QaCase.Of("C-35",
                "쥐 핫바에는 보유 기술만 올라가고 숫자키는 언제나 「쓴다」다",
                "상점 안팎으로 같은 키의 뜻이 바뀌어 살 때마다 헷갈린다",
                QaLayer.EditMode, QaStatus.Implemented),
```

도메인 머리말의 개수(`C 경제 · 테크 · UI · 데이터 (32)`)를 35로 고친다.
지금 핫바 구매를 재는 케이스(qa_v48 ⑤ 계열)의 본문을 팝업 기준으로 고친다.

- [ ] **Step 2: 대장을 다시 만든다**

`u_editor_menu`로 `Tools/CTC/Generate QA Case Ledger` → `docs/design-vault/QA 케이스 대장.md` 갱신.

- [ ] **Step 3: 코드 다이어그램을 고친다(규칙 8)**

`ShopController` 클래스 블록 근처에 `ShopPopup`·`ShopPopupLogic`을 더하고,
핫바 설명에서 「상점 안이면 산다」를 지운다.

- [ ] **Step 4: 전체 테스트**

```
Tools/CTC/Run EditMode Tests   → Failed: 0
cd CTC/tools/wesqa && python -m pytest -q   → 전부 통과
qa_v48 · qa_v59 실플레이       → rc=0
```

- [ ] **Step 5: 커밋**

```bash
git add CTC/Assets/Scripts/Tests/EditMode/QaCases.cs "CTC/docs/design-vault/QA 케이스 대장.md" "CTC/docs/design-vault/코드 다이어그램.md" CTC/docs/superpowers/plans/BACKLOG.md
git commit -m "docs: 상점 팝업 — 대장·다이어그램·대기열 갱신"
```

---

## 완료 기준

- 상점 원 안에서 E → 10종이 한 화면에, 클릭 한 번에 구매, ESC·E로 닫힘
- 맞으면 닫히고 커서가 다시 잠긴다
- 쥐 핫바에 「산다」가 없다 — 숫자키는 언제나 「쓴다」
- EditMode Failed 0 · pytest 통과 · `qa_v48`·`qa_v59` rc=0
- 고양이 조작은 그대로다(핫바 6칸 구매 · Space 광분)
