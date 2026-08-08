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
    /// </summary>
    public class EscapeRouter : MonoBehaviour
    {
        [SerializeField] private Economy.ShopHotbar m_Hotbar;
        [SerializeField] private UI.OptionsPopup m_Options;
        [SerializeField] private UI.ChatHud m_Chat;

        private void Awake()
        {
            // 도메인 리로드를 끄고 플레이하면 정적 상태가 남는다
            UiFocus.ResetForNewSession();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

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

            if (m_Options != null && m_Options.IsOpen)
            {
                m_Options.Close();
                return;
            }

            if (m_Options != null) m_Options.Open();
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
