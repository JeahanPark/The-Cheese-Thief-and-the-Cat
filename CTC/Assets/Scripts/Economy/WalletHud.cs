using CTC.Runtime.Lobby;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 자기 지갑 잔액 HUD. 게임 시작 후 쥐에게만 표시 —
    /// 고양이는 치즈 수입이 없으므로 노출 안 함(기획 변경 2026-07-22, 잔액은 상점 패널에서 확인).
    /// </summary>
    public class WalletHud : MonoBehaviour
    {
        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private TMP_Text m_Text;

        private CheeseWallet m_LocalWallet;

        private void Update()
        {
            bool show = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
            if (show && m_LocalWallet == null)
            {
                NetworkManager nm = NetworkManager.Singleton;
                NetworkObject local = nm != null && nm.SpawnManager != null
                    ? nm.SpawnManager.GetLocalPlayerObject()
                    : null;
                m_LocalWallet = local != null ? local.GetComponent<CheeseWallet>() : null;
            }
            if (!show) m_LocalWallet = null;

            Combat.RoleTag tag = m_LocalWallet != null ? m_LocalWallet.GetComponent<Combat.RoleTag>() : null;
            show = show && tag != null && tag.Role == RoleId.Mouse;

            if (m_Text.gameObject.activeSelf != show)
            {
                m_Text.gameObject.SetActive(show);
            }
            if (!show) return;

            m_Text.text = $"Cheese: {m_LocalWallet.Cheese}";
        }
    }
}
