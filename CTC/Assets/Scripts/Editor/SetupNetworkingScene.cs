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
        private const string WarehousePrefabPath = PrefabDir + "/CheeseWarehouse.prefab";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        // 로비에서 고를 수 있는 맵들 — 배열 순서가 곧 맵 인덱스다
        private static readonly string[] MapLayoutPaths =
        {
            "Assets/Prefabs/Maps/Layout_Sketch01.asset",
            "Assets/Prefabs/Maps/Layout_Expanded01.asset",
        };

        // 아직 굽지 않은 레이아웃은 건너뛴다 — 확장 맵 생성 전에도 Setup이 돌아야 한다
        private static Object[] LoadMapLayouts()
        {
            var list = new System.Collections.Generic.List<Object>();
            foreach (string path in MapLayoutPaths)
            {
                var layout = AssetDatabase.LoadAssetAtPath<MapLayout>(path);
                if (layout != null) list.Add(layout);
                else Debug.LogWarning($"[SetupNetworkingScene] MapLayout 없음(건너뜀): {path}");
            }
            if (list.Count == 0) Debug.LogError("[SetupNetworkingScene] 맵 레이아웃이 하나도 없다 — Generate Map Layout 먼저");
            return list.ToArray();
        }

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

            // 창고(v3.4): 세운 쥐에게 초당 생산. 요새(방어)와 역할을 가르려고 HP는 절반.
            GameObject warehousePrefab = CreateStructurePrefab(
                "CheeseWarehouse", WarehousePrefabPath, PrimitiveType.Cube,
                GridboxMaterial("Yellow"), maxHp: ShopLogic.WAREHOUSE_MAX_HP,
                scale: new Vector3(1f, 0.8f, 1f),
                addExtras: _root =>
                {
                    var production = _root.AddComponent<WarehouseProduction>();
                    SetRef(production, "m_ProduceVfx", CreateProduceVfx(_root));
                });

            GameObject catPrefab = CreatePlayerPrefab(
                "PlayerCat", CatPrefabPath, CatModelPath, catMat,
                ccHeight: 0.9f, ccRadius: 0.35f,
                // 원작: 술래가 먹이보다 빠름 (쥐 6, 밸런스 2026-07-26 2차)
                // camDistance는 쥐와 동일한 3 — 5는 좁은 방에서 카메라가 상시 벽 밖이었다(2026-07-28).
                // 고양이의 넓은 시야는 거리가 아니라 FOV(70)로 표현한다.
                moveSpeed: 6.7f, runThreshold: 3.2f, fov: 70f, camDistance: 3f,
                role: RoleId.Cat, maxHp: 1000,
                decoyPrefab: decoyPrefab, fortressPrefab: fortressPrefab, warehousePrefab: warehousePrefab);

            GameObject mousePrefab = CreatePlayerPrefab(
                "PlayerMouse", MousePrefabPath, MouseModelPath, mouseMat,
                ccHeight: 0.5f, ccRadius: 0.22f,
                moveSpeed: 6f, runThreshold: 4.2f, fov: 55f, camDistance: 3f,
                role: RoleId.Mouse, maxHp: 100,
                decoyPrefab: decoyPrefab, fortressPrefab: fortressPrefab, warehousePrefab: warehousePrefab);

            CreateGameScene(catPrefab, mousePrefab, fortressPrefab, decoyPrefab, warehousePrefab);
            Debug.Log($"[SetupNetworkingScene] 완료 — 프리팹 5종 + Game.unity 재생성 (맵 {MapLayoutPaths.Length}종, 런타임 생성)");
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
                // 프리미티브 기본 높이가 캡슐·구·실린더는 2, 큐브는 1 — 밑면을 바닥(y=0)에 맞춘다
                float unitHalfHeight = primitive == PrimitiveType.Cube ? 0.5f : 1f;
                body.transform.localPosition = new Vector3(0f, scale.y * unitHalfHeight, 0f);
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

        // 상점 핫바 슬롯 치수(하단 중앙)
        private const float SLOT_WIDTH = 132f;
        private const float SLOT_HEIGHT = 68f;
        private const float SLOT_GAP = 8f;

        // 창고 생산 연출 — 지붕에서 노란 치즈 조각이 튀어오른다. 서버 지급 순간에 Play()로 1회 분출.
        // 멀리서도 "저기서 벌고 있다"가 보여야 해서 위로 1m 가까이 솟는다(소리 가청 15m와 짝).
        private static ParticleSystem CreateProduceVfx(GameObject _root)
        {
            var vfxGo = new GameObject("ProduceVfx");
            vfxGo.transform.SetParent(_root.transform, false);
            vfxGo.transform.localPosition = new Vector3(0f, 0.85f, 0f); // 큐브(높이 0.8) 지붕 바로 위

            var ps = vfxGo.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.6f;
            main.startSpeed = 2.2f;
            main.startSize = 0.12f;
            main.startColor = new Color(1f, 0.85f, 0.2f);
            main.gravityModifier = 1.6f;
            main.maxParticles = 32;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 위로

            var renderer = vfxGo.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GridboxMaterial("Yellow");
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = PrimitiveMesh(PrimitiveType.Cube);

            return ps;
        }

        // 프리미티브 메시만 필요할 때 — 임시 오브젝트를 만들어 메시만 빼고 버린다.
        private static Mesh PrimitiveMesh(PrimitiveType _type)
        {
            GameObject temp = GameObject.CreatePrimitive(_type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        private static GameObject CreateFortressPrefab()
        {
            Material wallMat = GridboxMaterial("Brown");
            var root = new GameObject("CheeseFortress");
            try
            {
                root.AddComponent<NetworkObject>();

                var health = root.AddComponent<Health>();
                SetInt(health, "m_MaxHp", ShopLogic.FORTRESS_HP);

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
            GameObject decoyPrefab, GameObject fortressPrefab, GameObject warehousePrefab)
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
                SetRef(shop, "m_WarehousePrefab", warehousePrefab);

                if (role == RoleId.Cat) root.AddComponent<CatPounce>();
                else
                {
                    root.AddComponent<MousePopgun>();
                    root.AddComponent<MouseDash>();
                    root.AddComponent<CTC.Runtime.Audio.FarmingSfx>(); // 수입 짤랑 3D 공개음(20m)
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
            GameObject catPrefab, GameObject mousePrefab, GameObject fortressPrefab, GameObject decoyPrefab,
            GameObject warehousePrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // v3.5: 맵 지오메트리는 더 이상 여기서 굽지 않는다. 로비에서 고른 레이아웃으로
            // MapBuilder가 런타임에 짓는다 — 바닥·경계벽도 레이아웃의 HalfExtent에서 나온다.
            var mapBuilderGo = new GameObject("MapBuilder");
            var mapBuilder = mapBuilderGo.AddComponent<MapBuilder>();
            SetRef(mapBuilder, "m_WallMaterial", GridboxMaterial("Grey2"));
            SetRef(mapBuilder, "m_FloorMaterial", GridboxMaterial("Grey4"));
            SetRef(mapBuilder, "m_GroundMaterial", GridboxMaterial("Grey1"));
            SetRef(mapBuilder, "m_DiscHighMaterial", GridboxMaterial("Red"));
            SetRef(mapBuilder, "m_DiscMidMaterial", GridboxMaterial("Orange"));
            SetRef(mapBuilder, "m_DiscLowMaterial", GridboxMaterial("Yellow"));

            // NetworkManager — 자동 플레이어 스폰 없음(로비가 역할별로 스폰)
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7788);

            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = null;
            RegisterNetworkPrefabs(nm, catPrefab, mousePrefab, fortressPrefab, decoyPrefab, warehousePrefab);

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
            // 로비에서 고를 맵 목록 — 요새자리·스폰 공식이 선택된 레이아웃을 읽는다
            SetRefArray(lobby, "m_MapLayouts", LoadMapLayouts());
            SetRef(lobby, "m_MapBuilder", mapBuilder);
            var match = lobbyGo.AddComponent<MatchManager>(); // 동승 — 새 in-scene NetworkObject 없음
            SetRef(match, "m_Lobby", lobby);

            // 치즈 고갈 감독 — 지점 상태를 NetworkList로 복제하고, 매치 시작 시 매장량 복구,
            // 전 지점 고갈 시 최고 등급 1곳만 부활시켜 후반 결전지를 만든다
            var cheeseDirector = lobbyGo.AddComponent<CheeseSpotDirector>();
            SetRef(cheeseDirector, "m_Lobby", lobby);
            SetRef(cheeseDirector, "m_Match", match);
            SetRef(lobby, "m_CheeseDirector", cheeseDirector);
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
            // 역할별 덮어쓰기는 PlayerCameraBinder — 두 역할 모두 3으로 통일했다(2026-07-28)
            orbital.Radius = 3f;
            orbital.TargetOffset = new Vector3(0f, 0.7f, 0f);
            orbital.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbital.HorizontalAxis.Wrap = true;
            orbital.VerticalAxis.Range = new Vector2(-30f, 70f);
            orbital.VerticalAxis.Value = 20f;
            camGo.AddComponent<CinemachineRotationComposer>();

            // 지형-카메라 처리 (2026-07-28 개정): Deoccluder(당김) → Decollider(침투 방지) + OccluderFader(페이드).
            //   · 구 Deoccluder는 방(4~7m)·복도(1.6m)에서 차폐가 붙었다 떨어졌다 하며 진동하고,
            //     MinimumDistanceFromTarget 0.5까지 당겨 화면이 좁아졌다(플레이 피드백).
            //   · "Decollider 단독은 벽 너머에서 플레이어가 안 보인다"는 과거 결론은 맞다 —
            //     그 빈틈을 채우는 게 OccluderFader(가리는 벽만 반투명). 둘을 합쳐야 완성형이다.
            // 스크립트 AddComponent는 Reset이 안 불려 struct가 0(비활성)이 되므로 명시 설정.
            var decollider = camGo.AddComponent<CinemachineDecollider>();
            decollider.CameraRadius = 0.2f;
            decollider.Decollision = new CinemachineDecollider.DecollisionSettings
            {
                Enabled = true,
                ObstacleLayers = 1, // Default — 바닥은 Ignore Raycast(2)라 제외된다
                UseFollowTarget = new CinemachineDecollider.DecollisionSettings.FollowTargetSettings
                {
                    Enabled = true,
                    YOffset = 0.7f, // OrbitalFollow.TargetOffset과 동일 — 발밑이 아니라 몸통 기준
                },
                Damping = 0.3f,
                SmoothingTime = 0.2f,
            };

            camGo.AddComponent<OccluderFader>();
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
                .Append(warehousePrefab.GetComponent<NetworkObject>())
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
            panelRt.sizeDelta = new Vector2(480f, 620f); // 맵 선택 줄이 늘어 세로 확장
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.12f, 0.85f);

            TMP_Text title = CreateTmpText("Title", panel.transform, "Cheese Thief Lobby",
                32f, TextAlignmentOptions.Center, new Vector2(0f, -20f), new Vector2(440f, 50f));
            title.fontStyle = FontStyles.Bold;

            TMP_Text playerList = CreateTmpText("PlayerList", panel.transform, string.Empty,
                22f, TextAlignmentOptions.TopLeft, new Vector2(0f, -90f), new Vector2(420f, 180f));

            // 맵 선택 — 방장만 조작. 버튼 수는 MapLayoutPaths와 같다(비어 있으면 LobbyUI가 숨긴다)
            TMP_Text mapLabel = CreateTmpText("MapLabel", panel.transform, "맵",
                22f, TextAlignmentOptions.Center, new Vector2(0f, -286f), new Vector2(420f, 30f));
            // 라벨은 런타임에 LobbyUI가 MapLayout.DisplayName으로 덮어쓴다 — 여기선 자리만 잡는다.
            // 폭(186)+간격(8)에 맞춰 배치해야 두 버튼이 겹치지 않는다.
            var mapButtons = new Object[MapLayoutPaths.Length];
            const float MAP_BUTTON_WIDTH = 186f;
            for (int i = 0; i < MapLayoutPaths.Length; i++)
            {
                float x = (i - (MapLayoutPaths.Length - 1) * 0.5f) * (MAP_BUTTON_WIDTH + 8f);
                mapButtons[i] = CreateMuipButton(panel.transform, $"MapButton{i + 1}",
                    "-", new Vector2(x, -330f), MAP_BUTTON_WIDTH);
            }

            Button mouseButton = CreateMuipButton(panel.transform, "MouseButton", "Play as Mouse", new Vector2(0f, -400f));
            Button catButton = CreateMuipButton(panel.transform, "CatButton", "Play as Cat", new Vector2(0f, -470f));
            Button startButton = CreateMuipButton(panel.transform, "StartButton", "Start Game", new Vector2(0f, -550f));

            var ui = canvasGo.AddComponent<LobbyUI>();
            SetRef(ui, "lobby", lobby);
            SetRef(ui, "panel", panel);
            SetRef(ui, "playerListText", playerList);
            SetRef(ui, "mouseButton", mouseButton);
            SetRef(ui, "catButton", catButton);
            SetRef(ui, "startButton", startButton);
            SetRef(ui, "mapLabel", mapLabel);
            SetRefArray(ui, "mapButtons", mapButtons);

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

            // 상점 핫바 (하단 중앙 상시 표시, 숫자키 즉시 구매 — UX 개정 2026-07-27)
            var hotbarRoot = new GameObject("ShopHotbar", typeof(RectTransform));
            hotbarRoot.transform.SetParent(canvasGo.transform, false);
            var hotbarRt = (RectTransform)hotbarRoot.transform;
            hotbarRt.anchorMin = hotbarRt.anchorMax = new Vector2(0.5f, 0f);
            hotbarRt.pivot = new Vector2(0.5f, 0f);
            hotbarRt.anchoredPosition = new Vector2(0f, 28f);
            hotbarRt.sizeDelta = new Vector2(SLOT_WIDTH * ShopLogic.HOTBAR_SLOTS + SLOT_GAP * 4f, SLOT_HEIGHT);

            var slotRoots = new Object[ShopLogic.HOTBAR_SLOTS];
            var slotLabels = new Object[ShopLogic.HOTBAR_SLOTS];
            var slotCosts = new Object[ShopLogic.HOTBAR_SLOTS];
            float totalWidth = SLOT_WIDTH * ShopLogic.HOTBAR_SLOTS + SLOT_GAP * (ShopLogic.HOTBAR_SLOTS - 1);
            for (int i = 0; i < ShopLogic.HOTBAR_SLOTS; i++)
            {
                float x = -totalWidth * 0.5f + SLOT_WIDTH * 0.5f + i * (SLOT_WIDTH + SLOT_GAP);
                var slot = new GameObject($"Slot{i + 1}", typeof(RectTransform));
                slot.transform.SetParent(hotbarRoot.transform, false);
                var slotRt = (RectTransform)slot.transform;
                slotRt.anchorMin = slotRt.anchorMax = new Vector2(0.5f, 0.5f);
                slotRt.pivot = new Vector2(0.5f, 0.5f);
                slotRt.anchoredPosition = new Vector2(x, 0f);
                slotRt.sizeDelta = new Vector2(SLOT_WIDTH, SLOT_HEIGHT);
                var slotBg = slot.AddComponent<Image>();
                slotBg.color = new Color(0.07f, 0.055f, 0.04f, 0.82f);

                slotLabels[i] = CreateTmpText($"Name{i + 1}", slot.transform, "-",
                    22f, TextAlignmentOptions.Center, new Vector2(0f, -6f), new Vector2(SLOT_WIDTH - 8f, 28f));
                slotCosts[i] = CreateTmpText($"Cost{i + 1}", slot.transform, "-",
                    20f, TextAlignmentOptions.Center, new Vector2(0f, -34f), new Vector2(SLOT_WIDTH - 8f, 26f));
                slotRoots[i] = slot;
            }

            TMP_Text toastText = CreateTmpText("ShopToast", hotbarRoot.transform, "",
                26f, TextAlignmentOptions.Center, new Vector2(0f, 52f), new Vector2(640f, 36f));
            toastText.gameObject.SetActive(false);
            hotbarRoot.SetActive(false); // ShopHotbar가 게임 시작 시 켬

            var hotbar = canvasGo.AddComponent<ShopHotbar>();
            SetRef(hotbar, "m_Lobby", lobby);
            SetRef(hotbar, "m_Root", hotbarRoot);
            SetRefArray(hotbar, "m_Slots", slotRoots);
            SetRefArray(hotbar, "m_SlotLabels", slotLabels);
            SetRefArray(hotbar, "m_SlotCosts", slotCosts);
            SetRef(hotbar, "m_Toast", toastText);

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

            // 조작키 + 핫바 항목 설명 (좌하단 — 역할별, ControlsHud가 게임 중 표시).
            // 쥐는 6항목이라 세로가 길다 — 높이를 넉넉히 잡는다.
            var controlsTextGo = new GameObject("ControlsText");
            controlsTextGo.transform.SetParent(canvasGo.transform, false);
            var controlsText = controlsTextGo.AddComponent<TextMeshProUGUI>();
            controlsText.text = string.Empty;
            controlsText.fontSize = 19f;
            controlsText.lineSpacing = 12f;
            controlsText.alignment = TextAlignmentOptions.BottomLeft;
            controlsText.color = new Color(1f, 1f, 1f, 0.85f);
            var controlsRect = controlsTextGo.GetComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(0f, 0f);
            controlsRect.anchorMax = new Vector2(0f, 0f);
            controlsRect.pivot = new Vector2(0f, 0f);
            controlsRect.anchoredPosition = new Vector2(24f, 20f);
            controlsRect.sizeDelta = new Vector2(900f, 220f);
            controlsTextGo.SetActive(false);

            var controlsHud = canvasGo.AddComponent<ControlsHud>();
            SetRef(controlsHud, "m_Lobby", lobby);
            SetRef(controlsHud, "m_Text", controlsText);

            // 맵 설명 (좌하단 — 로비에서만. 게임이 시작되면 같은 자리를 조작키가 채운다)
            var mapInfoGo = new GameObject("MapInfoText");
            mapInfoGo.transform.SetParent(canvasGo.transform, false);
            var mapInfoText = mapInfoGo.AddComponent<TextMeshProUGUI>();
            mapInfoText.text = string.Empty;
            mapInfoText.fontSize = 20f;
            mapInfoText.alignment = TextAlignmentOptions.BottomLeft;
            mapInfoText.lineSpacing = 8f;
            var mapInfoRect = mapInfoGo.GetComponent<RectTransform>();
            mapInfoRect.anchorMin = mapInfoRect.anchorMax = new Vector2(0f, 0f);
            mapInfoRect.pivot = new Vector2(0f, 0f);
            mapInfoRect.anchoredPosition = new Vector2(24f, 20f);
            mapInfoRect.sizeDelta = new Vector2(560f, 130f);
            mapInfoGo.SetActive(false);

            var mapInfoHud = canvasGo.AddComponent<MapInfoHud>();
            SetRef(mapInfoHud, "m_Lobby", lobby);
            SetRef(mapInfoHud, "m_Text", mapInfoText);

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
                // 파밍 짤랑음과 구분되는 별도 음원 — 나무 쿵 + 짧은 동전 챙(생산 완료감).
                // 고양이가 소리만 듣고 "쥐가 파밍 중"과 "창고가 돌고 있다"를 구별해야 한다.
                LoadClip("Economy/sfx_warehouse_tick"),  // WarehouseTick
                LoadClip("Economy/sfx_cheese_depleted"), // CheeseDepleted
            });

            var matchSfx = sfxGo.AddComponent<CTC.Runtime.Audio.MatchSfx>();
            SetRef(matchSfx, "m_Lobby", lobby);
            SetRef(matchSfx, "m_Match", match);

            // 인게임 디버그 콘솔(yasirkula, MIT) — 빌드에서 로그 확인용. 기본 최소화, ` 키 토글.
            // 원격 테스터가 [SteamLobby]/[Lobby] 로그를 화면에서 바로 볼 수 있어야 원인 특정이 된다.
            var consolePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.yasirkula.ingamedebugconsole/Plugins/IngameDebugConsole/IngameDebugConsole.prefab");
            if (consolePrefab != null)
            {
                var console = (GameObject)PrefabUtility.InstantiatePrefab(consolePrefab);
                console.name = "IngameDebugConsole";
                foreach (MonoBehaviour mb in console.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb.GetType().Name != "DebugLogManager") continue;
                    var so = new SerializedObject(mb);
                    SerializedProperty toggle = so.FindProperty("toggleWithKey");
                    if (toggle != null) toggle.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else
            {
                Debug.LogWarning("[Setup] IngameDebugConsole 프리팹 없음 — 패키지 확인");
            }

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
        private static Button CreateMuipButton(
            Transform parent, string name, string label, Vector2 anchoredPos, float width = 380f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(width, 56f);

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
