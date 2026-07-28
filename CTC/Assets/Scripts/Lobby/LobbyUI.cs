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
        [SerializeField] private Button[] mapButtons;   // 인덱스 = 맵 인덱스
        [SerializeField] private TMP_Text mapLabel;

        private static readonly Color MAP_SELECTED = new Color(0.45f, 0.95f, 0.45f);
        private static readonly Color MAP_NORMAL = Color.white;

        private void Awake()
        {
            mouseButton.onClick.AddListener(() => lobby.SelectRole(RoleId.Mouse));
            catButton.onClick.AddListener(() => lobby.SelectRole(RoleId.Cat));
            startButton.onClick.AddListener(() => lobby.RequestStartGame());

            for (int i = 0; i < mapButtons.Length; i++)
            {
                int index = i; // 클로저 캡처 — 루프 변수를 그대로 넘기면 전부 마지막 값이 된다
                mapButtons[i].onClick.AddListener(() => lobby.SelectMap(index));
            }
        }

        private void Update()
        {
            NetworkManager nm = NetworkManager.Singleton;
            bool connected = nm != null && (nm.IsClient || nm.IsServer);
            bool inLobby = connected && lobby != null && lobby.IsSpawned && !lobby.GameStarted;

            panel.SetActive(inLobby);

            if (lobby != null && lobby.IsSpawned && lobby.GameStarted)
            {
                // 결과 패널이 커서를 점유 중이면 양보 (핫바는 커서를 쓰지 않는다)
                if (!Match.ResultHud.IsShowing && Cursor.lockState != CursorLockMode.Locked)
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

            UpdateMapButtons(isHost);
        }

        // 맵은 방장만 바꾼다(서버도 같은 가드). 비호스트에게도 어떤 맵인지는 보여야 한다.
        private void UpdateMapButtons(bool isHost)
        {
            int selected = lobby.MapIndex;
            if (mapLabel != null) mapLabel.text = $"맵: {lobby.MapNameAt(selected)}";

            for (int i = 0; i < mapButtons.Length; i++)
            {
                bool exists = i < lobby.MapCount;
                if (mapButtons[i].gameObject.activeSelf != exists) mapButtons[i].gameObject.SetActive(exists);
                if (!exists) continue;

                mapButtons[i].interactable = isHost && i != selected;
                var target = mapButtons[i].targetGraphic;
                if (target != null) target.color = i == selected ? MAP_SELECTED : MAP_NORMAL;

                // 버튼 라벨은 에셋 파일명이 아니라 맵의 표시 이름
                var text = mapButtons[i].GetComponentInChildren<TMP_Text>();
                string wanted = lobby.MapNameAt(i);
                if (text != null && text.text != wanted) text.text = wanted;
            }
        }
    }
}
