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
            GameObject _fortressPrefab, GameObject _warehousePrefab,
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
                SetRef(shop, "m_FortressPrefab", _fortressPrefab);
                // 요새가 내 위치 중심에 서면 겹친 남의 몸을 서버가 밀어낸다 — 소유자 RPC 수신처
                root.AddComponent<BodyPush>();
                SetRef(shop, "m_WarehousePrefab", _warehousePrefab);
                // 끈끈이 장판(테크 v3) — 씬이 아니라 여기서 굽는다. 팩토리는 있으면 그대로 돌려주므로
                // 씬 재생성과 순서를 다퉈도 같은 에셋을 가리킨다.
                SetRef(shop, "m_StickyPrefab", StickyPatchPrefabFactory.Build());
                root.AddComponent<TechSlots>(); // 테크 v3 — 보유 테크·레벨(NetworkList)

                // LShift 잠행 — 발소리 소거·이속 절반. **역할 분기 밖**이다: 2026-08-10에 쥐로
                // 확장했는데 고양이 분기 안에 두면 쥐 프리팹에 조용히 안 붙는다(그렇게 한 번 놓쳤다).
                // 배율은 컴포넌트가 자기 RoleTag를 보고 CSV에서 읽는다.
                root.AddComponent<Sneak>();

                if (_role == RoleId.Cat)
                {
                    // 덮치기 수치도 CSV(PlayerInfo.csv)에서 굽는다
                    var pounce = root.AddComponent<CatPounce>();
                    SetInt(pounce, "m_Damage", info.PounceDamage);
                    SetFloat(pounce, "m_CooldownSeconds", info.PounceCooldown);
                    SetFloat(pounce, "m_Range", info.PounceRange);
                    SetFloat(pounce, "m_HalfAngleDeg", info.PounceHalfAngleDeg);

                    // 전직(v3.9) — 상태와 스킬을 따로 둔다(기능 1개 = 컴포넌트 1개).
                    // 조건(강화 만렙)은 CatClass가 보고, 쓰는 것은 CatSkill이 맡는다.
                    root.AddComponent<CatClass>();
                    root.AddComponent<CatSkill>();

                    // 두꺼운 가죽(테크 v3 계획 2b) — 살 때 Health의 최대치를 올린다.
                    // 배고픔·예민한 귀는 컴포넌트가 없다(읽는 쪽이 TechSlots를 본다).
                    root.AddComponent<CatHide>();
                    // 광분 — 켜짐·쿨을 복제한다. 입력은 읽지 않는다(핫바 숫자키 → ShopController.Use).
                    root.AddComponent<CatFrenzy>();
                }
                else
                {
                    root.AddComponent<MousePopgun>();
                    root.AddComponent<MouseDash>();
                    root.AddComponent<PepperRounds>();   // 후추 탄 보유 플래그(빌드 심화 v1)
                    // 치즈 방패(테크 v3 계획 2a) — 쥐만. Health가 피격 때 IDamageAbsorber로 집어 간다.
                    root.AddComponent<CheeseShield>();
                    // 끈끈이 쿨다운(테크 v3 계획 2a) — 장판은 서버가 스폰하고, 이 컴포넌트는 쿨만 든다.
                    root.AddComponent<StickyUser>();
                    root.AddComponent<CTC.Runtime.Audio.FarmingSfx>(); // 수입 짤랑 3D 공개음(20m)

                    // 직업(v3.9) — 상자에서 주운 직업권이 여기 새겨진다.
                    // 임명은 별도 컴포넌트다(기능 1개 = 컴포넌트 1개).
                    root.AddComponent<CTC.Runtime.Match.MouseJob>();
                    root.AddComponent<CTC.Runtime.Match.GangRecruit>();
                    // 머리 위 편 표식 — 2단계는 쥐끼리 편이 갈리는데 표시가 없으면 누굴 쏠지 모른다
                    root.AddComponent<CTC.Runtime.UI.JobBadge>();
                }

                // 발소리 — 3D 한 걸음씩(거리 감쇠). FootstepPlayer가 이동 거리 누적으로 박자를 낸다(2026-08-17).
                // 한 걸음 클립 3종은 예전 루프(sfx_*_footstep.ogg)에서 잘라낸 것 — 보폭은 그 루프의 박자
                // (쥐 3.4걸음/s · 고양이 2.5걸음/s)를 기준 이속에서 그대로 재현하는 값이다.
                var stepSource = root.AddComponent<AudioSource>();
                stepSource.loop = false;
                stepSource.playOnAwake = false;
                stepSource.volume = 0.6f;
                stepSource.spatialBlend = 1f;
                stepSource.rolloffMode = AudioRolloffMode.Linear;
                stepSource.minDistance = 1.5f;
                // 고양이(위협 경보 반경 — 도주가 성립하는 거리), 쥐(고양이의 주 단서)
                stepSource.maxDistance = info.FootstepRangeMeters;
                var steps = root.AddComponent<CTC.Runtime.Audio.FootstepPlayer>();
                SetRef(steps, "m_Source", stepSource);
                string stepPrefix = _role == RoleId.Cat ? "sfx_cat_step" : "sfx_mouse_step";
                SetRefArray(steps, "m_StepClips", new Object[]
                {
                    AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Movement/{stepPrefix}1.ogg"),
                    AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Movement/{stepPrefix}2.ogg"),
                    AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Movement/{stepPrefix}3.ogg"),
                });
                SetFloat(steps, "m_StrideMeters", CTC.Runtime.Audio.FootstepLogic.StrideOf(
                    info.MoveSpeed, _role == RoleId.Cat ? 2.5f : 3.4f));
                // 잠행은 무음이 아니라 '좁게 들린다'(2026-08-10) — 넓은 맵에서 웅크려 버티기가
                // 무적이 되는 것을 막는다. 두 반경 다 PlayerInfo에서 굽는다.
                SetFloat(steps, "m_NormalRange", info.FootstepRangeMeters);
                SetFloat(steps, "m_SneakRange", info.SneakFootstepRangeMeters);
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
