using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// ESC의 **단독 소유자** (2026-08-01). 전에는 `QuitOnEscape`와 `ShopHotbar`가 같은 프레임에
    /// 각자 `escapeKey.wasPressedThisFrame`를 읽었다 — 스크립트 실행 순서가 미지정이라
    /// '배치 취소'와 '게임 종료'가 경합했다. 여기에 채팅·옵션까지 끼면 재현 불가한 버그가 된다.
    /// 그래서 한 컴포넌트가 읽고 **우선순위대로 하나만** 처리한다.
    ///
    /// 1. 설치 배치 중 → 배치 취소 (실수로 판을 나가는 게 제일 큰 사고다)
    /// 2. 채팅 입력 중 → 채팅 닫기
    /// 3. 옵션 열림   → 옵션 닫기
    /// 4. 그 외        → **옵션 열기**
    ///
    /// 게임 종료는 더 이상 ESC가 아니다 — 옵션 창의 '게임 종료' 버튼으로 옮겼다.
    /// ESC 한 번에 판이 날아가던 게 2026-08-01 이전 동작이다.
    ///
    /// **Enter도 여기가 단독으로 읽는다**(2026-09-07 — 테크 v3에서 Enter가 확정 구매를 겸하면서).
    /// 채팅 전송 → 확정 구매 → 채팅 열기 순(<see cref="EnterRouteLogic"/>).
    /// 상점 안에서 채팅을 열려면 후보가 없거나 칸이 찼을 때, 또는 밖으로 나가서 누른다 —
    /// 상점에 서 있는 동안 Enter는 "산다"가 우선이다.
    /// </summary>
    public class EscapeRouter : MonoBehaviour
    {
        [SerializeField] private Economy.ShopHotbar m_Hotbar;
        [SerializeField] private UI.ChatHud m_Chat;

        // 인트로는 메뉴에 '설정'이 이미 있다 — ESC가 창을 여는 건 그 화면에서만 어색하다(2026-09-01).
        // 팝업 닫기는 어디서나 그대로 돈다.
        [SerializeField] private bool m_OpenOptionsOnEscape = true;

        private void Awake()
        {
            // 도메인 리로드를 끄고 플레이하면 정적 상태가 남는다
            UiFocus.ResetForNewSession();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame) { HandleEscape(); return; }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) HandleEnter();
        }

        /// <summary>
        /// Enter 한 번 — **사람이 누르는 그 길**. 키를 읽는 곳과 나눈 이유는 QA다
        /// (<c>ShopHotbar.PressSlot</c>과 같은 규약): 확정 구매를 <c>TryConfirmBuy</c>로 직접
        /// 부르면 「채팅이 열려 있으면 Enter는 전송이다」라는 갈림길을 통째로 건너뛴다.
        /// </summary>
        public void PressEnterKey() => HandleEnter();

        // Enter 한 번 = 한 가지 일. 판정은 EnterRouteLogic이 하고 여기선 실행만 한다.
        private void HandleEnter()
        {
            bool chatOpen = m_Chat != null && m_Chat.IsOpen;
            bool canConfirm = m_Hotbar != null && m_Hotbar.CanConfirmBuy;

            switch (EnterRouteLogic.Route(chatOpen, canConfirm))
            {
                case EnterRoute.SubmitChat:
                    m_Chat.Submit();
                    break;
                case EnterRoute.ConfirmBuy:
                    m_Hotbar.TryConfirmBuy();
                    break;
                default:
                    if (m_Chat != null) m_Chat.Open();
                    break;
            }
        }

        private void HandleEscape()
        {
            if (m_Hotbar != null && m_Hotbar.IsPlacing)
            {
                m_Hotbar.CancelPlacementFromEscape();
                return;
            }

            if (m_Chat != null && m_Chat.IsOpen)
            {
                m_Chat.Close();
                return;
            }

            // 팝업 스택(2026-09-01) — 위에서부터 하나씩 닫는다. 옵션창도 이 스택 위에 뜬다.
            UI.Popup.PopupManager popups = UI.Popup.PopupManager.Instance;
            if (popups != null && popups.OpenedCount > 0)
            {
                popups.CloseTop();
                return;
            }

            // 아무것도 안 떠 있으면 옵션창을 연다 — ESC의 마지막 종착지다.
            if (m_OpenOptionsOnEscape && popups != null) popups.Open<UI.OptionsPopup>();
        }

        /// <summary>옵션 창의 '게임 종료'가 부른다. 에디터에서는 플레이모드만 끝낸다.</summary>
        public static void QuitGame()
        {
            Debug.Log("[EscapeRouter] 게임 종료");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
