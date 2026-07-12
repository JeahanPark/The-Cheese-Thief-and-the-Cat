using CTC.Runtime.Lobby;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>자기 지갑 잔액 HUD. 게임 시작 후에만 표시(로비 중 숨김).</summary>
    public class WalletHud : MonoBehaviour
    {
        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private TMP_Text m_Text;

        private CheeseWallet m_LocalWallet;

        private void Update()
        {
            bool show = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
            if (m_Text.gameObject.activeSelf != show)
            {
                m_Text.gameObject.SetActive(show);
            }
            if (!show)
            {
                m_LocalWallet = null;
                return;
            }

            if (m_LocalWallet == null)
            {
                NetworkManager nm = NetworkManager.Singleton;
                NetworkObject local = nm != null && nm.SpawnManager != null
                    ? nm.SpawnManager.GetLocalPlayerObject()
                    : null;
                m_LocalWallet = local != null ? local.GetComponent<CheeseWallet>() : null;
            }

            m_Text.text = m_LocalWallet != null ? $"Cheese: {m_LocalWallet.Cheese}" : "Cheese: -";
        }
    }
}
