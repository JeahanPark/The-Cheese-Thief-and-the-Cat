using CTC.Runtime.Networking;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.UI.Popup
{
    /// <summary>
    /// 글자 한 줄을 받는 창 (2026-09-14) — 방 비밀번호 · 방 이름이 같이 쓴다.
    ///
    /// R.E.P.O. 비밀번호 창을 따랐다: **비워 두면 확인 버튼이 「건너뛰기」가 된다**(비밀번호 없는 방),
    /// 가리기/보기 토글, 복사. 규칙(10자·공백 없음·대문자)은 <see cref="LobbyPasswordLogic"/>에 있다.
    ///
    /// 결과는 <see cref="Result"/>를 기다려 받는다. 취소(버튼·ESC로 닫힘)는 null.
    /// </summary>
    public class TextInputPopup : BasePopup
    {
        public enum Mode
        {
            Password = 0,
            RoomName = 1,
        }

        public const int ROOM_NAME_MAX = 24;

        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Hint;
        [SerializeField] private TMP_InputField m_Input;
        [SerializeField] private Button m_ConfirmButton;
        [SerializeField] private TMP_Text m_ConfirmLabel;
        [SerializeField] private Button m_CancelButton;
        [SerializeField] private Button m_RevealButton;
        [SerializeField] private TMP_Text m_RevealLabel;
        [SerializeField] private Button m_CopyButton;

        private readonly UniTaskCompletionSource<string> m_Result = new UniTaskCompletionSource<string>();
        private Mode m_Mode;
        private bool m_Revealed;

        /// <summary>확인하면 값(비밀번호는 빈 값일 수 있다), 취소면 null.</summary>
        public UniTask<string> Result => m_Result.Task;

        private void Awake()
        {
            if (m_ConfirmButton != null) m_ConfirmButton.OnClickAsObservable().Subscribe(_ => OnClickConfirm()).AddTo(this);
            if (m_CancelButton != null) m_CancelButton.OnClickAsObservable().Subscribe(_ => OnClickCancel()).AddTo(this);
            if (m_RevealButton != null) m_RevealButton.OnClickAsObservable().Subscribe(_ => OnClickReveal()).AddTo(this);
            if (m_CopyButton != null) m_CopyButton.OnClickAsObservable().Subscribe(_ => OnClickCopy()).AddTo(this);
            if (m_Input != null)
            {
                m_Input.onValueChanged.AsObservable().Subscribe(OnValueChanged).AddTo(this);
                m_Input.onSubmit.AsObservable().Subscribe(_ => OnClickConfirm()).AddTo(this);
            }
        }

        /// <summary>열자마자 부른다(데이터 주입 — WES 규약).</summary>
        public void Setup(Mode _mode, string _title, string _hint, string _initial = "")
        {
            m_Mode = _mode;
            if (m_Title != null) m_Title.text = _title;
            if (m_Hint != null) m_Hint.text = _hint;

            bool password = _mode == Mode.Password;
            if (m_RevealButton != null) m_RevealButton.gameObject.SetActive(password);
            if (m_CopyButton != null) m_CopyButton.gameObject.SetActive(password);

            if (m_Input != null)
            {
                m_Input.characterLimit = password ? LobbyPasswordLogic.MAX_LENGTH : ROOM_NAME_MAX;
                m_Revealed = !password;
                ApplyReveal();
                m_Input.text = _initial ?? "";
                m_Input.ActivateInputField();
            }
            RenderConfirm();
        }

        protected override void OnClosing()
        {
            // ESC로 닫혀도 기다리는 쪽이 멈추면 안 된다 — 확인으로 이미 끝났으면 무시된다.
            m_Result.TrySetResult(null);
        }

        private void OnValueChanged(string _text)
        {
            if (m_Mode == Mode.Password && m_Input != null)
            {
                string normalized = LobbyPasswordLogic.Normalize(_text);
                if (normalized != _text)
                {
                    m_Input.SetTextWithoutNotify(normalized);
                    m_Input.caretPosition = normalized.Length;
                }
            }
            RenderConfirm();
        }

        private void RenderConfirm()
        {
            if (m_ConfirmLabel == null) return;
            bool empty = m_Input == null || string.IsNullOrWhiteSpace(m_Input.text);
            m_ConfirmLabel.text = m_Mode == Mode.Password && empty ? "건너뛰기" : "확인";
            if (m_ConfirmButton != null) m_ConfirmButton.interactable = m_Mode == Mode.Password || !empty;
        }

        private void OnClickConfirm()
        {
            string text = m_Input != null ? m_Input.text : "";
            if (m_Mode == Mode.RoomName)
            {
                text = (text ?? "").Trim();
                if (text.Length == 0) return;   // 빈 이름은 막는다(R.E.P.O.와 같다)
            }
            else
            {
                text = LobbyPasswordLogic.Normalize(text);
            }

            m_Result.TrySetResult(text);
            Close();
        }

        private void OnClickCancel()
        {
            m_Result.TrySetResult(null);
            Close();
        }

        private void OnClickReveal()
        {
            m_Revealed = !m_Revealed;
            ApplyReveal();
        }

        private void ApplyReveal()
        {
            if (m_Input == null) return;
            m_Input.contentType = m_Revealed ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            m_Input.ForceLabelUpdate();
            if (m_RevealLabel != null) m_RevealLabel.text = m_Revealed ? "가리기" : "보기";
        }

        private void OnClickCopy()
        {
            if (m_Input == null) return;
            GUIUtility.systemCopyBuffer = LobbyPasswordLogic.Normalize(m_Input.text);
        }
    }
}
