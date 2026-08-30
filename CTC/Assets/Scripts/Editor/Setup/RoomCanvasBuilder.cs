using CTC.Runtime.Room;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CTC.Editor
{
    /// <summary>
    /// 대기실 화면(UI)만 짓는다 — <see cref="SetupNetworkingScene.RunRoom"/>의 일부.
    ///
    /// 3D가 주인공인 화면이라 UI는 **가장자리로 비켜선다** — 좌측 참가자 보드, 우측 맵 선택,
    /// 상단 방 이름, 하단 중앙 시작, 좌하단 채팅. 가운데는 참가자들이 걸어 다니는 자리로 비운다.
    ///
    /// 대기 중 화면은 통째로 `Hud` 하나에 담는다 — 추첨이 시작되면 <see cref="RoomDrawStage"/>가
    /// 그걸 한 번에 끈다. 요소마다 껐다 켜면 하나 빠뜨렸을 때 화면에 홀로 남는다.
    /// </summary>
    public static partial class SetupNetworkingScene
    {
        private const int ROOM_MAP_SLOTS = 3;

        private const float MAP_RIGHT = -300f;   // 우측 앵커 기준
        private const float MAP_W = 340f;
        private const float MAP_TOP = 260f;
        private const float MAP_ROW_H = 64f;
        private const float MAP_ROW_GAP = 8f;

        private static void BuildRoomCanvas(
            RoomSession _session, RoomDirector _director, RoomMapCatalog _catalog,
            Light _spotlight, Light _fillLight)
        {
            var canvasGo = new GameObject("RoomCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // ESC의 단독 소유자. 대기실에는 설치도 인게임 핫바도 없으니 채팅·옵션만 건다.
            var escGo = new GameObject("EscapeRouter");
            var escRouter = escGo.AddComponent<CTC.Runtime.Core.EscapeRouter>();

            // ── 대기 중 화면은 통째로 이 밑에 ──
            var hudGo = new GameObject("Hud", typeof(RectTransform));
            hudGo.transform.SetParent(canvasGo.transform, false);
            RectTransform hud = hudGo.GetComponent<RectTransform>();
            hud.anchorMin = Vector2.zero;
            hud.anchorMax = Vector2.one;
            hud.offsetMin = Vector2.zero;
            hud.offsetMax = Vector2.zero;

            // ── 머리: 나가기 · 방 이름 · 인원 ──
            Button leave = RoomButton(hud, "LeaveButton", "나가기",
                new Vector2(0f, 1f), new Vector2(BOARD_LEFT + 70f, -70f), new Vector2(140f, 52f), false);

            TMP_Text roomName = RoomText(hud, "RoomName", "대기실", 34, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(BOARD_LEFT + 400f, -68f), new Vector2(520f, 48f));
            roomName.alignment = TextAlignmentOptions.Left;

            TMP_Text count = RoomText(hud, "Count", RoomLogic.CountLabel(0), 22, FontStyles.Normal,
                new Vector2(0f, 1f), new Vector2(BOARD_LEFT + 780f, -70f), new Vector2(200f, 40f));
            count.alignment = TextAlignmentOptions.Left;
            count.color = ROOM_TEXT_DIM;

            // ── 좌측 참가자 보드 ──
            TMP_Text boardTitle = RoomText(hud, "BoardTitle", "참가자", 18, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(BOARD_LEFT + BOARD_W * 0.5f, -(BOARD_TOP - 46f)),
                new Vector2(BOARD_W, 30f));
            boardTitle.alignment = TextAlignmentOptions.Left;
            boardTitle.color = ROOM_TEXT_DIM;
            boardTitle.characterSpacing = 6f;

            var rows = new RoomMemberRow[RoomLogic.MAX_PLAYERS];
            for (int i = 0; i < rows.Length; i++)
            {
                float y = -(BOARD_TOP + i * (BOARD_ROW_H + BOARD_ROW_GAP));
                rows[i] = RoomRow(hud, $"Row{i + 1}", y);
            }

            // ── 우측 맵 선택 (방장만 누른다 — 나머지는 보기만) ──
            TMP_Text mapTitle = RoomText(hud, "MapTitle", "맵", 18, FontStyles.Bold,
                new Vector2(1f, 1f), new Vector2(MAP_RIGHT, -(MAP_TOP - 46f)), new Vector2(MAP_W, 30f));
            mapTitle.alignment = TextAlignmentOptions.Left;
            mapTitle.color = ROOM_TEXT_DIM;
            mapTitle.characterSpacing = 6f;

            var mapButtons = new Button[ROOM_MAP_SLOTS];
            for (int i = 0; i < ROOM_MAP_SLOTS; i++)
            {
                float y = -(MAP_TOP + i * (MAP_ROW_H + MAP_ROW_GAP));
                mapButtons[i] = RoomButton(hud, $"MapButton{i + 1}", "-",
                    new Vector2(1f, 1f), new Vector2(MAP_RIGHT, y), new Vector2(MAP_W, MAP_ROW_H), false);
            }

            TMP_Text mapBrief = RoomText(hud, "MapBrief", "", 18, FontStyles.Normal,
                new Vector2(1f, 1f),
                new Vector2(MAP_RIGHT, -(MAP_TOP + ROOM_MAP_SLOTS * (MAP_ROW_H + MAP_ROW_GAP) + 12f)),
                new Vector2(MAP_W, 30f));
            mapBrief.alignment = TextAlignmentOptions.Left;
            mapBrief.color = ROOM_TEXT_DIM;

            // ── 하단 중앙: 시작 ──
            Button start = RoomButton(hud, "StartButton", "시작",
                new Vector2(0.5f, 0f), new Vector2(0f, 148f), new Vector2(340f, 82f), true);

            TMP_Text hint = RoomText(hud, "StartHint", RoomLogic.StartHint(0, false), 19, FontStyles.Normal,
                new Vector2(0.5f, 0f), new Vector2(0f, 88f), new Vector2(900f, 34f));
            hint.color = ROOM_TEXT_DIM;

            // ── 채팅·옵션 (인게임과 같은 컴포넌트를 그대로 쓴다) ──
            // 이 둘은 캔버스 바로 밑에 붙는다 — Hud가 꺼져도 채팅은 남는다.
            BuildOptionsPopup(canvasGo, escRouter);
            BuildChatHud(canvasGo, escRouter);
            MoveChatBelowBoard(canvasGo);

            // ── 추첨 연출 (Hud 뒤에 만들어야 그 위에 그려진다) ──
            GameObject cardRoot = RoomPanel(canvasGo.transform, "DrawCard",
                new Color(0.004f, 0.006f, 0.012f, 0f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f)).gameObject;
            cardRoot.GetComponent<Image>().raycastTarget = false;

            // 화면 한가운데가 아니라 **위쪽**이다 — 뽑힌 참가자는 조명 밑에 서 있고,
            // 카드가 중앙에 오면 그 머리와 이름표를 덮는다(2026-08-30 실측).
            RectTransform card = RoomPanel(cardRoot.transform, "Card", ROOM_PANEL_DEEP,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 240f), new Vector2(940f, 240f));

            TMP_Text cardTitle = RoomText(card, "Title", "", 52, FontStyles.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 46f), new Vector2(880f, 80f));
            cardTitle.color = ROOM_ACCENT;

            TMP_Text cardName = RoomText(card, "Name", "", 34, FontStyles.Normal,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(880f, 60f));

            RectTransform fade = RoomPanel(canvasGo.transform, "Fade", Color.black,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
            fade.GetComponent<Image>().raycastTarget = false;
            var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;

            // ── 배선 ──
            var board = canvasGo.AddComponent<RoomBoardView>();
            var boardSo = new SerializedObject(board);
            boardSo.FindProperty("m_Session").objectReferenceValue = _session;
            SerializedProperty rowsProp = boardSo.FindProperty("m_Rows");
            rowsProp.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            }
            boardSo.ApplyModifiedPropertiesWithoutUndo();

            var hudView = canvasGo.AddComponent<RoomHudView>();
            var hudSo = new SerializedObject(hudView);
            hudSo.FindProperty("m_Session").objectReferenceValue = _session;
            hudSo.FindProperty("m_RoomNameLabel").objectReferenceValue = roomName;
            hudSo.FindProperty("m_CountLabel").objectReferenceValue = count;
            hudSo.FindProperty("m_StartButton").objectReferenceValue = start.gameObject;
            hudSo.FindProperty("m_StartHint").objectReferenceValue = hint;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            var mapView = canvasGo.AddComponent<RoomMapView>();
            var mapSo = new SerializedObject(mapView);
            mapSo.FindProperty("m_Session").objectReferenceValue = _session;
            mapSo.FindProperty("m_Catalog").objectReferenceValue = _catalog;
            SerializedProperty mapProp = mapSo.FindProperty("m_Buttons");
            mapProp.arraySize = mapButtons.Length;
            for (int i = 0; i < mapButtons.Length; i++)
            {
                mapProp.GetArrayElementAtIndex(i).objectReferenceValue = mapButtons[i];
            }
            mapSo.FindProperty("m_BriefLabel").objectReferenceValue = mapBrief;
            mapSo.ApplyModifiedPropertiesWithoutUndo();

            var controller = canvasGo.AddComponent<RoomController>();
            var ctrlSo = new SerializedObject(controller);
            ctrlSo.FindProperty("m_Session").objectReferenceValue = _session;
            ctrlSo.FindProperty("m_Director").objectReferenceValue = _director;
            ctrlSo.FindProperty("m_StartButton").objectReferenceValue = start;
            ctrlSo.FindProperty("m_LeaveButton").objectReferenceValue = leave;
            SerializedProperty ctrlMaps = ctrlSo.FindProperty("m_MapButtons");
            ctrlMaps.arraySize = mapButtons.Length;
            for (int i = 0; i < mapButtons.Length; i++)
            {
                ctrlMaps.GetArrayElementAtIndex(i).objectReferenceValue = mapButtons[i];
            }
            ctrlSo.ApplyModifiedPropertiesWithoutUndo();

            var stage = canvasGo.AddComponent<RoomDrawStage>();
            var stageSo = new SerializedObject(stage);
            stageSo.FindProperty("m_Session").objectReferenceValue = _session;
            stageSo.FindProperty("m_HudRoot").objectReferenceValue = hudGo;
            stageSo.FindProperty("m_FillLight").objectReferenceValue = _fillLight;
            stageSo.FindProperty("m_Spotlight").objectReferenceValue = _spotlight;
            stageSo.FindProperty("m_StandRadius").floatValue = ROOM_STAND_RADIUS;
            stageSo.FindProperty("m_CardRoot").objectReferenceValue = cardRoot;
            stageSo.FindProperty("m_Card").objectReferenceValue = card;
            stageSo.FindProperty("m_CardTitle").objectReferenceValue = cardTitle;
            stageSo.FindProperty("m_CardName").objectReferenceValue = cardName;
            stageSo.FindProperty("m_FadeGroup").objectReferenceValue = fadeGroup;
            stageSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 채팅창을 참가자 보드 **아래로** 내린다.
        ///
        /// 인게임 좌하단(24, 300)이 기본값인데 거기엔 보드가 없다. 대기실에는 정원 10칸이
        /// 그 자리를 지나가서 입력창이 보드 위에 겹쳐 뜬다(2026-08-30 실측).
        /// 자리만 다르고 컴포넌트는 인게임과 같은 것을 그대로 쓴다.
        /// </summary>
        private static void MoveChatBelowBoard(GameObject _canvasGo)
        {
            Transform chatRoot = _canvasGo.transform.Find("ChatRoot");
            if (chatRoot == null)
            {
                Debug.LogWarning("[RoomSceneBuilder] ChatRoot를 못 찾았다 — 채팅 자리가 보드와 겹친다");
                return;
            }

            ((RectTransform)chatRoot).anchoredPosition = new Vector2(BOARD_LEFT, 40f);
        }

        /// <summary>
        /// 참가자 한 칸. 이름 필드는 **`m_MemberName`**이다 — `m_Name`으로 부르면
        /// `FindProperty`가 오브젝트 이름을 집어 배선이 조용히 빈다(2026-08-26 실측).
        /// </summary>
        private static RoomMemberRow RoomRow(Transform _root, string _name, float _y)
        {
            RectTransform rect = RoomPanel(_root, _name, new Color(0.043f, 0.059f, 0.086f, 0.45f),
                new Vector2(0f, 1f), new Vector2(BOARD_LEFT + BOARD_W * 0.5f, _y),
                new Vector2(BOARD_W, BOARD_ROW_H));

            TMP_Text memberName = RoomText(rect, "Name", "빈 자리", 22, FontStyles.Normal,
                new Vector2(0f, 0.5f), new Vector2(24f + 150f, 0f), new Vector2(300f, 32f));
            memberName.alignment = TextAlignmentOptions.Left;

            TMP_Text hostBadge = RoomText(rect, "HostBadge", "방장", 16, FontStyles.Bold,
                new Vector2(1f, 0.5f), new Vector2(-52f, 0f), new Vector2(80f, 28f));
            hostBadge.color = ROOM_ACCENT;
            hostBadge.gameObject.SetActive(false);

            var item = rect.gameObject.AddComponent<RoomMemberRow>();
            var so = new SerializedObject(item);
            so.FindProperty("m_Background").objectReferenceValue = rect.GetComponent<Image>();
            so.FindProperty("m_MemberName").objectReferenceValue = memberName;
            so.FindProperty("m_HostBadge").objectReferenceValue = hostBadge;
            so.ApplyModifiedPropertiesWithoutUndo();

            return item;
        }

        private static Button RoomButton(Transform _root, string _name, string _label,
            Vector2 _anchor, Vector2 _pos, Vector2 _size, bool _primary)
        {
            RectTransform rect = RoomPanel(_root, _name,
                _primary ? ROOM_ACCENT : new Color(0.055f, 0.075f, 0.110f, 0.82f), _anchor, _pos, _size);

            if (!_primary)
            {
                var outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.126f, 0.169f, 0.239f, 1f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            var button = rect.gameObject.AddComponent<Button>();

            TMP_Text label = RoomText(rect, "Label", _label, _primary ? 28 : 20,
                _primary ? FontStyles.Bold : FontStyles.Normal,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(_size.x - 20f, _size.y - 12f));
            label.color = _primary ? new Color(0.133f, 0.102f, 0.016f) : ROOM_TEXT_ON;

            return button;
        }

        private static RectTransform RoomPanel(Transform _parent, string _name, Color _color,
            Vector2 _anchor, Vector2 _pos, Vector2 _size)
        {
            var go = new GameObject(_name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = _anchor;
            rect.anchorMax = _anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = _pos;
            rect.sizeDelta = _size;

            go.GetComponent<Image>().color = _color;
            return rect;
        }

        private static TMP_Text RoomText(Transform _parent, string _name, string _text, float _size,
            FontStyles _style, Vector2 _anchor, Vector2 _pos, Vector2 _rect)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.transform.SetParent(_parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = _anchor;
            rect.anchorMax = _anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = _pos;
            rect.sizeDelta = _rect;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = _text;
            text.fontSize = _size;
            text.fontStyle = _style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = ROOM_TEXT_ON;
            text.raycastTarget = false;
            return text;
        }
    }
}
