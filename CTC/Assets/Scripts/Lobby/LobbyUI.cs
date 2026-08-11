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
    /// 표시 갱신도 R3 구독이다 (`LobbyController.ViewChanged`, 2026-07-31) — 매 프레임 다시 그리지 않는다.
    /// `Update`에 남은 건 커서 잠금뿐이고, 그건 표시가 아니라 입력 계층이다.
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

        private void Start()
        {
            // Awake가 아니라 Start — LobbyController.Awake가 세션·맵 감독을 잡은 뒤여야 한다.
            if (m_Lobby != null) m_Lobby.ViewChanged.Subscribe(_ => Render()).AddTo(this);
        }

        private void OnClickMouse() => m_Lobby.SelectRole(RoleId.Mouse);
        private void OnClickCat() => m_Lobby.SelectRole(RoleId.Cat);
        private void OnClickStart() => m_Lobby.RequestStartGame();
        private void OnClickMap(int _index) => m_Lobby.SelectMap(_index);

        // 커서만 남긴다 — ResultHud·옵션 창이 커서를 점유했다 놓는 시점은 스트림으로 오지 않는다.
        // 판정은 CursorPolicyLogic(EditMode가 덮는다) — 2026-08-08 옵션 창에서 커서가 안 보이던 버그.
        private void Update()
        {
            // **접속 여부는 스트림으로 안 온다**(2026-08-11). 그래서 접속 전에는 Render가 한 번도
            // 안 돌아 로비 패널이 빈 껍데기로 떠 있었다 — 맵 버튼이 "-", 참가자 목록이 공백.
            // 매 프레임 조회가 아니라 **바뀐 순간에만** 다시 그린다(규칙 3의 취지 그대로).
            NetworkManager netManager = NetworkManager.Singleton;
            bool connectedNow = netManager != null && (netManager.IsClient || netManager.IsServer);
            if (connectedNow != m_WasConnected)
            {
                m_WasConnected = connectedNow;
                Render();
            }

            bool inGame = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;

            if (CursorPolicyLogic.ShouldRelease(inGame, Match.ResultHud.IsShowing, Core.UiFocus.ModalOpen))
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            else if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private bool m_WasConnected;

        private void Render()
        {
            NetworkManager nm = NetworkManager.Singleton;
            bool connected = nm != null && (nm.IsClient || nm.IsServer);
            bool inLobby = connected && m_Lobby != null && m_Lobby.IsSpawned && !m_Lobby.GameStarted;

            m_Panel.SetActive(inLobby);
            if (!inLobby) return;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < m_Lobby.PlayerCount; i++)
            {
                LobbyPlayerState p = m_Lobby.GetPlayer(i);
                string who = p.ClientId == nm.LocalClientId ? "나" : $"플레이어 {i + 1}";
                sb.AppendLine($"{who} — {RoleName(p.Role)}");
            }
            m_PlayerListText.text = sb.ToString();

            bool isHost = nm.IsHost;
            m_StartButton.gameObject.SetActive(isHost);
            m_StartButton.interactable = isHost && m_Lobby.CanStart;

            UpdateMapButtons(isHost);
        }

        // "None"을 그대로 보여 주면 뭘 해야 하는지 알 수가 없다 — 다음 행동을 말해 준다.
        private static string RoleName(RoleId _role)
        {
            switch (_role)
            {
                case RoleId.Cat: return "고양이";
                case RoleId.Mouse: return "쥐";
                default: return "아직 안 골랐다";
            }
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
                string wanted = ShortMapName(m_Lobby.MapNameAt(i));
                if (text != null && text.text != wanted) text.text = wanted;
            }
        }

        /// <summary>
        /// 버튼에는 이름만 — 괄호 안 크기는 뗀다. 맵이 셋이면 버튼 하나가 120px인데
        /// "대형 맵 (120×120)"을 그대로 넣으면 두 줄로 접혀 버튼 밖으로 흘러나온다.
        /// 크기는 바로 위 "맵: ~" 줄과 좌하단 맵 설명이 이미 보여 준다.
        /// </summary>
        private static string ShortMapName(string _displayName)
        {
            if (string.IsNullOrEmpty(_displayName)) return _displayName;

            int paren = _displayName.IndexOf('(');
            return paren > 0 ? _displayName.Substring(0, paren).TrimEnd() : _displayName;
        }
    }
}
