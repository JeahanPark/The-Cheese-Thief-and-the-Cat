using CTC.Runtime.Combat;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 좌하단 조작키 안내. 게임 중에만 표시, 로컬 플레이어 역할에 맞는 단축키를 보여준다.
    /// </summary>
    public class ControlsHud : MonoBehaviour
    {
        // 기본 TMP 폰트에 한글 글리프 없음(상점 라벨과 동일 제약) — 폰트 에셋 도입 전까지 영문
        private const string MOUSE_CONTROLS =
            "WASD Move   Mouse Look   LMB Shoot\nB Shop   LShift Dash (after buy)";
        private const string CAT_CONTROLS =
            "WASD Move   Mouse Look   LMB Pounce\nB Shop";

        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private TMP_Text m_Text;

        private void Update()
        {
            bool show = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
            RoleTag tag = null;
            if (show)
            {
                NetworkManager nm = NetworkManager.Singleton;
                NetworkObject local = nm != null && nm.SpawnManager != null
                    ? nm.SpawnManager.GetLocalPlayerObject()
                    : null;
                tag = local != null ? local.GetComponent<RoleTag>() : null;
                show = tag != null; // 관전자는 표시 안 함
            }

            if (m_Text.gameObject.activeSelf != show)
            {
                m_Text.gameObject.SetActive(show);
            }
            if (!show) return;

            m_Text.text = tag.Role == RoleId.Cat ? CAT_CONTROLS : MOUSE_CONTROLS;
        }
    }
}
