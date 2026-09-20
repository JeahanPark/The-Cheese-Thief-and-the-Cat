using CTC.Runtime.Core;
using CTC.Runtime.Networking;
using R3;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 대기실의 입력만 받는다(규칙 5). 버튼은 R3 구독, 핸들러 이름은 OnClick~(규칙 2·4).
    ///
    /// 나가기는 **NGO 세션을 먼저 닫고** 로비로 돌아간다 — 순서를 바꾸면 씬이 사라진 뒤에
    /// 끊기며 유령 세션이 남는다(스팀 방 이탈이 안 돼 재입장이 막힌다).
    /// </summary>
    public class RoomController : MonoBehaviour
    {
        [SerializeField] private RoomSession m_Session;
        [SerializeField] private RoomDirector m_Director;

        [Header("버튼")]
        [SerializeField] private Button m_StartButton;
        [SerializeField] private Button m_LeaveButton;
        // 봇 자리(개발 빌드 전용 UI — 표시 여부는 RoomHudView가 정한다)
        [SerializeField] private Button m_BotPlusButton;
        [SerializeField] private Button m_BotMinusButton;

        /// <summary>맵 버튼 — 배열 순서가 곧 맵 인덱스다(<see cref="RoomMapCatalog"/>와 같은 순서).</summary>
        [SerializeField] private Button[] m_MapButtons;

        /// <summary>나가기·끊김이 돌아가는 곳 — 2026-09-14부터 메인(Intro)이다(R.E.P.O.는 메인 메뉴로 돌아간다).</summary>
        [Header("씬")]
        [SerializeField] private string m_LobbySceneName = "Intro";

        private bool m_Leaving;

        private void Awake()
        {
            // 대기실은 **커서를 놓는다.** 버튼을 눌러야 하는 화면이고, 커서가 잠겨 있으면
            // `OrbitCameraController`가 돌지만 UI를 못 누른다(둘 다는 안 된다).
            // 인게임에서 돌아왔을 때 잠금이 남아 있을 수 있어 여기서 한 번 푼다.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (m_StartButton != null)
                m_StartButton.OnClickAsObservable().Subscribe(_ => OnClickStart()).AddTo(this);

            if (m_LeaveButton != null)
                m_LeaveButton.OnClickAsObservable().Subscribe(_ => OnClickLeave()).AddTo(this);

            if (m_BotPlusButton != null)
                m_BotPlusButton.OnClickAsObservable().Subscribe(_ => OnClickBotPlus()).AddTo(this);

            if (m_BotMinusButton != null)
                m_BotMinusButton.OnClickAsObservable().Subscribe(_ => OnClickBotMinus()).AddTo(this);

            if (m_MapButtons == null) return;
            for (int i = 0; i < m_MapButtons.Length; i++)
            {
                if (m_MapButtons[i] == null) continue;

                int index = i; // 클로저 캡처 — 루프 변수를 그대로 넘기면 전부 마지막 값이 된다
                m_MapButtons[i].OnClickAsObservable().Subscribe(_ => OnClickMap(index)).AddTo(this);
            }
        }

        private void OnEnable()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientDisconnectCallback += OnDisconnected;
        }

        private void OnDisable()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientDisconnectCallback -= OnDisconnected;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>QA 전용 — 시작 버튼을 사람처럼 누르려고 꺼낸다(흐린 버튼은 안 눌린다).</summary>
        public Button DevStartButton => m_StartButton;
#endif

        private void OnClickStart()
        {
            if (m_Director == null || m_Session == null) return;
            if (!m_Session.CanStart) return;

            m_Director.ServerStartMatch();
        }

        /// <summary>봇 한 마리 추가 — 남은 자리를 넘으면 서버가 깎는다.</summary>
        private void OnClickBotPlus()
        {
            if (m_Session == null || !m_Session.IsHost || m_Session.Starting) return;
            m_Session.SetBotCount(m_Session.BotCount + 1);
        }

        private void OnClickBotMinus()
        {
            if (m_Session == null || !m_Session.IsHost || m_Session.Starting) return;
            m_Session.SetBotCount(m_Session.BotCount - 1);
        }

        private void OnClickMap(int _index)
        {
            if (m_Session == null || !m_Session.IsHost || m_Session.Starting) return;
            m_Session.SelectMap(_index);
        }

        private void OnClickLeave()
        {
            if (m_Leaving) return;
            m_Leaving = true;

            Shutdown();
            SceneManager.LoadScene(m_LobbySceneName);
        }

        // 호스트가 방을 닫거나 연결이 끊겼다 — 대기실에 혼자 남아 있을 이유가 없다.
        private void OnDisconnected(ulong _clientId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || m_Leaving) return;
            if (nm.IsServer || nm.LocalClientId != _clientId) return;

            GameDebug.LogWarning("[Room] 연결이 끊겼다 — 메인으로 되돌린다");
            // 대기실 캔버스는 곧 사라진다 — 사유는 메인이 서면 띄운다.
            string reason = nm.DisconnectReason;
            Lobby.FrontDoor.FrontDoorNotice.Schedule("연결 끊김",
                string.IsNullOrEmpty(reason) ? "방과의 연결이 끊겼습니다. 방장이 방을 닫았을 수 있습니다."
                                             : ConnectionGateLogic.MessageOf(reason));
            m_Leaving = true;
            Shutdown();
            SceneManager.LoadScene(m_LobbySceneName);
        }

        private static void Shutdown()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsListening || nm.IsClient || nm.IsServer)) nm.Shutdown();

            var steam = Object.FindAnyObjectByType<SteamLobbyManager>();
            if (steam != null) steam.LeaveLobby();
        }
    }
}
