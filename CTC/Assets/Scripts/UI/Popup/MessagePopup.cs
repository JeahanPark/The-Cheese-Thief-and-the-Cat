using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.UI.Popup
{
    /// <summary>
    /// 제목·본문·버튼 한두 개짜리 창 (2026-09-14) — 접속 실패 알림과 「공개 게임」 선택이 같이 쓴다.
    ///
    /// 결과는 누른 버튼 번호다(<see cref="PRIMARY"/>·<see cref="SECONDARY"/>). 닫기·ESC는 <see cref="CANCELLED"/>.
    /// 두 번째 버튼을 안 주면 알림 창(확인 하나)이 된다.
    /// </summary>
    public class MessagePopup : BasePopup
    {
        public const int CANCELLED = -1;
        public const int PRIMARY = 0;
        public const int SECONDARY = 1;

        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Body;
        [SerializeField] private Button m_PrimaryButton;
        [SerializeField] private TMP_Text m_PrimaryLabel;
        [SerializeField] private Button m_SecondaryButton;
        [SerializeField] private TMP_Text m_SecondaryLabel;
        [SerializeField] private Button m_CloseButton;

        private float? m_PrimaryHomeX;
        private readonly UniTaskCompletionSource<int> m_Result = new UniTaskCompletionSource<int>();

        public UniTask<int> Result => m_Result.Task;

        private void Awake()
        {
            if (m_PrimaryButton != null) m_PrimaryButton.OnClickAsObservable().Subscribe(_ => OnClickChoice(PRIMARY)).AddTo(this);
            if (m_SecondaryButton != null) m_SecondaryButton.OnClickAsObservable().Subscribe(_ => OnClickChoice(SECONDARY)).AddTo(this);
            if (m_CloseButton != null) m_CloseButton.OnClickAsObservable().Subscribe(_ => OnClickChoice(CANCELLED)).AddTo(this);
        }

        /// <summary>두 번째 버튼 라벨이 비면 버튼 하나(알림).</summary>
        public void Setup(string _title, string _body, string _primary, string _secondary = null)
        {
            if (m_Title != null) m_Title.text = _title;
            if (m_Body != null) m_Body.text = _body;
            if (m_PrimaryLabel != null) m_PrimaryLabel.text = _primary;

            bool two = !string.IsNullOrEmpty(_secondary);
            if (m_SecondaryButton != null) m_SecondaryButton.gameObject.SetActive(two);

            // 하나뿐이면 가운데로 — 왼쪽에 치우친 확인 버튼은 "뭔가 빠진 창"처럼 보인다.
            if (m_PrimaryButton != null && m_PrimaryButton.transform is RectTransform rect)
            {
                if (m_PrimaryHomeX == null) m_PrimaryHomeX = rect.anchoredPosition.x;
                rect.anchoredPosition = new Vector2(two ? m_PrimaryHomeX.Value : 0f, rect.anchoredPosition.y);
            }
            if (m_SecondaryLabel != null) m_SecondaryLabel.text = _secondary ?? "";
        }

        protected override void OnClosing()
        {
            m_Result.TrySetResult(CANCELLED);
        }

        private void OnClickChoice(int _choice)
        {
            m_Result.TrySetResult(_choice);
            Close();
        }

        /// <summary>알림 한 장 띄우고 닫힐 때까지 기다린다. 팝업을 못 열면 바로 끝난다.</summary>
        public static async UniTask Notify(string _title, string _body)
        {
            PopupManager popups = PopupManager.Instance;
            if (popups == null) return;
            MessagePopup popup = popups.Open<MessagePopup>();
            if (popup == null) return;
            popup.Setup(_title, _body, "확인");
            await popup.Result;
        }
    }
}
