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
    /// 상점 원 안에서 **E**로 열고, 파는 물건 열 개를 한 화면에 깔아 **클릭 한 번**으로 산다.
    /// 예전에는 핫바 5칸이 10종의 후보를 돌려 가며 보여 줬다 — 무엇을 파는지 볼 방법이 없었다.
    ///
    /// <see cref="CatClassPopup"/>과 같은 규약이다: 캔버스에 구워진 패널을 켜고 끄고,
    /// <see cref="UiFocus.SetModalOpen"/>으로 커서를 푼다(`CursorPolicyLogic`이 그 값을 보고 잠금을 푼다).
    ///
    /// **맞으면 닫힌다.** 상점은 안전지대가 아니다 — 창을 연 동안에도 발소리는 들리므로
    /// 「듣고 닫고 도망친다」가 가능하다(사용자 결정 2026-09-19).
    /// </summary>
    public class ShopPopup : MonoBehaviour
    {
        [SerializeField] private GameObject m_Root;
        [SerializeField] private TMP_Text m_Header;
        [SerializeField] private Button[] m_Cards;
        [SerializeField] private TMP_Text[] m_CardTitles;
        [SerializeField] private TMP_Text[] m_CardHints;
        [SerializeField] private MatchManager m_Match;

        private readonly SerialDisposable m_Binding = new SerialDisposable();
        private readonly List<TechId> m_Catalog = new List<TechId>();
        private readonly List<bool> m_Buyable = new List<bool>();

        private ShopController m_Shop;
        private RoleTag m_Tag;
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

        private void Render()
        {
            if (m_Slots == null || m_Wallet == null || m_Tag == null) return;

            RoleId role = m_Tag.Role;
            int slotCap = TechLogic.SlotCapOf(role);

            m_Catalog.Clear();
            m_Buyable.Clear();
            foreach (TechId id in TechLogic.Catalog(role)) m_Catalog.Add(id);

            m_Header.text = $"상점 — {ShopLogic.CurrencyNameOf(role)} {m_Wallet.CheeseValue} · 칸 {m_Slots.Count}/{slotCap}";

            for (int i = 0; i < m_Cards.Length; i++)
            {
                bool used = i < m_Catalog.Count;
                m_Cards[i].gameObject.SetActive(used);
                if (!used) continue;

                ShopPopupCard card = ShopPopupLogic.CardOf(
                    m_Slots.Owned, m_Catalog[i], role, m_Wallet.CheeseValue, slotCap,
                    m_Shop != null ? m_Shop.SoldOutToday : null,
                    m_Popgun != null && m_Popgun.ClubActive,
                    m_Popgun != null && m_Popgun.SwordActive);

                RenderCard(i, card, role, slotCap);
                m_Buyable.Add(card.CanBuy);
            }
        }

        private void RenderCard(int _index, ShopPopupCard _card, RoleId _role, int _slotCap)
        {
            // 레벨 점 — 가진 만큼 채운다. 「지금 몇 단계인가」가 가격보다 먼저 읽혀야 한다.
            int max = TechLogic.MaxLevel(_card.Id);
            var dots = new StringBuilder(max);
            for (int lv = 1; lv <= max; lv++) dots.Append(lv <= _card.OwnedLevel ? '●' : '○');

            if (_card.NextLevel > 0)
            {
                TechRow next = TechLogic.RowOf(_card.Id, _card.NextLevel);
                m_CardTitles[_index].text = $"{next.Label}   {_card.Cost}";
                m_CardHints[_index].text = _card.CanBuy
                    ? $"{dots}  {next.Hint}"
                    : $"{dots}  {TechLogic.DenyText(_card.Deny, _role, _card.Short, _slotCap)}";
            }
            else
            {
                TechRow owned = TechLogic.RowOf(_card.Id, _card.OwnedLevel > 0 ? _card.OwnedLevel : 1);
                m_CardTitles[_index].text = $"{owned.Label}   만렙";
                m_CardHints[_index].text = $"{dots}  {owned.Hint}";
            }

            // 못 사는 카드는 흐리게. **클릭은 막지 않는다** — 진짜 판정은 서버가 하고, 거절 사유는 토스트로 뜬다.
            m_Cards[_index].image.color = _card.CanBuy ? UiPalette.Slot : UiPalette.SlotDim;
            m_CardTitles[_index].color = _card.CanBuy ? UiPalette.TextOn : UiPalette.TextDim;
        }

        private void OnClickCard(int _index)
        {
            if (m_Shop == null || _index >= m_Catalog.Count) return;

            m_Shop.Buy(m_Catalog[_index]);
        }

        // 철문이 닫힌 상점은 「상점 밖」과 같다 — 팝업을 못 열고, 열려 있으면 닫힌다(2026-09-19 상점 부스).
        private bool InShop() => m_Shop != null && ShopSpot.AnyOpenContains(m_Shop.transform.position);
        private bool Dead() => m_Health == null || m_Health.IsDead;
        private bool Locked() => m_Shop != null && ActionLock.Locked(m_Shop);
        private bool MatchOver() => m_Match != null && m_Match.Result != MatchResult.None;

        // ───────────────────────────── QA 전용 ─────────────────────────────

        /// <summary>QA: 지금 그린 카드 요약 — "테크:제목:살수있나"를 '|'로 잇는다.</summary>
        public string DevCardSummary()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < m_Catalog.Count && i < m_CardTitles.Length; i++)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append(m_Catalog[i]).Append(':').Append(m_CardTitles[i].text.Trim())
                  .Append(':').Append(i < m_Buyable.Count && m_Buyable[i]);
            }
            return sb.ToString();
        }

        /// <summary>QA: 그 기술의 카드 버튼을 **실제로 누른다**(사람의 클릭과 같은 길).</summary>
        public bool DevClick(string _tech)
        {
            for (int i = 0; i < m_Catalog.Count && i < m_Cards.Length; i++)
            {
                if (m_Catalog[i].ToString() != _tech) continue;

                m_Cards[i].onClick.Invoke();
                return true;
            }
            return false;
        }
    }
}
