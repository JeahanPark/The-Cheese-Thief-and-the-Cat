using System.Linq;
using System.Reflection;
using CTC.Runtime.Cameras;
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Managers;
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
                moveSpeed: 4.5f, runThreshold: 3.2f, fov: 70f, camDistance: 5f,
                role: RoleId.Cat, maxHp: 1000,
                decoyPrefab: decoyPrefab, fortressPrefab: fortressPrefab);

            GameObject mousePrefab = CreatePlayerPrefab(
                "PlayerMouse", MousePrefabPath, MouseModelPath, mouseMat,
                ccHeight: 0.5f, ccRadius: 0.22f,
                moveSpeed: 6f, runThreshold: 4.2f, fov: 55f, camDistance: 3f,
                role: RoleId.Mouse, maxHp: 100,
                decoyPrefab: decoyPrefab, fortressPrefab: fortressPrefab);

            CreateGameScene(catPrefab, mousePrefab, fortressPrefab, decoyPrefab);
            Debug.Log("[SetupNetworkingScene] 완료 — 프리팹 4종 + Game.unity 재생성 (요새 배선; 맵은 Plan 11이 교체 예정)");
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
                root.AddComponent<FortressHealZone>();

                float h = FORTRESS_WALL_HEIGHT;
                float half = FORTRESS_HALF;
                float t = FORTRESS_WALL_THICKNESS;

                // 뒷벽·좌우벽 (입구는 로컬 +Z면)
                AddBox(root, wallMat, "Wall_Back", new Vector3(0f, h * 0.5f, -half + t * 0.5f),
                    new Vector3(half * 2f, h, t));
                AddBox(root, wallMat, "Wall_Left", new Vector3(-half + t * 0.5f, h * 0.5f, 0f),
                    new Vector3(t, h, half * 2f - t * 2f));
                AddBox(root, wallMat, "Wall_Right", new Vector3(half - t * 0.5f, h * 0.5f, 0f),
                    new Vector3(t, h, half * 2f - t * 2f));

                // 앞벽: 입구(폭 1.0) 좌우 세그먼트 + 인방(입구 위 y 0.75~1.2)
                float sideWidth = half - FORTRESS_DOOR_WIDTH * 0.5f;
                float sideCenterX = FORTRESS_DOOR_WIDTH * 0.5f + sideWidth * 0.5f;
                float frontZ = half - t * 0.5f;
                AddBox(root, wallMat, "Wall_Front_L", new Vector3(-sideCenterX, h * 0.5f, frontZ),
                    new Vector3(sideWidth, h, t));
                AddBox(root, wallMat, "Wall_Front_R", new Vector3(sideCenterX, h * 0.5f, frontZ),
                    new Vector3(sideWidth, h, t));
                float lintelHeight = h - FORTRESS_DOOR_HEIGHT;
                AddBox(root, wallMat, "Wall_Lintel",
                    new Vector3(0f, FORTRESS_DOOR_HEIGHT + lintelHeight * 0.5f, frontZ),
                    new Vector3(FORTRESS_DOOR_WIDTH, lintelHeight, t));

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

                var shop = root.AddComponent<ShopController>();
                SetRef(shop, "m_DecoyPrefab", decoyPrefab);
                SetRef(shop, "m_FortressPrefab", fortressPrefab);

                if (role == RoleId.Cat) root.AddComponent<CatPounce>();
                else
                {
                    root.AddComponent<MousePopgun>();
                    root.AddComponent<MouseDash>();
                }

                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;

                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();

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
            RegisterNetworkPrefabs(nm, catPrefab, mousePrefab, fortressPrefab, decoyPrefab);

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

        // v2 그레이박스: 외벽 + 쥐 터널 링(내벽·쥐구멍·인방). 사물 조형 없음 — 아트 패스(8-B)에서 교체.
        // 구멍 치수는 CC skinWidth(0.08) 포함 실효 크기 기준 (SPEC §12-A):
        //   쥐   실효 폭 0.60 · 키 0.63 → 통과 (여유 폭 0.4 / 높이 0.12)
        //   고양이 실효 폭 0.86 · 키 1.03 → 인방(0.75)에 차단
        private const float TUNNEL_WALL_POS = 13.2f;   // 내벽 중심 좌표
        private const float HOLE_WIDTH = 1.0f;
        private const float HOLE_HEIGHT = 0.75f;
        private const float TUNNEL_WALL_HEIGHT = 1.2f;

        // 변마다 쥐구멍 위치(해당 축 좌표) — 총 9개
        private static readonly float[] HOLES_NORTH = { -7f, 0f, 7f };
        private static readonly float[] HOLES_SOUTH = { -5f, 7f };
        private static readonly float[] HOLES_WEST = { -6f, 6f };
        private static readonly float[] HOLES_EAST = { -6f, 6f };

        private static void BuildGreyboxMap()
        {
            Material wall = GridboxMaterial("Grey2");
            Material tunnelWall = GridboxMaterial("Brown");
            var root = new GameObject("GreyboxMap");

            AddBox(root, wall, "Wall_N", new Vector3(0f, 1.5f, 15f), new Vector3(30f, 3f, 0.5f));
            AddBox(root, wall, "Wall_S", new Vector3(0f, 1.5f, -15f), new Vector3(30f, 3f, 0.5f));
            AddBox(root, wall, "Wall_E", new Vector3(15f, 1.5f, 0f), new Vector3(0.5f, 3f, 30f));
            AddBox(root, wall, "Wall_W", new Vector3(-15f, 1.5f, 0f), new Vector3(0.5f, 3f, 30f));

            // 쥐 터널 내벽 4변 (구멍 뚫린 세그먼트) — 기획서 §8 v2: 맵 = 외벽·터널·치즈존만
            BuildTunnelWall(root, tunnelWall, "N", true, TUNNEL_WALL_POS, HOLES_NORTH);
            BuildTunnelWall(root, tunnelWall, "S", true, -TUNNEL_WALL_POS, HOLES_SOUTH);
            BuildTunnelWall(root, tunnelWall, "E", false, TUNNEL_WALL_POS, HOLES_EAST);
            BuildTunnelWall(root, tunnelWall, "W", false, -TUNNEL_WALL_POS, HOLES_WEST);
        }

        // 내벽 한 변: 구멍(폭 0.6)마다 벽을 끊고 인방(y 0.6~1.2)을 얹는다.
        // 쥐 CC(높이 0.5·Ø0.44)만 구멍 통과 — 고양이(0.9·Ø0.7)는 위·옆 다 걸림. 별도 로직 없음.
        private static void BuildTunnelWall(
            GameObject parent, Material material, string side, bool horizontal, float fixedCoord, float[] holes)
        {
            const float EXTENT = 13.2f; // 변 길이 절반 (모서리에서 만나 링이 닫힘)
            System.Array.Sort(holes);

            float cursor = -EXTENT;
            for (int i = 0; i <= holes.Length; i++)
            {
                float segEnd = i < holes.Length ? holes[i] - HOLE_WIDTH * 0.5f : EXTENT;
                float length = segEnd - cursor;
                if (length > 0.01f)
                {
                    float center = (cursor + segEnd) * 0.5f;
                    AddBox(parent, material, $"Tunnel_{side}_Seg{i}",
                        horizontal ? new Vector3(center, TUNNEL_WALL_HEIGHT * 0.5f, fixedCoord)
                                   : new Vector3(fixedCoord, TUNNEL_WALL_HEIGHT * 0.5f, center),
                        horizontal ? new Vector3(length, TUNNEL_WALL_HEIGHT, 0.4f)
                                   : new Vector3(0.4f, TUNNEL_WALL_HEIGHT, length));
                }

                if (i < holes.Length)
                {
                    // 인방 — 구멍 위(y 0.6~1.2)를 막아 고양이 통과 차단
                    float lintelHeight = TUNNEL_WALL_HEIGHT - HOLE_HEIGHT;
                    AddBox(parent, material, $"Tunnel_{side}_Lintel{i}",
                        horizontal ? new Vector3(holes[i], HOLE_HEIGHT + lintelHeight * 0.5f, fixedCoord)
                                   : new Vector3(fixedCoord, HOLE_HEIGHT + lintelHeight * 0.5f, holes[i]),
                        horizontal ? new Vector3(HOLE_WIDTH, lintelHeight, 0.4f)
                                   : new Vector3(0.4f, lintelHeight, HOLE_WIDTH));
                    cursor = holes[i] + HOLE_WIDTH * 0.5f;
                }
            }
        }

        // 치즈 지점(v2) — 등급 = 가장 가까운 쥐구멍까지의 거리. 터널 내부(±13.2~15)에는 두지 않음(수입 0 원칙).
        // G1 (3,1) r2는 QA 고정 (클론 넛지 TestMpNudgeLocalPlayer가 스폰(1,0,0)에서 도달하는 지점).
        private static readonly (Vector3 pos, int grade)[] CHEESE_SPOT_DEFS =
        {
            (new Vector3(3f, 0f, 1f), 1),        // QA — 중앙이지만 r2 (기존 절차 유지)
            (new Vector3(-5f, 0f, -12f), 2),     // 남쪽 구멍(-5) 코앞
            (new Vector3(7f, 0f, -12f), 3),      // 남쪽 구멍(7) 코앞
            (new Vector3(-12f, 0f, 6f), 4),      // 서쪽 구멍(6) 옆
            (new Vector3(12f, 0f, -6f), 5),      // 동쪽 구멍(-6) 옆
            (new Vector3(-5f, 0f, 5f), 6),       // 중앙 서쪽 — 구멍까지 ~8m
            (new Vector3(0f, 0f, -3f), 7),       // 정중앙 — 최고 위험
        };

        // 등급별 디스크 색 — 위험도 가독성 (1~3 노랑 / 4~5 주황 / 6~7 빨강)
        private static Material GradeDiscMaterial(int grade)
        {
            string name = grade >= 6 ? "Red" : grade >= 4 ? "Orange" : "Yellow";
            return GridboxMaterial(name);
        }

        private static void BuildCheeseSpots()
        {
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
