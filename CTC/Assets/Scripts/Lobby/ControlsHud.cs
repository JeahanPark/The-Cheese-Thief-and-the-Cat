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
        // 한글 표시는 Pretendard 동적 폰트(Tools/CTC/Create Korean Font Asset) 전제
        private const string MOUSE_CONTROLS =
            "WASD 이동   마우스 시점   좌클릭 발사   LShift 대시 (구매 후)\n숫자키 1~5 구매 (하단 핫바)";
        private const string CAT_CONTROLS =
            "WASD 이동   마우스 시점   좌클릭 덮치기\n숫자키 1~2 강화 (하단 핫바)";

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
