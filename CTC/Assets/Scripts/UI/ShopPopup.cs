using System.Collections.Generic;
using System.Text;
using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Map;
using CTC.Runtime.Match;
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
    /// 쥐 상점 팝업 (2026-09-19, 기획 `2026-09-19-shop-popup-design.md`).
    ///
    /// 상점 원 안에서 **E**로 연다. **왼쪽 목록 + 오른쪽 상세**(2026-09-19 시안 B — 사용자 선택):
    /// 목록은 축별(공격·방어·경제·도주·이동)로 묶이고, **마우스를 올린 줄**의 단계별 효과·가격·구매 버튼이 오른쪽에 뜬다.
    /// 줄을 누르거나 구매 버튼을 누르면 산다. 줄은 카탈로그 수만큼 그 자리에서 만들고 목록은 넘치면 스크롤된다.
    ///
    /// <see cref="CatClassPopup"/>과 같은 규약이다: 캔버스에 구워진 창을 켜고 끄고,
    /// <see cref="UiFocus.SetModalOpen"/>으로 커서를 푼다(`CursorPolicyLogic`이 그 값을 보고 잠금을 푼다).
    ///
    /// **맞으면 닫힌다.** 상점은 안전지대가 아니다 — 창을 연 동안에도 발소리는 들리므로
    /// 「듣고 닫고 도망친다」가 가능하다(사용자 결정 2026-09-19).
    /// </summary>
    public class ShopPopup : MonoBehaviour
    {
        /// <summary>상세 사다리 칸 수 — 기술의 최대 레벨(지금 전부 3). 더 긴 기술이 생기면 창을 다시 굽는다.</summary>
        public const int STEP_COUNT = 3;

        // 사다리 · 버튼 색 — 팔레트가 원본이다(UiPalette). 여기서는 역할만 붙인다.
        //
        // **테두리(Outline)를 단 칸의 바탕은 불투명해야 한다.** Outline은 그래픽을 네 방향으로 밀어 찍은 복사본이라
        // 바탕이 투명하면 테두리가 아니라 칸 전체가 선 색으로 칠해진다. 그래서 「비어 보이는」 칸은
        // 투명이 아니라 상세 판과 같은 색(Slot 0.72를 창 위에 얹은 결과)을 불투명으로 칠한다.

        /// <summary>상세 판 바탕(불투명) — 굽는 쪽(<c>BuildShopPopup</c>)도 이 값을 쓴다.</summary>
        public static readonly Color DETAIL_BG = new Color(0.052f, 0.070f, 0.103f, 1f);

        private static readonly Color STEP_NEXT_BG = new Color(0.079f, 0.103f, 0.147f, 1f);
        private static readonly Color BUTTON_IDLE_BG = UiPalette.WithAlpha(UiPalette.Slot, 1f);
        private static readonly Color STEP_BLOCKED_LINE = UiPalette.WithAlpha(UiPalette.Bad, 0.45f);
        private static readonly Color STEP_OWNED_BADGE = UiPalette.WithAlpha(UiPalette.Good, 0.16f);
        private static readonly Color CLEAR = new Color(0f, 0f, 0f, 0f);

        [SerializeField] private GameObject m_Root;
        [SerializeField] private MatchManager m_Match;

        [Header("머리")]
        [SerializeField] private TMP_Text m_CheeseValue;
        [SerializeField] private TMP_Text m_SlotText;
        [SerializeField] private Image[] m_SlotPips;

        [Header("목록")]
        [SerializeField] private RectTransform m_ListContent;
        [SerializeField] private ShopRowItem m_RowTemplate;
        [SerializeField] private TMP_Text m_GroupTemplate;

        [Header("상세")]
        [SerializeField] private TMP_Text m_DetailAxis;
        [SerializeField] private TMP_Text m_DetailTitle;
        [SerializeField] private TMP_Text m_DetailLevel;
        [SerializeField] private Image[] m_DetailBars;
        [SerializeField] private GameObject[] m_Steps;
        [SerializeField] private Image[] m_StepBackgrounds;
        [SerializeField] private Outline[] m_StepOutlines;
        [SerializeField] private Image[] m_StepBadges;
        [SerializeField] private TMP_Text[] m_StepBadgeTexts;
        [SerializeField] private TMP_Text[] m_StepTitles;
        [SerializeField] private TMP_Text[] m_StepHints;
        [SerializeField] private TMP_Text[] m_StepPrices;
        [SerializeField] private Button m_BuyButton;
        [SerializeField] private Image m_BuyBackground;
        [SerializeField] private Outline m_BuyOutline;
        [SerializeField] private TMP_Text m_BuyLabel;
        [SerializeField] private TMP_Text m_Note;

        private readonly SerialDisposable m_Binding = new SerialDisposable();
        private readonly List<TechId> m_Order = new List<TechId>();
        private readonly List<ShopRowItem> m_Rows = new List<ShopRowItem>();
        private readonly List<GameObject> m_Built = new List<GameObject>();
        private readonly List<ShopPopupCard> m_Cards = new List<ShopPopupCard>();

        private ShopController m_Shop;
        private RoleTag m_Tag;
        private TechSlots m_Slots;
        private CheeseWallet m_Wallet;
        private Health m_Health;
        private MousePopgun m_Popgun;
        private int m_LastHp;
        private RoleId m_BuiltFor = RoleId.None;
        private int m_Selected;

        /// <summary>떠 있는가 — QA가 눈이 아니라 값으로 잰다.</summary>
        public bool IsOpen => m_Root != null && m_Root.activeSelf;

        private void Start()
        {
            if (m_Root != null) m_Root.SetActive(false);
            if (m_RowTemplate != null) m_RowTemplate.gameObject.SetActive(false);
            if (m_GroupTemplate != null) m_GroupTemplate.gameObject.SetActive(false);

            if (m_BuyButton != null) m_BuyButton.OnClickAsObservable().Subscribe(_ => OnClickBuy()).AddTo(this);

            LocalPlayerBinder.LocalPlayer.Subscribe(Rebind).AddTo(this);
            m_Binding.AddTo(this);
        }

        private void OnDestroy()
        {
            foreach (ShopRowItem row in m_Rows)
            {
                if (row == null) continue;
                row.Hovered -= OnRowHovered;
                row.Clicked -= OnRowClicked;
            }
        }

        private void Rebind(NetworkObject _player)
        {
            m_Binding.Disposable = null;
            Close();

            m_Shop = _player != null ? _player.GetComponent<ShopController>() : null;
            m_Tag = _player != null ? _player.GetComponent<RoleTag>() : null;
            m_Slots = _player != null ? _player.GetComponent<TechSlots>() : null;
            m_Wallet = _player != null ? _player.GetComponent<CheeseWallet>() : null;
            m_Health = _player != null ? _player.GetComponent<Health>() : null;
            m_Popgun = _player != null ? _player.GetComponent<MousePopgun>() : null;
            m_LastHp = m_Health != null ? m_Health.Hp : 0;

            if (m_Slots == null || m_Wallet == null) return;

            // 보유·지갑이 바뀌면 떠 있는 창을 다시 그린다(규칙 3 — Update 폴링 금지).
            m_Binding.Disposable = Observable
                .Merge(m_Slots.Changed, m_Wallet.Cheese.Select(_ => Unit.Default))
                .Subscribe(_ => { if (IsOpen) Render(); });
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) OnPressE();

            if (!IsOpen) return;

            // 맞았는가 — HP가 줄어든 그 프레임에 닫는다. 창을 연 채로 맞아 죽는 것을 막지는 않는다(의도).
            bool hurt = m_Health != null && m_Health.Hp < m_LastHp;
            if (m_Health != null) m_LastHp = m_Health.Hp;

            if (ShopPopupLogic.ShouldClose(InShop(), Dead(), hurt, Locked(), MatchOver())) Close();
        }

        /// <summary>E 한 번 — 열거나 닫는다. QA도 이 길로 부른다(지름길을 만들면 배선 누락을 못 잡는다).</summary>
        public void OnPressE()
        {
            if (IsOpen) { Close(); return; }
            if (UiFocus.IsTyping) return;

            RoleId role = m_Tag != null ? m_Tag.Role : RoleId.None;
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

        /// <summary>닫는다. **모달을 반드시 푼다** — 안 풀면 커서가 풀린 채 조작이 굳는다(2026-09-03 실측).</summary>
        public void Close()
        {
            if (m_Root == null || !m_Root.activeSelf) return;

            m_Root.SetActive(false);
            UiFocus.SetModalOpen(false);
        }

        // ───────────────────────────── 그리기 ─────────────────────────────

        private void Render()
        {
            if (m_Slots == null || m_Wallet == null || m_Tag == null) return;

            RoleId role = m_Tag.Role;
            int slotCap = TechLogic.SlotCapOf(role);
            EnsureRows(role);

            if (m_CheeseValue != null) m_CheeseValue.text = m_Wallet.CheeseValue.ToString();
            if (m_SlotText != null) m_SlotText.text = $"{m_Slots.Count}/{slotCap}";
            for (int i = 0; i < m_SlotPips.Length; i++)
            {
                if (m_SlotPips[i] == null) continue;
                m_SlotPips[i].gameObject.SetActive(i < slotCap);
                m_SlotPips[i].color = i < m_Slots.Count ? UiPalette.Accent : UiPalette.Groove;
            }

            if (m_Selected < 0 || m_Selected >= m_Rows.Count) m_Selected = 0;
            m_Cards.Clear();
            for (int i = 0; i < m_Order.Count && i < m_Rows.Count; i++)
            {
                ShopPopupCard card = ShopPopupLogic.CardOf(
                    m_Slots.Owned, m_Order[i], role, m_Wallet.CheeseValue, slotCap,
                    m_Shop != null ? m_Shop.SoldOutToday : null,
                    m_Popgun != null && m_Popgun.ClubActive,
                    m_Popgun != null && m_Popgun.SwordActive);
                m_Cards.Add(card);

                bool sold = card.Deny == TechDeny.SoldOut;
                m_Rows[i].Bind(i, ShopPopupLogic.FamilyName(card.Id), card.OwnedLevel,
                    ShopPopupLogic.RowPrice(card, role), PriceColorOf(card), sold);
                m_Rows[i].SetSelected(i == m_Selected);
            }

            RenderDetail(role, slotCap);
        }

        /// <summary>
        /// 줄을 카탈로그 수만큼 만든다 — **역할이 바뀔 때만**(창을 열 때마다 새로 만들지 않는다).
        /// 축이 바뀌는 자리에 머리글을 끼운다. 레이아웃은 목록의 세로 레이아웃 그룹이 맡는다.
        /// </summary>
        private void EnsureRows(RoleId _role)
        {
            if (m_BuiltFor == _role || m_RowTemplate == null || m_ListContent == null) return;

            foreach (GameObject go in m_Built)
            {
                if (go != null) Destroy(go);
            }
            m_Built.Clear();
            m_Rows.Clear();
            m_Order.Clear();
            m_Order.AddRange(ShopPopupLogic.ListOrder(TechLogic.Catalog(_role)));

            TechAxis? axis = null;
            for (int i = 0; i < m_Order.Count; i++)
            {
                TechAxis rowAxis = TechLogic.RowOf(m_Order[i], 1).Axis;
                if (axis != rowAxis && m_GroupTemplate != null)
                {
                    TMP_Text group = Instantiate(m_GroupTemplate, m_ListContent);
                    group.text = ShopPopupLogic.AxisLabel(rowAxis);
                    group.gameObject.SetActive(true);
                    m_Built.Add(group.gameObject);
                    axis = rowAxis;
                }

                ShopRowItem row = Instantiate(m_RowTemplate, m_ListContent);
                row.gameObject.SetActive(true);
                row.Hovered += OnRowHovered;
                row.Clicked += OnRowClicked;
                m_Rows.Add(row);
                m_Built.Add(row.gameObject);
            }

            m_Selected = 0;
            m_BuiltFor = _role;
        }

        private void RenderDetail(RoleId _role, int _slotCap)
        {
            if (m_Selected < 0 || m_Selected >= m_Cards.Count) return;
            ShopPopupCard card = m_Cards[m_Selected];
            TechRow first = TechLogic.RowOf(card.Id, 1);

            m_DetailAxis.text = ShopPopupLogic.AxisLabel(first.Axis);
            m_DetailTitle.text = ShopPopupLogic.FamilyName(card.Id);
            m_DetailLevel.text = ShopPopupLogic.LevelText(card);
            m_DetailLevel.color = card.OwnedLevel > 0 ? UiPalette.Good : UiPalette.TextDim;
            for (int i = 0; i < m_DetailBars.Length; i++)
            {
                m_DetailBars[i].color = i < card.OwnedLevel ? UiPalette.Good : UiPalette.Groove;
            }

            int max = TechLogic.MaxLevel(card.Id);
            string currency = ShopLogic.CurrencyNameOf(_role);
            for (int i = 0; i < m_Steps.Length; i++)
            {
                int level = i + 1;
                bool used = level <= max;
                m_Steps[i].SetActive(used);
                if (used) RenderStep(i, card, level, currency);
            }

            ShopButtonTone tone = ShopPopupLogic.ButtonTone(card);
            bool buy = tone == ShopButtonTone.Buy;
            m_BuyBackground.color = buy ? UiPalette.Accent : BUTTON_IDLE_BG;
            m_BuyOutline.effectColor = buy ? CLEAR : UiPalette.Line;
            m_BuyLabel.text = ShopPopupLogic.ButtonText(card, _role, _slotCap);
            m_BuyLabel.color = tone == ShopButtonTone.Buy ? UiPalette.TextOnAccent
                : tone == ShopButtonTone.Short ? UiPalette.Bad
                : tone == ShopButtonTone.Maxed ? UiPalette.Good
                : UiPalette.TextDim;
            m_Note.text = ShopPopupLogic.NoteText(card, _role);
        }

        private void RenderStep(int _index, ShopPopupCard _card, int _level, string _currency)
        {
            TechRow row = TechLogic.RowOf(_card.Id, _level);
            ShopStep step = ShopPopupLogic.StepOf(_card, _level);
            bool next = step == ShopStep.Next || step == ShopStep.NextBlocked;
            bool lit = next || step == ShopStep.Owned;

            m_StepBackgrounds[_index].color = next ? STEP_NEXT_BG : DETAIL_BG;
            m_StepOutlines[_index].effectColor = step == ShopStep.Next ? UiPalette.Accent
                : step == ShopStep.NextBlocked ? STEP_BLOCKED_LINE
                : UiPalette.Line;
            m_StepBadges[_index].color = step == ShopStep.Owned ? STEP_OWNED_BADGE : next ? UiPalette.Accent : UiPalette.Groove;
            m_StepBadgeTexts[_index].text = $"Lv{_level}";
            m_StepBadgeTexts[_index].color = step == ShopStep.Owned ? UiPalette.Good : next ? UiPalette.TextOnAccent : UiPalette.TextDim;
            m_StepTitles[_index].text = row.Label;
            m_StepTitles[_index].color = lit ? UiPalette.TextOn : UiPalette.TextDim;
            m_StepHints[_index].text = row.Hint;
            m_StepHints[_index].color = lit ? UiPalette.TextSub : UiPalette.TextDim;
            m_StepPrices[_index].text = step == ShopStep.Owned ? "보유" : $"{row.Cost} {_currency}";
            m_StepPrices[_index].color = step == ShopStep.Owned ? UiPalette.Good
                : step == ShopStep.Next ? UiPalette.Accent
                : step == ShopStep.NextBlocked ? UiPalette.Bad
                : UiPalette.TextDim;
        }

        private static Color PriceColorOf(ShopPopupCard _card)
        {
            if (_card.NextLevel == 0) return UiPalette.Good;
            if (_card.Deny == TechDeny.SoldOut) return UiPalette.TextDim;
            if (_card.Deny == TechDeny.NotEnough) return UiPalette.Bad;
            return UiPalette.TextOn;
        }

        // ───────────────────────────── 입력 ─────────────────────────────

        // 마우스를 올린 줄이 골라진다 — 오른쪽 상세만 다시 그린다(목록은 그대로).
        private void OnRowHovered(int _index)
        {
            if (_index < 0 || _index >= m_Rows.Count || _index == m_Selected) return;

            if (m_Selected >= 0 && m_Selected < m_Rows.Count) m_Rows[m_Selected].SetSelected(false);
            m_Selected = _index;
            m_Rows[m_Selected].SetSelected(true);
            if (m_Tag != null) RenderDetail(m_Tag.Role, TechLogic.SlotCapOf(m_Tag.Role));
        }

        // 줄을 누르면 산다. **클릭은 막지 않는다** — 진짜 판정은 서버가 하고, 거절 사유는 토스트로 뜬다.
        private void OnRowClicked(int _index)
        {
            OnRowHovered(_index);
            Buy(_index);
        }

        private void OnClickBuy() => Buy(m_Selected);

        private void Buy(int _index)
        {
            if (m_Shop == null || _index < 0 || _index >= m_Order.Count) return;

            m_Shop.Buy(m_Order[_index]);
        }

        // 철문이 닫힌 상점은 「상점 밖」과 같다 — 팝업을 못 열고, 열려 있으면 닫힌다(2026-09-19 상점 부스).
        private bool InShop() => m_Shop != null && ShopSpot.AnyOpenContains(m_Shop.transform.position);
        private bool Dead() => m_Health == null || m_Health.IsDead;
        private bool Locked() => m_Shop != null && ActionLock.Locked(m_Shop);
        private bool MatchOver() => m_Match != null && m_Match.Result != MatchResult.None;

        // ───────────────────────────── QA 전용 ─────────────────────────────

        /// <summary>QA: 지금 그린 목록 요약 — "테크:이름:살수있나"를 '|'로 잇는다(목록 순서).</summary>
        public string DevCardSummary()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < m_Cards.Count; i++)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append(m_Cards[i].Id).Append(':').Append(ShopPopupLogic.FamilyName(m_Cards[i].Id))
                  .Append(':').Append(m_Cards[i].CanBuy);
            }
            return sb.ToString();
        }

        /// <summary>QA: 그 기술의 줄을 **실제로 누른다**(사람의 클릭과 같은 길).</summary>
        public bool DevClick(string _tech)
        {
            int index = IndexOf(_tech);
            if (index < 0) return false;

            m_Rows[index].DevClick();
            return true;
        }

        /// <summary>QA: 그 기술의 줄에 **커서를 올린다**(사람과 같은 길).</summary>
        public bool DevHover(string _tech)
        {
            int index = IndexOf(_tech);
            if (index < 0) return false;

            m_Rows[index].DevHover();
            return true;
        }

        /// <summary>QA: 오른쪽 상세에 **보이는 글** — "이름|Lv 1/3|버튼 글|밑줄 글|사다리 가격들".</summary>
        public string DevDetailSummary()
        {
            if (m_DetailTitle == null) return "";
            var prices = new StringBuilder();
            for (int i = 0; i < m_Steps.Length; i++)
            {
                if (!m_Steps[i].activeSelf) continue;
                if (prices.Length > 0) prices.Append(',');
                prices.Append(m_StepPrices[i].text);
            }
            return $"{m_DetailTitle.text}|{m_DetailLevel.text}|{m_BuyLabel.text}|{m_Note.text}|{prices}";
        }

        private int IndexOf(string _tech)
        {
            for (int i = 0; i < m_Order.Count && i < m_Rows.Count; i++)
            {
                if (m_Order[i].ToString() == _tech) return i;
            }
            return -1;
        }
    }
}
