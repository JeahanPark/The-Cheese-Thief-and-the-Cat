using CTC.Runtime.Core;
using CTC.Runtime.Networking;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CTC.Runtime.Lobby.Browser
{
    /// <summary>
    /// 로비 화면의 입력만 받는다(CTC 규칙 5). 버튼은 R3 구독, 핸들러 이름은 OnClick~(규칙 2·4).
    ///
    /// 방을 만들거나 참여하면 접속만 시작한다 — **대기실로 넘기는 것은 <see cref="RoomEntry"/>다.**
    /// 씬을 여기서 직접 열면 NGO가 모르는 로드가 되어 대기실의 in-scene NetworkObject가 안 선다
    /// (실측: 화면은 뜨는데 참가자가 0명이었다).
    /// 스팀 경로는 방장/참여자가 붙은 뒤 <see cref="SteamLobbyManager"/>가 알아서 NGO를 띄운다.
    /// </summary>
    public class LobbyBrowserController : MonoBehaviour
    {
        [SerializeField] private LobbyBrowser m_Browser;

        [Header("버튼")]
        [SerializeField] private Button m_RefreshButton;
        [SerializeField] private Button m_JoinButton;
        [SerializeField] private Button m_CreateButton;
        [SerializeField] private Button m_BackButton;

        [Header("씬")]
        [SerializeField] private string m_BackSceneName = "Intro";

        private bool m_Leaving;

        private void Awake()
        {
            if (m_RefreshButton != null)
                m_RefreshButton.OnClickAsObservable().Subscribe(_ => OnClickRefresh()).AddTo(this);

            if (m_JoinButton != null)
                m_JoinButton.OnClickAsObservable().Subscribe(_ => OnClickJoin()).AddTo(this);

            if (m_CreateButton != null)
                m_CreateButton.OnClickAsObservable().Subscribe(_ => OnClickCreate()).AddTo(this);

            if (m_BackButton != null)
                m_BackButton.OnClickAsObservable().Subscribe(_ => OnClickBack()).AddTo(this);
        }

        private void OnClickRefresh()
        {
            if (m_Browser != null) m_Browser.Refresh();
        }

        private void OnClickJoin()
        {
            if (m_Leaving || m_Browser == null || !m_Browser.HasSelection) return;

            LobbyRoomInfo room = m_Browser.Selected;
            if (room.IsFull)
            {
                GameDebug.LogWarning("[Lobby] 가득 찬 방 — 참여 무시");
                return;
            }

            GameDebug.Log($"[Lobby] 참여: {room.Name}");
            if (m_Browser.Join(room)) m_Leaving = true;   // 붙는 중 — 버튼 연타를 막는다
        }

        private void OnClickCreate()
        {
            if (m_Leaving || m_Browser == null) return;

            string owner = SteamBootstrap.Ready ? SafeSteamName() : "";
            string name = SteamLobbyLogic.DefaultRoomName(owner);

            GameDebug.Log($"[Lobby] 방 만들기: {name}");
            if (m_Browser.Create(name, true)) m_Leaving = true;
        }

        private void OnClickBack()
        {
            if (m_Leaving) return;
            if (string.IsNullOrEmpty(m_BackSceneName)) return;

            m_Leaving = true;
            SceneManager.LoadScene(m_BackSceneName);
        }

        private static string SafeSteamName()
        {
            try
            {
                return Steamworks.SteamClient.Name;
            }
            catch (System.Exception)
            {
                return "";
            }
        }
    }
}
