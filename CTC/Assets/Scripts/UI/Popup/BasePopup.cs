using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.UI.Popup
{
    /// <summary>
    /// 모든 팝업의 베이스 (2026-09-01, WES `BasePopup` 이식).
    ///
    /// **딤은 팝업이 각자 들고 있다**(사용자 결정) — 자기 자식으로 한 장 깔고 첫 sibling에 둔다.
    /// 겹치면 그만큼 짙어지므로 3겹 이상 쌓이는 UI는 만들지 않는다.
    ///
    /// 여닫기의 주인은 <see cref="PopupManager"/>다. 팝업이 스스로 `Destroy`하지 않는다 —
    /// 스택과 모달 상태(<see cref="Core.UiFocus"/>)가 어긋나면 팝업 뒤에서 캐릭터가 움직인다.
    /// </summary>
    public abstract class BasePopup : MonoBehaviour
    {
        /// <summary>열 때 아래를 어떻게 할 것인가.</summary>
        public enum OpenPolicy
        {
            StackOnTop = 0,      // 위에 쌓는다
            CloseAllAndOpen = 1, // 나머지를 닫고 혼자 뜬다
        }

        private const float DIM_ALPHA = 0.55f;

        [SerializeField] private OpenPolicy m_OpenPolicy = OpenPolicy.StackOnTop;
        [SerializeField] private bool m_UseDim = true;

        private GameObject m_Dim;

        public OpenPolicy Policy => m_OpenPolicy;

        /// <summary>매니저가 스택에 올린 직후 부른다. 데이터 주입은 이 뒤에 호출부가 한다.</summary>
        public void NotifyOpened()
        {
            EnsureDim();
            OnOpened();
        }

        /// <summary>매니저가 파괴하기 직전에 부른다 — 구독 해제 자리.</summary>
        public void NotifyClosing()
        {
            OnClosing();
        }

        /// <summary>닫기 버튼이 부르는 자리 — 실제 파괴는 매니저가 한다.</summary>
        public virtual void Close()
        {
            // 규칙 9 — Unity 객체는 `?.`를 안 쓴다(파괴된 객체가 통과한다).
            if (PopupManager.Instance != null) PopupManager.Instance.Close(this);
        }

        /// <summary>
        /// 씬 전환으로 캔버스째 파괴돼도 매니저가 알아야 한다 — 안 그러면 스택에 죽은 참조가 남고
        /// **모달 플래그가 켜진 채 굳어 조작이 영영 막힌다**(2026-09-03 실측: 인게임에서 이동 불가).
        /// </summary>
        private void OnDestroy()
        {
            if (PopupManager.Instance != null) PopupManager.Instance.NotifyDestroyed(this);
        }

        protected virtual void OnOpened() { }
        protected virtual void OnClosing() { }

        /// <summary>뒤 클릭을 막는 검은 판. 팝업 루트가 화면 전체를 덮는 전제다.</summary>
        private void EnsureDim()
        {
            if (!m_UseDim || m_Dim != null) return;

            m_Dim = new GameObject("DimBackdrop", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)m_Dim.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = m_Dim.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, DIM_ALPHA);
            image.raycastTarget = true;   // 뒤로 클릭이 새면 팝업이 모달이 아니다

            rect.SetAsFirstSibling();     // 내용 뒤에 깔린다
        }
    }
}
