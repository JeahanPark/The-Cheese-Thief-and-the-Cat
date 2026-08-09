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
    // SetupNetworkingScene의 일부 — 플레이어 프리팹(고양이·쥐) 생성 (개편 4단계 분할, 2026-07-31)
    public static partial class SetupNetworkingScene
    {
        private static GameObject CreatePlayerPrefab(
            string _name, string _prefabPath, string _modelPath, Material _material,
            float _ccHeight, float _ccRadius,
            float _moveSpeed, float _runThreshold, float _fov, float _camDistance,
            RoleId _role, int _maxHp,
            GameObject _decoyPrefab, GameObject _fortressPrefab, GameObject _warehousePrefab,
            int _layer = -1)
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            var root = new GameObject(_name);
            try
            {
                var cc = root.AddComponent<CharacterController>();
                cc.height = _ccHeight;
                cc.radius = _ccRadius;
                cc.center = new Vector3(0f, _ccHeight * 0.5f + 0.05f, 0f);

                root.AddComponent<NetworkObject>();
                root.AddComponent<ClientNetworkTransform>();

                CTC.Runtime.Info.PlayerInfo info = CTC.Runtime.Info.InfoManager.Instance.PlayerOf(_role);

                var move = root.AddComponent<PlayerMovement>();
                SetFloat(move, "m_MoveSpeed", _moveSpeed);
                SetFloat(move, "m_TurnSpeedDegPerSec", info.TurnSpeedDegPerSec);

                var anim = root.AddComponent<PlayerAnimationDriver>();
                SetFloat(anim, "m_RunThreshold", _runThreshold);

                var binder = root.AddComponent<PlayerCameraBinder>();
                SetFloat(binder, "m_CameraFov", _fov);
                SetFloat(binder, "m_CameraDistance", _camDistance);

                root.AddComponent<CheeseWallet>();

                var health = root.AddComponent<Health>();
                SetInt(health, "m_MaxHp", _maxHp);

                var roleTag = root.AddComponent<RoleTag>();
                SetInt(roleTag, "m_Role", (int)_role); // enum은 intValue로

                root.AddComponent<PlayerDeath>();
                root.AddComponent<HitFeedback>();
                root.AddComponent<LocalPlayerPublisher>(); // 스폰/디스폰을 LocalPlayerBinder에 알린다

                var shop = root.AddComponent<ShopController>();
                SetRef(shop, "m_DecoyPrefab", _decoyPrefab);
                SetRef(shop, "m_FortressPrefab", _fortressPrefab);
                SetRef(shop, "m_WarehousePrefab", _warehousePrefab);

                if (_role == RoleId.Cat)
                {
                    // 덮치기 수치도 CSV(PlayerInfo.csv)에서 굽는다
                    var pounce = root.AddComponent<CatPounce>();
                    SetInt(pounce, "m_Damage", info.PounceDamage);
                    SetFloat(pounce, "m_CooldownSeconds", info.PounceCooldown);
                    SetFloat(pounce, "m_Range", info.PounceRange);
                    SetFloat(pounce, "m_HalfAngleDeg", info.PounceHalfAngleDeg);
                    root.AddComponent<CatSneak>(); // LCtrl 잠행 — 발소리 소거·이속 절반
                }
                else
                {
                    root.AddComponent<MousePopgun>();
                    root.AddComponent<MouseDash>();
                    root.AddComponent<CTC.Runtime.Audio.FarmingSfx>(); // 수입 짤랑 3D 공개음(20m)
                }

                // 발소리 — 3D 루프(거리 감쇠), FootstepPlayer가 이동 속도로 재생 제어
                var stepSource = root.AddComponent<AudioSource>();
                stepSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(_role == RoleId.Cat
                    ? "Assets/Audio/Movement/sfx_cat_footstep.ogg"
                    : "Assets/Audio/Movement/sfx_mouse_footstep.ogg");
                stepSource.loop = true;
                stepSource.playOnAwake = false;
                stepSource.volume = 0.6f;
                stepSource.spatialBlend = 1f;
                stepSource.rolloffMode = AudioRolloffMode.Linear;
                stepSource.minDistance = 1.5f;
                // 고양이 15m(위협 경보 반경 — 도주가 성립하는 거리), 쥐 8m(고양이도 귀 사냥 가능)
                stepSource.maxDistance = info.FootstepRangeMeters;
                var steps = root.AddComponent<CTC.Runtime.Audio.FootstepPlayer>();
                SetRef(steps, "m_Source", stepSource);
                // 발소리가 제일 큰 효과음이다 — 팔로워가 없으면 "효과음을 줄였는데 발소리만 그대로"가 된다
                AddVolumeFollower(root, CTC.Runtime.Audio.AudioVolumeFollower.Category.Sfx);

                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(_modelPath);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;

                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(_material, r.sharedMaterials.Length).ToArray();

                // 카메라 Deoccluder IgnoreTag용 — 히트한 '콜라이더의 GO' 태그를 비교하므로
                // 모델 자식(Quirky 캡슐 콜라이더 포함)까지 전부 태깅해야 자기-차폐가 안 생긴다
                foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                    tr.gameObject.tag = "Player";

                // 레이어도 계층 전체에 — 콜라이더가 붙은 자식까지 칠해야 물리 필터가 걸린다
                if (_layer >= 0)
                {
                    foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                        tr.gameObject.layer = _layer;
                }

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
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
    }
}
