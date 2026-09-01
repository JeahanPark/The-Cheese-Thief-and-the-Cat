using UnityEngine;

namespace CTC.Runtime.UI.Popup
{
    /// <summary>
    /// 이 씬의 캔버스를 <see cref="PopupManager"/>에 물려준다 (2026-09-01, WES `InitializeForScene` 패턴).
    ///
    /// 매니저는 씬을 넘어 살고 캔버스는 씬마다 바뀐다 — 그 둘을 잇는 것이 이 컴포넌트다.
    /// 매니저가 아직 없으면 여기서 만든다: 어느 씬을 혼자 열어도(에디터·wesqa) 팝업이 돌아야 한다.
    /// </summary>
    public class PopupSceneBinder : MonoBehaviour
    {
        [SerializeField] private Canvas m_Canvas;

        private void Awake()
        {
            if (m_Canvas == null) m_Canvas = GetComponent<Canvas>();

            if (PopupManager.Instance == null)
            {
                var go = new GameObject(nameof(PopupManager));
                go.AddComponent<PopupManager>();   // Awake에서 DontDestroyOnLoad
            }

            PopupManager.Instance.InitializeForScene(m_Canvas);
        }
    }
}
