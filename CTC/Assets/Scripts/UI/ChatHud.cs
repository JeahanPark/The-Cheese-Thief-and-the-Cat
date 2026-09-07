using System.Collections.Generic;
using System.Text;
using CTC.Runtime.Managers;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;

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

        private static readonly Color COLOR_MINE = UiPalette.AccentSoft;
        private static readonly Color COLOR_SPECTATOR = UiPalette.Info;

        [SerializeField] private GameObject m_Root;
        [SerializeField] private TMP_Text m_Log;
        [SerializeField] private TMP_InputField m_Input;
        [SerializeField] private GameObject m_HintRoot;   // `/` 예약어 힌트 (개발 빌드 전용)
        [SerializeField] private TMP_Text m_HintText;

        private readonly List<string> m_Lines = new List<string>();
        private readonly StringBuilder m_Builder = new StringBuilder();
        private float m_HideAt;

        public bool IsOpen => m_Input != null && m_Input.gameObject.activeSelf;

        /// <summary>QA 전용 — 지금 이 클라이언트가 실제로 본 로그 전문.</summary>
        public string DevLogText => m_Log != null ? m_Log.text : "";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>QA 전용 — 예약어 힌트가 떠 있으면 그 내용, 아니면 빈 문자열.</summary>
        public string DevHintText =>
            m_HintRoot != null && m_HintRoot.activeSelf && m_HintText != null ? m_HintText.text : "";
#endif

        private void Start()
        {
            // Start인 이유: ChatManager.Instance가 Awake에서 잡힌다(LobbyUI·AbilityBar와 같은 규약)
            if (m_Input != null) m_Input.gameObject.SetActive(false);
            if (m_Log != null) m_Log.text = "";

            if (ChatManager.Instance != null)
            {
                ChatManager.Instance.OnMessageReceivedAsObservable
                    .Subscribe(Append)
                    .AddTo(this);
            }

            if (m_HintRoot != null) m_HintRoot.SetActive(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (m_Input != null)
            {
                m_Input.onValueChanged.AsObservable()
                    .Subscribe(OnInputChanged)
                    .AddTo(this);
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // `/`로 시작하는 동안 예약어 힌트를 띄운다(2026-08-20 건의). 명령이 아니게 되면 바로 걷는다.
        private void OnInputChanged(string _text)
        {
            if (m_HintRoot == null || m_HintText == null) return;

            var matches = ChatCommandLogic.Match(_text);
            bool show = IsOpen && matches.Count > 0;
            if (m_HintRoot.activeSelf != show) m_HintRoot.SetActive(show);
            if (!show) return;

            m_Builder.Clear();
            for (int i = 0; i < matches.Count; i++)
            {
                if (i > 0) m_Builder.Append('\n');
                m_Builder.Append("<color=#F2E68C>").Append(matches[i].Name).Append("</color>  ")
                         .Append(matches[i].Description);
            }
            m_HintText.text = m_Builder.ToString();
        }
#endif

        private void Update()
        {
            // Enter는 더 이상 여기서 안 읽는다(2026-09-07) — `EscapeRouter`가 단독으로 읽고
            // Submit/Open을 부른다. 여기서도 읽으면 상점 확정 구매와 같은 프레임에 겹쳤다.
            // 남은 건 유휴 후 로그 감추기뿐 — 표시는 전부 위 구독이 한다(규칙 3).
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
            if (m_HintRoot != null) m_HintRoot.SetActive(false);
            UnityEngine.EventSystems.EventSystem current = UnityEngine.EventSystems.EventSystem.current;
            if (current != null) current.SetSelectedGameObject(null); // UiFocus.IsTyping을 확실히 내린다
            m_HideAt = Time.time + IDLE_HIDE_SECONDS;
        }

        /// <summary>채팅 전송 — Enter는 `EscapeRouter`가 단독으로 읽고 여기를 부른다.</summary>
        public void Submit()
        {
            // **읽기 전에 조합을 확정시킨다.** 한글은 마지막 음절이 IME '조합 중' 상태로
            // 남아 있고, 그건 화면에만 그려질 뿐 InputField.text에는 아직 없다.
            // TMP가 조합을 text로 옮기는 곳은 OnDeselect 하나뿐이고,
            // DeactivateInputField()는 되레 IME 모드를 되돌리며 조합을 **버린다**
            // (com.unity.ugui TMP_InputField.OnDeselect 주석이 "final Hangul syllable"을 콕 집어 말한다).
            // 그래서 전에는 "안녕"이 "안"으로, "왜이래"가 "왜이"로 잘려 나갔다.
            CommitComposition();

            string text = m_Input != null ? m_Input.text : "";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 완성된 예약어는 채팅으로 보내지 않고 실행한다. 모르는 /문자열은 그냥 채팅이다.
            if (ChatCommandLogic.IsSendLog(text))
            {
                if (ChatManager.Instance != null) ChatManager.Instance.SendMyLogToHost();
                Close();
                return;
            }
#endif

            if (ChatManager.Instance != null && !string.IsNullOrWhiteSpace(text))
            {
                ChatManager.Instance.SendChatMessage(text);
            }
            Close();
        }

        // 선택 해제가 곧 조합 확정이다 — EventSystem이 OnDeselect를 그 자리에서 동기 호출하고,
        // TMP가 거기서 조합 문자열을 text에 덧붙인다. 조합 중이 아니면 아무 일도 없다.
        private void CommitComposition()
        {
            UnityEngine.EventSystems.EventSystem current = UnityEngine.EventSystems.EventSystem.current;
            if (current != null && current.currentSelectedGameObject == m_Input.gameObject)
            {
                current.SetSelectedGameObject(null);
            }
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
