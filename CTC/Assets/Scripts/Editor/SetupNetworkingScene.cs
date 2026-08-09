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
    /// 씬·프리팹 생성기 진입점. 1218줄 한 덩어리를 개편 4단계(2026-07-31)에서 partial 6파일로 갈랐다 —
    /// 프리팹 수치를 고칠 때와 UI를 고칠 때 같은 파일에서 부딪히던 문제.
    /// 나머지는 Setup/ 아래: PlayerPrefabFactory · StructurePrefabFactory · SceneComposer ·
    /// LobbyCanvasBuilder · EditorAssetUtil.
    /// </summary>
    public static partial class SetupNetworkingScene
    {
        private const string PrefabDir = "Assets/Prefabs";

        private const string MaterialsDir = "Assets/Materials";

        private const string CatPrefabPath = PrefabDir + "/PlayerCat.prefab";

        private const string MousePrefabPath = PrefabDir + "/PlayerMouse.prefab";

        private const string DecoyPrefabPath = PrefabDir + "/MouseDecoy.prefab";

        private const string FortressPrefabPath = PrefabDir + "/CheeseFortress.prefab";

        private const string WarehousePrefabPath = PrefabDir + "/CheeseWarehouse.prefab";

        // 벽감(v3.8) — 맵이 처음부터 들고 있는 부술 수 있는 벽과 그 안의 치즈
        private const string AlcovePrefabPath = PrefabDir + "/Alcove.prefab";

        private const string CheeseDropPrefabPath = PrefabDir + "/CheeseDrop.prefab";

        // 제3세력 강아지(v3.8 5번) — 벽감에서만 나온다
        private const string DogPrefabPath = PrefabDir + "/Dog.prefab";

        // 기본 맵의 벽감 수. 방 하나에 하나씩이라 방 수를 넘지 않는다.
        private const int ALCOVES_PER_MAP = 3;

        // 감옥 수(v3.8 4번). 하나면 고양이가 그 앞에 서 있는 게 최적해가 된다.
        private const int JAILS_PER_MAP = 2;

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
            // CSV를 고치고 바로 이 메뉴를 누르는 게 정상 흐름인데, InfoManager는 한 번 읽으면
            // 도메인 리로드까지 캐시를 들고 있다 → 옛 값을 프리팹·씬에 구워 버린다(2026-08-01 실측).
            CTC.Runtime.Info.InfoManager.Reload();

            int decoyLayer = EnsureLayer(DecoyLayers.DECOY);
            int mouseLayer = EnsureLayer(DecoyLayers.MOUSE);
            BakeDecoyMatrix(decoyLayer, mouseLayer);
            Material catMat = CreateUrpMaterial("M_Cat_URP", CatSourceMatPath);
            Material mouseMat = CreateUrpMaterial("M_Mouse_URP", MouseSourceMatPath);

            // 설치형 프리팹을 먼저 생성 — 플레이어 프리팹(ShopController)이 참조한다
            GameObject decoyPrefab = CreateStructurePrefab(
                "MouseDecoy", DecoyPrefabPath, PrimitiveType.Capsule,
                mouseMat, _maxHp: 1,
                _scale: new Vector3(0.4f, 0.25f, 0.4f),
                _addExtras: _root =>
                {
                    _root.AddComponent<DecoyLifetime>();
                    // 미끼의 값어치는 생김새가 아니라 소리다 — 진짜 쥐와 같은 발소리를 같은
                    // 반경(8m)으로 계속 흘려 고양이를 끌어당긴다. 소리 없는 캡슐은 아무도
                    // 속이지 못했다("미끼가 의미가 없다" 2026-07-28).
                    var lure = _root.AddComponent<AudioSource>();
                    lure.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/Audio/Movement/sfx_mouse_footstep.ogg");
                    lure.loop = true;
                    lure.playOnAwake = true;
                    lure.volume = 0.6f;
                    lure.spatialBlend = 1f;
                    lure.rolloffMode = AudioRolloffMode.Linear;
                    lure.minDistance = 1.5f;
                    lure.maxDistance = 8f;
                    AddVolumeFollower(_root, CTC.Runtime.Audio.AudioVolumeFollower.Category.Sfx);
                },
                // 쥐만 통과시키려면 미끼가 자기 레이어에 있어야 한다 — Mouse×Decoy 충돌 매트릭스 대상(DecoyLayers).
                // (`excludeLayers`는 CharacterController에 안 먹혀서 폐기된 접근이다)
                _layer: decoyLayer);

            GameObject fortressPrefab = CreateFortressPrefab();

            // 벽감과 그 안의 치즈 — 부수는 행위가 v3.8 콘텐츠의 유일한 입구다
            GameObject cheeseDropPrefab = CreateCheeseDropPrefab();
            GameObject dogPrefab = CreateDogPrefab();
            GameObject alcovePrefab = CreateAlcovePrefab(cheeseDropPrefab, dogPrefab);

            // 창고(v3.4): 세운 쥐에게 초당 생산. 요새(방어)와 역할을 가르려고 HP는 절반.
            GameObject warehousePrefab = CreateStructurePrefab(
                "CheeseWarehouse", WarehousePrefabPath, PrimitiveType.Cube,
                GridboxMaterial("Yellow"), _maxHp: ShopLogic.WAREHOUSE_MAX_HP,
                _scale: new Vector3(1f, 0.8f, 1f),
                _addExtras: _root =>
                {
                    var production = _root.AddComponent<WarehouseProduction>();
                    SetRef(production, "m_ProduceVfx", CreateProduceVfx(_root));
                });

            // 플레이어 수치는 CSV(PlayerInfo.csv)가 원본 — 이 생성기는 굽기만 한다(2026-07-31).
            // 이속·HP·FOV를 바꾸려면 CSV만 고치고 이 메뉴를 다시 돌리면 된다.
            var catInfo = CTC.Runtime.Info.InfoManager.Instance.PlayerOf(RoleId.Cat);
            var mouseInfo = CTC.Runtime.Info.InfoManager.Instance.PlayerOf(RoleId.Mouse);

            GameObject catPrefab = CreatePlayerPrefab(
                "PlayerCat", CatPrefabPath, CatModelPath, catMat,
                _ccHeight: catInfo.CcHeight, _ccRadius: catInfo.CcRadius,
                // 원작: 술래가 먹이보다 빠름. camDistance는 쥐와 동일한 3 —
                // 5는 좁은 방에서 카메라가 상시 벽 밖이었다(2026-07-28). 넓은 시야는 FOV로 표현.
                _moveSpeed: catInfo.MoveSpeed, _runThreshold: catInfo.RunThreshold,
                _fov: catInfo.Fov, _camDistance: catInfo.CamDistance,
                _role: RoleId.Cat, _maxHp: catInfo.MaxHp,
                _decoyPrefab: decoyPrefab, _fortressPrefab: fortressPrefab, _warehousePrefab: warehousePrefab);

            GameObject mousePrefab = CreatePlayerPrefab(
                "PlayerMouse", MousePrefabPath, MouseModelPath, mouseMat,
                _ccHeight: mouseInfo.CcHeight, _ccRadius: mouseInfo.CcRadius,
                _moveSpeed: mouseInfo.MoveSpeed, _runThreshold: mouseInfo.RunThreshold,
                _fov: mouseInfo.Fov, _camDistance: mouseInfo.CamDistance,
                _role: RoleId.Mouse, _maxHp: mouseInfo.MaxHp,
                _decoyPrefab: decoyPrefab, _fortressPrefab: fortressPrefab, _warehousePrefab: warehousePrefab,
                // 쥐만 전용 레이어 — 미끼(Decoy)와의 충돌을 매트릭스에서 끊기 위해서다(DecoyLayers).
                // 고양이는 Default에 남아 미끼에 부딪힌다.
                _layer: mouseLayer);

            // 레이아웃마다 벽감 자리를 덧붙인다(지형은 그대로 — 시드를 흔들지 않는다)
            foreach (string path in MapLayoutPaths)
            {
                MapLayout layout = AssetDatabase.LoadAssetAtPath<MapLayout>(path);
                PlanAlcovesInto(layout, ALCOVES_PER_MAP);
                PlanJailsInto(layout, JAILS_PER_MAP);
            }
            AssetDatabase.SaveAssets();

            CreateGameScene(catPrefab, mousePrefab, fortressPrefab, decoyPrefab, warehousePrefab,
                alcovePrefab, cheeseDropPrefab, dogPrefab);
            Debug.Log($"[SetupNetworkingScene] 완료 — 프리팹 8종 + Game.unity 재생성 (맵 {MapLayoutPaths.Length}종, 런타임 생성)");
        }
    }
}
