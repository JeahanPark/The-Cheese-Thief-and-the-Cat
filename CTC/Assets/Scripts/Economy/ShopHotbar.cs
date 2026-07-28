using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 하단 상시 핫바 — 숫자키 1~6으로 즉시 구매(커서 잠금 유지, 게임 안 멈춤).
    /// 설치물(요새·미끼·창고)은 고스트 미리보기 모드로 진입해 좌클릭 확정 / 우클릭·ESC 취소.
    /// 구 B키 패널을 대체한다 (UX 개정 2026-07-27 — 쫓기는 중에도 살 수 있어야 한다).
    /// </summary>
    public class ShopHotbar : MonoBehaviour
    {
        private const float TOAST_SECONDS = 1.4f;
        private const float GHOST_MIN_DISTANCE = 1.5f;
        private const float GHOST_MAX_DISTANCE = 4f;

        private static readonly string[] ITEM_LABELS =
        {
            "새총", "기관총", "대시", "미끼", "요새", "창고", "공격 강화", "이속 강화",
        };

        private static readonly Color COLOR_READY = Color.white;
        private static readonly Color COLOR_POOR = new Color(1f, 0.45f, 0.35f);
        private static readonly Color COLOR_OWNED = new Color(0.5f, 0.82f, 0.42f);

        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private GameObject m_Root;
        [SerializeField] private GameObject[] m_Slots;     // 슬롯 루트(표시/숨김)
        [SerializeField] private TMP_Text[] m_SlotLabels;  // "1 새총"
        [SerializeField] private TMP_Text[] m_SlotCosts;   // "150" / "보유"
        [SerializeField] private TMP_Text m_Toast;

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

        private void OnDestroy()
        {
            CancelPlacement();
            if (m_GhostMaterial != null) Destroy(m_GhostMaterial);
        }

        private void Update()
        {
            ShopController shop = LocalShop();
            bool inGame = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted && shop != null;

            if (m_Root.activeSelf != inGame) m_Root.SetActive(inGame);
            if (!inGame)
            {
                CancelPlacement();
                return;
            }

            RoleTag tag = shop.GetComponent<RoleTag>();
            CheeseWallet wallet = shop.GetComponent<CheeseWallet>();
            if (tag == null || wallet == null) return;

            var indices = ShopLogic.RoleCatalogIndices(tag.Role);
            RefreshSlots(shop, wallet.Cheese, indices);

            if (m_PlacingId != ShopItemId.None) UpdatePlacement(shop);
            else HandleHotkeys(shop, wallet.Cheese, indices);

            if (m_Toast.gameObject.activeSelf && Time.time >= m_ToastUntil)
            {
                m_Toast.gameObject.SetActive(false);
            }
        }

        private void RefreshSlots(ShopController _shop, int _cheese, System.Collections.Generic.List<int> _indices)
        {
            for (int slot = 0; slot < m_Slots.Length; slot++)
            {
                bool used = slot < _indices.Count;
                if (m_Slots[slot].activeSelf != used) m_Slots[slot].SetActive(used);
                if (!used) continue;

                ShopItem item = ShopLogic.CATALOG[_indices[slot]];
                bool owned = !_shop.CanApply(item.Id);
                bool affordable = ShopLogic.CanBuy(_cheese, item.Cost);

                m_SlotLabels[slot].text = $"{slot + 1}  {ITEM_LABELS[_indices[slot]]}";
                m_SlotCosts[slot].text = owned ? "보유" : item.Cost.ToString();
                Color color = owned ? COLOR_OWNED : affordable ? COLOR_READY : COLOR_POOR;
                m_SlotLabels[slot].color = color;
                m_SlotCosts[slot].color = color;
            }
        }

        private void HandleHotkeys(ShopController _shop, int _cheese, System.Collections.Generic.List<int> _indices)
        {
            // 인게임 콘솔 등 입력창에 포커스가 있으면 타이핑이 구매로 새지 않게 무시
            Keyboard kb = Keyboard.current;
            if (kb == null || IsTypingInUi()) return;

            for (int slot = 0; slot < _indices.Count && slot < ShopLogic.HOTBAR_SLOTS; slot++)
            {
                if (!kb[SlotKey(slot)].wasPressedThisFrame) continue;

                ShopItem item = ShopLogic.CATALOG[_indices[slot]];
                string name = ITEM_LABELS[_indices[slot]];

                if (!_shop.CanApply(item.Id)) { Say($"{name} 이미 보유"); return; }
                if (!ShopLogic.CanBuy(_cheese, item.Cost)) { Say($"치즈 부족 · {item.Cost - _cheese} 모자람"); return; }

                if (ShopLogic.IsPlaceable(item.Id)) BeginPlacement(item.Id, name);
                else { _shop.Buy(item.Id); Say($"{name} 구매"); }
                return;
            }
        }

        private static bool IsTypingInUi()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return false;
            return es.currentSelectedGameObject.GetComponent<TMP_InputField>() != null
                   || es.currentSelectedGameObject.GetComponent<InputField>() != null;
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
            m_PlacingId = _itemId;

            Vector3 half = ShopController.HalfExtentsOf(_itemId);
            m_Ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_Ghost.name = "PlacementGhost";
            Destroy(m_Ghost.GetComponent<Collider>()); // 고스트는 물리 무간섭
            m_Ghost.transform.localScale = half * 2f;

            if (m_GhostMaterial == null) m_GhostMaterial = CreateGhostMaterial();
            m_Ghost.GetComponent<Renderer>().sharedMaterial = m_GhostMaterial;

            Say($"{_name} 배치 — 좌클릭 설치 / 우클릭 취소");
        }

        private void UpdatePlacement(ShopController _shop)
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { CancelPlacement(); Say("배치 취소"); return; }
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) { CancelPlacement(); Say("배치 취소"); return; }

            Vector3 pos = GhostPosition(_shop.transform);
            m_GhostValid = ShopController.PlacementFits(m_PlacingId, pos);

            Vector3 half = ShopController.HalfExtentsOf(m_PlacingId);
            m_Ghost.transform.position = pos + Vector3.up * half.y;
            m_GhostMaterial.color = m_GhostValid
                ? new Color(0.45f, 0.85f, 0.35f, 0.35f)
                : new Color(0.9f, 0.3f, 0.2f, 0.35f);

            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (!m_GhostValid) { Say("여기엔 설치할 수 없다"); return; }

            _shop.BuyAt(m_PlacingId, pos); // 차감은 서버가 설치 확정 시점에
            CancelPlacement();
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

        private void CancelPlacement()
        {
            m_PlacingId = ShopItemId.None;
            if (m_Ghost != null) Destroy(m_Ghost);
            m_Ghost = null;
        }

        // URP Unlit 반투명 — 런타임 생성이라 머티리얼 에셋 없이 쓴다
        private static Material CreateGhostMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        private void Say(string _message)
        {
            m_Toast.text = _message;
            m_Toast.gameObject.SetActive(true);
            m_ToastUntil = Time.time + TOAST_SECONDS;
        }

        private static ShopController LocalShop()
        {
            NetworkManager nm = NetworkManager.Singleton;
            NetworkObject local = nm != null && nm.SpawnManager != null
                ? nm.SpawnManager.GetLocalPlayerObject()
                : null;
            return local != null ? local.GetComponent<ShopController>() : null;
        }
    }
}
