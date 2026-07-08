using System.Linq;
using CTC.Runtime.Networking;
using CTC.Runtime.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// Plan 1 검증용 Player 프리팹과 Game 씬을 생성한다.
    /// Tools > CTC > Setup Networking Scene 실행 (재실행 시 덮어씀).
    /// </summary>
    public static class SetupNetworkingScene
    {
        private const string PrefabDir = "Assets/Prefabs";
        private const string PlayerPrefabPath = PrefabDir + "/Player.prefab";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Tools/CTC/Setup Networking Scene")]
        public static void Run()
        {
            GameObject playerPrefab = CreatePlayerPrefab();
            CreateGameScene(playerPrefab);
            Debug.Log("[SetupNetworkingScene] 완료 — Player.prefab + Game.unity 생성, Build Settings 등록");
        }

        private static GameObject CreatePlayerPrefab()
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            var root = new GameObject("Player");
            try
            {
                var cc = root.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.5f;
                cc.center = new Vector3(0f, 1f, 0f);

                root.AddComponent<NetworkObject>();
                root.AddComponent<ClientNetworkTransform>();
                root.AddComponent<PlayerMovement>();

                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Visual";
                Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, 1f, 0f);

                return PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateGameScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(3f, 1f, 3f);

            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777);

            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = playerPrefab;
            nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = playerPrefab });

            nmGo.AddComponent<ConnectionUI>();

            EditorSceneManager.SaveScene(scene, GameScenePath);

            if (!EditorBuildSettings.scenes.Any(s => s.path == GameScenePath))
            {
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Append(new EditorBuildSettingsScene(GameScenePath, true))
                    .ToArray();
            }
        }
    }
}
