using System.Collections.Generic;
using System.Text;
using CTC.Runtime.Managers;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 채팅창 (2026-08-01 건의). Enter로 열고, 치고, Enter로 보낸다. ESC로 닫는다.
    ///
    /// **커서는 잠긴 채로 둔다.** 커서를 풀면 `OrbitCameraController`가 시점 회전을 멈춰
    /// 카메라가 굳는다 — 쫓기는 중에 채팅하다 죽는 게임이 된다. `ActivateInputField()`를
    /// 코드로 부르면 커서를 안 풀고도 포커스가 잡히고, `UiFocus.IsTyping`이 서면
    /// 이동·공격·핫키·ESC가 전부 알아서 비켜난다.
    ///
    /// 로그는 마지막 몇 줄만 텍스트 하나에 이어 붙인다 — 스크롤 컨테이너를 두면
    /// 마우스가 필요해지고, 위 결정(커서 잠금 유지)과 충돌한다.
    ///
    /// 표시 이름이 아직 없어 `Client N`으로 쓴다(닉네임 필드는 프로젝트에 없다).
    /// </summary>
    public class ChatHud : MonoBehaviour
    {
        private const int MAX_LINES = 8;
        private const float IDLE_HIDE_SECONDS = 12f;

        private static readonly Color COLOR_MINE = new Color(0.95f, 0.9f, 0.55f);
        private static readonly Color COLOR_SPECTATOR = new Color(0.6f, 0.75f, 0.95f);

        [SerializeField] private GameObject m_Root;
        [SerializeField] private TMP_Text m_Log;
        [SerializeField] private TMP_InputField m_Input;

        private readonly List<string> m_Lines = new List<string>();
        private readonly StringBuilder m_Builder = new StringBuilder();
        private float m_HideAt;

        public bool IsOpen => m_Input != null && m_Input.gameObject.activeSelf;

        /// <summary>QA 전용 — 지금 이 클라이언트가 실제로 본 로그 전문.</summary>
        public string DevLogText => m_Log != null ? m_Log.text : "";

        private void Start()
        {
            // Start인 이유: ChatManager.Instance가 Awake에서 잡힌다(LobbyUI·ControlsHud와 같은 규약)
            if (m_Input != null) m_Input.gameObject.SetActive(false);
            if (m_Log != null) m_Log.text = "";

            if (ChatManager.Instance != null)
            {
                ChatManager.Instance.OnMessageReceivedAsObservable
                    .Subscribe(Append)
                    .AddTo(this);
            }
        }

        private void Update()
        {
            // Update에 남은 건 키 입력뿐이다 — 표시는 전부 위 구독이 한다(규칙 3).
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                // 열기와 전송이 같은 프레임에 겹치면 열자마자 빈 줄을 보낸다 — 한 상태 머신에서 처리한다
                if (IsOpen) Submit();
                else Open();
            }

            if (!IsOpen && m_Root != null && m_Root.activeSelf && Time.time >= m_HideAt)
            {
                m_Root.SetActive(false);
            }
        }

        public void Open()
        {
            if (m_Input == null) return;
            if (!IsInMatch()) return;

            ShowLog();
            m_Input.gameObject.SetActive(true);
            m_Input.text = "";
            m_Input.characterLimit = ChatManager.MAX_MESSAGE_LENGTH;
            m_Input.ActivateInputField(); // 커서 잠금은 그대로 — 키보드 포커스만 가져온다
        }

        public void Close()
        {
            if (m_Input == null) return;
            m_Input.DeactivateInputField();
            m_Input.text = "";
            m_Input.gameObject.SetActive(false);
            UnityEngine.EventSystems.EventSystem current = UnityEngine.EventSystems.EventSystem.current;
            if (current != null) current.SetSelectedGameObject(null); // UiFocus.IsTyping을 확실히 내린다
            m_HideAt = Time.time + IDLE_HIDE_SECONDS;
        }

        private void Submit()
        {
            string text = m_Input != null ? m_Input.text : "";
            if (ChatManager.Instance != null && !string.IsNullOrWhiteSpace(text))
            {
                ChatManager.Instance.SendChatMessage(text);
            }
            Close();
        }

        private void Append(ChatMessage _message)
        {
            NetworkManager nm = NetworkManager.Singleton;
            bool mine = nm != null && _message.SenderId == nm.LocalClientId;

            string who = $"Client {_message.SenderId}";
            string color = mine ? ColorHex(COLOR_MINE) : _message.FromSpectator ? ColorHex(COLOR_SPECTATOR) : "FFFFFF";
            string tag = _message.FromSpectator ? "[관전] " : "";

            m_Lines.Add($"<color=#{color}>{tag}{who}: {_message.Message}</color>");
            while (m_Lines.Count > MAX_LINES) m_Lines.RemoveAt(0);

            m_Builder.Clear();
            for (int i = 0; i < m_Lines.Count; i++)
            {
                if (i > 0) m_Builder.Append('\n');
                m_Builder.Append(m_Lines[i]);
            }
            if (m_Log != null) m_Log.text = m_Builder.ToString();

            ShowLog();
        }

        // 새 줄이 오거나 입력을 열면 로그를 띄우고, 조용해지면 스스로 사라진다.
        private void ShowLog()
        {
            if (m_Root != null && !m_Root.activeSelf) m_Root.SetActive(true);
            m_HideAt = Time.time + IDLE_HIDE_SECONDS;
        }

        private static bool IsInMatch()
        {
            NetworkManager nm = NetworkManager.Singleton;
            return nm != null && (nm.IsClient || nm.IsServer);
        }

        private static string ColorHex(Color _c) => ColorUtility.ToHtmlStringRGB(_c);
    }
}
