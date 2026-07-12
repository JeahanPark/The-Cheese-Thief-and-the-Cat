using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WesQA
{
    /// <summary>Poco 정규화 좌표(top-left 0~1)를 스크린 픽셀로 변환해
    /// EventSystem 합성 포인터 입력을 주입한다. 모든 호출은 메인스레드(서버 펌프)에서 실행됨.</summary>
    public static class InputInjector
    {
        private static Vector2 ToScreen(double x, double y)
        {
            return new Vector2((float)(x * Screen.width), (float)((1.0 - y) * Screen.height));
        }

        private static GameObject Raycast(Vector2 screenPos, out RaycastResult hit)
        {
            hit = default;
            if (EventSystem.current == null) return null;
            var ev = new PointerEventData(EventSystem.current) { position = screenPos };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ev, results);
            if (results.Count == 0) return null;
            hit = results[0];
            return hit.gameObject;
        }

        private static PointerEventData MakePointer(Vector2 pos, RaycastResult hit,
            PointerEventData.InputButton button)
        {
            return new PointerEventData(EventSystem.current)
            {
                position = pos,
                button = button,
                pointerPressRaycast = hit,
                pointerCurrentRaycast = hit,
            };
        }

        public static bool Click(double x, double y) => DoClick(x, y, PointerEventData.InputButton.Left, 1);
        public static bool RClick(double x, double y) => DoClick(x, y, PointerEventData.InputButton.Right, 1);
        public static bool DoubleClick(double x, double y) => DoClick(x, y, PointerEventData.InputButton.Left, 2);

        private static bool DoClick(double x, double y, PointerEventData.InputButton button, int count)
        {
            var pos = ToScreen(x, y);
            var go = Raycast(pos, out var hit);
            if (go == null) return false;
            var ev = MakePointer(pos, hit, button);
            ev.clickCount = count;
            ExecuteEvents.Execute(go, ev, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go, ev, ExecuteEvents.pointerUpHandler);
            var clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go) ?? go;
            ExecuteEvents.Execute(clickTarget, ev, ExecuteEvents.pointerClickHandler);
            return true;
        }

        public static bool Swipe(double x1, double y1, double x2, double y2, double duration)
        {
            var p1 = ToScreen(x1, y1);
            var p2 = ToScreen(x2, y2);
            var go = Raycast(p1, out var hit);
            if (go == null) return false;
            var target = ExecuteEvents.GetEventHandler<IDragHandler>(go) ?? go;
            var ev = MakePointer(p1, hit, PointerEventData.InputButton.Left);
            ev.pressPosition = p1;
            ExecuteEvents.Execute(target, ev, ExecuteEvents.beginDragHandler);
            ev.position = p2;
            ev.delta = p2 - p1;
            ExecuteEvents.Execute(target, ev, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(target, ev, ExecuteEvents.endDragHandler);
            ExecuteEvents.Execute(go, ev, ExecuteEvents.pointerUpHandler);
            return true;
        }

        public static bool Scroll(string direction, double percent, double duration)
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var go = Raycast(center, out var hit);
            if (go == null) return false;
            var target = ExecuteEvents.GetEventHandler<IScrollHandler>(go);
            if (target == null) return false;
            var ev = MakePointer(center, hit, PointerEventData.InputButton.Left);
            float amt = (float)percent;
            ev.scrollDelta = direction == "horizontal" ? new Vector2(amt, 0) : new Vector2(0, amt);
            return ExecuteEvents.Execute(target, ev, ExecuteEvents.scrollHandler);
        }

        // LongClick: 메인스레드 블로킹 방지 위해 즉시 down/up. 진짜 hold 지속은 후속 코루틴화.
        public static bool LongClick(double x, double y, double duration)
        {
            return Click(x, y);
        }

        public static bool KeyEvent(string keycode)
        {
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel == null) return false;
            var ev = new BaseEventData(EventSystem.current);
            if (keycode == "enter" || keycode == "submit")
                return ExecuteEvents.Execute(sel, ev, ExecuteEvents.submitHandler);
            if (keycode == "escape" || keycode == "cancel")
                return ExecuteEvents.Execute(sel, ev, ExecuteEvents.cancelHandler);
            return false;
        }

        /// <summary>_instanceId로 GameObject를 찾아 InputField/TMP_InputField에 텍스트 설정.</summary>
        public static bool SetText(long instanceId, string text)
        {
            var obj = HierarchyDumper.ResolveObject(instanceId);
            if (obj == null) return false;

            var tmp = obj.GetComponent<TMP_InputField>();
            if (tmp != null)
            {
                tmp.text = text;
                tmp.onValueChanged.Invoke(text);
                tmp.onEndEdit.Invoke(text);
                return true;
            }
            var input = obj.GetComponent<InputField>();
            if (input != null)
            {
                input.text = text;
                input.onValueChanged.Invoke(text);
                input.onEndEdit.Invoke(text);
                return true;
            }
            return false;
        }
    }
}
