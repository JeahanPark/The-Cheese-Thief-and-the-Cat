using CTC.Runtime.Lobby;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 자기 지갑 잔액 HUD. 게임 시작 후 표시 — 쥐 `Cheese:` / 고양이 `Fun:`(스펙 v3.2).
    /// 관전자(로컬 플레이어 없음)는 숨김.
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
            show = show && tag != null; // 관전자(로컬 플레이어 없음)만 숨김

            if (m_Text.gameObject.activeSelf != show)
            {
                m_Text.gameObject.SetActive(show);
            }
            if (!show) return;

            // 스펙 v3.2: 고양이 재화 = 장난기(Fun) — 같은 지갑, 라벨만 역할별
            // 수입음은 FarmingSfx(3D 공개음)가 담당 — 자기 위치에서 나므로 본인에게도 들림
            string label = tag.Role == RoleId.Cat ? "장난기" : "치즈";
            m_Text.text = $"{label}: {m_LocalWallet.Cheese}";
        }
    }
}
