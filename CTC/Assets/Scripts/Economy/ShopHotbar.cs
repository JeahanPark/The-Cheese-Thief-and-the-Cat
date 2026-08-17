using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 하단 상시 핫바 — 숫자키 1~6으로 즉시 구매(커서 잠금 유지, 게임 안 멈춤).
    /// 창고만 고스트 미리보기 모드로 진입해 좌클릭 확정 / 우클릭·ESC 취소. 요새는 즉시(내 위치 중심).
    /// 구 B키 패널을 대체한다 (UX 개정 2026-07-27 — 쫓기는 중에도 살 수 있어야 한다).
    ///
    /// 개편 3b 마무리(2026-08-08): Update 폴링 → R3 구독. 매 프레임 로컬 플레이어를 찾고
    /// 슬롯 6칸 문자열·색을 재조립하던 것을 `LocalPlayerBinder.LocalPlayer` +
    /// `CheeseWallet.Cheese` 구독으로 바꿨다(View 8개 중 마지막). Update에 남은 건
    /// 핫키·고스트 추적·토스트 시한 — 프레임 단위 입력/연출이라 스트림 대상이 아니다.
    /// </summary>
    public class ShopHotbar : MonoBehaviour
    {
        private const float TOAST_SECONDS = 1.4f;
        // HUD 개편(2026-08-15): 오류 토스트(1.4s)와 달리 **사용법을 가르치는** 토스트는 읽을 시간을 준다.
        private const float TOAST_LONG_SECONDS = 3f;
        private const float INTRO_TOAST_SECONDS = 4f;
        private const float GHOST_MIN_DISTANCE = 1.5f;

        // 고스트가 뻗는 최대 거리 — 서버 검증(ShopController.MAX_PLACE_DISTANCE)보다 짧아야 한다.
        private static float GHOST_MAX_DISTANCE => Info.InfoManager.Instance.Balance.PlaceGhostMaxMeters;

        private static readonly string[] ITEM_LABELS =
        {
            "새총", "기관총", "대시", "요새", "창고", "공격 강화", "이속 강화",
        };

        // 슬롯 반줄 힌트 — "딱봤을 때 뭔지"만. 사용법 문장은 구매 토스트(UsageOf)의 몫.
        private static readonly string[] ITEM_HINTS =
        {
            "더 세고 멀리", "연사 — 고양이 사냥", "Space 순간 가속",
            "3방 버티는 즉석 벙커", "자동 치즈 생산", "덮치기 더 아프게", "더 빨리 달리게",
        };

        private static readonly Color COLOR_HINT = new Color(0.73f, 0.76f, 0.82f, 0.8f);

        private static readonly Color COLOR_READY = Color.white;
        private static readonly Color COLOR_POOR = new Color(1f, 0.45f, 0.35f);
        private static readonly Color COLOR_OWNED = new Color(0.5f, 0.82f, 0.42f);

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private GameObject m_Root;
        [SerializeField] private GameObject[] m_Slots;     // 슬롯 루트(표시/숨김)
        [SerializeField] private TMP_Text[] m_SlotLabels;  // "1 새총"
        [SerializeField] private TMP_Text[] m_SlotHints;   // 반줄 힌트 "고양이 낚는 가짜 쥐"
        [SerializeField] private TMP_Text[] m_SlotCosts;   // "150" / "보유"
        [SerializeField] private TMP_Text m_Toast;
        // 설치 미리보기 원본 — 에셋으로 둬야 빌드에서 셰이더가 스트리핑되지 않는다
        [SerializeField] private Material m_GhostMaterialSource;

        /// <summary>고스트 배치 중인가 — `EscapeRouter`가 ESC를 무엇에 쓸지 정할 때 본다.</summary>
        public bool IsPlacing => m_PlacingId != ShopItemId.None;

        private readonly SerialDisposable m_WalletSub = new SerialDisposable();

        private ShopController m_Shop;                       // 로컬 플레이어의 상점 (null = 관전·매치 전)
        private RoleId m_Role;
        private System.Collections.Generic.List<int> m_Indices;
        private int m_Cheese;
        private bool m_GameStarted;
        private bool m_SlotsDirty;
        private bool m_IntroShown; // 기본 조작 토스트는 매치당 1회 — 매판 반복되면 소음이다

        private float m_ToastUntil;
        private ShopItemId m_PlacingId = ShopItemId.None;
        private GameObject m_Ghost;
        private Material m_GhostMaterial;
        private bool m_GhostValid;

        private void Awake()
        {
            m_Root.SetActive(false);
            m_Toast.gameObject.SetActive(false);
        }

        private void Start()
        {
            m_WalletSub.AddTo(this);

            m_Lobby.GameStartedChanged.Subscribe(OnGameStartedChanged).AddTo(this);
            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
        }

        private void OnDestroy()
        {
            CancelPlacement();
            if (m_GhostMaterial != null) Destroy(m_GhostMaterial);
        }

        private void OnGameStartedChanged(bool _started)
        {
            m_GameStarted = _started;
            if (!_started) m_IntroShown = false;
            ApplyActive();
        }

        // 플레이어가 바뀌면 지갑 구독을 통째로 갈아 끼운다(SerialDisposable이 이전 것을 끊는다)
        private void OnLocalPlayerChanged(NetworkObject _player)
        {
            ShopController shop = _player != null ? _player.GetComponent<ShopController>() : null;
            RoleTag tag = _player != null ? _player.GetComponent<RoleTag>() : null;
            CheeseWallet wallet = _player != null ? _player.GetComponent<CheeseWallet>() : null;

            if (shop == null || tag == null || wallet == null)
            {
                m_Shop = null;
                m_WalletSub.Disposable = null;
                ApplyActive();
                return;
            }

            m_Shop = shop;
            m_Role = tag.Role;
            m_Indices = ShopLogic.RoleCatalogIndices(m_Role);
            m_WalletSub.Disposable = wallet.Cheese.Subscribe(c =>
            {
                m_Cheese = c;
                m_SlotsDirty = true;
            });
            ApplyActive();
        }

        // 표시 여부는 시작 신호 × 로컬 상점 — 어느 쪽이 바뀌어도 여기로 온다
        private void ApplyActive()
        {
            bool inGame = m_GameStarted && m_Shop != null;
            if (m_Root.activeSelf != inGame) m_Root.SetActive(inGame);
            if (inGame) m_SlotsDirty = true;
            else CancelPlacement();

            // 첫 스폰 1회 기본 조작 안내(2026-08-15) — 좌하단 장문을 칩으로 줄이면서
            // WASD류 기본 조작이 화면에서 사라졌다. 처음 한 번만 토스트로 알린다.
            if (inGame && !m_IntroShown)
            {
                m_IntroShown = true;
                string attack = m_Role == RoleId.Cat ? "덮치기" : "발사";
                Say($"WASD 이동 · 마우스 시점 · 좌클릭 {attack} · LShift 살금살금 · 숫자키 구매",
                    INTRO_TOAST_SECONDS);
            }
        }

        private void Update()
        {
            if (!m_GameStarted || m_Shop == null) return;

            // 구독 콜백이 아니라 여기서 그리는 이유: 구매 하나가 치즈·팝건 티어 등 NetworkVariable
            // 여러 개를 같은 틱에 바꾸는데, 치즈 콜백 시점엔 시블링 변수가 아직 옛값일 수 있다.
            // NGO가 EarlyUpdate에서 배치를 다 적용한 뒤라 여기서는 전부 새 값이다.
            if (m_SlotsDirty)
            {
                m_SlotsDirty = false;
                RefreshSlots();
            }

            if (m_PlacingId != ShopItemId.None) UpdatePlacement();
            else HandleHotkeys();

            if (m_Toast.gameObject.activeSelf && Time.time >= m_ToastUntil)
            {
                m_Toast.gameObject.SetActive(false);
            }
        }

        private void RefreshSlots()
        {
            for (int slot = 0; slot < m_Slots.Length; slot++)
            {
                bool used = slot < m_Indices.Count;
                if (m_Slots[slot].activeSelf != used) m_Slots[slot].SetActive(used);
                if (!used) continue;

                ShopItem item = ShopLogic.CATALOG[m_Indices[slot]];
                // 요새는 소유 개념이 없다(위치 조건뿐) — 슬롯은 항상 가격을 보여 준다
                bool owned = item.Id != ShopItemId.Fortress && !m_Shop.CanApply(item.Id);
                bool affordable = ShopLogic.CanBuy(m_Cheese, item.Cost);

                m_SlotLabels[slot].text = $"{slot + 1}  {ITEM_LABELS[m_Indices[slot]]}";
                m_SlotCosts[slot].text = owned ? "보유" : item.Cost.ToString();
                Color color = owned ? COLOR_OWNED : affordable ? COLOR_READY : COLOR_POOR;
                m_SlotLabels[slot].color = color;
                m_SlotCosts[slot].color = color;

                // 반줄 힌트 — 상태색을 안 따른다(정보이지 상태가 아니다).
                // 씬 재생성 전(미배선)에도 죽지 않게 비어 있으면 건너뛴다.
                if (m_SlotHints != null && slot < m_SlotHints.Length && m_SlotHints[slot] != null)
                {
                    m_SlotHints[slot].text = ITEM_HINTS[m_Indices[slot]];
                    m_SlotHints[slot].color = COLOR_HINT;
                }
            }
        }

        private void HandleHotkeys()
        {
            // 인게임 콘솔 등 입력창에 포커스가 있으면 타이핑이 구매로 새지 않게 무시
            Keyboard kb = Keyboard.current;
            if (kb == null || Core.UiFocus.BlocksGameplay) return;

            for (int slot = 0; slot < m_Indices.Count && slot < ShopLogic.HOTBAR_SLOTS; slot++)
            {
                if (!kb[SlotKey(slot)].wasPressedThisFrame) continue;

                ShopItem item = ShopLogic.CATALOG[m_Indices[slot]];
                string name = ITEM_LABELS[m_Indices[slot]];

                if (!m_Shop.CanApply(item.Id))
                {
                    // 요새는 "보유"가 아니라 "지금 이 자리"의 문제다 — 문구가 거짓이면 안 된다
                    Say(item.Id == ShopItemId.Fortress ? "요새 안에서는 못 세운다 — 밖으로 나가서" : $"{name} 이미 보유");
                    return;
                }
                if (!ShopLogic.CanBuy(m_Cheese, item.Cost))
                {
                    // 재화 이름은 역할별 — 고양이는 치즈를 캐지 않는다
                    Say($"{ShopLogic.CurrencyNameOf(m_Role)} 부족 · {item.Cost - m_Cheese} 모자람");
                    return;
                }

                // 구매 토스트가 곧 사용법이다(2026-08-15) — "새총 구매"만으로는 뭐가 달라졌는지 모른다
                if (ShopLogic.IsPlaceable(item.Id)) BeginPlacement(item.Id, name);
                else { m_Shop.Buy(item.Id); Say(UsageOf(item.Id), TOAST_LONG_SECONDS); }
                return;
            }
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
                default: return Key.Digit6;
            }
        }

        // ===== 설치물 고스트 =====

        private void BeginPlacement(ShopItemId _itemId, string _name)
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

            m_PlacingId = _itemId;

            Vector3 half = ShopController.HalfExtentsOf(_itemId);
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

            Vector3 pos = GhostPosition(m_Shop.transform);
            m_GhostValid = ShopController.PlacementFits(m_PlacingId, pos);

            Vector3 half = ShopController.HalfExtentsOf(m_PlacingId);
            m_Ghost.transform.position = pos + Vector3.up * half.y;
            m_GhostMaterial.color = m_GhostValid
                ? new Color(0.45f, 0.85f, 0.35f, 0.35f)
                : new Color(0.9f, 0.3f, 0.2f, 0.35f);

            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (!m_GhostValid) { Say("여기엔 설치할 수 없다"); return; }

            m_Shop.BuyAt(m_PlacingId, pos); // 차감은 서버가 설치 확정 시점에
            // 서버 재검증 실패(드묾 — 고스트가 이미 걸렀다)면 거절음만 나고 이 토스트는 살짝 거짓이 된다
            ShopItemId placed = m_PlacingId;
            CancelPlacement();
            Say(UsageOf(placed), TOAST_LONG_SECONDS);
        }

        // 카메라 전방 지면 지점 — 플레이어 기준 1.5~4m로 클램프
        private static Vector3 GhostPosition(Transform _player)
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
                distance = Mathf.Max(GHOST_MIN_DISTANCE, hit.distance - 0.5f);
            }

            Vector3 target = _player.position + dir * distance;
            target.y = _player.position.y; // 지형 높이는 평면 맵이라 플레이어 발치 기준
            return target;
        }

        /// <summary>ESC 처리 — `EscapeRouter`만 부른다(ESC를 두 곳에서 읽지 않으려고).</summary>
        public void CancelPlacementFromEscape()
        {
            CancelPlacement();
            Say("배치 취소");
        }

        private void CancelPlacement()
        {
            m_PlacingId = ShopItemId.None;
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

        // 구매 직후 사용법 한 줄 — 수치는 안 적는다(칩·힌트와 달리 이건 "그래서 뭘 하면 되나"다)
        private static string UsageOf(ShopItemId _id)
        {
            switch (_id)
            {
                case ShopItemId.Slingshot: return "새총 장착 — 좌클릭 발사가 더 세고 멀리 간다";
                case ShopItemId.MachineGun: return "기관총 장착 — 좌클릭 연사로 고양이를 노린다";
                case ShopItemId.Dash: return "대시 습득 — Space로 순간 가속해 거리를 벌린다";
                case ShopItemId.Fortress: return "요새 설치 — 고양이는 못 들어온다 · 세 방이면 부서진다";
                case ShopItemId.Warehouse: return "창고 설치 — 자리를 비워도 치즈가 쌓인다 · 소리로 위치가 팔린다";
                case ShopItemId.CatAttack: return "공격 강화 — 덮치기가 더 아프다";
                case ShopItemId.CatSpeed: return "이속 강화 — 이동이 더 빨라진다";
                default: return "";
            }
        }
    }
}
