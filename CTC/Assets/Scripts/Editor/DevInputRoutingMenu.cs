using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Editor
{
    /// <summary>
    /// QA 자동화용 입력 라우팅 토글. 기본값(PointersAndKeyboardsRespectGameViewFocus)에서는
    /// 게임뷰가 포커스를 잃으면 주입된 키 이벤트가 무시돼 wesqa SimulateKeyPress가 먹지 않는다.
    /// (2026-07-27 상점 핫키 검증에서 원인 규명 — 다른 RPC는 되는데 키 입력만 무반응이면 이것)
    /// </summary>
    public static class DevInputRoutingMenu
    {
        private const string MENU = "Tools/CTC/Dev/QA 입력을 항상 게임뷰로";

        [MenuItem(MENU)]
        public static void Toggle()
        {
            InputSettings settings = InputSystem.settings;
            bool always = settings.editorInputBehaviorInPlayMode
                == InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            settings.editorInputBehaviorInPlayMode = always
                ? InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus
                : InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            Debug.Log($"[DevInput] 입력 라우팅 → {settings.editorInputBehaviorInPlayMode}");
        }

        [MenuItem(MENU, true)]
        public static bool ToggleValidate()
        {
            Menu.SetChecked(MENU, InputSystem.settings.editorInputBehaviorInPlayMode
                == InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView);
            return true;
        }
    }
}
