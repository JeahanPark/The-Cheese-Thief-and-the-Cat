using System.Linq;
using System.Reflection;
using CTC.Runtime.Cameras;
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Managers;
using CTC.Runtime.Map;
using CTC.Runtime.Match;
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
        private const string DecoyPrefabPath = PrefabDir + "/MouseDecoy.prefab";
        private const string FortressPrefabPath = PrefabDir + "/CheeseFortress.prefab";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string ActiveLayoutPath = "Assets/Prefabs/Maps/Layout_Sketch01.asset";

        private const string QuirkyRoot = "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1";
        private const string CatModelPath = QuirkyRoot + "/Prefabs/Cat.prefab";
        private const string MouseModelPath = QuirkyRoot + "/Prefabs/Mouse.prefab";
        private const string CatSourceMatPath = QuirkyRoot + "/Materials/M_Cat.mat";
        private const string MouseSourceMatPath = QuirkyRoot + "/Materials/M_Mouse.mat";
        private const string UrpShaderPath =
            "Assets/Quirky Series/_Shader/URP (Unity 2019.3.0f3 and above)/SoftSurfaceGraph.shadergraph";

        private const string GridboxUrpDir =
            "Assets/Thirdparty/Ciathyza/Gridbox Prototype Materials/Materials/URP";
        private const string DefaultNetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

        [MenuItem("Tools/CTC/Setup Networking Scene")]
        public static void Run()
        {
            Material catMat = CreateUrpMaterial("M_Cat_URP", CatSourceMatPath);
            Material mouseMat = CreateUrpMaterial("M_Mouse_URP", MouseSourceMatPath);

            // 설치형 프리팹을 먼저 생성 — 플레이어 프리팹(ShopController)이 참조한다
            GameObject decoyPrefab = CreateStructurePrefab(
                "MouseDecoy", DecoyPrefabPath, PrimitiveType.Capsule,
                mouseMat, maxHp: 1,
                scale: new Vector3(0.4f, 0.25f, 0.4f),
                addExtras: _root => _root.AddComponent<DecoyLifetime>());

            GameObject fortressPrefab = CreateFortressPrefab();

            GameObject catPrefab = CreatePlayerPrefab(
                "PlayerCat", CatPrefabPath, CatModelPath, catMat,
                ccHeight: 0.9f, ccRadius: 0.35f,
                moveSpeed: 6.7f, runThreshold: 3.2f, fov: 70f, camDistance: 5f, // 원작: 술래가 먹이보다 빠름 (쥐 6, 밸런스 2026-07-26 2차)
                role: RoleId.Cat, maxHp: 1000,
                decoyPrefab: decoyPrefab, fortressPrefab: fortressPrefab);

            GameObject mousePrefab = CreatePlayerPrefab(
                "PlayerMouse", MousePrefabPath, MouseModelPath, mouseMat,
                ccHeight: 0.5f, ccRadius: 0.22f,
                moveSpeed: 6f, runThreshold: 4.2f, fov: 55f, camDistance: 3f,
                role: RoleId.Mouse, maxHp: 100,
                decoyPrefab: decoyPrefab, fortressPrefab: fortressPrefab);

            CreateGameScene(catPrefab, mousePrefab, fortressPrefab, decoyPrefab);
            Debug.Log($"[SetupNetworkingScene] 완료 — 프리팹 4종 + Game.unity 재생성 (맵: {ActiveLayoutPath})");
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

        // 설치형 NetworkObject 프리팹(요새·미끼). 프리미티브 + Health/RoleTag(Mouse)/PlayerDeath.
        // RoleTag.Mouse라 고양이 덮치기 판정을 그대로 받는다(파괴 가능, 보상은 플레이어 한정).
        private static GameObject CreateStructurePrefab(
            string name, string prefabPath, PrimitiveType primitive, Material material,
            int maxHp, Vector3 scale, System.Action<GameObject> addExtras)
        {
            var root = new GameObject(name);
            try
            {
                root.AddComponent<NetworkObject>();

                var health = root.AddComponent<Health>();
                SetInt(health, "m_MaxHp", maxHp);

                var roleTag = root.AddComponent<RoleTag>();
                SetInt(roleTag, "m_Role", (int)RoleId.Mouse);

                root.AddComponent<PlayerDeath>();
                root.AddComponent<HitFeedback>();
                var placeSfx = root.AddComponent<CTC.Runtime.Audio.PlaceSfxOnSpawn>();
                SetInt(placeSfx, "m_Sfx", (int)CTC.Runtime.Audio.SfxId.DecoyPlace);
                addExtras(root);

                var body = GameObject.CreatePrimitive(primitive);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, scale.y, 0f);
                body.transform.localScale = scale;
                if (material != null) body.GetComponent<Renderer>().sharedMaterial = material;

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                BakeGlobalObjectIdHash(asset.GetComponent<NetworkObject>());
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // v3 진입형 요새(원작 벙커): 외벽 4면 + 쥐 전용 입구(1.0×0.75 — v2 쥐구멍 실효치수).
        // 쥐 CC(0.44Ø/0.5h, skinWidth 포함 실효 0.60/0.63)만 통과, 고양이(0.86/1.03)는 차단.
        // 지붕 없음 — 점프가 없어 벽 1.2로 충분. 내부 회복은 FortressHealZone(반경 1.0).
        // 입구 방향 = 루트 로컬 +Z (스폰 시 yaw 회전으로 지정).
        private const float FORTRESS_WALL_HEIGHT = 1.2f;
        private const float FORTRESS_HALF = 1.2f;          // 풋프린트 2.4×2.4
        private const float FORTRESS_DOOR_WIDTH = 1.0f;
        private const float FORTRESS_DOOR_HEIGHT = 0.75f;
        private const float FORTRESS_WALL_THICKNESS = 0.2f;

        private static GameObject CreateFortressPrefab()
        {
            Material wallMat = GridboxMaterial("Brown");
            var root = new GameObject("CheeseFortress");
            try
            {
                root.AddComponent<NetworkObject>();

                var health = root.AddComponent<Health>();
                SetInt(health, "m_MaxHp", 300);

                var roleTag = root.AddComponent<RoleTag>();
                SetInt(roleTag, "m_Role", (int)RoleId.Mouse);

                root.AddComponent<PlayerDeath>();
                root.AddComponent<HitFeedback>();
                root.AddComponent<CTC.Runtime.Audio.PlaceSfxOnSpawn>(); // 기본값 FortressPlace
                root.AddComponent<FortressHealZone>();

                float h = FORTRESS_WALL_HEIGHT;
                float half = FORTRESS_HALF;
                float t = FORTRESS_WALL_THICKNESS;

                // 좌우벽만 통벽 — 앞(+Z)·뒤(-Z) 양면에 쥐 전용 문 (한쪽 문 캠핑을 반대편 탈출로 카운터, 2026-07-26)
                AddBox(root, wallMat, "Wall_Left", new Vector3(-half + t * 0.5f, h * 0.5f, 0f),
                    new Vector3(t, h, half * 2f - t * 2f));
                AddBox(root, wallMat, "Wall_Right", new Vector3(half - t * 0.5f, h * 0.5f, 0f),
                    new Vector3(t, h, half * 2f - t * 2f));

                // 문 있는 벽: 입구(폭 1.0) 좌우 세그먼트 + 인방(입구 위 y 0.75~1.2) — 앞뒤 공용
                float sideWidth = half - FORTRESS_DOOR_WIDTH * 0.5f;
                float sideCenterX = FORTRESS_DOOR_WIDTH * 0.5f + sideWidth * 0.5f;
                float lintelHeight = h - FORTRESS_DOOR_HEIGHT;
                foreach ((string tag, float z) in new[] { ("Front", half - t * 0.5f), ("Back", -half + t * 0.5f) })
                {
                    AddBox(root, wallMat, $"Wall_{tag}_L", new Vector3(-sideCenterX, h * 0.5f, z),
                        new Vector3(sideWidth, h, t));
                    AddBox(root, wallMat, $"Wall_{tag}_R", new Vector3(sideCenterX, h * 0.5f, z),
                        new Vector3(sideWidth, h, t));
                    AddBox(root, wallMat, $"Wall_{tag}_Lintel",
                        new Vector3(0f, FORTRESS_DOOR_HEIGHT + lintelHeight * 0.5f, z),
                        new Vector3(FORTRESS_DOOR_WIDTH, lintelHeight, t));
                }

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, FortressPrefabPath);
                BakeGlobalObjectIdHash(asset.GetComponent<NetworkObject>());
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePlayerPrefab(
            string name, string prefabPath, string modelPath, Material material,
            float ccHeight, float ccRadius,
            float moveSpeed, float runThreshold, float fov, float camDistance,
            RoleId role, int maxHp,
            GameObject decoyPrefab, GameObject fortressPrefab)
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
                root.AddComponent<HitFeedback>();

                var shop = root.AddComponent<ShopController>();
                SetRef(shop, "m_DecoyPrefab", decoyPrefab);
                SetRef(shop, "m_FortressPrefab", fortressPrefab);

                if (role == RoleId.Cat) root.AddComponent<CatPounce>();
                else
                {
                    root.AddComponent<MousePopgun>();
                    root.AddComponent<MouseDash>();
                }

                // 발소리 — 3D 루프(거리 감쇠), FootstepPlayer가 이동 속도로 재생 제어
                var stepSource = root.AddComponent<AudioSource>();
                stepSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(role == RoleId.Cat
                    ? "Assets/Audio/Movement/sfx_cat_footstep.ogg"
                    : "Assets/Audio/Movement/sfx_mouse_footstep.ogg");
                stepSource.loop = true;
                stepSource.playOnAwake = false;
                stepSource.volume = 0.6f;
                stepSource.spatialBlend = 1f;
                stepSource.rolloffMode = AudioRolloffMode.Linear;
                stepSource.minDistance = 1.5f;
                // 고양이 15m(위협 경보 반경 — 이속 6.7 기준 도주 성립 거리), 쥐 8m(살금살금 — 고양이도 귀 사냥 가능)
                stepSource.maxDistance = role == RoleId.Cat ? 15f : 8f;
                var steps = root.AddComponent<CTC.Runtime.Audio.FootstepPlayer>();
                SetRef(steps, "m_Source", stepSource);

                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;

                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();

                // 카메라 Deoccluder IgnoreTag용 — 히트한 '콜라이더의 GO' 태그를 비교하므로
                // 모델 자식(Quirky 캡슐 콜라이더 포함)까지 전부 태깅해야 자기-차폐가 안 생긴다
                foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                    tr.gameObject.tag = "Player";

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                // 저장 전 임시 씬 오브젝트에서 OnValidate가 구운 해시는 무효(두 프리팹이 동일값).
                // 영속화된 에셋에서 다시 구워야 에셋 GUID 기반의 유일한 해시가 나온다.
                BakeGlobalObjectIdHash(asset.GetComponent<NetworkObject>());
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateGameScene(
            GameObject catPrefab, GameObject mousePrefab, GameObject fortressPrefab, GameObject decoyPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 바닥: 두께 있는 박스(상면 y=0). 단면 Plane은 밑으로 한 번 빠지면 영구 낙하
            // (재시작 낙하 버그 안전망 — 리뷰 분석 1순위)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(30f, 0.5f, 30f);
            floor.GetComponent<Renderer>().sharedMaterial = GridboxMaterial("Grey1");
            // 카메라 Deoccluder(CollideAgainst=Default)가 바닥과 충돌해 낮은 앵글에서 튀는 것 방지.
            // Ignore Raycast 레이어는 물리 충돌(CC 접지)에는 영향 없음.
            floor.layer = 2; // Ignore Raycast

            MapLayout layout = LoadActiveLayout();
            BuildFromLayout(layout);
            BuildCheeseSpots(layout);

            // NetworkManager — 자동 플레이어 스폰 없음(로비가 역할별로 스폰)
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7788);

            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = null;
            RegisterNetworkPrefabs(nm, catPrefab, mousePrefab, fortressPrefab, decoyPrefab);

            // 스팀 매칭(Plan 13): Facepunch 트랜스포트 동승 — 기본은 UTP, 스팀 방 경로에서만 교체
            nmGo.AddComponent<Netcode.Transports.Facepunch.FacepunchTransport>();
            nmGo.AddComponent<SteamBootstrap>();
            nmGo.AddComponent<SteamLobbyManager>();

            nmGo.AddComponent<ConnectionUI>();
            nmGo.AddComponent<MppmAutoConnect>();
            nmGo.AddComponent<MultiplayerQaProbe>();

            // 로비 (in-scene NetworkObject — 서버 시작 시 자동 스폰)
            var lobbyGo = new GameObject("LobbyManager");
            lobbyGo.AddComponent<NetworkObject>();
            var lobby = lobbyGo.AddComponent<LobbyManager>();
            SetRef(lobby, "catPrefab", catPrefab);
            SetRef(lobby, "mousePrefab", mousePrefab);
            SetRef(lobby, "fortressPrefab", fortressPrefab);
            SetRef(lobby, "m_ActiveLayout", layout); // 요새자리·스폰 공식이 layout 참조
            var match = lobbyGo.AddComponent<MatchManager>(); // 동승 — 새 in-scene NetworkObject 없음
            SetRef(match, "m_Lobby", lobby);
            BuildLobbyCanvas(lobby, match);

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
            // 반경 3 + 타깃 오프셋 0.7: 기본 앵글(20°)에서 카메라 높이 ≈1.7 — 통로벽(1.6) 위로
            // 넘겨 보게 되어 좁은 통로에서 충돌 자체가 드물다 (맵 스케일: 방 5~9m·통로 1.6m)
            orbital.Radius = 3f;
            orbital.TargetOffset = new Vector3(0f, 0.7f, 0f);
            orbital.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbital.HorizontalAxis.Wrap = true;
            orbital.VerticalAxis.Range = new Vector2(-30f, 70f);
            orbital.VerticalAxis.Value = 20f;
            camGo.AddComponent<CinemachineRotationComposer>();

            // 지형-카메라 충돌: Deoccluder(시야 확보) — 실측 비교 결과:
            //   · Decollider 단독 = 벽 너머에서 플레이어가 아예 안 보임(침투만 막고 시야 방치) → 탈락
            //   · 구 설정의 멀미 원인은 Deoccluder가 아니라 궤도 반경 4m(좁은 맵에서 상시 차폐) → 반경 3으로 해소
            // 스크립트 AddComponent는 Reset이 안 불려 struct가 0(비활성)이 되므로 명시 설정.
            var deoccluder = camGo.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = 1; // Default — 지형(바닥은 Ignore Raycast로 제외)
            deoccluder.IgnoreTag = "Player"; // 플레이어 몸(Quirky 모델 콜라이더 포함) 자기-차폐 방지
            deoccluder.MinimumDistanceFromTarget = 0.5f; // 벽 밀착 시에만 근접 캠 허용
            deoccluder.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance
            {
                Enabled = true,
                DistanceLimit = 0f,
                CameraRadius = 0.25f,
                Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward,
                SmoothingTime = 0.2f,   // 벽 스침에 즉발로 튀지 않게
                Damping = 0.5f,         // 장애물 벗어난 뒤 부드럽게 복귀
                DampingWhenOccluded = 0.2f,
            };

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

            // in-scene NetworkObject 해시 베이크 — 첫 저장 전에는 GlobalObjectId가 무효
            // (identifierType=0)라 OnValidate가 스킵되어 해시가 0으로 직렬화된다.
            // 저장으로 씬 GUID/fileID가 생긴 뒤 다시 굽고 재저장한다.
            // (빌드 세팅 등록 후여야 CheckForInScenePlaced가 in-scene으로 판정)
            NetworkObject[] sceneObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include);
            foreach (NetworkObject no in sceneObjects)
            {
                BakeGlobalObjectIdHash(no);
            }
            // 직접 필드 수정은 씬을 dirty로 안 만들어 SaveScene이 no-op될 수 있음 → 명시 dirty
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScenePath);

            VerifyUniqueHashes(sceneObjects
                .Append(catPrefab.GetComponent<NetworkObject>())
                .Append(mousePrefab.GetComponent<NetworkObject>())
                .Append(fortressPrefab.GetComponent<NetworkObject>())
                .Append(decoyPrefab.GetComponent<NetworkObject>())
                .ToArray());
        }

        // NGO는 에디터 OnValidate에서 GlobalObjectIdHash = GlobalObjectId 문자열의 XXHash32로
        // 굽지만, 저장 전 임시 오브젝트에서는 무효한 id(targetObjectId=0)로 구워져 충돌한다.
        // 저장(영속화) 후 동일 계산을 직접 수행해 SerializedObject로 쓴다 — OnValidate 재실행 시에도
        // 같은 값이 나오므로 이후 에디터 churn이 없다. (필드·XXHash 모두 internal → 리플렉션)
        private static void BakeGlobalObjectIdHash(NetworkObject networkObject)
        {
            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(networkObject);
            if (globalId.identifierType == 0 || globalId.targetObjectId == 0)
            {
                Debug.LogError($"[SetupNetworkingScene] {networkObject.name} GlobalObjectId 무효({globalId}) — 해시 베이크 불가");
                return;
            }

            System.Type xxHash = typeof(NetworkObject).Assembly.GetType("Unity.Netcode.XXHash");
            MethodInfo hash32 = xxHash.GetMethod("Hash32", new[] { typeof(string) });
            uint hash = (uint)hash32.Invoke(null, new object[] { globalId.ToString() });

            var so = new SerializedObject(networkObject);
            so.FindProperty("GlobalObjectIdHash").uintValue = hash;
            so.ApplyModifiedPropertiesWithoutUndo();
            // 로드 시 OnValidate가 메모리만 교정(dirty 없음)하는 경우 위 Apply가 no-op이 되어
            // 디스크의 스테일/충돌 값이 영영 안 고쳐진다 → 무조건 dirty로 재직렬화 강제
            EditorUtility.SetDirty(networkObject);
        }

        // 베이크 결과 최종 검증 — 0이거나 충돌하면 NGO ScenePlacedObjects 등록이 깨진다.
        private static void VerifyUniqueHashes(params NetworkObject[] networkObjects)
        {
            var seen = new System.Collections.Generic.Dictionary<uint, string>();
            foreach (NetworkObject no in networkObjects)
            {
                uint hash = new SerializedObject(no).FindProperty("GlobalObjectIdHash").uintValue;
                if (hash == 0)
                    Debug.LogError($"[SetupNetworkingScene] {no.name} GlobalObjectIdHash=0");
                else if (seen.TryGetValue(hash, out string other))
                    Debug.LogError($"[SetupNetworkingScene] GlobalObjectIdHash 충돌: {no.name} == {other} ({hash})");
                else
                    seen.Add(hash, no.name);
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

            // 삭제된 프리팹(예: v2 요새 제거)의 null/missing 항목 정리
            for (int i = list.PrefabList.Count - 1; i >= 0; i--)
            {
                if (list.PrefabList[i] == null || list.PrefabList[i].Prefab == null)
                {
                    list.Remove(list.PrefabList[i]);
                    dirty = true;
                }
            }

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

        // MapLayout(생성 툴 산출, Plan 11) 하나를 읽어 방·통로 지오메트리와 치즈 지점을 굽는다.
        // 요새는 런타임(LobbyManager)이 layout.Fortresses로 스폰 — 여기선 지오메트리만.
        private const float ROOM_WALL_HEIGHT = 3f;
        private const float ROOM_WALL_THICK = 0.5f;
        private const float CORRIDOR_WALL_HEIGHT = 1.6f;

        private static MapLayout LoadActiveLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<MapLayout>(ActiveLayoutPath);
            if (layout == null)
                Debug.LogError($"[SetupNetworkingScene] MapLayout 없음: {ActiveLayoutPath} — 먼저 Generate Map Layout");
            return layout;
        }

        private static void BuildFromLayout(MapLayout layout)
        {
            if (layout == null) return;
            Material wall = GridboxMaterial("Grey2");
            Material floorMat = GridboxMaterial("Grey4");
            var root = new GameObject("GeneratedMap");

            // 맵 경계 외벽 — 지오메트리 틈으로 새어나가도 바닥(30×30) 밖 낙사 방지
            AddBox(root, wall, "Boundary_N", new Vector3(0f, 1.5f, 15f), new Vector3(30.5f, 3f, 0.5f));
            AddBox(root, wall, "Boundary_S", new Vector3(0f, 1.5f, -15f), new Vector3(30.5f, 3f, 0.5f));
            AddBox(root, wall, "Boundary_E", new Vector3(15f, 1.5f, 0f), new Vector3(0.5f, 3f, 30.5f));
            AddBox(root, wall, "Boundary_W", new Vector3(-15f, 1.5f, 0f), new Vector3(0.5f, 3f, 30.5f));

            // 방: 바닥 + 통로 접점만 개방한 4면 벽
            foreach (RoomRect r in layout.Rooms)
                BuildRoomWalls(root, wall, floorMat, r, layout.Corridors);

            // 통로(미로 포함): 중심선 세그먼트마다 폭만큼 바닥 + 양측 낮은 벽(접합부 개방)
            foreach (CorridorSeg c in layout.Corridors)
                BuildCorridor(root, wall, floorMat, c, layout.Corridors);

            // 막다른 통로 끝(방·타 세그먼트 미접촉)은 끝막이 벽 — 맵 외곽 공터로 새는 틈 봉쇄
            foreach (CorridorSeg c in layout.Corridors)
            {
                AddEndCapIfDeadEnd(root, wall, c, c.From, layout.Rooms, layout.Corridors);
                AddEndCapIfDeadEnd(root, wall, c, c.To, layout.Rooms, layout.Corridors);
            }
        }

        // 통로 끝점이 방 경계·다른 세그먼트에 닿지 않으면(진짜 막다른 끝) 폭+여유만큼 캡 벽을 세운다.
        private static void AddEndCapIfDeadEnd(
            GameObject root, Material wallMat, CorridorSeg c, Vector2 end,
            System.Collections.Generic.IReadOnlyList<RoomRect> rooms,
            System.Collections.Generic.IReadOnlyList<CorridorSeg> all)
        {
            const float TOUCH_TOLERANCE = 0.3f;

            foreach (RoomRect r in rooms)
            {
                if (end.x >= r.Min.x - TOUCH_TOLERANCE && end.x <= r.Max.x + TOUCH_TOLERANCE &&
                    end.y >= r.Min.y - TOUCH_TOLERANCE && end.y <= r.Max.y + TOUCH_TOLERANCE) return;
            }
            foreach (CorridorSeg o in all)
            {
                if (o.From == c.From && o.To == c.To) continue;
                if (DistancePointToSegment(end, o.From, o.To) < TOUCH_TOLERANCE) return;
            }

            Vector2 dir = (c.To - c.From).normalized;
            Quaternion rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
            AddOrientedBox(root, wallMat, "CorridorEndCap",
                new Vector3(end.x, CORRIDOR_WALL_HEIGHT * 0.5f, end.y), rot,
                new Vector3(c.Width + ROOM_WALL_THICK * 2f, CORRIDOR_WALL_HEIGHT, ROOM_WALL_THICK));
        }

        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }

        private static void BuildRoomWalls(
            GameObject root, Material wall, Material floorMat, RoomRect r, System.Collections.Generic.IReadOnlyList<CorridorSeg> corridors)
        {
            // 바닥류는 Ignore Raycast — 카메라 Deoccluder가 바닥과 충돌하지 않게 (전역 Floor와 동일)
            AddBox(root, floorMat, $"Floor_{r.Id}",
                new Vector3(r.Center.x, 0.02f, r.Center.y), new Vector3(r.Size.x, 0.04f, r.Size.y)).layer = 2;

            for (int side = 0; side < 4; side++)
                BuildWallWithGaps(root, wall, r, corridors, side);
        }

        // 방 한 변을 통로 접점(개구부)만 남기고 벽 세그먼트로 깐다 — 구 BuildTunnelWall과 동형.
        // side: 0=N(+z) 1=S(-z) 2=E(+x) 3=W(-x)
        private static void BuildWallWithGaps(
            GameObject root, Material wall, RoomRect r, System.Collections.Generic.IReadOnlyList<CorridorSeg> corridors, int side)
        {
            bool horizontal = side <= 1; // N/S 변은 x축 방향으로 뻗음
            float fixedCoord = side == 0 ? r.Max.y : side == 1 ? r.Min.y : side == 2 ? r.Max.x : r.Min.x;
            float min = horizontal ? r.Min.x : r.Min.y;
            float max = horizontal ? r.Max.x : r.Max.y;

            // 이 변에 닿는 통로 끝점 → 개구부(변 축 좌표, 반폭)
            var gaps = new System.Collections.Generic.List<(float center, float half)>();
            foreach (CorridorSeg c in corridors)
            {
                foreach (Vector2 p in new[] { c.From, c.To })
                {
                    float perp = horizontal ? p.y : p.x;
                    float along = horizontal ? p.x : p.y;
                    if (Mathf.Abs(perp - fixedCoord) > c.Width * 0.5f + 0.6f) continue;
                    if (along < min - 0.5f || along > max + 0.5f) continue;
                    gaps.Add((along, c.Width * 0.5f + 0.25f));
                }
            }
            gaps.Sort((a, b) => a.center.CompareTo(b.center));

            string sideName = side == 0 ? "N" : side == 1 ? "S" : side == 2 ? "E" : "W";
            float cursor = min;
            int seg = 0;
            for (int i = 0; i <= gaps.Count; i++)
            {
                float segEnd = i < gaps.Count ? gaps[i].center - gaps[i].half : max;
                float length = segEnd - cursor;
                if (length > 0.05f)
                {
                    float center = (cursor + segEnd) * 0.5f;
                    AddBox(root, wall, $"Wall_{r.Id}_{sideName}{seg++}",
                        horizontal ? new Vector3(center, ROOM_WALL_HEIGHT * 0.5f, fixedCoord)
                                   : new Vector3(fixedCoord, ROOM_WALL_HEIGHT * 0.5f, center),
                        horizontal ? new Vector3(length, ROOM_WALL_HEIGHT, ROOM_WALL_THICK)
                                   : new Vector3(ROOM_WALL_THICK, ROOM_WALL_HEIGHT, length));
                }
                if (i < gaps.Count) cursor = Mathf.Max(cursor, gaps[i].center + gaps[i].half);
            }
        }

        // 통로 세그먼트: 바닥 + 양측 낮은 벽(고양이도 통과 — 폭이 이동을 제약, 벽은 이탈 차단).
        // 대각 세그먼트(미로·사선 통로) 지원을 위해 회전 박스로 깐다.
        // 측벽은 다른 세그먼트가 가로지르는 지점(T자·꺾임 접합부)을 개구부로 남긴다 — 리뷰 C1.
        private static void BuildCorridor(GameObject root, Material wall, Material floorMat, CorridorSeg c,
            System.Collections.Generic.IReadOnlyList<CorridorSeg> all)
        {
            Vector3 a = new Vector3(c.From.x, 0f, c.From.y);
            Vector3 b = new Vector3(c.To.x, 0f, c.To.y);
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.01f) return;

            Vector3 mid = (a + b) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(dir / len);
            // 바닥류는 Ignore Raycast — 카메라 Deoccluder 충돌 제외
            AddOrientedBox(root, floorMat, "CorridorFloor",
                mid + Vector3.up * 0.02f, rot, new Vector3(c.Width, 0.04f, len)).layer = 2;

            BuildCorridorSideWall(root, wall, c, all, +1f);
            BuildCorridorSideWall(root, wall, c, all, -1f);
        }

        // 통로 한쪽 측벽: 다른 세그먼트 중심선이 이 벽 라인을 실제로 가로지르는 구간만
        // 개구부로 비우고 나머지를 세그먼트로 깐다. 교차는 2D(x,z) 선분-선분으로 판정 —
        // 분기가 뻗는 쪽 벽만 열리고(u∈[0,1] 조건) 반대쪽·바깥 코너는 닫힌 채 유지된다.
        private static void BuildCorridorSideWall(GameObject root, Material wallMat, CorridorSeg c,
            System.Collections.Generic.IReadOnlyList<CorridorSeg> all, float sideSign)
        {
            Vector2 a = c.From;
            Vector2 d = c.To - c.From;
            float len = d.magnitude;
            if (len < 0.01f) return;
            Vector2 fwd = d / len;
            Vector2 right = new Vector2(fwd.y, -fwd.x); // (x,z) 평면에서 진행방향의 오른쪽
            Vector2 wallStart = a + right * (sideSign * (c.Width * 0.5f + ROOM_WALL_THICK * 0.5f));

            var gaps = new System.Collections.Generic.List<(float center, float half)>();
            foreach (CorridorSeg o in all)
            {
                if (o.From == c.From && o.To == c.To) continue; // 자기 자신
                Vector2 od = o.To - o.From;
                float denom = od.x * fwd.y - od.y * fwd.x;     // cross(od, fwd)
                if (Mathf.Abs(denom) < 0.15f) continue;        // 평행/준평행 — 접합 아님

                Vector2 rel = wallStart - o.From;
                float u = (rel.x * fwd.y - rel.y * fwd.x) / denom;
                if (u < -0.05f || u > 1.05f) continue;         // 상대 선분 범위 밖

                Vector2 hit = o.From + od * u;
                float s = Vector2.Dot(hit - wallStart, fwd);
                if (s < -0.4f || s > len + 0.4f) continue;     // 이 벽 범위 밖

                float sin = Mathf.Abs(denom) / od.magnitude;   // 교차각 보정(사선 분기는 개구부 확대)
                gaps.Add((s, o.Width * 0.5f / Mathf.Max(sin, 0.4f) + 0.25f));
            }
            gaps.Sort((x, y) => x.center.CompareTo(y.center));

            Quaternion rot = Quaternion.LookRotation(new Vector3(fwd.x, 0f, fwd.y));
            string sideName = sideSign > 0 ? "R" : "L";
            float cursor = -ROOM_WALL_THICK * 0.5f;
            float end = len + ROOM_WALL_THICK * 0.5f;
            int seg = 0;
            for (int i = 0; i <= gaps.Count; i++)
            {
                float segEnd = i < gaps.Count ? gaps[i].center - gaps[i].half : end;
                float segLen = segEnd - cursor;
                if (segLen > 0.05f)
                {
                    Vector2 center2 = wallStart + fwd * ((cursor + segEnd) * 0.5f);
                    AddOrientedBox(root, wallMat, $"CorridorWall_{sideName}{seg++}",
                        new Vector3(center2.x, CORRIDOR_WALL_HEIGHT * 0.5f, center2.y), rot,
                        new Vector3(ROOM_WALL_THICK, CORRIDOR_WALL_HEIGHT, segLen));
                }
                if (i < gaps.Count) cursor = Mathf.Max(cursor, gaps[i].center + gaps[i].half);
            }
        }

        // 등급별 디스크 색 — 위험도 가독성 (1~3 노랑 / 4~5 주황 / 6~7 빨강)
        private static Material GradeDiscMaterial(int grade)
        {
            string name = grade >= 6 ? "Red" : grade >= 4 ? "Orange" : "Yellow";
            return GridboxMaterial(name);
        }

        private static void BuildCheeseSpots(MapLayout layout)
        {
            if (layout == null) return;
            var root = new GameObject("CheeseSpots");

            foreach (CheesePlacement cp in layout.Cheese)
            {
                var spotGo = new GameObject($"CheeseSpot_G{cp.Grade}");
                spotGo.transform.SetParent(root.transform, false);
                spotGo.transform.localPosition = new Vector3(cp.Pos.x, 0f, cp.Pos.y);

                var spot = spotGo.AddComponent<CheeseSpot>();
                SetInt(spot, "m_Grade", cp.Grade);
                SetFloat(spot, "m_Radius", cp.Radius);
                AddCheeseVisual(spotGo, cp.Grade);
            }
        }

        private static void AddCheeseVisual(GameObject spotGo, int grade)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            Object.DestroyImmediate(disc.GetComponent<CapsuleCollider>()); // 이동 방해·오버랩 오염 방지
            disc.transform.SetParent(spotGo.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            disc.transform.localScale = new Vector3(3f, 0.05f, 3f);
            Material discMat = GradeDiscMaterial(grade);
            if (discMat != null) disc.GetComponent<Renderer>().sharedMaterial = discMat;

            var labelGo = new GameObject("GradeLabel");
            labelGo.transform.SetParent(spotGo.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = grade.ToString();
            tmp.fontSize = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.yellow;
        }

        private static Material GridboxMaterial(string name)
        {
            string path = $"{GridboxUrpDir}/Prototype_512x512_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) Debug.LogError($"[SetupNetworkingScene] Gridbox 머티리얼 없음: {path}");
            return mat;
        }

        private static GameObject AddBox(GameObject parent, Material material, string name, Vector3 center, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent.transform, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        // 대각 통로용 회전 박스 (AddBox + 회전)
        private static GameObject AddOrientedBox(
            GameObject parent, Material material, string name, Vector3 center, Quaternion rotation, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent.transform, false);
            box.transform.localPosition = center;
            box.transform.localRotation = rotation;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        // 로비 UGUI — Canvas + EventSystem(InputSystemUIInputModule) + 패널 + 플레인 UGUI 버튼.
        private static void BuildLobbyCanvas(LobbyManager lobby, MatchManager match)
        {
            var canvasGo = new GameObject("LobbyCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Expand: 기준 해상도 레이아웃이 어떤 종횡비에서도 화면 밖으로 안 나가게 축소 매칭
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
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

            // 상점 패널 (B키 토글, ShopHud가 제어)
            var shopPanel = new GameObject("ShopPanel", typeof(RectTransform));
            shopPanel.transform.SetParent(canvasGo.transform, false);
            var shopRt = (RectTransform)shopPanel.transform;
            shopRt.anchorMin = shopRt.anchorMax = new Vector2(0.5f, 0.5f);
            shopRt.pivot = new Vector2(0.5f, 0.5f);
            shopRt.sizeDelta = new Vector2(420f, 560f);
            var shopBg = shopPanel.AddComponent<Image>();
            shopBg.color = new Color(0.08f, 0.09f, 0.12f, 0.9f);

            TMP_Text shopTitle = CreateTmpText("ShopTitle", shopPanel.transform, "SHOP (B)",
                30f, TextAlignmentOptions.Center, new Vector2(0f, -16f), new Vector2(380f, 44f));
            shopTitle.fontStyle = FontStyles.Bold;

            TMP_Text shopBalance = CreateTmpText("ShopBalance", shopPanel.transform, "Cheese: 0",
                24f, TextAlignmentOptions.Center, new Vector2(0f, -64f), new Vector2(380f, 36f));

            int catalogCount = ShopLogic.CATALOG.Length;
            var shopButtons = new Button[catalogCount];
            for (int i = 0; i < catalogCount; i++)
            {
                shopButtons[i] = CreateMuipButton(shopPanel.transform, $"ShopButton{i}", "-",
                    new Vector2(0f, -120f - i * 60f));
            }
            shopPanel.SetActive(false);

            var shopHud = canvasGo.AddComponent<ShopHud>();
            SetRef(shopHud, "m_Lobby", lobby);
            SetRef(shopHud, "m_Panel", shopPanel);
            SetRef(shopHud, "m_BalanceText", shopBalance);
            SetRefArray(shopHud, "m_Buttons", shopButtons);

            // 결과 패널 (매치 종료 시 ResultHud가 표시)
            var resultPanel = new GameObject("ResultPanel", typeof(RectTransform));
            resultPanel.transform.SetParent(canvasGo.transform, false);
            var resultRt = (RectTransform)resultPanel.transform;
            resultRt.anchorMin = resultRt.anchorMax = new Vector2(0.5f, 0.5f);
            resultRt.pivot = new Vector2(0.5f, 0.5f);
            resultRt.sizeDelta = new Vector2(520f, 260f);
            var resultBg = resultPanel.AddComponent<Image>();
            resultBg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

            TMP_Text resultText = CreateTmpText("ResultText", resultPanel.transform, "-",
                44f, TextAlignmentOptions.Center, new Vector2(0f, -40f), new Vector2(480f, 70f));
            resultText.fontStyle = FontStyles.Bold;

            Button restartButton = CreateMuipButton(resultPanel.transform, "RestartButton", "Restart",
                new Vector2(0f, -170f));
            resultPanel.SetActive(false);

            var resultHud = canvasGo.AddComponent<ResultHud>();
            SetRef(resultHud, "m_Match", match);
            SetRef(resultHud, "m_Panel", resultPanel);
            SetRef(resultHud, "m_Text", resultText);
            SetRef(resultHud, "m_RestartButton", restartButton);

            // 조작키 안내 (좌하단 — 역할별, ControlsHud가 게임 중 표시)
            var controlsTextGo = new GameObject("ControlsText");
            controlsTextGo.transform.SetParent(canvasGo.transform, false);
            var controlsText = controlsTextGo.AddComponent<TextMeshProUGUI>();
            controlsText.text = string.Empty;
            controlsText.fontSize = 22f;
            controlsText.alignment = TextAlignmentOptions.BottomLeft;
            controlsText.color = new Color(1f, 1f, 1f, 0.75f);
            var controlsRect = controlsTextGo.GetComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(0f, 0f);
            controlsRect.anchorMax = new Vector2(0f, 0f);
            controlsRect.pivot = new Vector2(0f, 0f);
            controlsRect.anchoredPosition = new Vector2(24f, 20f);
            controlsRect.sizeDelta = new Vector2(640f, 64f);
            controlsTextGo.SetActive(false);

            var controlsHud = canvasGo.AddComponent<ControlsHud>();
            SetRef(controlsHud, "m_Lobby", lobby);
            SetRef(controlsHud, "m_Text", controlsText);

            // 매치 타이머 (상단 중앙 — v3 안전 타이머)
            var timerTextGo = new GameObject("TimerText");
            timerTextGo.transform.SetParent(canvasGo.transform, false);
            var timerText = timerTextGo.AddComponent<TextMeshProUGUI>();
            timerText.text = "10:00";
            timerText.fontSize = 40f;
            timerText.alignment = TextAlignmentOptions.Top;
            var timerRect = timerTextGo.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = new Vector2(0f, -16f);
            timerRect.sizeDelta = new Vector2(200f, 52f);
            timerTextGo.SetActive(false);

            var timerHud = canvasGo.AddComponent<TimerHud>();
            SetRef(timerHud, "m_Lobby", lobby);
            SetRef(timerHud, "m_Match", match);
            SetRef(timerHud, "m_Text", timerText);

            // 인게임 BGM (게임 시작 시 루프 재생 — BgmPlayer가 제어)
            var bgmGo = new GameObject("BgmPlayer");
            var bgmSource = bgmGo.AddComponent<AudioSource>();
            bgmSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BGM/bgm_ingame.ogg");
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = 0.35f;
            bgmSource.spatialBlend = 0f; // 2D — 거리 감쇠 없음
            var bgm = bgmGo.AddComponent<CTC.Runtime.Audio.BgmPlayer>();
            SetRef(bgm, "m_Lobby", lobby);
            SetRef(bgm, "m_Source", bgmSource);

            // 로비 BGM — 게임이 안 도는 동안(접속 전·로비) 루프
            var lobbyBgmGo = new GameObject("LobbyBgmPlayer");
            var lobbyBgmSource = lobbyBgmGo.AddComponent<AudioSource>();
            lobbyBgmSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BGM/bgm_lobby.ogg");
            lobbyBgmSource.loop = true;
            lobbyBgmSource.playOnAwake = false;
            lobbyBgmSource.volume = 0.35f;
            lobbyBgmSource.spatialBlend = 0f;
            var lobbyBgm = lobbyBgmGo.AddComponent<CTC.Runtime.Audio.BgmPlayer>();
            SetRef(lobbyBgm, "m_Lobby", lobby);
            SetRef(lobbyBgm, "m_Source", lobbyBgmSource);
            SetInt(lobbyBgm, "m_PlayWhenGameStopped", 1);

            // 부엌 앰비언스 — BGM과 동일 조건(게임 중 루프), 볼륨만 낮게
            var ambGo = new GameObject("AmbiencePlayer");
            var ambSource = ambGo.AddComponent<AudioSource>();
            ambSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BGM/amb_kitchen.ogg");
            ambSource.loop = true;
            ambSource.playOnAwake = false;
            ambSource.volume = 0.15f;
            ambSource.spatialBlend = 0f;
            var amb = ambGo.AddComponent<CTC.Runtime.Audio.BgmPlayer>();
            SetRef(amb, "m_Lobby", lobby);
            SetRef(amb, "m_Source", ambSource);

            // 효과음 허브 — m_Clips는 SfxId enum 순서와 1:1
            var sfxGo = new GameObject("SfxPlayer");
            var sfxPlayer = sfxGo.AddComponent<CTC.Runtime.Audio.SfxPlayer>();
            SetRefArray(sfxPlayer, "m_Clips", new Object[]
            {
                LoadClip("Combat/sfx_popgun_fire"),      // PopgunFire
                LoadClip("Combat/sfx_slingshot_fire"),   // SlingshotFire
                LoadClip("Combat/sfx_machinegun_fire"),  // MachinegunFire
                LoadClip("Combat/sfx_pounce_swing"),     // PounceSwing
                LoadClip("Combat/sfx_mouse_hurt"),       // MouseHurt
                LoadClip("Combat/sfx_cat_hurt"),         // CatHurt
                LoadClip("Combat/sfx_mouse_death"),      // MouseDeath
                LoadClip("Combat/sfx_cat_death"),        // CatDeath
                LoadClip("Economy/sfx_fortress_hit"),    // FortressHit
                LoadClip("Economy/sfx_fortress_break"),  // FortressBreak
                LoadClip("Economy/sfx_fortress_place"),  // FortressPlace
                LoadClip("Economy/sfx_decoy_place"),     // DecoyPlace
                LoadClip("UI/sfx_match_start"),          // MatchStart
                LoadClip("UI/jingle_mouse_win"),         // MouseWin
                LoadClip("UI/jingle_cat_win"),           // CatWin
                LoadClip("Movement/sfx_mouse_dash"),     // MouseDash
                LoadClip("Economy/sfx_cheese_tick"),     // CheeseTick
                LoadClip("Economy/sfx_heal_tick"),       // HealTick
                LoadClip("Economy/sfx_shop_buy"),        // ShopBuy
                LoadClip("Economy/sfx_shop_deny"),       // ShopDeny
                LoadClip("UI/sfx_ui_click"),             // UiClick
                LoadClip("UI/sfx_timer_warn"),           // TimerWarn
            });

            var matchSfx = sfxGo.AddComponent<CTC.Runtime.Audio.MatchSfx>();
            SetRef(matchSfx, "m_Lobby", lobby);
            SetRef(matchSfx, "m_Match", match);

            // 밸런스 데이터 로거(스펙 v3.3) — 매치 종료 시 persistentDataPath/MatchStats/*.json
            var statsGo = new GameObject("MatchStatsRecorder");
            var stats = statsGo.AddComponent<MatchStatsRecorder>();
            SetRef(stats, "m_Lobby", lobby);
            SetRef(stats, "m_Match", match);
        }

        private static AudioClip LoadClip(string relativePath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + relativePath + ".ogg");
            if (clip == null) Debug.LogError($"[Setup] AudioClip 없음: Assets/Audio/{relativePath}.ogg");
            return clip;
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

        // 표준 UGUI 버튼(흰 배경 + TMP 라벨). MUIP 버튼은 런타임 상태 초기화가 세션에 따라
        // 깨지는 문제(비가시 버튼)와 한글 글리프·다중 라벨 이슈가 반복돼 그레이박스 단계에선
        // 플레인 UGUI로 통일 — 본 UI 패스에서 디자인 버튼으로 교체 전제.
        private static Button CreateMuipButton(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(380f, 56f);

            var bg = go.AddComponent<Image>();
            bg.color = Color.white;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.12f, 0.13f, 0.16f);
            tmp.raycastTarget = false;

            var button = go.AddComponent<Button>();
            button.targetGraphic = bg; // 기본 ColorTint 전환 사용
            go.AddComponent<CTC.Runtime.Audio.UiClickSfx>(); // 공통 클릭음(2D)
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

        private static void SetRefArray(Component c, string field, Object[] values)
        {
            var so = new SerializedObject(c);
            SerializedProperty prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
