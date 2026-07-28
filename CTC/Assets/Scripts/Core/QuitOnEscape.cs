using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// ESC로 게임을 끈다(2026-07-28 요청). 빌드에는 창을 닫는 것 말고 나가는 방법이 없었다.
    ///
    /// 설치 배치 중에는 ESC가 '배치 취소'라 그때는 종료하지 않는다 —
    /// 실수로 판을 나가버리는 게 더 큰 사고다. 배치를 취소한 뒤 한 번 더 누르면 꺼진다.
    /// 에디터에서는 플레이모드만 종료한다.
    /// </summary>
    public class QuitOnEscape : MonoBehaviour
    {
        [SerializeField] private Economy.ShopHotbar m_Hotbar;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (m_Hotbar != null && m_Hotbar.IsPlacing) return; // 이번 ESC는 배치 취소용

            Quit();
        }

        private static void Quit()
        {
            Debug.Log("[QuitOnEscape] ESC — 종료");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
