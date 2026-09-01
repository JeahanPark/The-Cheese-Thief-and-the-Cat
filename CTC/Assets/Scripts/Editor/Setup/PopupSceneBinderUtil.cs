using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>씬 캔버스에 팝업 바인더를 붙인다 — 씬 빌더 3종이 같이 쓴다(2026-09-01).</summary>
    internal static class PopupSceneBinder
    {
        public static void Attach(Canvas _canvas)
        {
            if (_canvas == null)
            {
                Debug.LogWarning("[Popup] 캔버스가 없다 — 바인더를 못 붙인다");
                return;
            }

            var binder = _canvas.gameObject.GetComponent<CTC.Runtime.UI.Popup.PopupSceneBinder>();
            if (binder == null) binder = _canvas.gameObject.AddComponent<CTC.Runtime.UI.Popup.PopupSceneBinder>();

            var so = new SerializedObject(binder);
            so.FindProperty("m_Canvas").objectReferenceValue = _canvas;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
