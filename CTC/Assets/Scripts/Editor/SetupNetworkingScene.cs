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


        private const string FortressPrefabPath = PrefabDir + "/CheeseFortress.prefab";

        private const string WarehousePrefabPath = PrefabDir + "/CheeseWarehouse.prefab";

        // 벽감(v3.8) — 맵이 처음부터 들고 있는 부술 수 있는 벽과 그 안의 치즈
        private const string AlcovePrefabPath = PrefabDir + "/Alcove.prefab";

        private const string CheeseDropPrefabPath = PrefabDir + "/CheeseDrop.prefab";

        // 제3세력 강아지(v3.8 5번) — 벽감에서만 나온다
        private const string DogPrefabPath = PrefabDir + "/Dog.prefab";

        // 상자(v3.9) — 부수면 안에 든 것이 나온다
        private const string LootBoxPrefabPath = PrefabDir + "/LootBox.prefab";

        // 직업권(v3.9) — 상자에서 나온다. LootId 순서와 1:1로 상자에 배선된다.
        private const string HeroTicketPrefabPath = PrefabDir + "/HeroTicket.prefab";
        private const string GangTicketPrefabPath = PrefabDir + "/GangTicket.prefab";

        // 바닥에 놓는 상자는 **없다**(2026-08-11). 상자는 전부 벽 뒤에 있고, 벽을 부숴야 드러난다 —
        // 직업권이 바닥에 굴러다니면 판을 뒤집는 물건이 아무 소리 없이 손에 들어간다.
        private const int BOXES_PER_MAP = 0;

        // 벽감 수는 맵 크기를 따라간다(2026-08-10, 120×120 대응).
        // 고정값이면 대형 맵(49방)에서 벽감 3개가 사실상 없는 것과 같아진다.
        // 하한이 4인 이유(2026-08-11): 강아지 1 + 직업권 2를 빼고도 **치즈 벽이 하나는 남아야** 한다.
        // 3개면 작은 맵에 "달려가서 뺏는 치즈"가 통째로 없어진다.
        private const int ROOMS_PER_ALCOVE = 8;   // 49방 → 6개, 9방 → 4개(하한)

        private static int AlcoveCountFor(MapLayout _layout) =>
            Mathf.Clamp(_layout.Rooms.Count / ROOMS_PER_ALCOVE, 4, 8);

        private const string GameScenePath = "Assets/Scenes/Game.unity";

        /// <summary>봇 전용 실험실 씬 — 사람 없이 봇만 도는 판(2026-08-18).</summary>
        private const string BotLabScenePath = "Assets/Scenes/BotLab.unity";

        // 로비에서 고를 수 있는 맵들 — 배열 순서가 곧 맵 인덱스다
        private static readonly string[] MapLayoutPaths =
        {
            "Assets/Prefabs/Maps/Layout_Sketch01.asset",
            "Assets/Prefabs/Maps/Layout_Expanded01.asset",
            "Assets/Prefabs/Maps/Layout_Large01.asset",
            "Assets/Prefabs/Maps/Layout_Warehouse01.asset",   // 창고 평면도(2026-09-07) — 손 배치
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

            // 쥐 전용 레이어 — 원래 미끼(Decoy) 충돌 매트릭스용이었다. 미끼는 2026-08-15 삭제됐지만
            // 프리팹 레이어를 되돌릴 이유가 없어(OccluderFader 등 마스크 전제) 그대로 둔다.
            int mouseLayer = EnsureLayer(MOUSE_LAYER);
            Material catMat = CreateUrpMaterial("M_Cat_URP", CatSourceMatPath);
            Material mouseMat = CreateUrpMaterial("M_Mouse_URP", MouseSourceMatPath);

            // 설치형 프리팹을 먼저 생성 — 플레이어 프리팹(ShopController)이 참조한다
            GameObject fortressPrefab = CreateFortressPrefab();

            // 벽감과 그 안의 치즈 — 부수는 행위가 v3.8 콘텐츠의 유일한 입구다
            GameObject cheeseDropPrefab = CreateCheeseDropPrefab();
            GameObject dogPrefab = CreateDogPrefab();
            GameObject heroTicket = CreateJobTicketPrefab(
                "HeroTicket", HeroTicketPrefabPath, CTC.Runtime.Match.MouseJobId.Hero, "Blue2",
                PropAssets.HERO_TICKET_MATERIAL);
            GameObject gangTicket = CreateJobTicketPrefab(
                "GangTicket", GangTicketPrefabPath, CTC.Runtime.Match.MouseJobId.Gang, "Purple",
                PropAssets.GANG_TICKET_MATERIAL);
            GameObject lootBoxPrefab = CreateLootBoxPrefab(cheeseDropPrefab, heroTicket, gangTicket);
            // 벽감은 상자를 참조한다(부수면 그 뒤에서 상자가 드러난다) — 순서가 중요하다
            GameObject alcovePrefab = CreateAlcovePrefab(lootBoxPrefab, dogPrefab);

            // 창고(v3.4): 세운 쥐에게 초당 생산. 요새(방어)와 역할을 가르려고 HP는 절반.
            GameObject warehousePrefab = CreateStructurePrefab(
                "CheeseWarehouse", WarehousePrefabPath, PrimitiveType.Cube,
                GridboxMaterial("Yellow"), _maxHp: ShopLogic.WAREHOUSE_MAX_HP,
                _scale: new Vector3(1f, 0.8f, 1f),
                _addExtras: _root =>
                {
                    var production = _root.AddComponent<WarehouseProduction>();
                    SetRef(production, "m_ProduceVfx", CreateProduceVfx(_root));
                    // 판 중에 세워지므로 구운 폴리곤에 없다 — 제 자리를 도려내야
                    // 길찾기가 창고를 통과하라고 답하지 않는다(2026-08-11 실측의 그 창고다)
                    _root.AddComponent<CTC.Runtime.Map.NavMeshCarver>();
                    // 격자에도 자국을 남긴다 — 고양이·강아지만 부순다(쥐 총알은 아군 구조물을 못 때린다)
                    var blocker = _root.AddComponent<CTC.Runtime.Ai.NavGridBlocker>();
                    SetInt(blocker, "m_Kind", (int)CTC.Runtime.Ai.NavStructureKind.CatBreakable);
                },
                // 한 조각이 빠진 치즈 바퀴로 보인다(2026-08-30) — 노란 큐브는 요새 벽과 구별이 안 됐다.
                // 콜라이더는 여전히 1×0.8×1 큐브다.
                _visualMesh: PropAssets.LoadMesh(PropAssets.WAREHOUSE_MESH),
                _visualMaterial: PropAssets.LoadMaterial(PropAssets.CHEESE_MATERIAL));

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
                _fortressPrefab: fortressPrefab, _warehousePrefab: warehousePrefab);

            GameObject mousePrefab = CreatePlayerPrefab(
                "PlayerMouse", MousePrefabPath, MouseModelPath, mouseMat,
                _ccHeight: mouseInfo.CcHeight, _ccRadius: mouseInfo.CcRadius,
                _moveSpeed: mouseInfo.MoveSpeed, _runThreshold: mouseInfo.RunThreshold,
                _fov: mouseInfo.Fov, _camDistance: mouseInfo.CamDistance,
                _role: RoleId.Mouse, _maxHp: mouseInfo.MaxHp,
                _fortressPrefab: fortressPrefab, _warehousePrefab: warehousePrefab,
                _layer: mouseLayer);

            // 레이아웃마다 벽감 자리를 덧붙인다(지형은 그대로 — 시드를 흔들지 않는다)
            foreach (string path in MapLayoutPaths)
            {
                MapLayout layout = AssetDatabase.LoadAssetAtPath<MapLayout>(path);
                if (layout == null) continue;
                if (layout.HandPlaced) continue;   // 창고 평면도는 좌표를 직접 든다 — 자동 계획 금지
                PlanAlcovesInto(layout, AlcoveCountFor(layout));
                PlanBoxesInto(layout, BOXES_PER_MAP);   // 벽감 자리를 피하므로 벽감 뒤에 온다
                PlanObstaclesInto(layout);              // 위의 것들을 전부 피하므로 마지막이다
            }
            AssetDatabase.SaveAssets();

            CreateGameScene(GameScenePath, _botLab: false,
                catPrefab, mousePrefab, fortressPrefab, warehousePrefab,
                alcovePrefab, cheeseDropPrefab, dogPrefab, lootBoxPrefab, heroTicket, gangTicket);
            Debug.Log($"[SetupNetworkingScene] 완료 — 프리팹 10종 + Game.unity 재생성 (맵 {MapLayoutPaths.Length}종, 런타임 생성)");
        }

        /// <summary>
        /// 봇 전용 실험실 씬을 만든다 (2026-08-18). **프리팹은 다시 굽지 않는다** —
        /// 이미 구워 둔 것을 불러 쓴다. 다시 구우면 레이아웃 에셋(벽감·상자·엄폐물 자리)이
        /// 덮여 Game.unity 쪽 측정 기준이 같이 흔들린다.
        ///
        /// 그래서 `Tools/CTC/Setup Networking Scene`을 **먼저** 한 번 돌린 상태여야 한다.
        /// 씬을 새로 만들면 in-scene NetworkObject 해시가 씬 GUID 기준으로 다시 굽히므로,
        /// MPPM 클론이 붙어 있었다면 `Tools/CTC/Dev/MPPM 클론 재시작`으로 옛 해시를 버려야 한다.
        /// </summary>
        [MenuItem("Tools/CTC/Setup BotLab Scene (봇 전용 실험실)")]
        public static void RunBotLab()
        {
            CTC.Runtime.Info.InfoManager.Reload();

            GameObject Load(string _path)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
                if (go == null) Debug.LogError($"[BotLab] 프리팹 없음: {_path} — 먼저 'Setup Networking Scene'을 돌려라");
                return go;
            }

            GameObject catPrefab = Load(CatPrefabPath);
            GameObject mousePrefab = Load(MousePrefabPath);
            GameObject fortressPrefab = Load(FortressPrefabPath);
            GameObject warehousePrefab = Load(WarehousePrefabPath);
            GameObject alcovePrefab = Load(AlcovePrefabPath);
            GameObject cheeseDropPrefab = Load(CheeseDropPrefabPath);
            GameObject dogPrefab = Load(DogPrefabPath);
            GameObject lootBoxPrefab = Load(LootBoxPrefabPath);
            GameObject heroTicket = Load(HeroTicketPrefabPath);
            GameObject gangTicket = Load(GangTicketPrefabPath);

            if (catPrefab == null || mousePrefab == null || fortressPrefab == null) return;

            CreateGameScene(BotLabScenePath, _botLab: true,
                catPrefab, mousePrefab, fortressPrefab, warehousePrefab,
                alcovePrefab, cheeseDropPrefab, dogPrefab, lootBoxPrefab, heroTicket, gangTicket);
            Debug.Log("[SetupNetworkingScene] BotLab.unity 생성 — 플레이하면 봇만 도는 판이 자동으로 열린다");
        }
    }
}
