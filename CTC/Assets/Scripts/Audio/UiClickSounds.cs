using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 앞단 화면 버튼에 클릭음을 붙이는 Worker(2026-09-27 사용자 보고 — 「UI 클릭 소리가 안 난다」).
    ///
    /// 클릭음(<see cref="UiClickSfx"/>)은 게임 캔버스 생성기만 버튼마다 달아 줬다. 메인·서버 목록·대기실 생성기는
    /// 안 달아서 **그 세 화면의 버튼은 전부 무음**이었다. 생성기 셋을 고치고 씬을 다시 굽는 대신, 씬이 뜰 때
    /// 루트 캔버스를 훑어 빠진 버튼에 붙인다 — <see cref="UI.UiCorners"/>와 같은 방식.
    ///
    /// **생성기가 이미 클릭음을 고른 캔버스는 건드리지 않는다**(<see cref="ShouldAttach"/>). 게임 캔버스는 상점 줄처럼
    /// 구매음이 따로 나는 버튼에 일부러 클릭음을 안 달았다 — 여기서 다 붙이면 소리가 겹친다.
    /// 템플릿에서 복제하는 버튼은 템플릿(비활성 포함)에 붙으므로 복제본이 따라온다.
    /// </summary>
    public static class UiClickSounds
    {
        private static bool s_Hooked;

        /// <summary>그 버튼에 클릭음을 붙일까 — 캔버스가 이미 클릭음을 고른 적이 없고, 버튼에도 없을 때만.</summary>
        public static bool ShouldAttach(bool _canvasManagesClicks, bool _buttonHasClick) =>
            !_canvasManagesClicks && !_buttonHasClick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!s_Hooked)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                s_Hooked = true;
            }
            ApplyAll();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            s_Hooked = false;
        }

        private static void OnSceneLoaded(Scene _scene, LoadSceneMode _mode) => ApplyAll();

        /// <summary>떠 있는 모든 루트 캔버스를 훑는다. 새로 붙인 버튼 수를 돌려준다.</summary>
        public static int ApplyAll()
        {
            int attached = 0;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas != null && canvas.isRootCanvas) attached += ApplyTo(canvas.transform);
            }
            return attached;
        }

        public static int ApplyTo(Transform _root)
        {
            if (_root == null) return 0;
            bool managed = _root.GetComponentInChildren<UiClickSfx>(true) != null;
            int attached = 0;
            foreach (Button button in _root.GetComponentsInChildren<Button>(true))
            {
                if (button == null) continue;
                if (!ShouldAttach(managed, button.GetComponent<UiClickSfx>() != null)) continue;
                button.gameObject.AddComponent<UiClickSfx>();
                attached++;
            }
            return attached;
        }
    }
}
