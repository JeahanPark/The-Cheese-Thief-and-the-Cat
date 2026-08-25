using UnityEngine;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine.InputSystem;   // AddBinding 확장 메서드
#endif

namespace CTC.Runtime.Core
{
    /// <summary>
    /// 인게임 로그 콘솔(` 키)을 **개발용 빌드에서만** 켠다 — `GameDebug`와 같은 판단 기준이다
    /// (2026-07-31 지정). 개발 빌드가 아니면 콘솔 오브젝트를 통째로 끈다.
    ///
    /// `GameDebug`가 `DEVELOPMENT_BUILD`에서만 로그를 남기므로, 배포판에서는 콘솔을 열어도
    /// 볼 게 없고 실수로 여는 경로만 남는다. 둘의 조건을 하나로 묶어 둔다.
    ///
    /// 에디터는 개발 환경이므로 함께 켠다 — 플레이모드에서 콘솔이 사라지면 QA가 로그를 못 본다.
    /// </summary>
    public class DebugConsoleGate : MonoBehaviour
    {
        [SerializeField] private GameObject m_Console;

        private void Awake()
        {
            if (m_Console == null) return;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            const bool ENABLED = true;
#else
            const bool ENABLED = false;
#endif
            if (m_Console.activeSelf != ENABLED) m_Console.SetActive(ENABLED);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            AddFallbackToggle();
#endif
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>
        /// ` 키가 키보드·레이아웃에 따라 안 먹는 사례가 있어(2026-08-24 실측) **F2를 예비
        /// 토글로 추가**한다. 콘솔 어셈블리를 직접 참조하지 않으려고 리플렉션으로 꽂는다 —
        /// 배포 빌드에는 이 메서드째 없다.
        /// </summary>
        private void AddFallbackToggle()
        {
            foreach (MonoBehaviour mb in m_Console.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name != "DebugLogManager") continue;

                var field = mb.GetType().GetField("toggleBinding",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null && field.GetValue(mb) is InputAction action)
                {
                    bool wasEnabled = action.enabled;
                    if (wasEnabled) action.Disable();   // 켜진 액션엔 바인딩을 못 더한다
                    action.AddBinding("<Keyboard>/f2");
                    if (wasEnabled) action.Enable();
                }
                return;
            }
        }
#endif
    }
}
