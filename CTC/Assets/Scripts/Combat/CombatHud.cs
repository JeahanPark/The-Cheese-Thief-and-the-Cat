using CTC.Runtime.Lobby;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>좌상단 아래쪽: 자기 HP + 고양이 HP(공동 목표 게이지).</summary>
    public class CombatHud : MonoBehaviour
    {
        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private TMP_Text m_Text;

        private void Update()
        {
            bool show = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
            if (m_Text.gameObject.activeSelf != show)
            {
                m_Text.gameObject.SetActive(show);
            }
            if (!show) return;

            NetworkManager nm = NetworkManager.Singleton;
            NetworkObject local = nm != null && nm.SpawnManager != null
                ? nm.SpawnManager.GetLocalPlayerObject()
                : null;
            Health myHealth = local != null ? local.GetComponent<Health>() : null;

            Health catHealth = null;
            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                RoleTag tag = h.GetComponent<RoleTag>();
                if (tag != null && tag.Role == RoleId.Cat) { catHealth = h; break; }
            }

            string mine = myHealth != null ? $"HP {myHealth.Hp}/{myHealth.MaxHp}" : "SPECTATING";
            string cat = catHealth != null ? $"CAT {catHealth.Hp}/{catHealth.MaxHp}" : "CAT -";
            m_Text.text = $"{mine}   {cat}";
        }
    }
}
