using CTC.Runtime.UI;
using System.Linq;
using CTC.Runtime.Cameras;
using CTC.Runtime.Lobby;
using CTC.Runtime.Networking;
using CTC.Runtime.Player;
using CTC.Runtime.Room;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CTC.Editor
{
    /// <summary>
    /// 대기실 씬을 굽는다 — Tools > CTC > Setup Room Scene (2026-08-30).
    ///
    /// **먼저 `Setup Networking Scene`을 한 번 돌린 상태여야 한다** — 여기서는 게임 프리팹을
    /// 다시 굽지 않고 불러 쓴다. 다시 구우면 레이아웃 에셋(벽감·상자 자리)이 덮여
    /// 인게임 측정 기준이 같이 흔들린다(BotLab과 같은 이유).
    ///
    /// 짓는 것 셋:
    /// 1. `RoomMouse.prefab` — 대기실 전용 몸. **이동·시점·이름표뿐**이다(경제·전투 없음).
    /// 2. `NetworkRoot.prefab` — 로비부터 들고 다니는 NetworkManager 한 벌.
    /// 3. `Room.unity` — 3D 대기실 + 참가자 보드 + 시작.
    ///
    /// 다시 실행하면 셋 다 새로 굽는다(멱등).
    /// </summary>
    public static partial class SetupNetworkingScene
    {
        private const string RoomScenePath = "Assets/Scenes/Room.unity";
        private const string RoomAvatarPrefabPath = PrefabDir + "/RoomMouse.prefab";
        private const string NetworkRootPrefabPath = PrefabDir + "/NetworkRoot.prefab";
        private const string RoomMaterialDir = "Assets/Materials/Room";

        /// <summary>참가자들이 둘러서는 원의 반지름(m). 정원 10명이 3m씩 벌어진다.</summary>
        private const float ROOM_STAND_RADIUS = 5f;

        private static readonly Color ROOM_FLOOR = new Color(0.11f, 0.09f, 0.07f);
        private static readonly Color ROOM_LIGHT = new Color(1f, 0.91f, 0.72f);
        // 값의 출처는 CTC.Runtime.UI.UiPalette 하나다.
        private static readonly Color ROOM_ACCENT = UiPalette.Accent;
        private static readonly Color ROOM_PANEL_DEEP = UiPalette.PanelSolid;
        private static readonly Color ROOM_TEXT_ON = UiPalette.TextOn;
        private static readonly Color ROOM_TEXT_DIM = UiPalette.TextDim;

        // 보드 자리(1920×1080 기준) — 좌측 세로. 로비의 목록과 같은 왼쪽 여백을 쓴다.
        private const float BOARD_LEFT = 96f;
        private const float BOARD_W = 420f;
        private const float BOARD_TOP = 210f;
        private const float BOARD_ROW_H = 56f;
        private const float BOARD_ROW_GAP = 6f;

        [MenuItem("Tools/CTC/Setup Room Scene (대기실)")]
        public static void RunRoom()
        {
            CTC.Runtime.Info.InfoManager.Reload();

            GameObject avatar = CreateRoomAvatarPrefab();
            if (avatar == null) return;

            GameObject netRoot = CreateNetworkRootPrefab(avatar);
            if (netRoot == null) return;

            CreateRoomScene(netRoot, avatar);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RoomSceneBuilder] 완료 → {RoomScenePath}");
        }

        // ── 1. 대기실 몸 ──────────────────────────────────────────────

        /// <summary>
        /// 대기실 전용 쥐. 인게임의 `PlayerMouse`를 **그대로 쓰지 않는다** — 지갑·전투·상점·
        /// 발소리까지 딸려 오고, 그것들이 대기실에서 서버 상태를 건드리면 판이 열리기도 전에
        /// 경제가 돈다. 대기실 몸은 이동·시점·이름표뿐이다(기획 2026-08-26).
        /// </summary>
        private static GameObject CreateRoomAvatarPrefab()
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MouseModelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"[RoomSceneBuilder] 쥐 모델이 없다: {MouseModelPath}");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/M_Mouse_URP.mat");
            if (material == null) material = CreateUrpMaterial("M_Mouse_URP", MouseSourceMatPath);

            CTC.Runtime.Info.PlayerInfo info = CTC.Runtime.Info.InfoManager.Instance.PlayerOf(RoleId.Mouse);

            var root = new GameObject("RoomMouse");
            try
            {
                var cc = root.AddComponent<CharacterController>();
                cc.height = info.CcHeight;
                cc.radius = info.CcRadius;
                cc.center = new Vector3(0f, info.CcHeight * 0.5f + 0.05f, 0f);

                root.AddComponent<NetworkObject>();
                root.AddComponent<ClientNetworkTransform>();

                var move = root.AddComponent<PlayerMovement>();
                SetFloat(move, "m_MoveSpeed", info.MoveSpeed);
                SetFloat(move, "m_TurnSpeedDegPerSec", info.TurnSpeedDegPerSec);

                var anim = root.AddComponent<PlayerAnimationDriver>();
                SetFloat(anim, "m_RunThreshold", info.RunThreshold);

                var binder = root.AddComponent<PlayerCameraBinder>();
                SetFloat(binder, "m_CameraFov", info.Fov);
                SetFloat(binder, "m_CameraDistance", info.CamDistance);

                var placer = root.AddComponent<RoomSpawnPlacer>();
                SetFloat(placer, "m_Radius", ROOM_STAND_RADIUS);

                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;

                // Quirky 프리팹은 자기 셰이더 머티리얼을 들고 있어 URP에서 마젠타로 뜬다(실측)
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                }

                BuildNameTag(root, info.CcHeight);

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, RoomAvatarPrefabPath);

                // 저장 전 임시 씬 오브젝트에서 구운 해시는 무효 — 에셋에서 다시 구워야
                // 에셋 GUID 기반의 유일한 값이 나온다(PlayerPrefabFactory와 같은 이유).
                BakeGlobalObjectIdHash(asset.GetComponent<NetworkObject>());
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>머리 위 이름표. 월드 캔버스라 3D 공간에 떠 있고, 매 프레임 카메라를 본다.</summary>
        private static void BuildNameTag(GameObject _root, float _bodyHeight)
        {
            var tagGo = new GameObject("NameTag", typeof(RectTransform), typeof(Canvas));
            tagGo.transform.SetParent(_root.transform, false);
            // 쥐가 작다(CC 높이 1m 미만) — 배율을 몸 크기에 맞춘다. 0.006이면 이름표가
            // 몸보다 커져 화면을 덮었다(2026-08-30 실측).
            tagGo.transform.localPosition = new Vector3(0f, _bodyHeight + 0.30f, 0f);
            tagGo.transform.localScale = Vector3.one * 0.0016f;

            tagGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            tagGo.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 90f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(tagGo.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "";
            label.fontSize = 52f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = ROOM_TEXT_ON;
            label.raycastTarget = false;
            label.outlineWidth = 0.22f;
            label.outlineColor = new Color32(0, 0, 0, 255);

            var tag = _root.AddComponent<RoomNameTag>();
            SetRef(tag, "m_Label", label);
            SetRef(tag, "m_Pivot", tagGo.transform);
        }

        // ── 2. 들고 다니는 NetworkManager ─────────────────────────────

        /// <summary>
        /// 로비에서 세워 인게임까지 그대로 타는 NetworkManager 한 벌. NGO가 스스로
        /// `DontDestroyOnLoad`를 걸므로 **한 번만 서면 된다.**
        ///
        /// 프리팹 목록은 `Assets/DefaultNetworkPrefabs.asset` **하나를 공유한다** —
        /// 인게임 씬의 NetworkManager와 같은 에셋이라 목록이 갈라질 수가 없다
        /// (갈라지면 접속에서 NetworkConfig mismatch로 튕긴다).
        /// </summary>
        private static GameObject CreateNetworkRootPrefab(GameObject _roomAvatar)
        {
            var root = new GameObject("NetworkRoot");
            try
            {
                var nm = root.AddComponent<NetworkManager>();
                var transport = root.AddComponent<UnityTransport>();
                transport.SetConnectionData("127.0.0.1", 7788);

                nm.NetworkConfig.NetworkTransport = transport;
                nm.NetworkConfig.PlayerPrefab = null;   // 스폰은 대기실·인게임이 직접 한다
                // 인게임 씬의 NetworkManager와 **같은 값**이어야 한다 — 다르면 접속이 튕긴다.
                nm.NetworkConfig.ProtocolVersion = NetVersionLogic.Protocol(PlayerSettings.bundleVersion);

                GameObject[] known =
                {
                    _roomAvatar,
                    AssetDatabase.LoadAssetAtPath<GameObject>(CatPrefabPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(MousePrefabPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(FortressPrefabPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(WarehousePrefabPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(AlcovePrefabPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(CheeseDropPrefabPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(DogPrefabPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(LootBoxPrefabPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(HeroTicketPrefabPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(GangTicketPrefabPath),
                };

                GameObject[] present = known.Where(p => p != null).ToArray();
                if (present.Length < known.Length)
                {
                    Debug.LogWarning(
                        "[RoomSceneBuilder] 인게임 프리팹이 일부 없다 — 'Setup Networking Scene'을 먼저 돌려라");
                }

                RegisterNetworkPrefabs(nm, present);

                root.AddComponent<Netcode.Transports.Facepunch.FacepunchTransport>();
                root.AddComponent<SteamBootstrap>();
                root.AddComponent<SteamLobbyManager>();

                // MPPM 클론이 로비에서부터 붙게 한다 — 안 붙이면 클론이 대기실에 못 들어와
                // 2인 이상 흐름(추첨·시작)을 에디터에서 한 번도 못 본다. 메인 에디터·빌드에선
                // 아무 일도 안 한다(자기가 클론인지 경로로 판별한다).
                root.AddComponent<MppmAutoConnect>();

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, NetworkRootPrefabPath);
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ── 3. 대기실 씬 ──────────────────────────────────────────────

        private static void CreateRoomScene(GameObject _netRootPrefab, GameObject _avatarPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildRoomLighting(out Light spotlight, out Light fillLight);
            BuildRoomFloor();
            BuildRoomCamera();

            var netRootGo = new GameObject("NetworkRoot");
            var netRoot = netRootGo.AddComponent<NetworkRoot>();
            SetRef(netRoot, "m_NetworkPrefab", _netRootPrefab);

            // 대기실의 상태·몸·진행 — 인게임의 LobbyController와 같은 형태로 한 오브젝트에 동승한다.
            var directorGo = new GameObject("RoomDirector");
            directorGo.AddComponent<NetworkObject>();
            var session = directorGo.AddComponent<RoomSession>();          // 상태(Model)
            var spawner = directorGo.AddComponent<RoomAvatarSpawner>();    // 몸(Worker)
            SetRef(spawner, "m_AvatarPrefab", _avatarPrefab);
            SetFloat(spawner, "m_Radius", ROOM_STAND_RADIUS);
            var director = directorGo.AddComponent<RoomDirector>();        // 진행(Worker)

            // 고를 수 있는 맵 — **인게임과 같은 순서여야** 대기실에서 고른 맵이 그대로 선다.
            var catalog = directorGo.AddComponent<RoomMapCatalog>();
            var layouts = LoadMapLayouts();
            SetRefArray(catalog, "m_Layouts", layouts);
            // 새 방의 기본 맵 = 첫 정식(대형) 맵 — 배포에서 작은 맵이 기본으로 서는 것을 막는다(2026-08-31).
            var extents = new float[layouts.Length];
            var devOnly = new bool[layouts.Length];
            for (int i = 0; i < layouts.Length; i++)
            {
                extents[i] = layouts[i] is CTC.Runtime.Map.MapLayout ml ? ml.HalfExtent : 0f;
                devOnly[i] = layouts[i] is CTC.Runtime.Map.MapLayout md && md.DevOnly;
            }
            SetInt(session, "m_DefaultMapIndex", CTC.Runtime.Map.MapCatalogLogic.DefaultIndex(extents, devOnly));

            // 채팅 — 인게임과 똑같이 별도 in-scene NetworkObject다([Rpc]는 스폰된 NetworkBehaviour가 필요).
            var chatGo = new GameObject("ChatManager");
            chatGo.AddComponent<NetworkObject>();
            chatGo.AddComponent<CTC.Runtime.Managers.ChatManager>();

            BuildRoomCanvas(session, director, catalog, spotlight, fillLight);

            // 배경음(2026-09-01) — 앞단 세 화면이 무음이었다. 인게임과 같은 비율(0.35)로 깐다.
            SceneBgmBuilder.Attach(SceneBgmBuilder.LOBBY_BGM);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, RoomScenePath);
            RegisterRoomInBuildSettings();

            // in-scene NetworkObject 해시 베이크 — 첫 저장 전에는 GlobalObjectId가 무효라
            // 해시가 0으로 직렬화된다. 빌드 세팅 등록 후여야 in-scene으로 판정된다.
            NetworkObject[] sceneObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include);
            foreach (NetworkObject no in sceneObjects) BakeGlobalObjectIdHash(no);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, RoomScenePath);
        }

        /// <summary>로비 바로 뒤에 놓는다 — 순서가 곧 흐름이다(인트로 · 로비 · 대기실 · 인게임).</summary>
        private static void RegisterRoomInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == RoomScenePath)) return;

            int lobbyAt = scenes.FindIndex(s => s.path == "Assets/Scenes/Lobby.unity");
            int insertAt = lobbyAt >= 0 ? lobbyAt + 1 : scenes.Count;
            scenes.Insert(insertAt, new EditorBuildSettingsScene(RoomScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[RoomSceneBuilder] 빌드 설정에 대기실을 넣었다");
        }

        private static void BuildRoomLighting(out Light _spotlight, out Light _fillLight)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.030f, 0.036f, 0.048f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.010f, 0.014f, 0.020f);
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.skybox = null;

            // 스카이박스를 지워도 반사 프로브가 남으면 바닥이 하늘색을 물어 온다(인트로 실측).
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0f;

            var spotGo = new GameObject("Spotlight");
            spotGo.transform.position = new Vector3(0f, 9f, 0f);
            spotGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Light spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = ROOM_LIGHT;
            spot.intensity = 900f;
            spot.range = 22f;
            spot.spotAngle = 78f;
            spot.innerSpotAngle = 40f;
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.85f;

            // 위에서만 때리면 참가자 옆면이 캄캄하다(인트로 실측) — 아주 약한 채움광 하나.
            var fillGo = new GameObject("FillLight");
            fillGo.transform.position = new Vector3(0f, 2.4f, 0f);
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = ROOM_LIGHT;
            fill.intensity = 2.4f;
            fill.range = 14f;
            fill.shadows = LightShadows.None;

            // 추첨 연출이 이 둘을 직접 만진다 — 스포트라이트는 참가자 사이를 돌고, 채움광은 꺼진다.
            _spotlight = spot;
            _fillLight = fill;
        }

        private static void BuildRoomFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(4f, 1f, 4f);   // 40×40m
            floor.GetComponent<Renderer>().sharedMaterial =
                MakeRoomMaterial("M_RoomFloor", ROOM_FLOOR, 0.06f);
        }

        private static void BuildRoomCamera()
        {
            // 몸이 스폰되기 전까지는 궤도 카메라가 안 붙는다 — 그 몇 프레임 동안 카메라가
            // 원점(=바닥 높이)에 있으면 화면이 통째로 검다(실측). 처음부터 방을 내려다보게 둔다.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 7f, -11f);
            camGo.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.008f, 0.012f, 0.020f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();

            // 인게임과 같은 궤도 카메라를 쓴다 — 몸이 스폰되면 PlayerCameraBinder가 자기에게 가져간다.
            // **커서가 풀려 있으면 회전은 안 한다**(OrbitCameraController의 규칙). 대기실은 UI를
            // 눌러야 하는 화면이라 커서를 놓고, 그래서 시점은 고정각으로 따라만 다닌다.
            var orbitGo = new GameObject("OrbitCamera");
            orbitGo.AddComponent<CinemachineCamera>();
            var orbital = orbitGo.AddComponent<CinemachineOrbitalFollow>();
            orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;

            CTC.Runtime.Info.BalanceInfo balance = CTC.Runtime.Info.InfoManager.Instance.Balance;
            orbital.Radius = 4.5f;
            orbital.TargetOffset = new Vector3(0f, 0.7f, 0f);
            orbital.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbital.HorizontalAxis.Wrap = true;
            orbital.VerticalAxis.Range = new Vector2(balance.CamMinPitchDeg, balance.CamMaxPitchDeg);
            orbital.VerticalAxis.Value = balance.CamDefaultPitchDeg;

            var composer = orbitGo.AddComponent<CinemachineRotationComposer>();
            composer.Composition.ScreenPosition = new Vector2(0f, balance.CamScreenY);

            orbitGo.AddComponent<OrbitCameraController>();
        }

        private static Material MakeRoomMaterial(string _name, Color _color, float _smoothness)
        {
            if (!AssetDatabase.IsValidFolder(RoomMaterialDir))
                AssetDatabase.CreateFolder("Assets/Materials", "Room");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            string path = $"{RoomMaterialDir}/{_name}.mat";
            var mat = new Material(shader) { name = _name };
            mat.SetColor("_BaseColor", _color);
            mat.SetColor("_Color", _color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", _smoothness);

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
