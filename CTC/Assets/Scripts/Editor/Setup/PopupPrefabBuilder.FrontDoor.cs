using CTC.Runtime.UI;
using CTC.Runtime.UI.Popup;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Editor
{
    /// <summary>
    /// 대기실 앞단 팝업 둘(2026-09-14) — <see cref="TextInputPopup"/>(비밀번호·방 이름) · <see cref="MessagePopup"/>(알림·선택).
    /// 톤은 인트로·로비와 같다: 남색 패널 + 치즈 노랑 강조 하나(<see cref="UiPalette"/>).
    /// </summary>
    public static partial class PopupPrefabBuilder
    {
        private const float WINDOW_W = 680f;

        [MenuItem("Tools/CTC/Build Popup Prefabs (앞단 팝업만)")]
        public static void BuildFrontDoorOnly()
        {
            EnsureFolder();
            BuildTextInputPopupPrefab();
            BuildMessagePopupPrefab();
            AssetDatabase.SaveAssets();
        }

        private static void BuildTextInputPopupPrefab()
        {
            GameObject root = PopupRoot("TextInputPopup");
            RectTransform window = Window(root.transform, new Vector2(WINDOW_W, 360f));

            TMP_Text title = Label(window, "Title", "방 비밀번호", 34, FontStyles.Bold, new Vector2(0f, 128f), new Vector2(WINDOW_W - 80f, 48f));
            title.color = UiPalette.AccentSoft;
            TMP_Text hint = Label(window, "Hint", "비워 두면 비밀번호 없이 열립니다.", 20, FontStyles.Normal, new Vector2(0f, 74f), new Vector2(WINDOW_W - 80f, 56f));
            hint.color = UiPalette.TextDim;

            TMP_InputField input = Input(window, "Input", new Vector2(-70f, 2f), new Vector2(WINDOW_W - 220f, 64f), "입력");
            Button reveal = SmallButton(window, "RevealButton", "보기", new Vector2(WINDOW_W * 0.5f - 88f, 20f), new Vector2(116f, 30f), out TMP_Text revealLabel);
            Button copy = SmallButton(window, "CopyButton", "복사", new Vector2(WINDOW_W * 0.5f - 88f, -16f), new Vector2(116f, 30f), out _);

            Button cancel = WideButton(window, "CancelButton", "취소", false, new Vector2(-150f, -120f), out _);
            Button confirm = WideButton(window, "ConfirmButton", "확인", true, new Vector2(150f, -120f), out TMP_Text confirmLabel);

            var popup = root.AddComponent<TextInputPopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("m_Title").objectReferenceValue = title;
            so.FindProperty("m_Hint").objectReferenceValue = hint;
            so.FindProperty("m_Input").objectReferenceValue = input;
            so.FindProperty("m_ConfirmButton").objectReferenceValue = confirm;
            so.FindProperty("m_ConfirmLabel").objectReferenceValue = confirmLabel;
            so.FindProperty("m_CancelButton").objectReferenceValue = cancel;
            so.FindProperty("m_RevealButton").objectReferenceValue = reveal;
            so.FindProperty("m_RevealLabel").objectReferenceValue = revealLabel;
            so.FindProperty("m_CopyButton").objectReferenceValue = copy;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePopup(root, "TextInputPopup");
        }

        private static void BuildMessagePopupPrefab()
        {
            GameObject root = PopupRoot("MessagePopup");
            RectTransform window = Window(root.transform, new Vector2(WINDOW_W, 320f));

            TMP_Text title = Label(window, "Title", "알림", 34, FontStyles.Bold, new Vector2(0f, 104f), new Vector2(WINDOW_W - 80f, 48f));
            title.color = UiPalette.AccentSoft;
            TMP_Text body = Label(window, "Body", "내용", 22, FontStyles.Normal, new Vector2(0f, 20f), new Vector2(WINDOW_W - 100f, 110f));
            body.color = UiPalette.TextOn;

            // 둘일 때: 왼쪽=주 버튼(강조) · 오른쪽=보조. 하나일 때는 보조가 꺼져 주 버튼만 남는다 — 가운데로 온다.
            Button primary = WideButton(window, "PrimaryButton", "확인", true, new Vector2(-150f, -104f), out TMP_Text primaryLabel);
            Button secondary = WideButton(window, "SecondaryButton", "취소", false, new Vector2(150f, -104f), out TMP_Text secondaryLabel);

            Button close = SmallButton(window, "CloseButton", "X", new Vector2(WINDOW_W * 0.5f - 34f, 128f), new Vector2(44f, 44f), out _);

            var popup = root.AddComponent<MessagePopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("m_Title").objectReferenceValue = title;
            so.FindProperty("m_Body").objectReferenceValue = body;
            so.FindProperty("m_PrimaryButton").objectReferenceValue = primary;
            so.FindProperty("m_PrimaryLabel").objectReferenceValue = primaryLabel;
            so.FindProperty("m_SecondaryButton").objectReferenceValue = secondary;
            so.FindProperty("m_SecondaryLabel").objectReferenceValue = secondaryLabel;
            so.FindProperty("m_CloseButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePopup(root, "MessagePopup");
        }

        // ── 조립 도구 ─────────────────────────────────────────

        private static GameObject PopupRoot(string _name)
        {
            var root = new GameObject(_name, typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return root;
        }

        private static void SavePopup(GameObject _root, string _address)
        {
            string path = $"{POPUP_DIR}/{_address}.prefab";
            PrefabUtility.SaveAsPrefabAsset(_root, path);
            Object.DestroyImmediate(_root);
            RegisterAddressable(path, _address);
        }

        private static RectTransform Window(Transform _parent, Vector2 _size)
        {
            RectTransform rect = Rect(_parent, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, _size);
            rect.gameObject.AddComponent<Image>().color = UiPalette.PanelSolid;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = UiPalette.Line;
            outline.effectDistance = new Vector2(2f, -2f);

            RectTransform bar = Rect(rect, "AccentBar", new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(_size.x, 6f));
            bar.gameObject.AddComponent<Image>().color = UiPalette.Accent;
            return rect;
        }

        private static RectTransform Rect(Transform _parent, string _name, Vector2 _anchor, Vector2 _pos, Vector2 _size)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.transform.SetParent(_parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = _anchor;
            rect.anchorMax = _anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = _pos;
            rect.sizeDelta = _size;
            return rect;
        }

        private static TMP_Text Label(Transform _parent, string _name, string _text, float _size, FontStyles _style,
            Vector2 _pos, Vector2 _rect)
        {
            RectTransform rect = Rect(_parent, _name, new Vector2(0.5f, 0.5f), _pos, _rect);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = _text;
            text.fontSize = _size;
            text.fontStyle = _style;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static Button WideButton(Transform _parent, string _name, string _label, bool _primary, Vector2 _pos,
            out TMP_Text _text)
        {
            RectTransform rect = Rect(_parent, _name, new Vector2(0.5f, 0.5f), _pos, new Vector2(260f, 64f));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = _primary ? UiPalette.Accent : UiPalette.Slot;
            if (!_primary)
            {
                var outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = UiPalette.Line;
                outline.effectDistance = new Vector2(1f, -1f);
            }
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            _text = Label(rect, "Label", _label, _primary ? 26 : 24, _primary ? FontStyles.Bold : FontStyles.Normal,
                Vector2.zero, new Vector2(240f, 56f));
            _text.color = _primary ? UiPalette.TextOnAccent : UiPalette.TextOn;
            return button;
        }

        private static Button SmallButton(Transform _parent, string _name, string _label, Vector2 _pos, Vector2 _size,
            out TMP_Text _text)
        {
            RectTransform rect = Rect(_parent, _name, new Vector2(0.5f, 0.5f), _pos, _size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = UiPalette.Slot;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            _text = Label(rect, "Label", _label, 18, FontStyles.Normal, Vector2.zero, _size);
            _text.color = UiPalette.TextOn;
            return button;
        }

        // TMP_InputField는 Text Area(RectMask2D) + Text + Placeholder 자식 구조를 직접 만들어야 한다(LobbyCanvasBuilder와 같다).
        private static TMP_InputField Input(Transform _parent, string _name, Vector2 _pos, Vector2 _size, string _placeholder)
        {
            RectTransform rect = Rect(_parent, _name, new Vector2(0.5f, 0.5f), _pos, _size);
            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = UiPalette.Groove;

            RectTransform area = Rect(rect, "Text Area", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(18f, 6f);
            area.offsetMax = new Vector2(-18f, -6f);
            area.gameObject.AddComponent<RectMask2D>();

            RectTransform textRect = Rect(area, "Text", Vector2.zero, Vector2.zero, Vector2.zero);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 30f;
            text.alignment = TextAlignmentOptions.Left;
            text.color = UiPalette.TextOn;

            RectTransform phRect = Rect(area, "Placeholder", Vector2.zero, Vector2.zero, Vector2.zero);
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            var placeholder = phRect.gameObject.AddComponent<TextMeshProUGUI>();
            placeholder.text = _placeholder;
            placeholder.fontSize = 26f;
            placeholder.alignment = TextAlignmentOptions.Left;
            placeholder.color = UiPalette.WithAlpha(UiPalette.TextDim, 0.8f);

            var field = rect.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.targetGraphic = bg;
            return field;
        }
    }
}
