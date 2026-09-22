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
        private const string HINT_MARK = "<mark=#F2E68C40>";   // 고른 줄 배경
        private const string HINT_NAME = "#F2E68C";
        private const string HINT_NAME_DIM = "#A89F5C";
        private const string HINT_DESC_DIM = "#9AA0A6";

        private readonly List<ChatCommandLogic.Command> m_Matches = new List<ChatCommandLogic.Command>();
        private int m_HintIndex;
        private bool m_CaretToEnd;

        /// <summary>QA 전용 — 예약어 힌트가 떠 있으면 그 내용, 아니면 빈 문자열.</summary>
        public string DevHintText =>
            m_HintRoot != null && m_HintRoot.activeSelf && m_HintText != null ? m_HintText.text : "";

        /// <summary>QA 전용 — 입력창에 지금 든 글자.</summary>
        public string DevInputText => m_Input != null ? m_Input.text : "";

        /// <summary>QA 전용 — 힌트에서 지금 고른 예약어(없으면 빈 문자열).</summary>
        public string DevHintSelected => HintShown ? m_Matches[m_HintIndex].Name : "";

        /// <summary>QA 전용 — 위(−1)·아래(+1) 키와 같은 길로 고른 줄을 옮긴다.</summary>
        public void DevMoveHint(int _delta) => MoveHint(_delta);

        /// <summary>QA 전용 — Tab과 같은 길로 고른 예약어를 입력창에 채운다.</summary>
        public void DevCompleteHint() => CompleteHint();

        private bool HintShown => IsOpen && m_Matches.Count > 0 && m_HintIndex < m_Matches.Count;
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

            m_Matches.Clear();
            m_Matches.AddRange(ChatCommandLogic.Match(_text));
            m_HintIndex = 0; // 글자가 바뀌면 후보도 바뀐다 — 맨 위부터 다시

            bool show = IsOpen && m_Matches.Count > 0;
            if (m_HintRoot.activeSelf != show) m_HintRoot.SetActive(show);
            if (show) RenderHint();
        }

        // 고른 줄은 배경을 깔고 밝게, 나머지는 흐리게 — 위아래로 옮기는 게 눈에 보이게
        private void RenderHint()
        {
            m_Builder.Clear();
            for (int i = 0; i < m_Matches.Count; i++)
            {
                if (i > 0) m_Builder.Append('\n');
                bool selected = i == m_HintIndex;
                if (selected) m_Builder.Append(HINT_MARK);
                m_Builder.Append("<color=").Append(selected ? HINT_NAME : HINT_NAME_DIM).Append('>')
                         .Append(m_Matches[i].Name).Append("</color>  ");
                if (selected) m_Builder.Append(m_Matches[i].Description).Append("</mark>");
                else m_Builder.Append("<color=").Append(HINT_DESC_DIM).Append('>')
                              .Append(m_Matches[i].Description).Append("</color>");
            }
            m_HintText.text = m_Builder.ToString();
        }

        // 위·아래 = 고르기, Tab = 채우기. Enter는 EscapeRouter → Submit이 처리한다(치는 중이면 채우기).
        private void ReadHintKeys()
        {
            if (!HintShown) return;
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            if (kb.upArrowKey.wasPressedThisFrame) MoveHint(-1);
            else if (kb.downArrowKey.wasPressedThisFrame) MoveHint(+1);
            else if (kb.tabKey.wasPressedThisFrame) CompleteHint();
        }

        private void MoveHint(int _delta)
        {
            if (!HintShown) return;
            m_HintIndex = ChatCommandLogic.MoveSelection(m_HintIndex, _delta, m_Matches.Count);
            RenderHint();
            m_CaretToEnd = true; // 한 줄 입력창은 위·아래 키에 캐럿을 맨 앞/뒤로 보낸다 — 되돌린다
        }

        private void CompleteHint()
        {
            if (!HintShown) return;
            string chosen = m_Matches[m_HintIndex].Name;
            m_Input.text = chosen; // onValueChanged → 힌트가 그 한 줄로 좁혀진다
            if (!m_Input.isFocused) m_Input.ActivateInputField();
            m_CaretToEnd = true;
        }
#endif

        private void Update()
        {
            // Enter는 더 이상 여기서 안 읽는다(2026-09-07) — `EscapeRouter`가 단독으로 읽고
            // Submit/Open을 부른다. 여기서도 읽으면 상점 확정 구매와 같은 프레임에 겹쳤다.
            // 여기서 읽는 건 힌트의 위·아래·Tab뿐 — 입력창이 열려 있을 때만 의미가 있어 다른 입력과 겹치지 않는다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ReadHintKeys();
#endif
            if (!IsOpen && m_Root != null && m_Root.activeSelf && Time.time >= m_HideAt)
            {
                m_Root.SetActive(false);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 입력창이 키를 처리한 **뒤에** 캐럿을 끝으로 — Update에서 하면 입력창이 다시 앞으로 보낸다
        private void LateUpdate()
        {
            if (!m_CaretToEnd || m_Input == null) return;
            m_CaretToEnd = false;
            int end = m_Input.text.Length;
            m_Input.stringPosition = end;
            m_Input.caretPosition = end;
        }
#endif

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
            // 치는 중(`/`·`/se`)에 Enter면 고른 줄로 채우기만 한다 — 반쪽 명령이 채팅으로 새지 않게.
            // 한 번 더 Enter를 누르면 그때 실행된다.
            if (HintShown && !ChatCommandLogic.IsComplete(text))
            {
                CompleteHint();
                return;
            }

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
