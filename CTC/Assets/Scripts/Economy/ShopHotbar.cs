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
    /// 하단 상시 핫바 — 테크 v3(2026-09-07). **역할이 둘로 갈린다**(2026-09-11):
    ///
    /// **보유 전용(쥐)** — 칸 = 산 테크. **숫자키는 언제나 「쓴다」**(요새 즉발·창고 조준·대시).
    /// 사는 곳은 상점 팝업(E)으로 갔다(2026-09-19) — 10종을 5칸으로 훑던 「후보 넘기기 + Enter 확정」은 사라졌다.
    ///
    /// **고정(고양이)** — 6종에 칸 6개, 카탈로그 순서로 **1키 = 1테크**. 넘기기도 Enter도 없다:
    /// 상점 면제라 늘 구매 모드고 6종 중 다섯이 순수 스탯이라 「고르기」가 값을 못 했다 —
    /// 오히려 두 칸이 같은 테크를 가리키는 사고만 냈다(2026-09-11 신고). 액티브(광분)는 Space가 맡는다.
    /// 커서는 끝까지 잠겨 있다 — 3인칭에서 커서가 곧 시야다.
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

        // 액티브를 쓴 뒤 **쿨다운이 도착할 때까지** 다시 그리기를 켜 두는 시간(초).
        // 쿨다운의 주인은 서버이고 클론에서는 왕복이 있어, 쓴 직후 한 번만 그리면 아직 0을 본다.
        private const float USE_REDRAW_GRACE_SECONDS = 1.5f;

        // 숫자키는 1~9까지만 있다 — 칸이 더 늘면 키가 아니라 다른 조작이 필요하다
        private const int MAX_HOTKEYS = 9;

        // 액티브 전용 키의 글자 — 단일 출처는 능력 바다(같은 키를 두 화면이 다르게 부르면 안 된다).
        private const string ACTIVE_KEY_LABEL = UI.AbilityBarLogic.KEY_FRENZY;

        // 상점 원 안인데 철문이 내려왔을 때 모드 줄에 붙는 말 — "E = 상점" 자리(2026-09-19)
        public const string SHOP_CLOSED_LABEL = "철문 닫힘";

        // 고스트가 뻗는 최대 거리 — 서버 검증(ShopController.MAX_PLACE_DISTANCE)보다 짧아야 한다.
        private static float GHOST_MAX_DISTANCE => Info.InfoManager.Instance.Balance.PlaceGhostMaxMeters;

        // 칸 수는 구조 제약(기획 §2)이고 **역할마다 다르다**(2026-09-11):
        // 쥐는 CSV의 전역 상한(5)으로 10종을 훑고, 고양이는 제 테크 수(6)만큼 칸을 갖는다.
        private int SlotCap => TechLogic.SlotCapOf(m_Role);

        private static readonly Color COLOR_HINT = UI.UiPalette.WithAlpha(UI.UiPalette.TextDim, 0.9f);
        private static readonly Color COLOR_READY = UI.UiPalette.TextOn;
        private static readonly Color COLOR_POOR = UI.UiPalette.Bad;
        private static readonly Color COLOR_OWNED = UI.UiPalette.Good;
        private static readonly Color COLOR_EMPTY = UI.UiPalette.WithAlpha(UI.UiPalette.TextDim, 0.5f);
        // 만렙 칸 — 더 살 게 없다. 칸을 지우지는 않는다(핫바가 곧 제 스탯 창이다) 대신 흐리게 물러난다.
        private static readonly Color COLOR_MAXED = UI.UiPalette.WithAlpha(UI.UiPalette.Good, 0.45f);
        // 자동 축소 하한 — 이보다 작아지면 읽히지 않는다
        private const float LABEL_MIN_FONT = 13f;

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
        private Player.CatFrenzy m_Frenzy;
        private RoleId m_Role;
        private int m_Cheese;
        private bool m_GameStarted;
        private bool m_SlotsDirty;
        private bool m_CooldownTicking;   // 지금 화면에 흐르는 쿨다운이 있나 — 있을 때만 다시 그린다
        private float m_NextCooldownRedraw;
        private float m_CooldownWatchUntil;   // 방금 액티브를 썼다 — 쿨다운이 복제돼 올 때까지 지켜본다
        private bool m_IntroShown; // 기본 조작 토스트는 매치당 1회 — 매판 반복되면 소음이다
        private bool m_InShop;
        private bool m_ShopClosed;   // 상점 원 안인데 철문이 닫혔다(2026-09-19) — "E = 상점"을 거두고 닫혔다고 알린다

        // 오늘의 품절(계획 3 Task 3) — 판정은 ShopController가 하고 View는 받아 쓰기만 한다(규칙 5).
        private static readonly TechId[] NONE_SOLD_OUT = new TechId[0];
        private IReadOnlyList<TechId> m_SoldOut = NONE_SOLD_OUT;
        private bool m_SoldOutShown;   // 품절 토스트도 매치당 1회
        private CancellationTokenSource m_SoldOutToastCts;

        private UnityEngine.UI.Image[] m_SlotBgs;
        private Color[] m_SlotBgBase;

        private float m_ToastUntil;
        private TechId m_PlacingId = TechId.None;
        private GameObject m_Ghost;
        private Material m_GhostMaterial;
        private bool m_GhostValid;

        private void Awake()
        {
            m_Root.SetActive(false);
            m_Toast.gameObject.SetActive(false);

            // 씬은 **가장 많은 역할**에 맞춰 굽고 런타임에 역할별로 남는 칸을 숨긴다(2026-09-11).
            // 모자라면 조용히 칸이 사라지므로 알린다.
            int maxCap = TechLogic.SlotCapMax();
            int slots = m_Slots != null ? m_Slots.Length : 0;
            if (slots < maxCap)
            {
                Core.GameDebug.LogWarning(
                    $"[ShopHotbar] 슬롯 {slots}칸 < 최대 칸 수 {maxCap} — Setup Networking Scene 재실행");
            }
            // 칸 배경 — 지금은 바탕색을 되돌릴 일만 있다(「Enter가 살 칸」 표시는 2026-09-19에 사라졌다).
            m_SlotBgs = new UnityEngine.UI.Image[slots];
            m_SlotBgBase = new Color[slots];
            for (int i = 0; i < slots; i++)
            {
                if (m_Slots[i] == null) continue;
                m_SlotBgs[i] = m_Slots[i].GetComponent<UnityEngine.UI.Image>();
                if (m_SlotBgs[i] != null) m_SlotBgBase[i] = m_SlotBgs[i].color;
            }

            // 라벨은 씬이 구워 주지만 **맞춤 규칙은 글자를 쓰는 쪽이 안다.** 「두꺼운 가죽」처럼
            // 긴 이름이 칸 폭을 넘으면 두 줄로 접혀 아래 힌트 줄을 덮는다(2026-09-10 실플레이).
            // 자동 축소를 켜면 폭·높이에 맞춰 줄어들어 겹치지 않는다.
            for (int i = 0; i < (m_SlotLabels != null ? m_SlotLabels.Length : 0); i++)
            {
                if (m_SlotLabels[i] == null) continue;
                m_SlotLabels[i].enableAutoSizing = true;
                m_SlotLabels[i].fontSizeMax = m_SlotLabels[i].fontSize;
                m_SlotLabels[i].fontSizeMin = LABEL_MIN_FONT;
            }
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
                m_Frenzy = null;
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
            m_Frenzy = _player.GetComponent<Player.CatFrenzy>();
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

        /// <summary>
        /// 칸마다 시작 후보를 **어긋나게** 둔다. 전부 0으로 두면 빈 칸 다섯이 똑같은 테크를 보여
        /// 「고를 게 하나뿐」처럼 읽힌다(2026-09-10 실플레이에서 「왜 다 발톱이냐」로 드러났다).
        /// 후보 수보다 칸이 많아도 `% Count`가 접어 주므로 겹치는 건 그때뿐이다.
        /// </summary>
        private void ResetCandidates()
        {
            // 남은 것이 없다 — 후보 넘기기는 2026-09-19에 상점 팝업으로 갔다. 호출부는 그대로 둔다.
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
                // 고정 배치 역할은 넘길 것도 확정할 것도 없다 — 누른 칸이 곧 산 것이다.
                string shop = TechLogic.FixedSlots(m_Role)
                    ? "숫자키로 산다 · 키운다"
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
            Vector3 here = m_Shop.transform.position;
            bool needsSpot = TechLogic.NeedsShopSpot(m_Role);
            bool inShop = !needsSpot || Map.ShopSpot.AnyContains(here);
            // 고양이가 가까이 와 철문이 내려오면 E가 안 먹는다 — 안내도 같이 바꾼다(말없이 안 열리면 고장으로 보인다)
            bool closed = needsSpot && inShop && !Map.ShopSpot.AnyOpenContains(here);
            if (inShop != m_InShop || closed != m_ShopClosed)
            {
                m_InShop = inShop;
                m_ShopClosed = closed;
                m_SlotsDirty = true;
            }

            // 구독 콜백이 아니라 여기서 그리는 이유: 구매 하나가 치즈·테크 목록 등 NetworkVariable 여러 개를
            // 같은 틱에 바꾸는데, 콜백 시점엔 시블링 변수가 아직 옛값일 수 있다. 여기서는 전부 새 값이다.
            // 쿨다운이 흐르는 동안만 다시 그린다 — 매 프레임 RefreshSlots를 도는 건 낭비다.
            // 쓴 직후의 유예(m_CooldownWatchUntil)가 없으면 **첫 쿨다운을 영영 못 본다**:
            // 다시 그리기는 "지금 쿨이 흐른다"를 본 뒤에야 켜지는데, 그걸 볼 기회 자체가
            // 없어서 광분·끈끈이·대시의 남은 초가 칸에 안 찍혔다(2026-09-09 qa_v51 ⑥-a 실측).
            if ((m_CooldownTicking || Time.time < m_CooldownWatchUntil)
                && Time.time >= m_NextCooldownRedraw)
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
            int cap = SlotCap;
            bool fixedSlots = TechLogic.FixedSlots(m_Role);

            // DrawOwned가 다시 켠다 — 쿨이 끝나면 저절로 꺼져 다시 그리기가 멈춘다.
            m_CooldownTicking = false;

            for (int slot = 0; slot < m_Slots.Length; slot++)
            {
                bool visible = slot < cap;
                if (m_Slots[slot].activeSelf != visible) m_Slots[slot].SetActive(visible);
                if (!visible) continue;

                if (fixedSlots) DrawFixedSlot(slot, owned);
                else if (slot < owned.Count) DrawOwned(slot, owned[slot], owned);
                else DrawEmpty(slot);
            }

            // 숫자키의 뜻은 **역할마다 하나뿐**이다(2026-09-19) — 상점 안팎으로 바뀌지 않는다.
            // 상점 원 안에 서 있으면 "E = 상점"만 덧붙인다(구매는 팝업이 맡는다). 철문이 닫혔으면 그렇다고.
            string mode = fixedSlots ? "숫자키 = 산다 / 키운다" : "숫자키 = 쓴다";
            if (!fixedSlots && m_InShop) mode += m_ShopClosed ? $" · {SHOP_CLOSED_LABEL}" : " · E = 상점";
            if (FirstActiveKeySlot(owned) >= 0) mode += $" · {ACTIVE_KEY_LABEL} = 쓴다";
            if (m_ModeLabel != null) m_ModeLabel.text = $"{mode}    보유 {owned.Count}/{cap}";
        }

        /// <summary>
        /// 이 테크를 <b>액티브 전용 키</b>로 쓰는가 — 상점 면제 역할(고양이)의 액티브만 해당한다.
        ///
        /// 기획 §10이 액티브 전용 키(E·R)를 자른 이유는 「상점에서 키우니 <b>밖의</b> 숫자키가 비었다」인데,
        /// 고양이에게는 그 「밖」이 없다: <see cref="TechLogic.NeedsShopSpot"/>이 false라 핫바가 영원히
        /// 구매 모드고, 그래서 산 광분을 켤 길이 아예 없었다(2026-09-09 감사 P0-1).
        /// 그 역할에만 전용 키를 되살린다 — 쥐의 Space는 <c>MouseDash</c>가 읽으므로 역할이 갈려 겹치지 않는다.
        /// </summary>
        private bool UsesActiveKey(TechId _id) => TechLogic.NeedsActiveKey(m_Role, _id);

        /// <summary>액티브 전용 키가 쓸 맨 왼쪽 칸(-1 = 없음).</summary>
        private int FirstActiveKeySlot(IReadOnlyList<TechEntry> _owned)
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (UsesActiveKey(_owned[i].Id)) return i;
            }
            return -1;
        }

        private void DrawOwned(int _slot, TechEntry _e, IReadOnlyList<TechEntry> _owned)
        {
            TechRow cur = TechLogic.RowOf(_e.Id, _e.Level);
            TechRow next = TechLogic.NextRow(_owned, _e.Id);
            m_SlotLabels[_slot].text = $"{_slot + 1}  {cur.Label} Lv{_e.Level}";
            SetHint(_slot, cur.Hint);

            Color c;
            // 고정 배치 역할(고양이)만 핫바로 산다 — 쥐의 칸은 2026-09-19부터 「쓰는 칸」이라
            // 값이 아니라 쿨다운·사용 가능 여부를 보여 준다(사는 곳은 상점 팝업이다).
            if (TechLogic.FixedSlots(m_Role))
            {
                bool maxed = !next.IsValid;
                bool affordable = next.IsValid && m_Cheese >= next.Cost;
                m_SlotCosts[_slot].text = maxed ? "MAX" : next.Cost.ToString();
                c = maxed ? COLOR_MAXED : affordable ? COLOR_READY : COLOR_POOR;
                // 다 키운 칸은 더 살 게 없다 — 칸은 남기되(핫바가 곧 스탯 창) 글자로 못박고 흐려진다.
                if (maxed) m_SlotLabels[_slot].text = $"{_slot + 1}  {cur.Label} Lv{_e.Level} · 만렙";

                // 구매 모드에서도 **쿨다운이 보여야 한다** — 이 칸은 숫자키로 키우고 Space로 쓴다.
                // 값(치즈)은 숫자 칸이 갖고, 「지금 쓸 수 있나」는 반줄 힌트가 말한다.
                if (UsesActiveKey(_e.Id))
                {
                    float active = CooldownOf(_e.Id);
                    if (active > 0f) m_CooldownTicking = true;
                    SetHint(_slot, active > 0f
                        ? $"{ACTIVE_KEY_LABEL} 쿨 {Mathf.CeilToInt(active)}s"
                        : $"{ACTIVE_KEY_LABEL}로 쓴다 — {cur.Hint}");
                }
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
                // 광분은 상점 면제 역할(고양이)의 액티브라 이 칸이 늘 「산다」 모드로 그려진다 —
                // 여기 없으면 쿨다운이 화면 어디에도 안 나온다(2026-09-09 감사 G-1).
                case TechId.CatFrenzy: return m_Frenzy != null ? m_Frenzy.CooldownRemaining : 0f;
                default: return 0f;
            }
        }

        /// <summary>
        /// 고정 배치 칸(고양이) — **칸 하나가 테크 하나를 평생 맡는다**(2026-09-11).
        /// 샀으면 레벨과 다음 값을, 안 샀으면 Lv1 값을 그린다. 넘기기가 없으니 Enter 표시도 없다.
        /// </summary>
        private void DrawFixedSlot(int _slot, IReadOnlyList<TechEntry> _owned)
        {
            TechId id = TechLogic.SlotTechOf(m_Role, _slot);
            if (id == TechId.None)
            {
                m_SlotLabels[_slot].text = $"{_slot + 1}  —";
                m_SlotCosts[_slot].text = "";
                SetHint(_slot, "");
                m_SlotLabels[_slot].color = COLOR_EMPTY;
                return;
            }

            int level = TechLogic.LevelOf(_owned, id);
            if (level > 0) { DrawOwned(_slot, new TechEntry(id, level), _owned); return; }

            // 이 역할엔 품절이 안 걸리지만(§7은 쥐 테크만) 규칙이 바뀌어도 칸이 거짓말하지 않게 둔다.
            if (TechRandomLogic.IsSoldOut(m_SoldOut, id))
            {
                m_SlotLabels[_slot].text = $"{_slot + 1}  {TechLogic.RowOf(id, 1).Label}";
                m_SlotCosts[_slot].text = "";
                SetHint(_slot, "오늘 품절");
                m_SlotLabels[_slot].color = COLOR_EMPTY;
                m_SlotCosts[_slot].color = COLOR_EMPTY;
                return;
            }
            DrawBuyable(_slot, id);
        }

        /// <summary>
        /// 아직 안 산 칸 — **빈 자리로 둔다**(2026-09-19). 파는 물건은 상점 팝업이 보여 주므로
        /// 핫바가 카탈로그를 겸하지 않는다. 상점 원 안이면 어디서 사는지만 한 줄로 알린다.
        /// 넘기기 역할(쥐) 전용이다 — 고정 배치는 <see cref="DrawFixedSlot"/>가 그린다.
        /// </summary>
        private void DrawEmpty(int _slot)
        {
            m_SlotLabels[_slot].text = $"{_slot + 1}  —";
            m_SlotCosts[_slot].text = "";
            SetHint(_slot, m_InShop && !m_ShopClosed ? "E로 산다" : "");
            m_SlotLabels[_slot].color = COLOR_EMPTY;
        }

        /// <summary>아직 없는 테크 한 칸 — Lv1 값과 힌트. 돈이 모자라면 붉게.</summary>
        private void DrawBuyable(int _slot, TechId _id)
        {
            TechRow row = TechLogic.RowOf(_id, 1);
            bool affordable = m_Cheese >= row.Cost;
            m_SlotLabels[_slot].text = $"{_slot + 1}  {row.Label}";
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

            // 액티브 전용 키(상점 면제 역할). 쓸 게 없으면 false를 주고 숫자키 검사로 흘러간다.
            if (kb.spaceKey.wasPressedThisFrame && PressActiveKey()) return;

            // Enter는 여기서 안 읽는다 — `EscapeRouter`가 단독으로 읽어 채팅으로 보낸다.
            int cap = SlotCap;
            for (int slot = 0; slot < cap && slot < m_Slots.Length && slot < MAX_HOTKEYS; slot++)
            {
                if (!kb[SlotKey(slot)].wasPressedThisFrame) continue;   // 키가 없으면 할당 0
                PressSlot(slot);
                return;
            }
        }

        /// <summary>
        /// 숫자키 한 번 — **사람이 누르는 그 길**. 키를 읽는 곳과 분기를 나눈 이유는 QA다:
        /// wesqa가 <c>ShopController.Use</c>를 직접 부르면 이 분기를 통째로 건너뛰어,
        /// 「고양이는 산 광분을 켤 수 없다」를 통과시킨다(2026-09-09 감사 G-1·C-01).
        /// <c>TestPressSlotKey</c>는 반드시 여기로 들어온다.
        /// </summary>
        public void PressSlot(int _slot)
        {
            if (!m_GameStarted || m_Shop == null || m_TechSlots == null) return;
            if (_slot < 0 || _slot >= SlotCap) return;

            IReadOnlyList<TechEntry> owned = m_TechSlots.Owned;

            // 고정 배치(고양이) — **칸이 곧 테크**다. 안 샀으면 산다, 샀으면 키운다.
            // 이 역할은 상점 면제라 핫바가 늘 구매 모드고(NeedsShopSpot=false) 「쓴다」로 갈 일이 없다.
            // 그래서 액티브(광분)는 숫자키가 아니라 전용 키(Space)가 맡는다 — PressActiveKey.
            if (TechLogic.FixedSlots(m_Role))
            {
                TechId fixedId = TechLogic.SlotTechOf(m_Role, _slot);
                if (fixedId != TechId.None) TryBuy(fixedId);
                return;
            }

            // 넘기기 역할(쥐) — 칸은 **보유 기술**이고 숫자키는 언제나 「쓴다」(2026-09-19).
            if (_slot < owned.Count) TryUse(owned[_slot].Id);
            else if (m_ShopClosed) Say(TechLogic.DenyText(TechDeny.ShopClosed, m_Role, 0, SlotCap));
            else Say(m_InShop ? "E로 상점을 연다" : "상점에서 E로 산다");
        }

        /// <summary>
        /// 액티브 전용 키 한 번 — 상점 면제 역할의 **맨 왼쪽 액티브 칸**을 쓴다.
        /// 지금 그 역할의 액티브는 광분 하나뿐이라 「맨 왼쪽」이 곧 그것이다.
        /// 쓸 칸이 없으면 <c>false</c> — 그 역할이 아니거나 아직 안 샀다는 뜻이다.
        /// </summary>
        public bool PressActiveKey()
        {
            if (!m_GameStarted || m_Shop == null || m_TechSlots == null) return false;

            IReadOnlyList<TechEntry> owned = m_TechSlots.Owned;
            int slot = FirstActiveKeySlot(owned);
            if (slot < 0) return false;

            TryUse(owned[slot].Id);
            return true;
        }

        private void TryBuy(TechId _id)
        {
            TechRow next = TechLogic.NextRow(m_TechSlots.Owned, _id);
            TechDeny deny = m_Shop.CanBuyNow(_id);
            if (deny != TechDeny.None)
            {
                Say(TechLogic.DenyText(deny, m_Role, next.IsValid ? next.Cost - m_Cheese : 0, SlotCap));
                return;
            }
            m_Shop.Buy(_id);
            // 구매 토스트가 곧 사용법이다 — 힌트를 그대로 읽어 준다.
            // 액티브 전용 키를 쓰는 테크는 **어느 키인지도 여기서 알린다**(새 화면 0, 기획 §9).
            string howTo = UsesActiveKey(_id) ? $" · {ACTIVE_KEY_LABEL}로 쓴다" : "";
            Say($"{next.Label} Lv{next.Level} — {next.Hint}{howTo}", TOAST_LONG_SECONDS);
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

            // 쿨다운은 서버가 새기고 복제돼 온다 — 도착할 때까지 다시 그리기를 켜 둔다.
            m_CooldownWatchUntil = Time.time + USE_REDRAW_GRACE_SECONDS;
            m_NextCooldownRedraw = 0f;
            m_SlotsDirty = true;
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
