using CTC.Runtime.Economy;
using CTC.Runtime.Match;
using CTC.Runtime.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Editor
{
    /// <summary>
    /// 쥐 상점 팝업 굽기 — **시안 B「목록 + 상세」**(2026-09-19 사용자 선택, 캔버스 시안 `쥐 상점 UI 시안`).
    ///
    /// 게임의 다른 창과 같은 말로 짓는다(사용자: 「다른 UI들 디자인 참고해서」) — 각진 남색 창 · 위쪽 노란 줄 ·
    /// 옅은 노랑 제목(앞문 팝업 `PopupPrefabBuilder.Window`) · 줄은 로비 방 목록(`LobbyRowItem`)처럼 골라지면 옅은 노랑 ·
    /// 주 버튼은 노랑 바탕에 어두운 글. 수치는 시안(1280×720) × 1.5 = 기준 해상도 1920×1080.
    ///
    /// 줄은 굽지 않는다 — 틀(<c>RowTemplate</c>·<c>GroupTemplate</c>)만 굽고 런타임이 카탈로그 수만큼 찍는다.
    /// 목록은 스크롤 영역이라 기술이 늘어도 창을 다시 짤 필요가 없다(시안 B를 고른 이유).
    /// </summary>
    public static partial class SetupNetworkingScene
    {
        private const float SHOP_W = 1680f;
        private const float SHOP_H = 960f;
        private const float SHOP_PAD = 36f;
        private const float SHOP_BODY_TOP = 138f;
        private const float SHOP_BODY_H = 746f;
        private const float SHOP_LIST_W = 690f;
        private const float SHOP_GAP = 24f;
        private const float SHOP_ROW_H = 48f;
        private const float SHOP_PRICE_W = 126f;
        private const float SHOP_STEP_H = 78f;
        private const float SHOP_STEP_GAP = 12f;
        private const float SHOP_VEIL_ALPHA = 0.5f;

        private static ShopPopup BuildShopPopup(GameObject _canvasGo, MatchManager _match)
        {
            // 루트 = 화면 전체(옅은 베일 — 뒤 HUD 클릭도 막는다) + 가운데 창
            var root = new GameObject("ShopPopupRoot", typeof(RectTransform));
            root.transform.SetParent(_canvasGo.transform, false);
            StretchFull((RectTransform)root.transform);
            AddStretchedImage(root.transform, "Veil", UiPalette.WithAlpha(UiPalette.Veil, SHOP_VEIL_ALPHA));

            var panel = new GameObject("ShopPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(SHOP_W, SHOP_H);
            panel.AddComponent<Image>().color = UiPalette.PanelSolid;
            AddLine(panel, 2f);
            ShopRect(panel.transform, "AccentBar", 0f, 0f, SHOP_W, 6f).gameObject.AddComponent<Image>().color = UiPalette.Accent;

            // ── 머리: 왼쪽 치즈 · 가운데 제목 · 오른쪽 칸 ──
            ShopText(panel.transform, "CheeseLabel", "치즈", 19f, false, UiPalette.TextDim, TextAlignmentOptions.BottomLeft, SHOP_PAD, 58f, 60f, 30f);
            TMP_Text cheese = ShopText(panel.transform, "CheeseValue", "0", 36f, true, UiPalette.Accent, TextAlignmentOptions.BottomLeft, SHOP_PAD + 56f, 40f, 260f, 48f);
            ShopText(panel.transform, "Title", "상점", 34f, true, UiPalette.AccentSoft, TextAlignmentOptions.Center, (SHOP_W - 600f) * 0.5f, 34f, 600f, 46f);
            ShopText(panel.transform, "Subtitle", "상점 원 안에서만 · 고양이가 오면 철문이 닫힌다", 19f, false, UiPalette.TextDim, TextAlignmentOptions.Center, (SHOP_W - 900f) * 0.5f, 82f, 900f, 28f);

            const float slotTextW = 90f;
            TMP_Text slotText = ShopText(panel.transform, "SlotText", "0/5", 24f, true, UiPalette.TextOn, TextAlignmentOptions.MidlineRight, SHOP_W - SHOP_PAD - slotTextW, 50f, slotTextW, 36f);
            int pipCount = TechLogic.SlotCapMax();
            const float pipW = 13.5f, pipGap = 4.5f;
            float pipsRight = SHOP_W - SHOP_PAD - slotTextW - 12f;
            float pipsLeft = pipsRight - (pipCount * pipW + (pipCount - 1) * pipGap);
            var pips = new Image[pipCount];
            for (int i = 0; i < pipCount; i++)
            {
                RectTransform pip = ShopRect(panel.transform, $"SlotPip{i}", pipsLeft + i * (pipW + pipGap), 56f, pipW, 24f);
                pips[i] = pip.gameObject.AddComponent<Image>();
                pips[i].color = UiPalette.Groove;
            }
            ShopText(panel.transform, "SlotLabel", "칸", 19f, false, UiPalette.TextDim, TextAlignmentOptions.MidlineRight, pipsLeft - 12f - 40f, 54f, 40f, 30f);

            // ── 왼쪽 목록(스크롤) ──
            RectTransform list = ShopRect(panel.transform, "ShopList", SHOP_PAD, SHOP_BODY_TOP, SHOP_LIST_W, SHOP_BODY_H);
            list.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // 줄 사이에서도 휠이 먹게
            var scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 40f;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(list, false);
            StretchFull((RectTransform)viewport.transform);
            viewport.AddComponent<RectMask2D>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.sizeDelta = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4.5f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = crt;

            // 축 머리글 틀
            var groupGo = new GameObject("GroupTemplate", typeof(RectTransform));
            groupGo.transform.SetParent(content.transform, false);
            var group = groupGo.AddComponent<TextMeshProUGUI>();
            group.text = "공격";
            group.fontSize = 18f;
            group.fontStyle = FontStyles.Bold;
            group.color = UiPalette.TextDim;
            group.alignment = TextAlignmentOptions.BottomLeft;
            group.margin = new Vector4(3f, 0f, 0f, 6f);
            group.raycastTarget = false;
            groupGo.AddComponent<LayoutElement>().preferredHeight = 36f;
            groupGo.SetActive(false);

            // 한 줄 틀 — 이름 · 단계 막대 3 · 값
            var rowGo = new GameObject("RowTemplate", typeof(RectTransform));
            rowGo.transform.SetParent(content.transform, false);
            rowGo.AddComponent<LayoutElement>().preferredHeight = SHOP_ROW_H;
            var rowBg = rowGo.AddComponent<Image>();
            rowBg.color = UiPalette.WithAlpha(UiPalette.Slot, 0.72f);
            var rowGroup = rowGo.AddComponent<CanvasGroup>();
            var rowItem = rowGo.AddComponent<ShopRowItem>();

            const float barW = 18f, barGap = 3f, barsToPrice = 12f;
            float barsRightEdge = -(18f + SHOP_PRICE_W + barsToPrice);
            var rowTitleGo = new GameObject("Title", typeof(RectTransform));
            rowTitleGo.transform.SetParent(rowGo.transform, false);
            var rowTitleRt = (RectTransform)rowTitleGo.transform;
            rowTitleRt.anchorMin = Vector2.zero;
            rowTitleRt.anchorMax = Vector2.one;
            rowTitleRt.offsetMin = new Vector2(18f, 0f);
            rowTitleRt.offsetMax = new Vector2(barsRightEdge - (3f * barW + 2f * barGap) - 12f, 0f);
            var rowTitle = rowTitleGo.AddComponent<TextMeshProUGUI>();
            rowTitle.text = "기술";
            rowTitle.fontSize = 21f;
            rowTitle.fontStyle = FontStyles.Bold;
            rowTitle.color = UiPalette.TextOn;
            rowTitle.alignment = TextAlignmentOptions.MidlineLeft;
            rowTitle.raycastTarget = false;

            var rowBars = new Image[ShopPopup.STEP_COUNT];
            for (int i = 0; i < rowBars.Length; i++)
            {
                RectTransform bar = RightMid(rowGo.transform, $"Bar{i}", barsRightEdge - (rowBars.Length - 1 - i) * (barW + barGap), barW, 6f);
                rowBars[i] = bar.gameObject.AddComponent<Image>();
                rowBars[i].color = UiPalette.Groove;
                rowBars[i].raycastTarget = false;
            }

            RectTransform priceRt = RightMid(rowGo.transform, "Price", -18f, SHOP_PRICE_W, 40f);
            var rowPrice = priceRt.gameObject.AddComponent<TextMeshProUGUI>();
            rowPrice.text = "0 치즈";
            rowPrice.fontSize = 20f;
            rowPrice.fontStyle = FontStyles.Bold;
            rowPrice.color = UiPalette.TextOn;
            rowPrice.alignment = TextAlignmentOptions.MidlineRight;
            rowPrice.raycastTarget = false;

            SetRef(rowItem, "m_Background", rowBg);
            SetRef(rowItem, "m_Group", rowGroup);
            SetRef(rowItem, "m_Title", rowTitle);
            SetRefArray(rowItem, "m_Bars", rowBars);
            SetRef(rowItem, "m_Price", rowPrice);
            rowGo.SetActive(false);

            // ── 오른쪽 상세 ──
            float detailW = SHOP_W - SHOP_PAD * 2f - SHOP_LIST_W - SHOP_GAP;
            float innerW = detailW - 66f;
            RectTransform detail = ShopRect(panel.transform, "ShopDetail", SHOP_PAD + SHOP_LIST_W + SHOP_GAP, SHOP_BODY_TOP, detailW, SHOP_BODY_H);
            detail.gameObject.AddComponent<Image>().color = ShopPopup.DETAIL_BG;   // 불투명 — Outline 주의(ShopPopup 머리말)
            AddLine(detail.gameObject, 1f);

            TMP_Text axis = ShopText(detail, "Axis", "", 19f, false, UiPalette.TextDim, TextAlignmentOptions.TopLeft, 33f, 30f, 400f, 26f);
            TMP_Text title = ShopText(detail, "Title", "", 36f, true, UiPalette.AccentSoft, TextAlignmentOptions.TopLeft, 33f, 56f, 520f, 48f);
            TMP_Text level = ShopText(detail, "Level", "", 20f, true, UiPalette.Good, TextAlignmentOptions.TopRight, detailW - 33f - 300f, 36f, 300f, 28f);
            const float bigBarW = 54f, bigBarGap = 4.5f;
            float bigBarsLeft = detailW - 33f - (ShopPopup.STEP_COUNT * bigBarW + (ShopPopup.STEP_COUNT - 1) * bigBarGap);
            var detailBars = new Image[ShopPopup.STEP_COUNT];
            for (int i = 0; i < detailBars.Length; i++)
            {
                RectTransform bar = ShopRect(detail, $"Bar{i}", bigBarsLeft + i * (bigBarW + bigBarGap), 74f, bigBarW, 9f);
                detailBars[i] = bar.gameObject.AddComponent<Image>();
                detailBars[i].color = UiPalette.Groove;
            }

            var steps = new GameObject[ShopPopup.STEP_COUNT];
            var stepBgs = new Image[ShopPopup.STEP_COUNT];
            var stepLines = new Outline[ShopPopup.STEP_COUNT];
            var badges = new Image[ShopPopup.STEP_COUNT];
            var badgeTexts = new TMP_Text[ShopPopup.STEP_COUNT];
            var stepTitles = new TMP_Text[ShopPopup.STEP_COUNT];
            var stepHints = new TMP_Text[ShopPopup.STEP_COUNT];
            var stepPrices = new TMP_Text[ShopPopup.STEP_COUNT];
            for (int i = 0; i < ShopPopup.STEP_COUNT; i++)
            {
                RectTransform step = ShopRect(detail, $"Step{i}", 33f, 132f + i * (SHOP_STEP_H + SHOP_STEP_GAP), innerW, SHOP_STEP_H);
                steps[i] = step.gameObject;
                stepBgs[i] = step.gameObject.AddComponent<Image>();
                stepBgs[i].color = ShopPopup.DETAIL_BG;
                stepLines[i] = AddLine(step.gameObject, 1f);

                RectTransform badge = ShopRect(step, "Badge", 18f, 15f, 60f, 48f);
                badges[i] = badge.gameObject.AddComponent<Image>();
                badgeTexts[i] = ShopText(badge, "Text", $"Lv{i + 1}", 20f, true, UiPalette.TextOnAccent, TextAlignmentOptions.Center, 0f, 0f, 60f, 48f);
                stepTitles[i] = ShopText(step, "Title", "", 22.5f, true, UiPalette.TextOn, TextAlignmentOptions.TopLeft, 96f, 11f, 560f, 32f);
                stepHints[i] = ShopText(step, "Hint", "", 19.5f, false, UiPalette.TextSub, TextAlignmentOptions.TopLeft, 96f, 42f, 560f, 28f);
                stepPrices[i] = ShopText(step, "Price", "", 21f, true, UiPalette.Accent, TextAlignmentOptions.MidlineRight, innerW - 18f - 220f, 0f, 220f, SHOP_STEP_H);
            }

            RectTransform buy = ShopRect(detail, "BuyButton", 33f, SHOP_BODY_H - 30f - 28f - 12f - 72f, innerW, 72f);
            var buyBg = buy.gameObject.AddComponent<Image>();
            buyBg.color = UiPalette.Accent;
            Outline buyLine = AddLine(buy.gameObject, 1f);
            var buyButton = buy.gameObject.AddComponent<Button>();
            buyButton.targetGraphic = buyBg;
            buy.gameObject.AddComponent<CTC.Runtime.Audio.UiClickSfx>();
            TMP_Text buyLabel = ShopText(buy, "Label", "", 25.5f, true, UiPalette.TextOnAccent, TextAlignmentOptions.Center, 0f, 0f, innerW, 72f);
            TMP_Text note = ShopText(detail, "Note", "", 19f, false, UiPalette.TextDim, TextAlignmentOptions.Center, 33f, SHOP_BODY_H - 30f - 28f, innerW, 28f);

            // ── 발 ──
            ShopText(panel.transform, "FootLeft", "마우스를 올리면 오른쪽에 자세히 · 클릭해서 산다", 19f, false, UiPalette.TextDim, TextAlignmentOptions.MidlineLeft, SHOP_PAD, SHOP_H - 54f, 900f, 30f);
            ShopText(panel.transform, "FootRight", "E · ESC 닫기", 19f, false, UiPalette.TextDim, TextAlignmentOptions.MidlineRight, SHOP_W - SHOP_PAD - 400f, SHOP_H - 54f, 400f, 30f);

            var popup = _canvasGo.AddComponent<ShopPopup>();
            SetRef(popup, "m_Root", root);
            SetRef(popup, "m_Match", _match);
            SetRef(popup, "m_CheeseValue", cheese);
            SetRef(popup, "m_SlotText", slotText);
            SetRefArray(popup, "m_SlotPips", pips);
            SetRef(popup, "m_ListContent", crt);
            SetRef(popup, "m_RowTemplate", rowItem);
            SetRef(popup, "m_GroupTemplate", group);
            SetRef(popup, "m_DetailAxis", axis);
            SetRef(popup, "m_DetailTitle", title);
            SetRef(popup, "m_DetailLevel", level);
            SetRefArray(popup, "m_DetailBars", detailBars);
            SetRefArray(popup, "m_Steps", steps);
            SetRefArray(popup, "m_StepBackgrounds", stepBgs);
            SetRefArray(popup, "m_StepOutlines", stepLines);
            SetRefArray(popup, "m_StepBadges", badges);
            SetRefArray(popup, "m_StepBadgeTexts", badgeTexts);
            SetRefArray(popup, "m_StepTitles", stepTitles);
            SetRefArray(popup, "m_StepHints", stepHints);
            SetRefArray(popup, "m_StepPrices", stepPrices);
            SetRef(popup, "m_BuyButton", buyButton);
            SetRef(popup, "m_BuyBackground", buyBg);
            SetRef(popup, "m_BuyOutline", buyLine);
            SetRef(popup, "m_BuyLabel", buyLabel);
            SetRef(popup, "m_Note", note);

            root.SetActive(false);
            return popup;
        }

        /// <summary>부모의 왼쪽 위 기준 사각형 — 시안의 (x, y, w, h)를 그대로 옮긴다(y는 아래로).</summary>
        private static RectTransform ShopRect(Transform _parent, string _name, float _x, float _y, float _w, float _h)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.transform.SetParent(_parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(_x, -_y);
            rt.sizeDelta = new Vector2(_w, _h);
            return rt;
        }

        /// <summary>부모의 오른쪽 가운데 기준 — 오른쪽 끝이 <paramref name="_right"/>(음수 = 안쪽)에 온다.</summary>
        private static RectTransform RightMid(Transform _parent, string _name, float _right, float _w, float _h)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.transform.SetParent(_parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(_right, 0f);
            rt.sizeDelta = new Vector2(_w, _h);
            return rt;
        }

        private static TMP_Text ShopText(Transform _parent, string _name, string _text, float _size, bool _bold,
            Color _color, TextAlignmentOptions _align, float _x, float _y, float _w, float _h)
        {
            RectTransform rt = ShopRect(_parent, _name, _x, _y, _w, _h);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = _text;
            tmp.fontSize = _size;
            tmp.fontStyle = _bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.color = _color;
            tmp.alignment = _align;
            tmp.raycastTarget = false;
            return tmp;
        }

        // 다른 창과 같은 테두리 — 앞문 팝업·패널 버튼이 쓰는 Outline(선 색 = UiPalette.Line)
        private static Outline AddLine(GameObject _go, float _px)
        {
            var outline = _go.AddComponent<Outline>();
            outline.effectColor = UiPalette.Line;
            outline.effectDistance = new Vector2(_px, -_px);
            return outline;
        }
    }
}
