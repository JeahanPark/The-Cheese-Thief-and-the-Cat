using CTC.Runtime.Core;
using CTC.Runtime.Lobby.FrontDoor;
using CTC.Runtime.UI.Popup;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CTC.Runtime.Lobby.Browser
{
    /// <summary>
    /// 서버 목록 화면의 입력만 받는다(CTC 규칙 5). 버튼은 R3 구독, 핸들러 이름은 OnClick~(규칙 2·4).
    ///
    /// 방을 만들거나 참여하면 접속만 시작한다 — **대기실로 넘기는 것은 <see cref="RoomEntry"/>다.**
    /// 씬을 여기서 직접 열면 NGO가 모르는 로드가 되어 대기실의 in-scene NetworkObject가 안 선다
    /// (실측: 화면은 뜨는데 참가자가 0명이었다).
    ///
    /// 2026-09-14: 접속 절차는 <see cref="SessionStarter"/>가 맡는다. 「새로 만들기」는 이름을 받아 **공개 방**을 연다(R.E.P.O.).
    /// </summary>
    public class LobbyBrowserController : MonoBehaviour
    {
        [SerializeField] private LobbyBrowser m_Browser;
        [SerializeField] private SessionStarter m_Starter;

        [Header("버튼")]
        [SerializeField] private Button m_RefreshButton;
        [SerializeField] private Button m_JoinButton;
        [SerializeField] private Button m_CreateButton;
        [SerializeField] private Button m_BackButton;
        [SerializeField] private Button m_PrevButton;
        [SerializeField] private Button m_NextButton;
        [SerializeField] private TMP_InputField m_SearchInput;

        [Header("씬")]
        [SerializeField] private string m_BackSceneName = "Intro";

        private bool m_Leaving;

        private bool Locked => m_Leaving || (m_Starter != null && m_Starter.Connecting.CurrentValue);

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

            if (m_PrevButton != null)
                m_PrevButton.OnClickAsObservable().Subscribe(_ => OnClickPage(-1)).AddTo(this);

            if (m_NextButton != null)
                m_NextButton.OnClickAsObservable().Subscribe(_ => OnClickPage(1)).AddTo(this);

            if (m_SearchInput != null)
                m_SearchInput.onValueChanged.AsObservable().Subscribe(OnSearchChanged).AddTo(this);
        }

        private void OnClickRefresh()
        {
            if (m_Browser != null) m_Browser.Refresh();
        }

        private void OnClickPage(int _delta)
        {
            if (m_Browser != null) m_Browser.TurnPage(_delta);
        }

        private void OnSearchChanged(string _query)
        {
            if (m_Browser != null) m_Browser.SetSearch(_query);
        }

        private void OnClickJoin()
        {
            if (Locked || m_Browser == null || m_Starter == null || !m_Browser.HasSelection) return;

            LobbyRoomInfo room = m_Browser.Selected;
            if (room.IsFull)
            {
                GameDebug.LogWarning("[Lobby] 가득 찬 방 — 참여 무시");
                return;
            }

            GameDebug.Log($"[Lobby] 참여: {room.Name}");
            m_Starter.JoinListed(m_Browser.SelectedLobby);
        }

        private void OnClickCreate()
        {
            if (Locked || m_Starter == null) return;
            CreateAsync().Forget();
        }

        private async UniTaskVoid CreateAsync()
        {
            PopupManager popups = PopupManager.Instance;
            TextInputPopup popup = popups != null ? popups.Open<TextInputPopup>() : null;
            if (popup == null) return;

            string owner = Networking.SteamBootstrap.Ready ? SafeSteamName() : "";
            popup.Setup(TextInputPopup.Mode.RoomName, "새 공개 방", "목록에 보일 방 이름을 정해 주세요.",
                Networking.SteamLobbyLogic.DefaultRoomName(owner));

            string name = await popup.Result;
            if (name == null || this == null) return;

            GameDebug.Log($"[Lobby] 공개 방 만들기: {name}");
            m_Starter.HostPublic(name);
        }

        private void OnClickBack()
        {
            if (Locked) return;
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
