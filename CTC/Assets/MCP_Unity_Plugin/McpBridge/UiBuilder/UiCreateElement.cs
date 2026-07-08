// UiCreateElement.cs — creates a GameObject with the correct uGUI components for a ComponentNode.
// Ported from phucnguyen752/unity-ui-mcp (MIT).
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace McpUiGen
{
    public static class CreateElementTool
    {
        public static GameObject Create(ComponentNode node, Transform parent)
        {
            var go = new GameObject(node.name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {node.name}");
            GameObjectUtility.SetParentAndAlign(go, parent.gameObject);

            go.AddComponent<RectTransform>();

            switch (node.type)
            {
                case UiComponentType.Empty:       /* RectTransform only */   break;
                case UiComponentType.Panel:       SetupPanel(go, node);      break;
                case UiComponentType.Button:      SetupButton(go, node);     break;
                case UiComponentType.Image:       SetupImage(go, node);      break;
                case UiComponentType.Text:        SetupText(go, node);       break;
                case UiComponentType.InputField:  SetupInputField(go, node); break;
                case UiComponentType.ScrollView:  SetupScrollView(go, node); break;
                case UiComponentType.Toggle:      SetupToggle(go, node);     break;
                case UiComponentType.Slider:      SetupSlider(go, node);     break;
            }
            return go;
        }

        private static void SetupPanel(GameObject go, ComponentNode node)
        {
            var img  = go.AddComponent<Image>();
            img.color = node.color;
            img.type  = Image.Type.Sliced;
        }

        private static void SetupButton(GameObject go, ComponentNode node)
        {
            var img   = go.AddComponent<Image>();
            img.color = node.color;
            img.type  = Image.Type.Sliced;

            go.AddComponent<Button>();

            var labelGo = new GameObject("Label");
            Undo.RegisterCreatedObjectUndo(labelGo, "Create Label");
            GameObjectUtility.SetParentAndAlign(labelGo, go);

            var rt = labelGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var tmp       = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = string.IsNullOrEmpty(node.text) ? "Button" : node.text;
            tmp.fontSize  = node.fontSize;
            tmp.fontStyle = ConvertFontStyle(node.fontStyle);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
        }

        private static void SetupImage(GameObject go, ComponentNode node)
        {
            var img  = go.AddComponent<Image>();
            img.color = node.color;
        }

        private static void SetupText(GameObject go, ComponentNode node)
        {
            var tmp       = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = node.text;
            tmp.fontSize  = node.fontSize;
            tmp.fontStyle = ConvertFontStyle(node.fontStyle);
            tmp.color     = node.color;
        }

        private static void SetupInputField(GameObject go, ComponentNode node)
        {
            var bg   = go.AddComponent<Image>();
            bg.color = node.color;
            ApplySprite(bg, node.spritePath, node.ppum);

            var field = go.AddComponent<TMP_InputField>();

            var phGo = new GameObject("Placeholder");
            Undo.RegisterCreatedObjectUndo(phGo, "Create Placeholder");
            GameObjectUtility.SetParentAndAlign(phGo, go);
            var phRt = phGo.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(8, 4);
            phRt.offsetMax = new Vector2(-8, -4);
            var phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.text     = string.IsNullOrEmpty(node.text) ? "Enter text..." : node.text;
            phTmp.color    = new Color(0.5f, 0.5f, 0.5f, 0.75f);
            phTmp.fontSize = node.fontSize;

            var taGo = new GameObject("Text Area");
            Undo.RegisterCreatedObjectUndo(taGo, "Create Text Area");
            GameObjectUtility.SetParentAndAlign(taGo, go);
            var taRt = taGo.AddComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(8, 4);
            taRt.offsetMax = new Vector2(-8, -4);
            var taTmp = taGo.AddComponent<TextMeshProUGUI>();
            taTmp.fontSize = node.fontSize;
            taTmp.color    = Color.black;

            field.placeholder   = phTmp;
            field.textComponent = taTmp;
        }

        private static void SetupScrollView(GameObject go, ComponentNode node)
        {
            var img  = go.AddComponent<Image>();
            img.color = node.color;

            var sv = go.AddComponent<ScrollRect>();

            var vpGo = new GameObject("Viewport");
            Undo.RegisterCreatedObjectUndo(vpGo, "Create Viewport");
            GameObjectUtility.SetParentAndAlign(vpGo, go);
            var vpRt = vpGo.AddComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;
            vpGo.AddComponent<Image>().color = Color.clear;
            vpGo.AddComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content");
            Undo.RegisterCreatedObjectUndo(contentGo, "Create Content");
            GameObjectUtility.SetParentAndAlign(contentGo, vpGo);
            var cRt = contentGo.AddComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0, 1);
            cRt.anchorMax = new Vector2(1, 1);
            cRt.pivot     = new Vector2(0.5f, 1);
            cRt.sizeDelta = Vector2.zero;

            sv.viewport = vpRt;
            sv.content  = cRt;
        }

        // Toggle: 박스(targetGraphic) + Checkmark(graphic, on일 때 표시). 체크마크 텍스처 슬롯 지원.
        private static void SetupToggle(GameObject go, ComponentNode node)
        {
            var bg = go.AddComponent<Image>();
            bg.color = node.color;
            ApplySprite(bg, node.spritePath, node.ppum);

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bg;

            var ckGo = NewChild(go, "Checkmark");
            var ckRt = ckGo.GetComponent<RectTransform>();
            ckRt.anchorMin = Vector2.zero;
            ckRt.anchorMax = Vector2.one;
            ckRt.offsetMin = new Vector2(4, 4);
            ckRt.offsetMax = new Vector2(-4, -4);
            var ck = ckGo.AddComponent<Image>();
            ck.color = node.textColor ?? Color.white;
            ApplySprite(ck, node.checkSpritePath, node.ppum);
            toggle.graphic = ck;

            toggle.isOn = false;
        }

        // Slider: Background + Fill Area/Fill(fillRect) + Handle Slide Area/Handle(handleRect). 텍스처 슬롯 지원.
        private static void SetupSlider(GameObject go, ComponentNode node)
        {
            var slider = go.AddComponent<Slider>();

            var bgGo = NewChild(go, "Background");
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.15f);
            bgRt.anchorMax = new Vector2(1, 0.85f);
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = node.color;
            ApplySprite(bgImg, node.spritePath, node.ppum);

            var faGo = NewChild(go, "Fill Area");
            var faRt = faGo.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0, 0.15f);
            faRt.anchorMax = new Vector2(1, 0.85f);
            faRt.offsetMin = new Vector2(4, 0);
            faRt.offsetMax = new Vector2(-4, 0);
            var fillGo = NewChild(faGo, "Fill");
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(0, 1);
            fillRt.sizeDelta = new Vector2(10, 0);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = node.textColor ?? new Color(0.82f, 0.62f, 0.22f, 1f);
            ApplySprite(fillImg, node.fillSpritePath, node.ppum);
            slider.fillRect = fillRt;

            var hsaGo = NewChild(go, "Handle Slide Area");
            var hsaRt = hsaGo.GetComponent<RectTransform>();
            hsaRt.anchorMin = Vector2.zero;
            hsaRt.anchorMax = Vector2.one;
            hsaRt.offsetMin = new Vector2(10, 0);
            hsaRt.offsetMax = new Vector2(-10, 0);
            var handleGo = NewChild(hsaGo, "Handle");
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(20, 0);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = Color.white;
            ApplySprite(handleImg, node.handleSpritePath, node.ppum);
            // 핸들 스프라이트 없으면 게이지 용도 — 흰 박스 숨김(투명)
            if (string.IsNullOrEmpty(node.handleSpritePath))
                handleImg.color = new Color(0f, 0f, 0f, 0f);
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;

            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = node.value;
        }

        private static GameObject NewChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            GameObjectUtility.SetParentAndAlign(go, parent);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void ApplySprite(Image img, string spritePath, float? ppum)
        {
            if (string.IsNullOrEmpty(spritePath)) return;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null) return;
            img.sprite = sprite;
            img.type   = Image.Type.Sliced;
            if (ppum.HasValue) img.pixelsPerUnitMultiplier = ppum.Value;
        }

        private static FontStyles ConvertFontStyle(FontStyle s) => s switch
        {
            FontStyle.Bold          => FontStyles.Bold,
            FontStyle.Italic        => FontStyles.Italic,
            FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
            _                       => FontStyles.Normal,
        };
    }
}
