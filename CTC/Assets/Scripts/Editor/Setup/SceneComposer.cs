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
    // SetupNetworkingScene의 일부 — 씬 오브젝트 배선 + 네트워크 프리팹 등록 (개편 4단계 분할, 2026-07-31)
    public static partial class SetupNetworkingScene
    {
        /// <summary>
        /// 씬을 통째로 짓는다. `_botLab`이면 **봇 전용 실험실**(BotLab.unity)이 된다 —
        /// 로비 UI·궤도 카메라·스팀/접속 UI를 안 만들고, 대신 자유 카메라와 경로선 그리기,
        /// 그리고 플레이 즉시 판을 여는 <see cref="CTC.Runtime.Bots.BotLab"/>을 얹는다.
        /// **한 함수로 둘 다 짓는 이유**: 두 벌로 나누면 배선이 서서히 어긋나 실험실에서 잰 수치가
        /// 본 게임과 다른 것을 재게 된다.
        /// </summary>
        private static void CreateGameScene(
            string _scenePath, bool _botLab,
            GameObject _catPrefab, GameObject _mousePrefab, GameObject _fortressPrefab,
            GameObject _warehousePrefab, GameObject _alcovePrefab, GameObject _cheeseDropPrefab,
            GameObject _dogPrefab, GameObject _lootBoxPrefab,
            GameObject _heroTicket, GameObject _gangTicket)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // v3.5: 맵 지오메트리는 더 이상 여기서 굽지 않는다. 로비에서 고른 레이아웃으로
            // MapBuilder가 런타임에 짓는다 — 바닥·경계벽도 레이아웃의 HalfExtent에서 나온다.
            var mapBuilderGo = new GameObject("MapBuilder");
            var mapBuilder = mapBuilderGo.AddComponent<MapBuilder>();
            // 시맨틱 교체(2026-08-31) — 팬트리 머티리얼이 없으면 Gridbox로 되돌아간다(마젠타 방지).
            Material roomWall = MapAssets.LoadMaterial(MapAssets.ROOM_WALL_MAT);
            Material roomFloor = MapAssets.LoadMaterial(MapAssets.ROOM_FLOOR_MAT);
            Material ground = MapAssets.LoadMaterial(MapAssets.GROUND_MAT);
            Material boundary = MapAssets.LoadMaterial(MapAssets.BOUNDARY_MAT);
            Material corridor = MapAssets.LoadMaterial(MapAssets.CORRIDOR_WALL_MAT);
            SetRef(mapBuilder, "m_WallMaterial", roomWall != null ? roomWall : GridboxMaterial("Grey2"));
            SetRef(mapBuilder, "m_FloorMaterial", roomFloor != null ? roomFloor : GridboxMaterial("Grey4"));
            SetRef(mapBuilder, "m_GroundMaterial", ground != null ? ground : GridboxMaterial("Grey1"));
            SetRef(mapBuilder, "m_BoundaryMaterial", boundary);
            SetRef(mapBuilder, "m_CorridorWallMaterial", corridor);
            SetRef(mapBuilder, "m_DiscHighMaterial", GridboxMaterial("Red"));      // 기능색 — 유지
            SetRef(mapBuilder, "m_DiscMidMaterial", GridboxMaterial("Orange"));
            SetRef(mapBuilder, "m_DiscLowMaterial", GridboxMaterial("Yellow"));

            // NetworkManager — 자동 플레이어 스폰 없음(로비가 역할별로 스폰)
            var nmGo = new GameObject("NetworkManager");
            // 대기실에서 넘어오면 이미 NetworkManager가 하나 살아 있다 — 그때는 이 오브젝트가
            // 통째로 두 번째가 되므로 스스로 사라진다(2026-08-30). 혼자 열면 아무 일도 안 한다.
            nmGo.AddComponent<NetworkRootGuard>();
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7788);

            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = null;
            // 구버전 클라이언트가 신버전 호스트에 붙는 것을 막는다 — 0이면 아무나 붙는다.
            // 씬에 굽히는 값이라 **버전을 올리면 씬을 다시 구워야** 반영된다.
            nm.NetworkConfig.ProtocolVersion = NetVersionLogic.Protocol(PlayerSettings.bundleVersion);
            RegisterNetworkPrefabs(nm, _catPrefab, _mousePrefab, _fortressPrefab, _warehousePrefab,
                _alcovePrefab, _cheeseDropPrefab, _dogPrefab, _lootBoxPrefab, _heroTicket, _gangTicket);

            // 스팀 매칭(Plan 13): Facepunch 트랜스포트 동승 — 기본은 UTP, 스팀 방 경로에서만 교체.
            // 실험실은 혼자 도는 판이라 접속·스팀·MPPM 배선을 전부 뺀다(화면도 로그도 깨끗해진다).
            if (!_botLab)
            {
                nmGo.AddComponent<Netcode.Transports.Facepunch.FacepunchTransport>();
                nmGo.AddComponent<SteamBootstrap>();
                nmGo.AddComponent<SteamLobbyManager>();

                nmGo.AddComponent<ConnectionUI>();
                nmGo.AddComponent<MppmAutoConnect>();
                nmGo.AddComponent<MultiplayerQaProbe>();
            }

            // 로비 (in-scene NetworkObject — 서버 시작 시 자동 스폰)
            var lobbyGo = new GameObject("LobbyController");
            lobbyGo.AddComponent<NetworkObject>();
            // 개편 3단계(2026-07-31): 상태·스폰·맵이 각각 별도 컴포넌트로 갈라져 한 오브젝트에 동승한다.
            // NetworkBehaviour를 여러 개 붙여도 RPC 발신자 식별은 각자 그대로 동작한다.
            lobbyGo.AddComponent<LobbySession>();                        // 상태(Model)
            var spawner = lobbyGo.AddComponent<PlayerSpawner>();         // 스폰(Worker)
            SetRef(spawner, "m_CatPrefab", _catPrefab);
            SetRef(spawner, "m_MousePrefab", _mousePrefab);

            // 벽감(Worker, v3.8) — 매치 시작에 맵이 벽을 깐다(요새·창고는 쥐가 산다)
            var alcoveSpawner = lobbyGo.AddComponent<CTC.Runtime.Map.AlcoveSpawner>();
            SetRef(alcoveSpawner, "m_AlcovePrefab", _alcovePrefab);

            // 상자(Worker, v3.9) — 벽감과 같은 취급. LobbyController가 GetComponent로 찾으므로
            // **같은 오브젝트에 있어야 한다** (안 붙여서 상자가 0개 스폰된 적이 있다, 2026-08-10).
            var boxSpawner = lobbyGo.AddComponent<CTC.Runtime.Map.LootBoxSpawner>();
            SetRef(boxSpawner, "m_BoxPrefab", _lootBoxPrefab);

            // 2단계 감독(Worker, v3.9) — 고양이가 죽어도 깡패가 있으면 판이 안 끝난다.
            var phaseDirector = lobbyGo.AddComponent<CTC.Runtime.Match.PhaseDirector>();

            var mapDirector = lobbyGo.AddComponent<CTC.Runtime.Map.MapDirector>(); // 맵(Worker)
            // 로비에서 고를 맵 목록 — 요새자리·스폰 공식이 선택된 레이아웃을 읽는다
            SetRefArray(mapDirector, "m_MapLayouts", LoadMapLayouts());
            SetRef(mapDirector, "m_MapBuilder", mapBuilder);

            // 가구 옷(2026-08-31) — 비주얼 전용. 팩이 없으면 배열이 비어 백킹만 남는다.
            var dresser = mapBuilderGo.AddComponent<CTC.Runtime.Map.MapDresser>();
            SetRef(dresser, "m_Builder", mapBuilder);
            SetRefArray(dresser, "m_ShelfUnits", MapAssets.LoadPrefabs(MapAssets.SHELF_PREFABS));
            SetRefArray(dresser, "m_CrateUnits", MapAssets.LoadPrefabs(MapAssets.CRATE_PREFABS));
            SetRefArray(dresser, "m_ObstacleUnits", MapAssets.LoadPrefabs(MapAssets.OBSTACLE_PREFABS));
            SetRef(mapDirector, "m_Dresser", dresser);

            var lobby = lobbyGo.AddComponent<LobbyController>();             // 조립자(Controller)
            var match = lobbyGo.AddComponent<MatchManager>(); // 동승 — 새 in-scene NetworkObject 없음
            SetRef(match, "m_Lobby", lobby);

            // 대기실이 뽑아 넘긴 판을 서버가 알아서 연다(2026-08-30). 쪽지가 비면 아무 일도 안 한다 —
            // 실험실은 자기 방식으로 판을 열므로 붙이지 않는다.
            if (!_botLab)
            {
                var autoStart = lobbyGo.AddComponent<MatchAutoStart>();
                SetRef(autoStart, "m_Lobby", lobby);
            }

            // 치즈 고갈 감독 — 지점 상태를 NetworkList로 복제하고, 매치 시작 시 매장량 복구,
            // 전 지점 고갈 시 최고 등급 1곳만 부활시켜 후반 결전지를 만든다
            var cheeseDirector = lobbyGo.AddComponent<CheeseSpotDirector>();
            SetRef(cheeseDirector, "m_Lobby", lobby);
            SetRef(cheeseDirector, "m_Match", match);
            SetRef(mapDirector, "m_CheeseDirector", cheeseDirector);
            // 3판 시리즈(2026-08-30) — 대기실에서 온 판에서만 돈다. 단독 실행은 지금까지 그대로.
            var series = lobbyGo.AddComponent<MatchSeries>();
            SetRef(series, "m_Lobby", lobby);
            SetRef(series, "m_Match", match);

            if (!_botLab) BuildLobbyCanvas(lobby, match, phaseDirector, series);

            // 밸런스 측정용 봇을 **밖에서** 조종하는 매니저(2026-08-11) — 개발 빌드 전용.
            // 봇의 몸에는 아무것도 안 붙는다. 여기서 플레이어 프리팹을 그대로 세우고 손만 움직인다.
            var botGo = new GameObject("TestBotManager");
            var bots = botGo.AddComponent<CTC.Runtime.Bots.TestBotManager>();
            SetRef(bots, "m_Lobby", lobby);
            SetRef(bots, "m_Spots", cheeseDirector);
            SetRef(bots, "m_Map", mapDirector);
            SetRef(bots, "m_CatPrefab", _catPrefab);
            SetRef(bots, "m_MousePrefab", _mousePrefab);

            // 로컬 플레이어 발행자 — HUD들이 매 프레임 탐색하는 대신 이 스트림을 구독한다(개편 3b)
            var binderGo = new GameObject("LocalPlayerBinder");
            binderGo.AddComponent<CTC.Runtime.Core.LocalPlayerBinder>();

            // 채팅 (in-scene NetworkObject — LobbyController와 동일 패턴, [Rpc]는 스폰된 NetworkBehaviour 필요)
            var chatGo = new GameObject("ChatManager");
            chatGo.AddComponent<NetworkObject>();
            chatGo.AddComponent<ChatManager>();

            // 카메라 — Main Camera에 Brain, 별도 오브젝트에 궤도 vcam
            var mainCam = Camera.main;
            SpectatorFallback spectator = null;
            if (_botLab)
            {
                // **궤도 카메라를 안 만드는 것이 핵심이다.** 봇 몸은 호스트에서 IsOwner라
                // `PlayerCameraBinder`가 스폰될 때마다 궤도 카메라를 자기에게 가져간다 —
                // 컨트롤러가 없으면 경고 한 줄만 남기고 아무 일도 안 한다.
                if (mainCam != null)
                {
                    mainCam.farClipPlane = 500f;
                    mainCam.gameObject.AddComponent<CTC.Runtime.Bots.BotLabCamera>();
                    mainCam.gameObject.AddComponent<CTC.Runtime.Bots.BotPathDebug>();
                }

                var labGo = new GameObject("BotLab");
                var lab = labGo.AddComponent<CTC.Runtime.Bots.BotLab>();
                SetRef(lab, "m_Lobby", lobby);
            }
            else
            {
            if (mainCam != null) mainCam.gameObject.AddComponent<CinemachineBrain>();

            var camGo = new GameObject("OrbitCamera");
            camGo.AddComponent<CinemachineCamera>();
            var orbital = camGo.AddComponent<CinemachineOrbitalFollow>();
            orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            // 반경 3 + 타깃 오프셋 0.7. 기본 앵글은 CSV(CamDefaultPitchDeg=35°)라 카메라 높이 ≈2.4 —
            // 통로벽(1.6) 위로 넘겨 보게 되어 좁은 통로에서 충돌 자체가 드물다 (맵 스케일: 방 5~9m·통로 1.6m)
            // 역할별 덮어쓰기는 PlayerCameraBinder — 두 역할 모두 3으로 통일했다(2026-07-28)
            CTC.Runtime.Info.BalanceInfo balance = CTC.Runtime.Info.InfoManager.Instance.Balance;
            orbital.Radius = 3f;
            orbital.TargetOffset = new Vector3(0f, 0.7f, 0f);
            orbital.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbital.HorizontalAxis.Wrap = true;
            orbital.VerticalAxis.Range = new Vector2(balance.CamMinPitchDeg, balance.CamMaxPitchDeg);
            orbital.VerticalAxis.Value = balance.CamDefaultPitchDeg;

            // 구도: 캐릭터를 화면 중앙보다 아래에 둔다(CamScreenY=-0.15 → 화면 위에서 65% 지점).
            // 3인칭에서 캐릭터 **위쪽**이 곧 "앞쪽 바닥"이라, 캐릭터를 내리면 갈 곳이 넓게 보인다.
            // 2026-08-01 피드백: "위쪽만 보이고 아래가 안 보여 불편하다".
            var composer = camGo.AddComponent<CinemachineRotationComposer>();
            composer.Composition.ScreenPosition = new Vector2(0f, balance.CamScreenY);

            // 지형-카메라 처리 (2026-07-28 2차 개정): 카메라 충돌 보정을 **전부 뺐다**.
            //   Deoccluder(당김) → Decollider(침투 방지)로 바꿔도 좁은 방·복도에서는 여전히
            //   카메라가 밀려 시점이 들쭉날쭉했다("카메라 충돌 빼기, 오히려 불편함" 플레이 피드백).
            //   이제 카메라는 벽을 그냥 통과하고, 시야는 OccluderFader(가리는 벽 반투명)로만 확보한다.
            //   거리가 상수라 시점이 절대 흔들리지 않는 대신, 카메라가 벽 속에 들어가는 프레임이 생긴다 —
            //   페이드가 그 벽을 지우므로 화면에는 드러나지 않는다.
            camGo.AddComponent<OccluderFader>();
            camGo.AddComponent<OrbitCameraController>();
            spectator = camGo.AddComponent<SpectatorFallback>();
            SetRef(spectator, "m_Lobby", lobby);
            }

            // 따봉 — 관전자가 보고 있는 쥐를 응원한다. 관전 대상은 SpectatorFallback가 안다.
            // (실험실에는 관전자가 없다 — 따봉을 보낼 사람도 없으니 null로 둔다.)
            var cheer = lobbyGo.AddComponent<CheerDirector>();
            SetRef(cheer, "m_Lobby", lobby);
            if (spectator != null) SetRef(cheer, "m_Spectator", spectator);

            EditorSceneManager.SaveScene(scene, _scenePath);

            if (!EditorBuildSettings.scenes.Any(s => s.path == _scenePath))
            {
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Append(new EditorBuildSettingsScene(_scenePath, true))
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
            EditorSceneManager.SaveScene(scene, _scenePath);

            VerifyUniqueHashes(sceneObjects
                .Append(_catPrefab.GetComponent<NetworkObject>())
                .Append(_mousePrefab.GetComponent<NetworkObject>())
                .Append(_fortressPrefab.GetComponent<NetworkObject>())
                .Append(_warehousePrefab.GetComponent<NetworkObject>())
                .ToArray());
        }

        // NGO의 NetworkConfig.Prefabs.Add()는 [NonSerialized] 런타임 리스트(m_Prefabs /
        // m_RuntimeAddedPrefabs)에만 기록되어 씬에 직렬화되지 않는다. 직렬화되는 경로는
        // NetworkPrefabs.NetworkPrefabsLists([SerializeField]) → NetworkPrefabsList 에셋뿐이므로
        // DefaultNetworkPrefabs.asset에 프리팹을 등록하고 그 리스트를 명시적으로 연결한다.
        private static void RegisterNetworkPrefabs(NetworkManager _nm, params GameObject[] _prefabs)
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

            foreach (GameObject prefab in _prefabs)
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

            if (!_nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(list))
                _nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
        }

        // 설치 미리보기 머티리얼을 '에셋'으로 만든다.
        // 런타임 Shader.Find는 빌드에서 셰이더가 스트리핑돼 null을 반환한다 — 에셋이 참조하면 살아남는다.
        private static Material CreateGhostMaterialAsset()
        {
            const string path = MaterialsDir + "/M_PlacementGhost.mat";
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("[SetupNetworkingScene] URP Unlit 셰이더를 못 찾음 — 고스트 머티리얼 생성 실패");
                return null;
            }

            var mat = new Material(shader) { name = "M_PlacementGhost" };
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
