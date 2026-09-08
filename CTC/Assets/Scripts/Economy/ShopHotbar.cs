using System;
using System.Collections.Generic;
using System.Threading;
using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using Cysharp.Threading.Tasks;
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
    /// Enter는 여기서 읽지 않는다 — `EscapeRouter`가 단독으로 읽고 <see cref="TryConfirmBuy"/>를 부른다(2026-09-07).
    /// View다 — 판정은 ShopController.CanBuyNow/CanUse와 TechLogic이 하고 여기선 보여 주기만 한다.
    /// </summary>
    public class ShopHotbar : MonoBehaviour
    {
        private const float TOAST_SECONDS = 1.4f;
        // HUD 개편(2026-08-15): 오류 토스트(1.4s)와 달리 **사용법을 가르치는** 토스트는 읽을 시간을 준다.
        private const float TOAST_LONG_SECONDS = 3f;
        private const float INTRO_TOAST_SECONDS = 4f;
        /// <summary>벽에 맞았을 때 벽면에서 떼어 놓는 여유(m).</summary>
        private const float GHOST_WALL_GAP = 0.5f;

        /// <summary>겨눈 자리가 막혔을 때 발치까지 당겨 보는 횟수 — 좁은 방에서 첫 빈 자리를 찾는다.</summary>
        private const int GHOST_RETREAT_STEPS = 6;

        // 쿨다운은 매 프레임 흐르지만 화면엔 초 단위만 보인다 — 이 간격으로만 다시 그린다.
        private const float COOLDOWN_REDRAW_SECONDS = 0.25f;

        // 숫자키는 1~9까지만 있다 — 칸이 더 늘면 키가 아니라 다른 조작이 필요하다
        private const int MAX_HOTKEYS = 9;

        // 고스트가 뻗는 최대 거리 — 서버 검증(ShopController.MAX_PLACE_DISTANCE)보다 짧아야 한다.
        private static float GHOST_MAX_DISTANCE => Info.InfoManager.Instance.Balance.PlaceGhostMaxMeters;

        // 칸 수는 구조 제약(기획 §2) — CSV가 단일 출처다.
        private static int SLOT_CAP => Info.InfoManager.Instance.Balance.TechSlotCap;

        private static readonly Color COLOR_HINT = UI.UiPalette.WithAlpha(UI.UiPalette.TextDim, 0.9f);
        private static readonly Color COLOR_READY = UI.UiPalette.TextOn;
        private static readonly Color COLOR_POOR = UI.UiPalette.Bad;
        private static readonly Color COLOR_OWNED = UI.UiPalette.Good;
        private static readonly Color COLOR_EMPTY = UI.UiPalette.WithAlpha(UI.UiPalette.TextDim, 0.5f);

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private GameObject m_Root;
        [SerializeField] private GameObject[] m_Slots;     // 슬롯 루트(표시/숨김)
        [SerializeField] private TMP_Text[] m_SlotLabels;  // "1  새총 Lv1"
        [SerializeField] private TMP_Text[] m_SlotHints;   // 반줄 힌트 — CSV의 Hint를 그대로 읽는다
        [SerializeField] private TMP_Text[] m_SlotCosts;   // "150" / "MAX" / "쓰기"
        [SerializeField] private TMP_Text m_ModeLabel;     // 지금이 사는 모드인지 쓰는 모드인지 + 보유 n/5
        [SerializeField] private TMP_Text m_Toast;
        // 설치 미리보기 원본 — 에셋으로 둬야 빌드에서 셰이더가 스트리핑되지 않는다
        [SerializeField] private Material m_GhostMaterialSource;

        /// <summary>고스트 배치 중인가 — `EscapeRouter`가 ESC를 무엇에 쓸지 정할 때 본다.</summary>
        public bool IsPlacing => m_PlacingId != TechId.None;

        private readonly SerialDisposable m_WalletSub = new SerialDisposable();
        private readonly SerialDisposable m_SlotsSub = new SerialDisposable();
        private readonly SerialDisposable m_SoldOutSub = new SerialDisposable();

        private ShopController m_Shop;                       // 로컬 플레이어의 상점 (null = 관전·매치 전)
        private TechSlots m_TechSlots;
        // 액티브 쿨다운의 주인들 — 둘 다 **소유자 로컬 값**이라 내 몸에서만 뜻이 있다.
        // 로컬 플레이어가 바뀔 때 한 번만 잡는다(View가 매 프레임 GetComponent를 돌지 않게).
        private StickyUser m_Sticky;
        private Player.MouseDash m_Dash;
        private RoleId m_Role;
        private int m_Cheese;
        private bool m_GameStarted;
        private bool m_SlotsDirty;
        private bool m_CooldownTicking;   // 지금 화면에 흐르는 쿨다운이 있나 — 있을 때만 다시 그린다
        private float m_NextCooldownRedraw;
        private bool m_IntroShown; // 기본 조작 토스트는 매치당 1회 — 매판 반복되면 소음이다
        private bool m_InShop;

        // 오늘의 품절(계획 3 Task 3) — 판정은 ShopController가 하고 View는 받아 쓰기만 한다(규칙 5).
        private static readonly TechId[] NONE_SOLD_OUT = new TechId[0];
        private IReadOnlyList<TechId> m_SoldOut = NONE_SOLD_OUT;
        private bool m_SoldOutShown;   // 품절 토스트도 매치당 1회
        private CancellationTokenSource m_SoldOutToastCts;

        // 빈 칸마다 "지금 보여 주는 후보" 인덱스 — 같은 숫자키를 반복하면 넘어간다
        private int[] m_CandidateIndex;

        private float m_ToastUntil;
        private TechId m_PlacingId = TechId.None;
        private GameObject m_Ghost;
        private Material m_GhostMaterial;
        private bool m_GhostValid;

        private void Awake()
        {
            m_Root.SetActive(false);
            m_Toast.gameObject.SetActive(false);

            // 칸 수의 주인은 CSV고 씬은 구울 때 그걸 따라간다 — 어긋나면 조용히 칸이 사라지므로 알린다.
            int slots = m_Slots != null ? m_Slots.Length : 0;
            if (slots != SLOT_CAP)
            {
                Core.GameDebug.LogWarning(
                    $"[ShopHotbar] 슬롯 {slots}칸 ≠ TechSlotCap {SLOT_CAP} — Setup Networking Scene 재실행");
            }
            m_CandidateIndex = new int[Mathf.Max(slots, SLOT_CAP)];
        }

        private void Start()
        {
            m_WalletSub.AddTo(this);
            m_SlotsSub.AddTo(this);
            m_SoldOutSub.AddTo(this);
            m_Lobby.GameStartedChanged.Subscribe(OnGameStartedChanged).AddTo(this);
            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
        }

        private void OnDestroy()
        {
            CancelSoldOutToast();
            CancelPlacement();
            if (m_GhostMaterial != null) Destroy(m_GhostMaterial);
        }

        private void OnGameStartedChanged(bool _started)
        {
            m_GameStarted = _started;
            if (!_started)
            {
                m_IntroShown = false;
                m_SoldOutShown = false;   // 다음 판은 품절 목록이 다르다 — 다시 알린다
                CancelSoldOutToast();
            }
            ApplyActive();
        }

        // 플레이어가 바뀌면 지갑·테크 구독을 통째로 갈아 끼운다(SerialDisposable이 이전 것을 끊는다)
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
                m_Sticky = null;
                m_Dash = null;
                m_WalletSub.Disposable = null;
                m_SlotsSub.Disposable = null;
                m_SoldOutSub.Disposable = null;
                m_SoldOut = NONE_SOLD_OUT;
                ApplyActive();
                return;
            }

            m_Shop = shop;
            m_TechSlots = slots;
            m_Sticky = _player.GetComponent<StickyUser>();
            m_Dash = _player.GetComponent<Player.MouseDash>();
            m_Role = tag.Role;
            ResetCandidates();
            m_WalletSub.Disposable = wallet.Cheese.Subscribe(c => { m_Cheese = c; m_SlotsDirty = true; });
            // 하나 사면 후보 목록이 줄고 빈 칸도 한 칸 밀린다 — 옛 인덱스를 들고 있으면 엉뚱한 게 뜬다
            m_SlotsSub.Disposable = slots.Changed.Subscribe(_ => { ResetCandidates(); m_SlotsDirty = true; });
            // 품절이 정해지면 후보 목록이 줄어든다 — 옛 인덱스를 들고 있으면 엉뚱한 게 뜬다(구매와 같은 이유)
            m_SoldOutSub.Disposable = shop.SoldOutChanged.Subscribe(list =>
            {
                m_SoldOut = list != null ? list : (IReadOnlyList<TechId>)NONE_SOLD_OUT;
                ResetCandidates();
                m_SlotsDirty = true;
                TrySaySoldOut();
            });
            ApplyActive();
        }

        /// <summary>
        /// 빈 칸에 돌려 볼 후보 — **오늘 품절은 아예 뺀다**(기획서 §7-2).
        /// 남겨 두고 누를 때 거절하면 못 사는 것을 보여 주고 5키를 헛돌리게 된다.
        /// </summary>
        private IReadOnlyList<TechId> CandidatesNow(IReadOnlyList<TechEntry> _owned)
        {
            IReadOnlyList<TechId> all = TechLogic.Candidates(m_Role, _owned);
            if (m_SoldOut.Count == 0) return all;

            var list = new List<TechId>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (!TechRandomLogic.IsSoldOut(m_SoldOut, all[i])) list.Add(all[i]);
            }
            return list;
        }

        /// <summary>
        /// 판 시작 품절 안내 — **새 화면 0**(기획서 §9)이라 기본 조작 토스트와 같은 자리를 쓴다.
        /// 시작 신호와 시드가 **둘 다** 온 뒤 한 번만 띄운다: 시드는 판 시작 직후에 따로 오므로
        /// 한쪽만 보고 띄우면 그 판은 아무도 품절을 모른 채 지나간다.
        /// 고양이에게도 띄운다 — §7이 "전원 동일·공개"다.
        /// </summary>
        private void TrySaySoldOut()
        {
            if (m_SoldOutShown || !m_GameStarted || m_Shop == null || m_SoldOut.Count == 0) return;
            m_SoldOutShown = true;

            var names = new List<string>(m_SoldOut.Count);
            for (int i = 0; i < m_SoldOut.Count; i++)
            {
                TechRow row = TechLogic.RowOf(m_SoldOut[i], 1);
                names.Add(row.IsValid ? row.Label : m_SoldOut[i].ToString());
            }

            CancelSoldOutToast();
            m_SoldOutToastCts = new CancellationTokenSource();
            SaySoldOutLater($"오늘 품절: {string.Join(" · ", names)}", m_SoldOutToastCts.Token).Forget();
        }

        /// <summary>
        /// 기본 조작 토스트가 끝난 뒤 이어서 띄운다. 코루틴 금지(CTC 규칙 1)라 UniTask다.
        ///
        /// **한 프레임을 먼저 흘린다.** 품절 목록은 로컬 플레이어를 잡는 그 순간 도착하는데,
        /// 기본 조작 토스트는 같은 함수의 끝(`ApplyActive`)에서 **그 뒤에** 뜬다 —
        /// 남은 시간을 지금 재면 0이 나와 띄우자마자 기본 조작 토스트에 덮인다(2026-09-08 실측).
        /// </summary>
        private async UniTaskVoid SaySoldOutLater(string _text, CancellationToken _token)
        {
            bool canceled = await UniTask.NextFrame(_token).SuppressCancellationThrow();
            if (canceled || m_Toast == null) return;

            float wait = Mathf.Max(0f, m_ToastUntil - Time.time);
            if (wait > 0f)
            {
                canceled = await UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: _token)
                    .SuppressCancellationThrow();
                if (canceled || m_Toast == null) return;
            }
            Say(_text, TOAST_LONG_SECONDS);
        }

        private void CancelSoldOutToast()
        {
            // 순수 C# 객체라 ?.를 써도 된다(규칙 9 예외) — 그래도 흐름을 맞춰 명시적으로 쓴다.
            if (m_SoldOutToastCts == null) return;
            m_SoldOutToastCts.Cancel();
            m_SoldOutToastCts.Dispose();
            m_SoldOutToastCts = null;
        }

        private void ResetCandidates()
        {
            for (int i = 0; i < m_CandidateIndex.Length; i++) m_CandidateIndex[i] = 0;
        }

        // 표시 여부는 시작 신호 × 로컬 상점 — 어느 쪽이 바뀌어도 여기로 온다
        private void ApplyActive()
        {
            bool inGame = m_GameStarted && m_Shop != null;
            if (m_Root.activeSelf != inGame) m_Root.SetActive(inGame);
            if (inGame) m_SlotsDirty = true;
            else CancelPlacement();

            // 첫 스폰 1회 기본 조작 안내(2026-08-15) — 매판 반복되면 소음이다
            if (inGame && !m_IntroShown)
            {
                m_IntroShown = true;
                string attack = m_Role == RoleId.Cat ? "덮치기" : "발사";
                string shop = m_Role == RoleId.Cat
                    ? "숫자키로 고르고 Enter로 산다"
                    : "상점에서 숫자키로 고르고 Enter로 산다";
                Say($"WASD 이동 · 마우스 시점 · 좌클릭 {attack} · LShift 살금살금 · {shop}", INTRO_TOAST_SECONDS);
            }

            // 시드가 먼저 오고 시작 신호가 나중일 수도 있다 — 양쪽에서 같은 문을 두드린다.
            if (inGame) TrySaySoldOut();
        }

        private void Update()
        {
            if (!m_GameStarted || m_Shop == null) return;

            // 상점 안/밖은 위치라 매 프레임 본다 — ShopSpot은 정적 목록(≤4)이라 싸다
            bool inShop = !TechLogic.NeedsShopSpot(m_Role) || Map.ShopSpot.AnyContains(m_Shop.transform.position);
            if (inShop != m_InShop) { m_InShop = inShop; m_SlotsDirty = true; }

            // 구독 콜백이 아니라 여기서 그리는 이유: 구매 하나가 치즈·테크 목록 등 NetworkVariable 여러 개를
            // 같은 틱에 바꾸는데, 콜백 시점엔 시블링 변수가 아직 옛값일 수 있다. 여기서는 전부 새 값이다.
            // 쿨다운이 흐르는 동안만 다시 그린다 — 매 프레임 RefreshSlots를 도는 건 낭비다.
            if (m_CooldownTicking && Time.time >= m_NextCooldownRedraw)
            {
                m_NextCooldownRedraw = Time.time + COOLDOWN_REDRAW_SECONDS;
                m_SlotsDirty = true;
            }

            if (m_SlotsDirty) { m_SlotsDirty = false; RefreshSlots(); }

            if (m_PlacingId != TechId.None) UpdatePlacement();
            else HandleHotkeys();

            if (m_Toast.gameObject.activeSelf && Time.time >= m_ToastUntil) m_Toast.gameObject.SetActive(false);
        }

        // ===== 그리기 =====

        private void RefreshSlots()
        {
            IReadOnlyList<TechEntry> owned = m_TechSlots.Owned;
            IReadOnlyList<TechId> candidates = CandidatesNow(owned);
            int cap = SLOT_CAP;

            // DrawOwned가 다시 켠다 — 쿨이 끝나면 저절로 꺼져 다시 그리기가 멈춘다.
            m_CooldownTicking = false;

            for (int slot = 0; slot < m_Slots.Length; slot++)
            {
                bool visible = slot < cap;
                if (m_Slots[slot].activeSelf != visible) m_Slots[slot].SetActive(visible);
                if (!visible) continue;

                if (slot < owned.Count) DrawOwned(slot, owned[slot], owned);
                else DrawEmpty(slot, candidates);
            }

            string mode = m_InShop ? "상점 · 숫자키 = 산다 / 키운다 · Enter = 확정" : "숫자키 = 쓴다";
            if (m_ModeLabel != null) m_ModeLabel.text = $"{mode}    보유 {owned.Count}/{cap}";
        }

        private void DrawOwned(int _slot, TechEntry _e, IReadOnlyList<TechEntry> _owned)
        {
            TechRow cur = TechLogic.RowOf(_e.Id, _e.Level);
            TechRow next = TechLogic.NextRow(_owned, _e.Id);
            m_SlotLabels[_slot].text = $"{_slot + 1}  {cur.Label} Lv{_e.Level}";
            SetHint(_slot, cur.Hint);

            Color c;
            if (m_InShop)
            {
                bool affordable = next.IsValid && m_Cheese >= next.Cost;
                m_SlotCosts[_slot].text = next.IsValid ? next.Cost.ToString() : "MAX";
                c = !next.IsValid ? COLOR_OWNED : affordable ? COLOR_READY : COLOR_POOR;
            }
            else
            {
                bool usable = TechLogic.IsActive(_e.Id);
                float cooldown = usable ? CooldownOf(_e.Id) : 0f;
                if (cooldown > 0f) m_CooldownTicking = true;
                // 남은 초는 올림 — 0.4초 남았는데 "0s"라 쓰면 이미 쓸 수 있다는 거짓말이 된다.
                m_SlotCosts[_slot].text = !usable ? ""
                    : cooldown > 0f ? $"{Mathf.CeilToInt(cooldown)}s" : "쓰기";
                c = !usable ? COLOR_OWNED : cooldown > 0f ? COLOR_POOR : COLOR_READY;
            }
            m_SlotLabels[_slot].color = c;
            m_SlotCosts[_slot].color = c;
        }

        /// <summary>
        /// 이 액티브가 지금 몇 초 남았나(0 = 지금 쓸 수 있다).
        /// 판정이 아니라 **표시**다 — 못 쓰는 진짜 이유는 여전히 <see cref="ShopController.CanUse"/>가 안다.
        /// 둘 다 소유자 로컬 값이라 남의 몸에서는 0으로 보인다(핫바는 내 화면에만 있으니 충분하다).
        /// </summary>
        private float CooldownOf(TechId _id)
        {
            switch (_id)
            {
                case TechId.Sticky: return m_Sticky != null ? m_Sticky.CooldownRemaining : 0f;
                case TechId.Dash: return m_Dash != null ? m_Dash.CooldownRemaining : 0f;
                default: return 0f;
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
            m_SlotLabels[_slot].color = c;
            m_SlotCosts[_slot].color = c;
        }

        // 반줄 힌트 — 상태색을 안 따른다(정보이지 상태가 아니다).
        // 씬 재생성 전(미배선)에도 죽지 않게 비어 있으면 건너뛴다.
        private void SetHint(int _slot, string _text)
        {
            if (m_SlotHints == null || _slot >= m_SlotHints.Length || m_SlotHints[_slot] == null) return;
            m_SlotHints[_slot].text = _text;
            m_SlotHints[_slot].color = COLOR_HINT;
        }

        // ===== 입력 =====

        private void HandleHotkeys()
        {
            // 인게임 콘솔 등 입력창에 포커스가 있으면 타이핑이 구매로 새지 않게 무시
            Keyboard kb = Keyboard.current;
            if (kb == null || Core.UiFocus.BlocksGameplay) return;

            // Enter(확정 구매)는 여기서 안 읽는다 — `EscapeRouter`가 단독으로 읽고 TryConfirmBuy를 부른다.
            int cap = SLOT_CAP;
            int pressed = -1;
            for (int slot = 0; slot < cap && slot < m_Slots.Length && slot < MAX_HOTKEYS; slot++)
            {
                if (kb[SlotKey(slot)].wasPressedThisFrame) { pressed = slot; break; }
            }
            if (pressed < 0) return;   // 키가 없으면 목록을 만들지 않는다(프레임당 할당 0)

            IReadOnlyList<TechEntry> owned = m_TechSlots.Owned;
            IReadOnlyList<TechId> candidates = CandidatesNow(owned);

            if (pressed < owned.Count)
            {
                if (m_InShop) TryBuy(owned[pressed].Id);       // 키운다
                else TryUse(owned[pressed].Id);                // 쓴다
            }
            else if (m_InShop && pressed == owned.Count && candidates.Count > 0)
            {
                m_CandidateIndex[pressed] = (m_CandidateIndex[pressed] + 1) % candidates.Count;   // 후보 넘김
                m_SlotsDirty = true;
            }
            else if (m_InShop)
            {
                // 빈 칸이 여럿 보여도 살 수 있는 건 맨 왼쪽 하나다 — 나머지는 아직 자리만 있는 칸
                Say(candidates.Count > 0 ? "빈 칸은 왼쪽부터 채운다" : "살 게 없다");
            }
            else
            {
                Say("상점에서만 살 수 있다");
            }
        }

        /// <summary>지금 Enter가 확정 구매로 갈 수 있는가 — `EscapeRouter`가 Enter를 나눌 때 본다.</summary>
        public bool CanConfirmBuy
        {
            get
            {
                if (!m_GameStarted || m_Shop == null || m_TechSlots == null || !m_InShop) return false;
                int owned = m_TechSlots.Count;
                return owned < SLOT_CAP && owned < m_CandidateIndex.Length
                       && CandidatesNow(m_TechSlots.Owned).Count > 0;
            }
        }

        /// <summary>첫 빈 칸에 지금 걸린 후보를 확정 구매. 못 사는 상황이면 아무 일도 없이 false.</summary>
        public bool TryConfirmBuy()
        {
            if (!CanConfirmBuy) return false;
            IReadOnlyList<TechEntry> owned = m_TechSlots.Owned;
            IReadOnlyList<TechId> candidates = CandidatesNow(owned);
            TryBuy(candidates[m_CandidateIndex[owned.Count] % candidates.Count]);
            return true;
        }

        private void TryBuy(TechId _id)
        {
            TechRow next = TechLogic.NextRow(m_TechSlots.Owned, _id);
            TechDeny deny = m_Shop.CanBuyNow(_id);
            if (deny != TechDeny.None)
            {
                Say(TechLogic.DenyText(deny, m_Role, next.IsValid ? next.Cost - m_Cheese : 0, SLOT_CAP));
                return;
            }
            m_Shop.Buy(_id);
            // 구매 토스트가 곧 사용법이다 — 힌트를 그대로 읽어 준다
            Say($"{next.Label} Lv{next.Level} — {next.Hint}", TOAST_LONG_SECONDS);
        }

        private void TryUse(TechId _id)
        {
            // 왜 막혔는지는 ShopController가 안다 — View가 TechId로 추측하면 거짓말을 한다
            UseDeny deny = m_Shop.CanUse(_id);
            if (deny != UseDeny.None)
            {
                Say(TechLogic.UseDenyText(deny, m_Role, ShopController.UseCostOf(_id) - m_Cheese));
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
                case 4: return Key.Digit5;
                case 5: return Key.Digit6;
                case 6: return Key.Digit7;
                case 7: return Key.Digit8;
                default: return Key.Digit9;
            }
        }

        // ===== 설치물 고스트 =====

        private void BeginPlacement(TechId _id, string _name)
        {
            CancelPlacement();

            // 머티리얼부터 확보한다 — 실패하면 배치 모드에 들어가지 않는다.
            // (예전엔 고스트를 만든 뒤 머티리얼에서 터져 m_PlacingId만 남았고,
            //  다음 프레임부터 UpdatePlacement가 null을 참조해 매 프레임 예외를 뿜었다)
            if (m_GhostMaterial == null) m_GhostMaterial = CreateGhostMaterial();
            if (m_GhostMaterial == null)
            {
                Say("설치 미리보기를 만들 수 없다");
                return;
            }

            m_PlacingId = _id;

            Vector3 half = ShopController.HalfExtentsOf(_id);
            m_Ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_Ghost.name = "PlacementGhost";
            Destroy(m_Ghost.GetComponent<Collider>()); // 고스트는 물리 무간섭
            m_Ghost.transform.localScale = half * 2f;
            m_Ghost.GetComponent<Renderer>().sharedMaterial = m_GhostMaterial;

            Say($"{_name} 배치 — 좌클릭 설치 / 우클릭 취소");
        }

        private void UpdatePlacement()
        {
            Mouse mouse = Mouse.current;
            // ESC는 EscapeRouter가 단독으로 읽고 CancelPlacementFromEscape()를 부른다(2026-08-01)
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) { CancelPlacement(); Say("배치 취소"); return; }

            Vector3 pos = GhostPosition(m_Shop.transform, m_PlacingId);
            m_GhostValid = ShopController.PlacementFits(m_PlacingId, pos);

            Vector3 half = ShopController.HalfExtentsOf(m_PlacingId);
            m_Ghost.transform.position = pos + Vector3.up * half.y;
            m_GhostMaterial.color = m_GhostValid
                ? UI.UiPalette.WithAlpha(UI.UiPalette.Good, 0.35f)
                : UI.UiPalette.WithAlpha(UI.UiPalette.Bad, 0.35f);

            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (!m_GhostValid) { Say("여기엔 설치할 수 없다"); return; }

            m_Shop.UseAt(m_PlacingId, pos); // 설치권은 이미 샀다 — 서버는 자리만 재검증한다
            // 서버 재검증 실패(드묾 — 고스트가 이미 걸렀다)면 거절음만 나고 이 토스트는 살짝 거짓이 된다
            CancelPlacement();
            Say("창고 설치 — 자리를 비워도 치즈가 쌓인다 · 소리로 위치가 팔린다", TOAST_LONG_SECONDS);
        }

        /// <summary>
        /// 카메라 전방 지면 지점 — 겨눈 자리가 막혔으면 **발치 쪽으로 당기며** 첫 빈 자리를 쓴다.
        ///
        /// 예전에는 벽에 맞아도 <c>Mathf.Max(1.5m, hit.distance - 0.5m)</c>로 최소 거리를 강제했다.
        /// 그래서 벽이 2 m보다 가까우면 고스트가 **벽 너머로 밀려나** 어디를 겨눠도 빨간색이었다.
        /// 기본 맵의 상점이 4×6 m 방 한가운데 앉는 바람에 사방이 그 상태였고,
        /// 「상점에서 설치권을 사고 그 자리에 세운다」가 통째로 막혔다(2026-09-08).
        /// 최소 거리는 없앴다 — 벽이 가까우면 고스트도 가까이 선다.
        /// </summary>
        private static Vector3 GhostPosition(Transform _player, TechId _id)
        {
            Camera cam = Camera.main;
            Vector3 dir = cam != null ? cam.transform.forward : _player.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = _player.forward;
            dir.Normalize();

            Vector3 origin = _player.position + Vector3.up * 1.5f;
            float distance = GHOST_MAX_DISTANCE;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, GHOST_MAX_DISTANCE,
                    ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                // 벽 앞에서 멈춘다 — 최소 거리를 이유로 **벽을 넘지 않는다**
                distance = hit.distance - GHOST_WALL_GAP;
            }
            distance = Mathf.Clamp(distance, 0f, GHOST_MAX_DISTANCE);

            Vector3 feet = _player.position;
            for (int step = 0; step <= GHOST_RETREAT_STEPS; step++)
            {
                float d = distance * (1f - (float)step / GHOST_RETREAT_STEPS);
                Vector3 at = feet + dir * d;
                at.y = feet.y;   // 지형 높이는 평면 맵이라 플레이어 발치 기준
                if (ShopController.PlacementFits(_id, at)) return at;
            }
            return feet;   // 발치까지 막혔다 — 고스트가 빨간색으로 그 사실을 보여 준다
        }

        /// <summary>ESC 처리 — `EscapeRouter`만 부른다(ESC를 두 곳에서 읽지 않으려고).</summary>
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

        // URP Unlit 반투명 — 런타임 생성이라 머티리얼 에셋 없이 쓴다
        // 원본은 씬 생성기가 만든 에셋(m_GhostMaterialSource)이고, 여기선 색을 바꿔 쓸 사본만 만든다.
        //
        // 예전엔 Shader.Find("Universal Render Pipeline/Unlit")로 런타임 생성했는데,
        // 빌드에서는 그 셰이더를 참조하는 에셋이 없어 스트리핑돼 null이 반환됐다 →
        // new Material(null)이 터지고 설치가 통째로 막혔다(에디터에서는 재현 안 됨).
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
