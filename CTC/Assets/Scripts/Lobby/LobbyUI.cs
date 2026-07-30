using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 로비 UGUI 컨트롤러(Modern UI Pack 위젯은 SetupNetworkingScene이 배선).
    /// 접속 후~시작 전 패널 표시, 역할 선택/시작 버튼, 커서 관리.
    ///
    /// 버튼 연결은 R3 (CTC 규칙 2·4) — 구독은 버튼 파괴 시 자동 해제되고,
    /// 핸들러가 `OnClick~` 이름을 가져 검색·재사용이 된다.
    /// 표시 갱신은 아직 Update 폴링이다 — 개편 3b단계에서 R3 구독으로 바뀐다.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        private static readonly Color MAP_SELECTED = new Color(0.45f, 0.95f, 0.45f);
        private static readonly Color MAP_NORMAL = Color.white;

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private GameObject m_Panel;
        [SerializeField] private TMP_Text m_PlayerListText;
        [SerializeField] private Button m_MouseButton;
        [SerializeField] private Button m_CatButton;
        [SerializeField] private Button m_StartButton;
        [SerializeField] private Button[] m_MapButtons;   // 인덱스 = 맵 인덱스
        [SerializeField] private TMP_Text m_MapLabel;

        private void Awake()
        {
            m_MouseButton.OnClickAsObservable().Subscribe(_ => OnClickMouse()).AddTo(this);
            m_CatButton.OnClickAsObservable().Subscribe(_ => OnClickCat()).AddTo(this);
            m_StartButton.OnClickAsObservable().Subscribe(_ => OnClickStart()).AddTo(this);

            for (int i = 0; i < m_MapButtons.Length; i++)
            {
                int index = i; // 클로저 캡처 — 루프 변수를 그대로 넘기면 전부 마지막 값이 된다
                m_MapButtons[i].OnClickAsObservable().Subscribe(_ => OnClickMap(index)).AddTo(this);
            }
        }

        private void OnClickMouse() => m_Lobby.SelectRole(RoleId.Mouse);
        private void OnClickCat() => m_Lobby.SelectRole(RoleId.Cat);
        private void OnClickStart() => m_Lobby.RequestStartGame();
        private void OnClickMap(int _index) => m_Lobby.SelectMap(_index);

        private void Update()
        {
            NetworkManager nm = NetworkManager.Singleton;
            bool connected = nm != null && (nm.IsClient || nm.IsServer);
            bool inLobby = connected && m_Lobby != null && m_Lobby.IsSpawned && !m_Lobby.GameStarted;

            m_Panel.SetActive(inLobby);

            if (m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted)
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
            for (int i = 0; i < m_Lobby.PlayerCount; i++)
            {
                LobbyPlayerState p = m_Lobby.GetPlayer(i);
                string self = p.ClientId == nm.LocalClientId ? " (You)" : "";
                sb.AppendLine($"Client {p.ClientId}{self}: {p.Role}");
            }
            m_PlayerListText.text = sb.ToString();

            bool isHost = nm.IsHost;
            m_StartButton.gameObject.SetActive(isHost);
            m_StartButton.interactable = isHost && m_Lobby.CanStart;

            UpdateMapButtons(isHost);
        }

        // 맵은 방장만 바꾼다(서버도 같은 가드). 비호스트에게도 어떤 맵인지는 보여야 한다.
        private void UpdateMapButtons(bool _isHost)
        {
            int selected = m_Lobby.MapIndex;
            if (m_MapLabel != null) m_MapLabel.text = $"맵: {m_Lobby.MapNameAt(selected)}";

            for (int i = 0; i < m_MapButtons.Length; i++)
            {
                bool exists = i < m_Lobby.MapCount;
                if (m_MapButtons[i].gameObject.activeSelf != exists) m_MapButtons[i].gameObject.SetActive(exists);
                if (!exists) continue;

                m_MapButtons[i].interactable = _isHost && i != selected;
                Graphic target = m_MapButtons[i].targetGraphic;
                if (target != null) target.color = i == selected ? MAP_SELECTED : MAP_NORMAL;

                // 버튼 라벨은 에셋 파일명이 아니라 맵의 표시 이름
                var text = m_MapButtons[i].GetComponentInChildren<TMP_Text>();
                string wanted = m_Lobby.MapNameAt(i);
                if (text != null && text.text != wanted) text.text = wanted;
            }
        }
    }
}
