using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WesQA
{
    /// <summary>모든 Canvas 루트를 순회해 Poco 노드 스키마
    /// {name, payload{...}, children[]}로 덤프. 좌표는 스크린 정규화(0~1).</summary>
    public static class HierarchyDumper
    {
        // GetInstanceID/InstanceIDToObject가 6000.5에서 obsolete-error(CS0619)라
        // 자체 레지스트리로 대체 — _instanceId는 클라이언트 입장에서 opaque 숫자 id이므로 계약 동일.
        private static readonly Dictionary<GameObject, int> s_idByObject = new Dictionary<GameObject, int>();
        private static readonly Dictionary<int, GameObject> s_objectById = new Dictionary<int, GameObject>();
        private static int s_nextId = 1;

        internal static int RegisterObject(GameObject go)
        {
            if (s_idByObject.TryGetValue(go, out int id)) return id;
            id = s_nextId++;
            s_idByObject[go] = id;
            s_objectById[id] = go;
            return id;
        }

        internal static GameObject ResolveObject(long id)
        {
            if (s_objectById.TryGetValue((int)id, out GameObject go) && go != null) return go;
            return null;
        }

        public static Dictionary<string, object> Dump(bool onlyVisibleNode)
        {
            var root = new Dictionary<string, object>
            {
                ["name"] = "Root",
                ["payload"] = new Dictionary<string, object>
                {
                    ["name"] = "Root", ["type"] = "Root", ["visible"] = true,
                },
            };
            var children = new List<object>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.transform.parent != null) continue; // 루트 Canvas만
                var node = BuildNode(canvas.gameObject, onlyVisibleNode);
                if (node != null) children.Add(node);
            }
            root["children"] = children;
            return root;
        }

        private static Dictionary<string, object> BuildNode(GameObject go, bool onlyVisible)
        {
            bool visible = go.activeInHierarchy;
            if (onlyVisible && !visible) return null;

            var rt = go.transform as RectTransform;
            var payload = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["type"] = ResolveType(go),
                ["visible"] = visible,
                ["text"] = ResolveText(go),
                ["_instanceId"] = RegisterObject(go),
                ["clickable"] = go.GetComponent<Selectable>() != null,
            };
            FillRect(payload, rt);

            var node = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["payload"] = payload,
            };

            var kids = new List<object>();
            foreach (Transform child in go.transform)
            {
                var c = BuildNode(child.gameObject, onlyVisible);
                if (c != null) kids.Add(c);
            }
            node["children"] = kids.Count > 0 ? kids : null;
            return node;
        }

        private static string ResolveType(GameObject go)
        {
            if (go.GetComponent<Button>() != null) return "Button";
            if (go.GetComponent<Toggle>() != null) return "Toggle";
            if (go.GetComponent<TMP_InputField>() != null) return "InputField";
            if (go.GetComponent<InputField>() != null) return "InputField";
            if (go.GetComponent<TMP_Text>() != null) return "Text";
            if (go.GetComponent<Text>() != null) return "Text";
            if (go.GetComponent<Image>() != null) return "Image";
            return go.transform is RectTransform ? "Node" : "GameObject";
        }

        private static string ResolveText(GameObject go)
        {
            var tmp = go.GetComponent<TMP_Text>();
            if (tmp != null) return tmp.text;
            var tmpInput = go.GetComponent<TMP_InputField>();
            if (tmpInput != null) return tmpInput.text;
            var t = go.GetComponent<Text>();
            if (t != null) return t.text;
            var inp = go.GetComponent<InputField>();
            if (inp != null) return inp.text;
            return null;
        }

        // RectTransform → 스크린 중심 정규화 pos·size
        private static void FillRect(Dictionary<string, object> payload, RectTransform rt)
        {
            float sw = Screen.width, sh = Screen.height;
            if (rt == null || sw <= 0 || sh <= 0)
            {
                payload["pos"] = new[] { 0.5f, 0.5f };
                payload["size"] = new[] { 0f, 0f };
                payload["anchorPoint"] = new[] { 0.5f, 0.5f };
                payload["scale"] = new[] { 1f, 1f };
                payload["zOrders"] = new Dictionary<string, object> { ["global"] = 0, ["local"] = 0 };
                return;
            }
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var cam = GetCanvasCamera(rt);
            Vector3 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector3 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            float cx = (min.x + max.x) * 0.5f / sw;
            float cy = 1f - (min.y + max.y) * 0.5f / sh; // 좌상단 원점(Poco 관례)
            payload["pos"] = new[] { cx, cy };
            payload["size"] = new[] { Mathf.Abs(max.x - min.x) / sw, Mathf.Abs(max.y - min.y) / sh };
            payload["anchorPoint"] = new[] { 0.5f, 0.5f };
            payload["scale"] = new[] { rt.localScale.x, rt.localScale.y };
            payload["zOrders"] = new Dictionary<string, object>
            {
                ["global"] = 0,
                ["local"] = rt.GetSiblingIndex(),
            };
        }

        private static Camera GetCanvasCamera(RectTransform rt)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return canvas != null ? canvas.worldCamera : null;
        }
    }
}
