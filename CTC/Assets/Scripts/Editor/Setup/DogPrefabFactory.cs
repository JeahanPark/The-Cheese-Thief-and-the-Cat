using System.Linq;
using CTC.Runtime.Ai;
using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    // SetupNetworkingScene의 일부 — 제3세력 강아지 프리팹 (v3.8 5번, 2026-08-09)
    public static partial class SetupNetworkingScene
    {
        private const string DogModelPath =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Models/Dog_LOD0.fbx";

        private const string DogControllerPath =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Animations/AC_Dog.controller";

        private const string DogSourceMatPath =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Materials/M_Dog.mat";

        /// <summary>
        /// 벽감을 부수면 나오는 강아지.
        ///
        /// `CreateStructurePrefab`도 `CreatePlayerPrefab`도 쓰지 않는다 —
        /// ① 구조물 경로에는 `PlayerDeath`가 붙는데 그건 고양이가 아닌 것을 **감옥으로 보낸다**.
        ///    강아지가 갇히면 갇힌 수에 끼고 구출 대상이 된다.
        /// ② 플레이어 경로에는 입력·상점·지갑이 딸려 온다. 강아지는 아무도 조종하지 않는다.
        ///
        /// `RoleTag`는 `None`이다 — 어느 쪽도 아닌 게 이 기능의 전부고, `Mouse`면 쥐가 못 쏘고
        /// `Cat`이면 고양이가 못 때린다. 그 대신 무기의 표적 규칙을 `CombatLogic.CanAttack`
        /// ("제 진영만 아니면 때린다")으로 바꿨다 — 안 그러면 **아무도 강아지를 못 죽인다**
        /// (2026-08-09 qa_v40이 잡았다: 덮치기가 400→400).
        /// </summary>
        private static GameObject CreateDogPrefab()
        {
            var root = new GameObject("Dog");
            try
            {
                var balance = CTC.Runtime.Info.InfoManager.Instance.Balance;

                root.AddComponent<NetworkObject>();
                // 서버만 움직이므로 소유권 이전이 없는 기본 NetworkTransform으로 충분하다
                // (플레이어의 ClientNetworkTransform은 소유자가 쓰는 물건이다).
                root.AddComponent<NetworkTransform>();

                var cc = root.AddComponent<CharacterController>();
                cc.height = 0.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0f, 0.4f, 0f);

                var health = root.AddComponent<Health>();
                SetInt(health, "m_MaxHp", balance.DogHp);

                var roleTag = root.AddComponent<RoleTag>();
                SetInt(roleTag, "m_Role", (int)RoleId.None);

                // 때려도 장난기가 안 나온다 — 안 붙이면 '강아지 때리기가 최고 수입'이 되어
                // v3.5의 '벽 때리기' 실패가 되살아난다.
                root.AddComponent<BountyExempt>();

                var feedback = root.AddComponent<HitFeedback>();
                SetInt(feedback, "m_OverrideSfx", 1);
                SetInt(feedback, "m_HitSfx", (int)CTC.Runtime.Audio.SfxId.DogHurt);
                SetInt(feedback, "m_BreakSfx", (int)CTC.Runtime.Audio.SfxId.DogDeath);
                SetFloat(feedback, "m_SfxMaxDistance", balance.DogBarkSoundMeters);

                root.AddComponent<DogBrain>();
                root.AddComponent<DogMover>();
                root.AddComponent<DogBite>();
                root.AddComponent<DogBark>();
                root.AddComponent<DogDeath>();

                // 애니메이션은 플레이어 것을 그대로 쓴다 — 위치 변화로 Idle/Walk를 고르는 방식이라
                // 조종 주체를 안 가린다. AC_Dog에도 Idle_A·Walk·Run·Attack·Hit이 다 있다.
                var driver = root.AddComponent<CTC.Runtime.Player.PlayerAnimationDriver>();
                SetFloat(driver, "m_RunThreshold", balance.DogMoveSpeed * 0.7f);

                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DogModelPath);
                if (modelAsset == null)
                {
                    Debug.LogError($"[SetupNetworkingScene] 강아지 모델 없음: {DogModelPath}");
                }
                else
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                    model.name = "Model";
                    model.transform.SetParent(root.transform, false);
                    model.transform.localPosition = Vector3.zero;

                    var animator = model.GetComponent<Animator>();
                    if (animator == null) animator = model.AddComponent<Animator>();
                    animator.runtimeAnimatorController =
                        AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DogControllerPath);

                    // 모델에 딸려 온 콜라이더는 지운다 — CharacterController와 겹쳐 서로 밀어낸다.
                    foreach (Collider col in model.GetComponentsInChildren<Collider>(true))
                    {
                        Object.DestroyImmediate(col);
                    }

                    // 고양이·쥐와 같은 방식 — Quirky 원본 머티리얼을 URP로 옮겨 쓴다.
                    // 프로토타입 색(Gridbox)을 덮으면 강아지가 통짜 빨강으로 나온다(2026-08-10 지적).
                    Material dogMat = CreateUrpMaterial("M_Dog_URP", DogSourceMatPath);
                    foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                    {
                        r.sharedMaterials = Enumerable.Repeat(dogMat, r.sharedMaterials.Length).ToArray();
                    }
                }

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, DogPrefabPath);
                BakeGlobalObjectIdHash(asset.GetComponent<NetworkObject>());
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
