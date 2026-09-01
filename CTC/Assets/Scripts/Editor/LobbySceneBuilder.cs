using CTC.Runtime.UI;
using System.Collections.Generic;
using CTC.Runtime.Lobby.Browser;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CTC.Editor
{
    /// <summary>
    /// 로비(방 목록) 씬을 굽는다 — Tools > CTC > Setup Lobby Scene.
    ///
    /// 3D가 없다. 인트로의 어두운 톤만 이어받은 평면 화면이라 카메라와 캔버스뿐이다.
    /// 배치는 시안대로 **좌: 목록 / 우: 고른 방 상세 / 하단: 방 만들기**.
    ///
    /// 다시 실행하면 씬을 새로 굽는다(멱등).
    /// </summary>
    public static class LobbySceneBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/Lobby.unity";
        private const string BACKDROP_PATH = "Assets/Materials/UI/T_LobbyBackdrop.png";

        /// <summary>`Setup Room Scene`이 굽는다 — 로비부터 들고 다니는 NetworkManager 한 벌.</summary>
        private const string NETWORK_ROOT_PATH = "Assets/Prefabs/NetworkRoot.prefab";

        // 값의 출처는 CTC.Runtime.UI.UiPalette 하나다 — 화면마다 따로 적으면 서서히 갈라진다.
        private static readonly Color ACCENT = UiPalette.Accent;
        // 배경 그림의 치즈·자루가 몰린 자리에 앉아 반투명이면 글자가 시끄럽다 — 거의 불투명하게 덮는다.
        private static readonly Color PANEL = UiPalette.Panel;
        private static readonly Color LINE = UiPalette.Line;
        private static readonly Color TEXT_DIM = UiPalette.TextDim;
        private static readonly Color TEXT_ON = UiPalette.TextOn;

        // 배경 그림의 주인공(램프·치즈 더미)이 화면 **오른쪽 위**에 있다. UI를 상단에만 몰면
        // 상세 패널이 램프를 가리고, 목록 아래로 그림이 반쯤 드러나 경계가 어정쩡해진다.
        // 그래서 목록을 좌측 **세로로 길게** 깔아 그 절반을 채우고, 상세는 램프를 피해 **아래**에 둔다.
        private const int ROW_SLOTS = 9;
        private const float ROW_H = 76f;
        private const float ROW_GAP = 8f;
        private const float LIST_LEFT = 96f;
        private const float LIST_W = 1000f;
        private const float LIST_TOP = 196f;      // 화면 위에서
        private const float DETAIL_LEFT = 1180f;
        private const float DETAIL_W = 640f;
        private const float DETAIL_TOP = 520f;    // 램프 아래로 내린다

        [MenuItem("Tools/CTC/Setup Lobby Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildNetworkRoot();

            var browserGo = new GameObject("LobbyBrowser");
            LobbyBrowser browser = browserGo.AddComponent<LobbyBrowser>();

            BuildCanvas(browser);

            // 배경음(2026-09-01) — 앞단 세 화면이 무음이었다. 인게임과 같은 비율(0.35)로 깐다.
            SceneBgmBuilder.Attach(SceneBgmBuilder.LOBBY_BGM);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[LobbySceneBuilder] 완료 → {SCENE_PATH}");
        }

        /// <summary>인트로 바로 뒤에 놓는다 — 순서가 곧 흐름이다.</summary>
        private static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == SCENE_PATH) return;
            }

            int insertAt = scenes.Count > 0 ? 1 : 0;
            scenes.Insert(insertAt, new EditorBuildSettingsScene(SCENE_PATH, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[LobbySceneBuilder] 빌드 설정에 로비를 넣었다");
        }

        /// <summary>
        /// 접속은 **로비에서 시작된다**(방 만들기·참여). 그런데 NetworkManager는 인게임 씬에만
        /// 있었다 — 그래서 로비의 버튼이 스팀을 못 찾아 아무 일도 안 했다(2026-08-30).
        /// 여기서 한 번 세우면 NGO가 `DontDestroyOnLoad`를 걸어 대기실·인게임까지 그대로 탄다.
        /// </summary>
        private static void BuildNetworkRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NETWORK_ROOT_PATH);
            if (prefab == null)
            {
                Debug.LogWarning($"[LobbySceneBuilder] {NETWORK_ROOT_PATH}가 없다 — 'Setup Room Scene'을 먼저 돌려라");
                return;
            }

            var go = new GameObject("NetworkRoot");
            var root = go.AddComponent<CTC.Runtime.Networking.NetworkRoot>();
            var so = new SerializedObject(root);
            so.FindProperty("m_NetworkPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 대기실로 넘기는 문. 방장이 서면 NGO로 씬을 연다 — 참여자는 따라온다.
            new GameObject("RoomEntry").AddComponent<RoomEntry>();
        }

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.031f, 0.043f, 0.063f);
            go.AddComponent<AudioListener>();
        }

        private static void BuildCanvas(LobbyBrowser _browser)
        {
            var canvasGo = new GameObject("LobbyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            // 팝업 매니저에 이 씬 캔버스를 물려준다(2026-09-01) — 로비에서도 옵션창이 뜬다.
            PopupSceneBinder.Attach(canvas);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            RectTransform root = canvasGo.GetComponent<RectTransform>();

            BuildBackdrop(root);

            // ── 머리 ──
            TMP_Text title = Text(root, "Title", "방 목록", 40, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(LIST_LEFT + 120f, -104f), new Vector2(260f, 56f));
            title.alignment = TextAlignmentOptions.Left;
            title.color = TEXT_ON;

            TMP_Text count = Text(root, "Count", "0개", 22, FontStyles.Normal,
                new Vector2(0f, 1f), new Vector2(LIST_LEFT + 330f, -108f), new Vector2(220f, 40f));
            count.alignment = TextAlignmentOptions.Left;
            count.color = TEXT_DIM;

            Button refresh = TextButton(root, "RefreshButton", "새로고침",
                new Vector2(0f, 1f), new Vector2(LIST_LEFT + LIST_W - 90f, -110f), new Vector2(160f, 52f), false);

            Button back = TextButton(root, "BackButton", "뒤로",
                new Vector2(1f, 1f), new Vector2(-120f, -110f), new Vector2(130f, 52f), false);

            // ── 목록 머리글 ──
            Header(root, "방 이름", LIST_LEFT + 24f, 300f);
            Header(root, "인원", LIST_LEFT + LIST_W - 300f, 120f);
            Header(root, "상태", LIST_LEFT + LIST_W - 130f, 120f);

            // ── 줄들 ──
            var rows = new LobbyRowItem[ROW_SLOTS];
            for (int i = 0; i < ROW_SLOTS; i++)
            {
                float y = -(LIST_TOP + i * (ROW_H + ROW_GAP));
                rows[i] = Row(root, $"Row{i + 1}", y);
            }

            TMP_Text empty = Text(root, "EmptyLabel", "열린 방이 없습니다.", 24, FontStyles.Normal,
                new Vector2(0f, 1f), new Vector2(LIST_LEFT + LIST_W * 0.5f, -(LIST_TOP + 120f)), new Vector2(LIST_W, 60f));
            empty.alignment = TextAlignmentOptions.Center;
            empty.color = TEXT_DIM;
            empty.gameObject.SetActive(false);

            // ── 상세 ──
            RectTransform detail = Panel(root, "DetailPanel", PANEL,
                new Vector2(0f, 1f), new Vector2(DETAIL_LEFT + DETAIL_W * 0.5f, -(DETAIL_TOP + 210f)),
                new Vector2(DETAIL_W, 420f));

            TMP_Text dName = Text(detail, "Name", "방 이름", 30, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(210f, -56f), new Vector2(400f, 48f));
            dName.alignment = TextAlignmentOptions.Left;
            dName.color = new Color(1f, 0.914f, 0.639f);

            DetailRow(detail, "방장", out TMP_Text dHost, -130f);
            DetailRow(detail, "인원", out TMP_Text dCount, -180f);

            TMP_Text note = Text(detail, "Note", "고양이 1명은 시작할 때 무작위로 정해집니다", 19, FontStyles.Normal,
                new Vector2(0f, 1f), new Vector2(DETAIL_W * 0.5f, -240f), new Vector2(DETAIL_W - 60f, 40f));
            note.alignment = TextAlignmentOptions.Left;
            note.color = TEXT_DIM;

            Button join = TextButton(detail, "JoinButton", "참여하기",
                new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(DETAIL_W - 64f, 74f), true);

            detail.gameObject.SetActive(false);

            // ── 하단 ──
            Button create = TextButton(root, "CreateButton", "방 만들기",
                new Vector2(0f, 0f), new Vector2(LIST_LEFT + 150f, 96f), new Vector2(300f, 78f), true);

            TMP_Text version = Text(root, "Version", "v0.9.2-dev", 18, FontStyles.Normal,
                new Vector2(1f, 0f), new Vector2(-160f, 60f), new Vector2(280f, 30f));
            version.alignment = TextAlignmentOptions.Right;
            version.color = TEXT_DIM;

            // ── 배선 ──
            var view = canvasGo.AddComponent<LobbyBrowserView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("m_Browser").objectReferenceValue = _browser;
            SerializedProperty rowsProp = viewSo.FindProperty("m_Rows");
            rowsProp.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++) rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            viewSo.FindProperty("m_CountLabel").objectReferenceValue = count;
            viewSo.FindProperty("m_EmptyLabel").objectReferenceValue = empty;
            viewSo.FindProperty("m_DetailPanel").objectReferenceValue = detail.gameObject;
            viewSo.FindProperty("m_DetailName").objectReferenceValue = dName;
            viewSo.FindProperty("m_DetailHost").objectReferenceValue = dHost;
            viewSo.FindProperty("m_DetailCount").objectReferenceValue = dCount;
            viewSo.FindProperty("m_JoinButton").objectReferenceValue = join.gameObject;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            var controller = canvasGo.AddComponent<LobbyBrowserController>();
            var ctrlSo = new SerializedObject(controller);
            ctrlSo.FindProperty("m_Browser").objectReferenceValue = _browser;
            ctrlSo.FindProperty("m_RefreshButton").objectReferenceValue = refresh;
            ctrlSo.FindProperty("m_JoinButton").objectReferenceValue = join;
            ctrlSo.FindProperty("m_CreateButton").objectReferenceValue = create;
            ctrlSo.FindProperty("m_BackButton").objectReferenceValue = back;
            ctrlSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 화면을 꽉 채우는 배경 그림. 위에 어두운 막을 한 겹 덮는다 —
        /// 원본이 이미 어둡지만 그림 위에 흰 글씨를 올리면 대비가 아슬아슬해서,
        /// 막 하나로 글자 가독성을 확정해 둔다(그림이 바뀌어도 UI가 안 깨진다).
        /// </summary>
        private static void BuildBackdrop(RectTransform _root)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BACKDROP_PATH);

            var go = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root, false);
            Stretch(go.GetComponent<RectTransform>());

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;   // 화면비가 달라도 빈 데가 생기면 안 된다
                image.color = Color.white;
            }
            else
            {
                Debug.LogWarning($"[LobbySceneBuilder] 배경 그림이 없다 — 단색으로 간다: {BACKDROP_PATH}");
                image.color = new Color(0.031f, 0.043f, 0.063f);
            }

            var veilGo = new GameObject("BackdropVeil", typeof(RectTransform), typeof(Image));
            veilGo.transform.SetParent(_root, false);
            Stretch(veilGo.GetComponent<RectTransform>());

            var veil = veilGo.GetComponent<Image>();
            veil.raycastTarget = false;
            veil.color = new Color(0.008f, 0.012f, 0.020f, 0.45f);
        }

        private static void Stretch(RectTransform _rect)
        {
            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.one;
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }

        private static void Header(Transform _root, string _text, float _x, float _w)
        {
            TMP_Text t = Text(_root, $"Head_{_text}", _text, 18, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(_x + _w * 0.5f, -(LIST_TOP - 46f)), new Vector2(_w, 30f));
            t.alignment = TextAlignmentOptions.Left;
            t.color = TEXT_DIM;
            t.characterSpacing = 6f;
        }

        private static void DetailRow(Transform _parent, string _label, out TMP_Text _value, float _y)
        {
            TMP_Text label = Text(_parent, $"Label_{_label}", _label, 20, FontStyles.Normal,
                new Vector2(0f, 1f), new Vector2(80f, _y), new Vector2(120f, 34f));
            label.alignment = TextAlignmentOptions.Left;
            label.color = TEXT_DIM;

            _value = Text(_parent, $"Value_{_label}", "-", 20, FontStyles.Normal,
                new Vector2(1f, 1f), new Vector2(-140f, _y), new Vector2(280f, 34f));
            _value.alignment = TextAlignmentOptions.Right;
            _value.color = TEXT_ON;
        }

        private static LobbyRowItem Row(Transform _root, string _name, float _y)
        {
            RectTransform rect = Panel(_root, _name, new Color(0.055f, 0.075f, 0.110f, 0.72f),
                new Vector2(0f, 1f), new Vector2(LIST_LEFT + LIST_W * 0.5f, _y), new Vector2(LIST_W, ROW_H));

            TMP_Text name = Text(rect, "Name", "방 이름", 24, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(24f + 260f, -22f), new Vector2(520f, 32f));
            name.alignment = TextAlignmentOptions.Left;

            TMP_Text host = Text(rect, "Host", "방장", 18, FontStyles.Normal,
                new Vector2(0f, 1f), new Vector2(24f + 260f, -50f), new Vector2(520f, 26f));
            host.alignment = TextAlignmentOptions.Left;
            host.color = TEXT_DIM;

            TMP_Text count = Text(rect, "Count", "0 / 10", 22, FontStyles.Bold,
                new Vector2(0f, 0.5f), new Vector2(LIST_W - 300f + 60f, 0f), new Vector2(120f, 32f));
            count.alignment = TextAlignmentOptions.Left;

            TMP_Text state = Text(rect, "State", "대기 중", 19, FontStyles.Bold,
                new Vector2(0f, 0.5f), new Vector2(LIST_W - 130f + 60f, 0f), new Vector2(120f, 32f));
            state.alignment = TextAlignmentOptions.Left;

            var item = rect.gameObject.AddComponent<LobbyRowItem>();
            var so = new SerializedObject(item);
            so.FindProperty("m_Background").objectReferenceValue = rect.GetComponent<Image>();
            so.FindProperty("m_RoomName").objectReferenceValue = name;
            so.FindProperty("m_Host").objectReferenceValue = host;
            so.FindProperty("m_Count").objectReferenceValue = count;
            so.FindProperty("m_State").objectReferenceValue = state;
            so.ApplyModifiedPropertiesWithoutUndo();

            rect.gameObject.SetActive(false);
            return item;
        }

        private static Button TextButton(Transform _root, string _name, string _label,
            Vector2 _anchor, Vector2 _pos, Vector2 _size, bool _primary)
        {
            RectTransform rect = Panel(_root, _name,
                _primary ? ACCENT : new Color(0.055f, 0.075f, 0.110f, 0.82f), _anchor, _pos, _size);

            if (!_primary)
            {
                var outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = LINE;
                outline.effectDistance = new Vector2(1f, -1f);
            }

            var button = rect.gameObject.AddComponent<Button>();

            TMP_Text label = Text(rect, "Label", _label, _primary ? 26 : 22,
                _primary ? FontStyles.Bold : FontStyles.Normal,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(_size.x - 20f, _size.y - 12f));
            label.alignment = TextAlignmentOptions.Center;
            label.color = _primary ? new Color(0.133f, 0.102f, 0.016f) : TEXT_ON;

            return button;
        }

        private static RectTransform Panel(Transform _parent, string _name, Color _color,
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

        private static TMP_Text Text(Transform _parent, string _name, string _text, float _size,
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
            text.color = TEXT_ON;
            text.raycastTarget = false;
            return text;
        }
    }
}
