using System.Linq;
using CTC.Runtime.Cameras;
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Managers;
using CTC.Runtime.Networking;
using CTC.Runtime.Player;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CTC.Editor
{
    /// <summary>
    /// Plan 3: PlayerCat/PlayerMouse 프리팹(Quirky 모델 + URP 머티리얼)과
    /// 로비·카메라가 배선된 Game 씬을 생성한다. 재실행 시 덮어씀.
    /// Tools > CTC > Setup Networking Scene
    /// </summary>
    public static class SetupNetworkingScene
    {
        private const string PrefabDir = "Assets/Prefabs";
        private const string MaterialsDir = "Assets/Materials";
        private const string CatPrefabPath = PrefabDir + "/PlayerCat.prefab";
        private const string MousePrefabPath = PrefabDir + "/PlayerMouse.prefab";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        private const string QuirkyRoot = "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1";
        private const string CatModelPath = QuirkyRoot + "/Prefabs/Cat.prefab";
        private const string MouseModelPath = QuirkyRoot + "/Prefabs/Mouse.prefab";
        private const string CatSourceMatPath = QuirkyRoot + "/Materials/M_Cat.mat";
        private const string MouseSourceMatPath = QuirkyRoot + "/Materials/M_Mouse.mat";
        private const string UrpShaderPath =
            "Assets/Quirky Series/_Shader/URP (Unity 2019.3.0f3 and above)/SoftSurfaceGraph.shadergraph";

        private const string GridboxUrpDir =
            "Assets/Thirdparty/Ciathyza/Gridbox Prototype Materials/Materials/URP";
        private const string MuipButtonPrefabPath = "Assets/Modern UI Pack/Prefabs/Button/Basic/White.prefab";
        private const string DefaultNetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

        [MenuItem("Tools/CTC/Setup Networking Scene")]
        public static void Run()
        {
            Material catMat = CreateUrpMaterial("M_Cat_URP", CatSourceMatPath);
            Material mouseMat = CreateUrpMaterial("M_Mouse_URP", MouseSourceMatPath);

            GameObject catPrefab = CreatePlayerPrefab(
                "PlayerCat", CatPrefabPath, CatModelPath, catMat,
                ccHeight: 0.9f, ccRadius: 0.35f,
                moveSpeed: 4.5f, runThreshold: 3.2f, fov: 70f, camDistance: 5f,
                role: RoleId.Cat, maxHp: 1000);

            GameObject mousePrefab = CreatePlayerPrefab(
                "PlayerMouse", MousePrefabPath, MouseModelPath, mouseMat,
                ccHeight: 0.5f, ccRadius: 0.22f,
                moveSpeed: 6f, runThreshold: 4.2f, fov: 55f, camDistance: 3f,
                role: RoleId.Mouse, maxHp: 100);

            CreateGameScene(catPrefab, mousePrefab);
            Debug.Log("[SetupNetworkingScene] 완료 — PlayerCat/PlayerMouse 프리팹 + Game.unity 재생성");
        }

        // Quirky 원본 머티리얼의 색/텍스처를 URP SoftSurfaceGraph 머티리얼로 복사한다.
        // (원본은 빌트인 CGPROGRAM 셰이더라 URP에서 마젠타)
        private static Material CreateUrpMaterial(string name, string sourceMatPath)
        {
            if (!AssetDatabase.IsValidFolder(MaterialsDir))
                AssetDatabase.CreateFolder("Assets", "Materials");

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(UrpShaderPath);
            var source = AssetDatabase.LoadAssetAtPath<Material>(sourceMatPath);

            var mat = new Material(shader) { name = name };
            if (source != null)
            {
                if (source.HasProperty("_Color")) mat.SetColor("_Color", source.GetColor("_Color"));
                if (source.HasProperty("_MainTex")) mat.SetTexture("_MainTex", source.GetTexture("_MainTex"));
                if (source.HasProperty("_Emission")) mat.SetFloat("_Emission", source.GetFloat("_Emission"));
            }

            string path = MaterialsDir + "/" + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject CreatePlayerPrefab(
            string name, string prefabPath, string modelPath, Material material,
            float ccHeight, float ccRadius,
            float moveSpeed, float runThreshold, float fov, float camDistance,
            RoleId role, int maxHp)
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            var root = new GameObject(name);
            try
            {
                var cc = root.AddComponent<CharacterController>();
                cc.height = ccHeight;
                cc.radius = ccRadius;
                cc.center = new Vector3(0f, ccHeight * 0.5f + 0.05f, 0f);

                root.AddComponent<NetworkObject>();
                root.AddComponent<ClientNetworkTransform>();

                var move = root.AddComponent<PlayerMovement>();
                SetFloat(move, "moveSpeed", moveSpeed);

                var anim = root.AddComponent<PlayerAnimationDriver>();
                SetFloat(anim, "runThreshold", runThreshold);

                var binder = root.AddComponent<PlayerCameraBinder>();
                SetFloat(binder, "cameraFov", fov);
                SetFloat(binder, "cameraDistance", camDistance);

                root.AddComponent<CheeseWallet>();

                var health = root.AddComponent<Health>();
                SetInt(health, "m_MaxHp", maxHp);

                var roleTag = root.AddComponent<RoleTag>();
                SetInt(roleTag, "m_Role", (int)role); // enum은 intValue로

                root.AddComponent<PlayerDeath>();

                if (role == RoleId.Cat) root.AddComponent<CatPounce>();
                else root.AddComponent<MousePopgun>();

                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;

                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateGameScene(GameObject catPrefab, GameObject mousePrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(3f, 1f, 3f);
            floor.GetComponent<Renderer>().sharedMaterial = GridboxMaterial("Grey1");

            BuildGreyboxMap();
            BuildCheeseSpots();

            // NetworkManager — 자동 플레이어 스폰 없음(로비가 역할별로 스폰)
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7788);

            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = null;
            RegisterNetworkPrefabs(nm, catPrefab, mousePrefab);

            nmGo.AddComponent<ConnectionUI>();
            nmGo.AddComponent<MppmAutoConnect>();
            nmGo.AddComponent<MultiplayerQaProbe>();

            // 로비 (in-scene NetworkObject — 서버 시작 시 자동 스폰)
            var lobbyGo = new GameObject("LobbyManager");
            lobbyGo.AddComponent<NetworkObject>();
            var lobby = lobbyGo.AddComponent<LobbyManager>();
            SetRef(lobby, "catPrefab", catPrefab);
            SetRef(lobby, "mousePrefab", mousePrefab);
            BuildLobbyCanvas(lobby);

            // 채팅 (in-scene NetworkObject — LobbyManager와 동일 패턴, [Rpc]는 스폰된 NetworkBehaviour 필요)
            var chatGo = new GameObject("ChatManager");
            chatGo.AddComponent<NetworkObject>();
            chatGo.AddComponent<ChatManager>();

            // 카메라 — Main Camera에 Brain, 별도 오브젝트에 궤도 vcam
            var mainCam = Camera.main;
            if (mainCam != null) mainCam.gameObject.AddComponent<CinemachineBrain>();

            var camGo = new GameObject("OrbitCamera");
            camGo.AddComponent<CinemachineCamera>();
            var orbital = camGo.AddComponent<CinemachineOrbitalFollow>();
            orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbital.Radius = 4f;
            orbital.TargetOffset = new Vector3(0f, 0.6f, 0f);
            orbital.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbital.HorizontalAxis.Wrap = true;
            orbital.VerticalAxis.Range = new Vector2(-30f, 70f);
            orbital.VerticalAxis.Value = 20f;
            camGo.AddComponent<CinemachineRotationComposer>();
            camGo.AddComponent<OrbitCameraController>();
            var spectator = camGo.AddComponent<SpectatorFallback>();
            SetRef(spectator, "m_Lobby", lobby);

            EditorSceneManager.SaveScene(scene, GameScenePath);

            if (!EditorBuildSettings.scenes.Any(s => s.path == GameScenePath))
            {
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Append(new EditorBuildSettingsScene(GameScenePath, true))
                    .ToArray();
            }
        }

        // NGO의 NetworkConfig.Prefabs.Add()는 [NonSerialized] 런타임 리스트(m_Prefabs /
        // m_RuntimeAddedPrefabs)에만 기록되어 씬에 직렬화되지 않는다. 직렬화되는 경로는
        // NetworkPrefabs.NetworkPrefabsLists([SerializeField]) → NetworkPrefabsList 에셋뿐이므로
        // DefaultNetworkPrefabs.asset에 프리팹을 등록하고 그 리스트를 명시적으로 연결한다.
        private static void RegisterNetworkPrefabs(NetworkManager nm, params GameObject[] prefabs)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultNetworkPrefabsPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                var so = new SerializedObject(list);
                SerializedProperty isDefault = so.FindProperty("IsDefault");
                if (isDefault != null) isDefault.boolValue = true; // NGO 자동생성 에셋과 동일 취급
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(list, DefaultNetworkPrefabsPath);
            }

            bool dirty = false;
            foreach (GameObject prefab in prefabs)
            {
                if (list.Contains(prefab)) continue;
                list.Add(new NetworkPrefab { Prefab = prefab });
                dirty = true;
            }
            if (dirty)
            {
                EditorUtility.SetDirty(list);
                AssetDatabase.SaveAssets();
            }

            if (!nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(list))
                nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
        }

        // 그레이박스 맵 — Plan 9 아트 패스 전 블록아웃. 프리미티브 큐브 + Gridbox 프로토타입 머티리얼.
        // 바닥 Plane(scale 3 = 30x30)에 맞춰 외벽은 ±15.
        // 큐브 스케일에 따른 텍스처 타일링 왜곡은 프로토타입 단계라 허용.
        private static void BuildGreyboxMap()
        {
            Material wall = GridboxMaterial("Grey2");
            Material counter = GridboxMaterial("Grey4");
            var root = new GameObject("GreyboxMap");

            AddBox(root, wall, "Wall_N", new Vector3(0f, 1.5f, 15f), new Vector3(30f, 3f, 0.5f));
            AddBox(root, wall, "Wall_S", new Vector3(0f, 1.5f, -15f), new Vector3(30f, 3f, 0.5f));
            AddBox(root, wall, "Wall_E", new Vector3(15f, 1.5f, 0f), new Vector3(0.5f, 3f, 30f));
            AddBox(root, wall, "Wall_W", new Vector3(-15f, 1.5f, 0f), new Vector3(0.5f, 3f, 30f));

            // 부엌 블록아웃: ㄱ자 카운터 + 중앙 아일랜드 + 가전/가구 블록
            AddBox(root, counter, "Counter_N", new Vector3(-5f, 0.6f, 13.5f), new Vector3(14f, 1.2f, 2f));
            AddBox(root, counter, "Counter_W", new Vector3(-13.5f, 0.6f, 6f), new Vector3(2f, 1.2f, 13f));
            AddBox(root, counter, "Island", new Vector3(0f, 0.6f, 3f), new Vector3(6f, 1.2f, 3f));
            AddBox(root, GridboxMaterial("White"), "Fridge", new Vector3(13f, 1.25f, 12f), new Vector3(2f, 2.5f, 2f));
            AddBox(root, GridboxMaterial("Brown"), "Table", new Vector3(8f, 0.5f, -6f), new Vector3(4f, 1f, 4f));
            Material crate = GridboxMaterial("Orange");
            AddBox(root, crate, "Crate_A", new Vector3(-8f, 0.5f, -9f), new Vector3(2f, 1f, 2f));
            AddBox(root, crate, "Crate_B", new Vector3(-5.5f, 0.4f, -10.5f), new Vector3(1.2f, 0.8f, 1.2f));
        }

        // 치즈 지점(원작 돈줄) — 구석=저등급, 개활지=고등급. G1 (3,1)은 QA용
        // (클론 넛지 TestMpNudgeLocalPlayer가 스폰(1,0,0)에서 도달하는 지점).
        private static readonly (Vector3 pos, int grade)[] CHEESE_SPOT_DEFS =
        {
            (new Vector3(-11f, 0f, -11f), 1),
            (new Vector3(3f, 0f, 1f), 1),
            (new Vector3(11f, 0f, -12f), 2),
            (new Vector3(-9f, 0f, 9f), 2),
            (new Vector3(11.5f, 0f, 8f), 3),
            (new Vector3(0f, 0f, -6f), 3),
        };

        private static void BuildCheeseSpots()
        {
            var yellow = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Thirdparty/Ciathyza/Gridbox Prototype Materials/Materials/URP/Prototype_512x512_Yellow.mat");
            var root = new GameObject("CheeseSpots");

            foreach ((Vector3 pos, int grade) in CHEESE_SPOT_DEFS)
            {
                var spotGo = new GameObject($"CheeseSpot_G{grade}");
                spotGo.transform.SetParent(root.transform, false);
                spotGo.transform.localPosition = pos;

                var spot = spotGo.AddComponent<CheeseSpot>();
                SetInt(spot, "m_Grade", grade);
                SetFloat(spot, "m_Radius", grade == 1 && pos.x == 3f ? 2f : 1.5f); // QA 지점만 넉넉히

                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Disc";
                Object.DestroyImmediate(disc.GetComponent<CapsuleCollider>()); // 이동 방해·오버랩 오염 방지
                disc.transform.SetParent(spotGo.transform, false);
                disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                disc.transform.localScale = new Vector3(3f, 0.05f, 3f);
                if (yellow != null) disc.GetComponent<Renderer>().sharedMaterial = yellow;

                var labelGo = new GameObject("GradeLabel");
                labelGo.transform.SetParent(spotGo.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                var tmp = labelGo.AddComponent<TextMeshPro>();
                tmp.text = grade.ToString();
                tmp.fontSize = 6f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.yellow;
            }
        }

        private static Material GridboxMaterial(string name)
        {
            string path = $"{GridboxUrpDir}/Prototype_512x512_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) Debug.LogError($"[SetupNetworkingScene] Gridbox 머티리얼 없음: {path}");
            return mat;
        }

        private static void AddBox(GameObject parent, Material material, string name, Vector3 center, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent.transform, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }

        // 로비 UGUI — Canvas + EventSystem(InputSystemUIInputModule) + 패널 + MUIP 버튼 3개.
        // MUIP는 asmdef 없이 Assembly-CSharp 소속이라 타입 직접 참조 불가 →
        // GetComponent("ButtonManager") + SerializedObject로 배선하고, LobbyUI는 표준 Button 경유.
        private static void BuildLobbyCanvas(LobbyManager lobby)
        {
            var canvasGo = new GameObject("LobbyCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>(); // 레거시 StandaloneInputModule 금지

            // 중앙 패널 (반투명 다크)
            var panel = new GameObject("LobbyPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(480f, 520f);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.12f, 0.85f);

            TMP_Text title = CreateTmpText("Title", panel.transform, "Cheese Thief Lobby",
                32f, TextAlignmentOptions.Center, new Vector2(0f, -20f), new Vector2(440f, 50f));
            title.fontStyle = FontStyles.Bold;

            TMP_Text playerList = CreateTmpText("PlayerList", panel.transform, string.Empty,
                22f, TextAlignmentOptions.TopLeft, new Vector2(0f, -90f), new Vector2(420f, 180f));

            Button mouseButton = CreateMuipButton(panel.transform, "MouseButton", "Play as Mouse", new Vector2(0f, -300f));
            Button catButton = CreateMuipButton(panel.transform, "CatButton", "Play as Cat", new Vector2(0f, -370f));
            Button startButton = CreateMuipButton(panel.transform, "StartButton", "Start Game", new Vector2(0f, -450f));

            var ui = canvasGo.AddComponent<LobbyUI>();
            SetRef(ui, "lobby", lobby);
            SetRef(ui, "panel", panel);
            SetRef(ui, "playerListText", playerList);
            SetRef(ui, "mouseButton", mouseButton);
            SetRef(ui, "catButton", catButton);
            SetRef(ui, "startButton", startButton);

            // 치즈 잔액 HUD (우상단, 게임 시작 후 WalletHud가 표시 제어)
            var hudTextGo = new GameObject("WalletText");
            hudTextGo.transform.SetParent(canvasGo.transform, false);
            var hudText = hudTextGo.AddComponent<TextMeshProUGUI>();
            hudText.text = "Cheese: 0";
            hudText.fontSize = 32f;
            hudText.alignment = TextAlignmentOptions.TopRight;
            var hudRect = hudTextGo.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(1f, 1f);
            hudRect.anchorMax = new Vector2(1f, 1f);
            hudRect.pivot = new Vector2(1f, 1f);
            hudRect.anchoredPosition = new Vector2(-24f, -24f);
            hudRect.sizeDelta = new Vector2(320f, 48f);
            hudTextGo.SetActive(false); // WalletHud가 게임 시작 시 켬

            var walletHud = canvasGo.AddComponent<WalletHud>();
            SetRef(walletHud, "m_Lobby", lobby);
            SetRef(walletHud, "m_Text", hudText);

            // 전투 HUD (좌상단 아래쪽, CombatHud가 게임 시작 시 켬)
            var combatTextGo = new GameObject("CombatText");
            combatTextGo.transform.SetParent(canvasGo.transform, false);
            var combatText = combatTextGo.AddComponent<TextMeshProUGUI>();
            combatText.text = "HP -   CAT -";
            combatText.fontSize = 32f;
            combatText.alignment = TextAlignmentOptions.TopLeft;
            var combatRect = combatTextGo.GetComponent<RectTransform>();
            combatRect.anchorMin = new Vector2(0f, 1f);
            combatRect.anchorMax = new Vector2(0f, 1f);
            combatRect.pivot = new Vector2(0f, 1f);
            combatRect.anchoredPosition = new Vector2(24f, -110f); // ConnectionUI(IMGUI) 아래
            combatRect.sizeDelta = new Vector2(480f, 48f);
            combatTextGo.SetActive(false);

            var combatHud = canvasGo.AddComponent<CombatHud>();
            SetRef(combatHud, "m_Lobby", lobby);
            SetRef(combatHud, "m_Text", combatText);
        }

        private static TMP_Text CreateTmpText(
            string name, Transform parent, string text,
            float fontSize, TextAlignmentOptions alignment, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        // MUIP 버튼 프리팹 인스턴스 + 라벨 설정. 프리팹 루트에 표준 Button이 없어서 추가한다
        // (ButtonManager.Initialize가 런타임에 기존 Button을 재사용하므로 중복 생성 없음).
        private static Button CreateMuipButton(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MuipButtonPrefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(380f, 56f);

            var buttonManager = go.GetComponent("ButtonManager");
            if (buttonManager != null)
            {
                var so = new SerializedObject(buttonManager);
                so.FindProperty("buttonText").stringValue = label;
                SerializedProperty autoFit = so.FindProperty("autoFitContent");
                if (autoFit != null) autoFit.boolValue = false; // 명시 크기 사용
                SerializedProperty icon = so.FindProperty("enableIcon");
                if (icon != null) icon.boolValue = false; // 텍스트 전용 버튼
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // enableIcon=false는 런타임 UpdateUI에서 반영되지만 에디터 뷰에도 즉시 적용
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == "Icon Parent") t.gameObject.SetActive(false);

            // ButtonManager.UpdateUI는 런타임 OnEnable에서 라벨을 재적용하지만,
            // 에디터/씬 뷰에서도 바로 보이도록 자식 TMP 텍스트를 직접 갱신
            foreach (var tmp in go.GetComponentsInChildren<TMP_Text>(true))
                tmp.text = label;

            var button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None; // 시각 전환은 ButtonManager 담당
            return button;
        }

        // 직렬화 private 필드 세팅 유틸 (프리팹 저장 전 인스턴스에 적용)
        private static void SetFloat(Component c, string field, float value)
        {
            var so = new SerializedObject(c);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Component c, string field, int value)
        {
            var so = new SerializedObject(c);
            so.FindProperty(field).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(Component c, string field, Object value)
        {
            var so = new SerializedObject(c);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
