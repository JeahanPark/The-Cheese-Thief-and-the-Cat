using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 로비 UGUI 컨트롤러(Modern UI Pack 위젯은 SetupNetworkingScene이 배선).
    /// 접속 후~시작 전 패널 표시, 역할 선택/시작 버튼, 커서 관리.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private LobbyManager lobby;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text playerListText;
        [SerializeField] private Button mouseButton;
        [SerializeField] private Button catButton;
        [SerializeField] private Button startButton;

        private void Awake()
        {
            mouseButton.onClick.AddListener(() => lobby.SelectRole(RoleId.Mouse));
            catButton.onClick.AddListener(() => lobby.SelectRole(RoleId.Cat));
            startButton.onClick.AddListener(() => lobby.RequestStartGame());
        }

        private void Update()
        {
            NetworkManager nm = NetworkManager.Singleton;
            bool connected = nm != null && (nm.IsClient || nm.IsServer);
            bool inLobby = connected && lobby != null && lobby.IsSpawned && !lobby.GameStarted;

            panel.SetActive(inLobby);

            if (lobby != null && lobby.IsSpawned && lobby.GameStarted)
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            else if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (!inLobby) return;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lobby.PlayerCount; i++)
            {
                LobbyPlayerState p = lobby.GetPlayer(i);
                string self = p.ClientId == nm.LocalClientId ? " (You)" : "";
                sb.AppendLine($"Client {p.ClientId}{self}: {p.Role}");
            }
            playerListText.text = sb.ToString();

            bool isHost = nm.IsHost;
            startButton.gameObject.SetActive(isHost);
            startButton.interactable = isHost && lobby.CanStart;
        }
    }
}
