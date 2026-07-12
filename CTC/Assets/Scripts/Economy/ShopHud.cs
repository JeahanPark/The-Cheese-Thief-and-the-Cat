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
    /// B키 상점 패널. 역할에 맞는 카탈로그 버튼만 표시, 잔액·구매가능 상태 갱신.
    /// 구매 클릭은 로컬 플레이어의 ShopController.Buy — 판정 권위는 서버.
    /// 열림 동안 커서를 해제하며, LobbyUI의 인게임 강제 잠금은 IsOpen으로 양보받는다.
    /// </summary>
    public class ShopHud : MonoBehaviour
    {
        /// <summary>상점이 열려 커서를 점유 중인지 — LobbyUI가 잠금 강제 전 확인.</summary>
        public static bool IsOpen { get; private set; }

        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private GameObject m_Panel;
        [SerializeField] private TMP_Text m_BalanceText;
        [SerializeField] private Button[] m_Buttons; // ShopLogic.CATALOG 순서

        // MUIP 기본 TMP 폰트가 한글 글리프 미포함 — 영문 라벨 사용 (한글화는 폰트 에셋 도입 시)
        private static readonly string[] ITEM_LABELS =
        {
            "Slingshot", "Machine Gun", "Dash", "Decoy", "ATK Up", "SPD Up",
        };

        private void Awake()
        {
            for (int i = 0; i < m_Buttons.Length; i++)
            {
                int idx = i; // 클로저 캡처
                m_Buttons[i].onClick.AddListener(() => BuyIndex(idx));
            }
            m_Panel.SetActive(false);
            IsOpen = false;
        }

        private void OnDestroy()
        {
            IsOpen = false; // 씬 전환/종료 시 잔존 플래그 방지
        }

        private void Update()
        {
            bool inGame = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted
                          && LocalShop() != null;
            if (!inGame)
            {
                if (m_Panel.activeSelf) Close();
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.bKey.wasPressedThisFrame)
            {
                if (m_Panel.activeSelf) Close();
                else Open();
            }

            if (m_Panel.activeSelf) Refresh();
        }

        private void Open()
        {
            m_Panel.SetActive(true);
            IsOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Close()
        {
            m_Panel.SetActive(false);
            IsOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Refresh()
        {
            ShopController shop = LocalShop();
            if (shop == null) { Close(); return; }

            CheeseWallet wallet = shop.GetComponent<CheeseWallet>();
            RoleTag tag = shop.GetComponent<RoleTag>();
            int cheese = wallet != null ? wallet.Cheese : 0;
            m_BalanceText.text = $"Cheese: {cheese}";

            for (int i = 0; i < m_Buttons.Length && i < ShopLogic.CATALOG.Length; i++)
            {
                ShopItem item = ShopLogic.CATALOG[i];
                bool forMyRole = tag != null && tag.Role == item.Role;
                m_Buttons[i].gameObject.SetActive(forMyRole);
                if (!forMyRole) continue;

                // MUIP 버튼은 상태별 TMP 라벨이 여러 개(normal/highlighted) — 전부 갱신
                foreach (TMP_Text label in m_Buttons[i].GetComponentsInChildren<TMP_Text>(true))
                {
                    label.text = $"{ITEM_LABELS[i]} {item.Cost}";
                }
                m_Buttons[i].interactable = ShopLogic.CanBuy(cheese, item.Cost) && shop.CanApply(item.Id);
            }
        }

        private void BuyIndex(int _index)
        {
            ShopController shop = LocalShop();
            if (shop == null || _index >= ShopLogic.CATALOG.Length) return;
            shop.Buy(ShopLogic.CATALOG[_index].Id);
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
