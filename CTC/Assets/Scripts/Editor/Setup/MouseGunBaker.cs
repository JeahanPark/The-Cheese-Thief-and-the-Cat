using CTC.Runtime.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 쥐 앞발에 총을 물리고, 사격 상태 둘을 <c>AC_Mouse</c>에 넣는다 —
    /// Tools > CTC > Dev > Bake Mouse Gun
    ///
    /// <see cref="EyeAnchorBaker"/>와 같은 규칙이다: **씬 생성기(PlayerPrefabFactory)와 메뉴가
    /// 같은 함수를 부른다.** 프리팹을 다시 굽든 손으로 고치든 값이 갈리지 않게.
    ///
    /// 좌표의 출처는 `Tools/CTC/Dev/Probe Mouse Gun Anchor`가 실제로 잰 값이다(2026-09-12).
    /// 아래 상수의 근거는 각 상수에 적어 뒀다 — 눈대중이 아니라 계산이다.
    /// </summary>
    public static class MouseGunBaker
    {
        private const string MOUSE_PREFAB = "Assets/Prefabs/PlayerMouse.prefab";
        private const string GUN_PREFAB = "Assets/Prefabs/Props/MouseGun.prefab";

        private const string MOUSE_CONTROLLER =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Animations/AC_Mouse.controller";

        private const string CLIPS_FBX = "Assets/Animations/Pets/CTC_Mouse_Clips.fbx";

        /// <summary>든 총을 매다는 본. 클립이 이 본에 총을 들려 놓고 짜여 있다.</summary>
        public const string GUN_BONE = "leg.F.R";

        /// <summary>
        /// 수납된 총을 매다는 본. **앞다리가 아니라 몸통이다** — 앞다리는 엉덩이에 닿을 수 없어서,
        /// 총 하나만 발에 물려 놓으면 뽑는 동작이 「가슴 밑에서 들어 올린다」로 읽힌다(2026-09-12).
        /// </summary>
        public const string HOLSTER_BONE = "body";

        /// <summary>총 오브젝트 이름 — 다시 구울 때 이 이름으로 찾아 지운다(멱등).</summary>
        private const string GUN_OBJECT_NAME = "MouseGun";

        private const string HOLSTERED_OBJECT_NAME = "MouseGunHolstered";

        /// <summary>
        /// 파지 지점(본 로컬). 실측: `leg.F.R`에 매인 정점 73개 중 밑동에서 먼 20%(= 발 뭉치)
        /// 37개의 중심이 (0.0000, 0.0018, 0.0000)이었다 — 본 원점에서 발까지 0.0018단위,
        /// 본의 월드 스케일이 100이므로 **월드로 0.18m**다(앞다리 실측 길이 0.193m과 맞는다).
        /// 발끝 한 점(0.0019)이 아니라 뭉치 중심을 쓴다 — 끝점에 쥐면 총이 발 밖에 떠 보인다.
        /// </summary>
        private static readonly Vector3 GUN_LOCAL_POSITION = new Vector3(0f, 0.0018f, 0f);

        /// <summary>
        /// 총의 로컬 회전(본 기준). **눈대중이 아니라 역산이다.**
        ///
        /// 아는 요구는 하나뿐이다 — 「쏘는 순간 총구(+Z)가 몸 정면을 수평으로 봐야 한다」.
        /// 총알이 실제로 그렇게 나간다(<c>MousePopgun.FireRpc</c>가 방향의 y를 0으로 눕힌다).
        /// 총은 본의 자식이라 <c>본회전 × 총로컬회전 = 총월드회전</c>이므로,
        /// 총로컬회전 = (쏘는 순간 본회전)⁻¹ 이다.
        ///
        /// 실측(몸 기준 본 회전): Shoot은 bf04~bf05에서, ShootDraw는 bf05~bf07에서
        /// X가 300.7~302.0으로 평평해진다 — 두 클립이 **301.6°** 근처에서 일치한다. 그 구간이
        /// 겨누고 쏘는 순간이다. Y·Z는 전 구간 고정(0, 180)이라 팔은 X 한 축으로만 돈다.
        ///
        /// (301.6, 0, 180)은 180° 회전이라 자기 자신이 역이다 — 그래서 역산 값이 같은 숫자로 나온다.
        /// </summary>
        private static readonly Vector3 GUN_LOCAL_EULER = new Vector3(301.6f, 0f, 180f);

        /// <summary>
        /// 본의 월드 스케일이 100이다(리그가 100배로 들어와 바인드포즈가 흡수한다).
        /// 총은 **미터로** 구워졌으므로(전장 0.22m) 그대로 붙이면 22m짜리가 된다 —
        /// 1/100을 걸어 원래 크기로 되돌린다.
        /// </summary>
        private const float GUN_LOCAL_SCALE = 0.01f;

        /// <summary>
        /// 허리춤 자리(몸통 본 로컬). 몸통 본 축은 몸 축과 **같다**(실측: right=+X·up=+Y·fwd=+Z)라
        /// 여기 숫자를 그대로 「오른쪽·아래·뒤」로 읽으면 된다. ×100이 미터다.
        ///
        /// 근거(2026-09-12 실측):
        ///  - 꼬리 밑동(= 해부학적 엉덩이)이 몸통 본 로컬 (0, -0.0007, -0.0022), 월드 0.228m 뒤.
        ///  - 몸통 옆구리 |x| 프로파일을 (z, y) 5×5로 찍었더니, 총이 누울 띠(y≈-0.0004)에서
        ///    앞쪽 z=-0.0008은 0.00194, 꼬리 쪽 z=-0.0022는 0.00161로 **뒤로 갈수록 가늘어진다.**
        ///  - 그래서 x는 그 중간(0.0019)으로 잡았다. 손잡이 쪽은 옆구리에 살짝 파묻히고
        ///    총구 쪽은 꼬리 앞으로 조금 비어져 나온다 — 「끼워 넣었다」로 읽히는 상태다.
        ///  - z = -0.00105는 몸통 한가운데 조금 뒤. 총이 0.22m라 꼬리 밑동에 손잡이를 두면
        ///    총구가 몸 뒤로 15cm 튀어나와 꼬리처럼 보인다 — 앞으로 당겨 옆구리에 눕혔다.
        /// </summary>
        private static readonly Vector3 HOLSTER_LOCAL_POSITION = new Vector3(0.0019f, -0.0004f, -0.00105f);

        /// <summary>
        /// 허리춤 총의 회전. Y를 180° 돌려 **총구를 뒤(-Z)로** 보내고, X로 20° 숙여
        /// 총구가 아래로 처지게 한다 — 꽂아 둔 총의 자세다. Y를 192°로 12° 더 튼 것은
        /// 몸이 뒤로 갈수록 가늘어지기 때문이다(위 프로파일 0.00194 → 0.00161):
        /// 그 기울기를 따라가야 총이 옆구리에 붙어 있고, 안 그러면 총구 쪽이 떠 보인다.
        ///
        /// Y를 180° 쓰는 이유가 따로 있다 — X만으로 총구를 뒤로 보내려면 90°를 넘겨야 하고,
        /// 그러면 총의 +Y(슬라이드 위쪽)가 **아래로 뒤집힌다.** Y로 돌리면 위가 위로 남는다.
        /// </summary>
        private static readonly Vector3 HOLSTER_LOCAL_EULER = new Vector3(20f, 192f, 0f);

        [MenuItem("Tools/CTC/Dev/Bake Mouse Gun")]
        public static void BakeAll()
        {
            AddMouseOnlyStates();
            BakeIntoPrefab();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 쥐 프리팹의 앞발에 총을 달고 <see cref="MouseGunVisual"/>을 배선한다.
        /// 생성기(<c>PlayerPrefabFactory</c>)가 갓 만든 계층에도, 이미 저장된 프리팹에도 같이 쓴다.
        /// </summary>
        public static void Attach(GameObject _root)
        {
            if (_root == null) return;

            GameObject gun = AttachOne(
                _root, GUN_BONE, GUN_OBJECT_NAME, GUN_LOCAL_POSITION, GUN_LOCAL_EULER);

            GameObject holstered = AttachOne(
                _root, HOLSTER_BONE, HOLSTERED_OBJECT_NAME,
                HOLSTER_LOCAL_POSITION, HOLSTER_LOCAL_EULER);

            var visual = _root.GetComponent<MouseGunVisual>();
            if (visual == null) visual = _root.AddComponent<MouseGunVisual>();

            var so = new SerializedObject(visual);
            so.FindProperty("m_Gun").objectReferenceValue = gun;
            so.FindProperty("m_HolsteredGun").objectReferenceValue = holstered;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 총 하나를 지정한 본에 단다. 든 것과 수납된 것이 **같은 프리팹·같은 함수**를 탄다 —
        /// 티어가 달라도 모델이 하나여야 하는 것과 같은 이유로, 둘도 서로 달라 보이면 안 된다.
        /// </summary>
        private static GameObject AttachOne(
            GameObject _root, string _boneName, string _objectName, Vector3 _position, Vector3 _euler)
        {
            Transform bone = FindDeep(_root.transform, _boneName);
            if (bone == null)
            {
                Debug.LogWarning($"[MouseGunBaker] '{_boneName}' 본을 못 찾았다 — {_objectName}을 못 단다");
                return null;
            }

            // 멱등 — 전에 단 것이 있으면 지우고 다시 단다. 자리·회전의 주인이 이 코드라
            // 「있으면 안 덮어쓴다」 규칙(손으로 옮긴 프리팹 보호)의 대상이 아니다.
            Transform old = bone.Find(_objectName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var gunAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GUN_PREFAB);
            if (gunAsset == null)
            {
                Debug.LogWarning($"[MouseGunBaker] 총 프리팹이 없다: {GUN_PREFAB}");
                return null;
            }

            var gun = (GameObject)PrefabUtility.InstantiatePrefab(gunAsset);
            gun.name = _objectName;
            Place(gun.transform, bone, _position, _euler);

            // 몸과 같은 태그·레이어 — 카메라 Deoccluder와 물리 필터가 계층 전체를 같은 값으로 본다.
            gun.tag = bone.gameObject.tag;
            gun.layer = bone.gameObject.layer;

            // **꺼진 채로 굽는다.** 총이 상시로 보이면 고양이가 한 발도 맞기 전에
            // "저 쥐는 무장했다"를 알게 된다 — 원래 없던 정보다(MouseGunVisual 주석 참조).
            gun.SetActive(false);

            return gun;
        }

        /// <summary>
        /// 총을 본에 얹는다 — **자리·회전·크기의 유일한 원본.** 미리보기 렌더러
        /// (<see cref="ClipPreviewRender"/>)도 이 함수를 부른다: 렌더에서 본 자세와
        /// 게임에서 보는 자세가 갈리면 미리보기가 거짓말을 한다.
        /// </summary>
        public static void Place(Transform _gun, Transform _bone, Vector3 _position, Vector3 _euler)
        {
            if (_gun == null || _bone == null) return;

            _gun.SetParent(_bone, false);
            _gun.localPosition = _position;
            _gun.localRotation = Quaternion.Euler(_euler);
            _gun.localScale = Vector3.one * GUN_LOCAL_SCALE;

            // 인스펙터가 보여 주는 각을 같이 맞춘다. 회전은 쿼터니언으로 저장되지만 인스펙터는
            // `m_LocalEulerAnglesHint`를 읽는데, 코드로 회전을 넣으면 그 힌트가 **안 따라온다**
            // (2026-09-12 실측: `localEulerAngles` 세터로 넣어도 0으로 남았다). 그대로 두면
            // 프리팹을 연 사람이 Rotation (0,0,0)이라는 거짓말을 본다.
            var so = new SerializedObject(_gun);
            SerializedProperty hint = so.FindProperty("m_LocalEulerAnglesHint");
            if (hint != null)
            {
                hint.vector3Value = _euler;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>이름으로 본을 찾는다 — 계층 어디에 있든.</summary>
        public static Transform FindBone(Transform _root, string _name) => FindDeep(_root, _name);

        /// <summary>미리보기 렌더러가 **같은 숫자**로 붙이도록 내보낸다 — 둘이 갈리면 미리보기가 거짓말을 한다.</summary>
        public static Vector3 HeldPosition => GUN_LOCAL_POSITION;

        public static Vector3 HeldEuler => GUN_LOCAL_EULER;

        public static Vector3 HolsterPosition => HOLSTER_LOCAL_POSITION;

        public static Vector3 HolsterEuler => HOLSTER_LOCAL_EULER;

        private static void BakeIntoPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(MOUSE_PREFAB);
            if (root == null)
            {
                Debug.LogError($"[MouseGunBaker] 프리팹 없음: {MOUSE_PREFAB}");
                return;
            }

            try
            {
                Attach(root);
                PrefabUtility.SaveAsPrefabAsset(root, MOUSE_PREFAB);
                Debug.Log($"[MouseGunBaker] 총을 물렸다: {MOUSE_PREFAB} :: {GUN_BONE}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// <c>Shoot</c>·<c>ShootDraw</c> 상태를 AC_Mouse에 넣는다. **AC_Cat은 건드리지 않는다** —
        /// 고양이는 총을 쏘지 않는다. 상태 이름이 없으면 <c>CrossFadeInFixedTime</c>은
        /// 에러도 경고도 없이 아무 일도 안 한다.
        ///
        /// YAML을 손으로 쓰지 않는다 — FBX 하위 클립의 fileID는 임포터가 정하는 값이라
        /// 손으로 적으면 엉뚱한 클립을 가리킬 수 있다. 컨트롤러 API가 그 참조를 맺게 한다.
        /// </summary>
        private static void AddMouseOnlyStates()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MOUSE_CONTROLLER);
            if (controller == null)
            {
                Debug.LogError($"[MouseGunBaker] 컨트롤러 없음: {MOUSE_CONTROLLER}");
                return;
            }

            AnimatorStateMachine machine = null;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                machine = layer.stateMachine;
                break;
            }
            if (machine == null) machine = controller.layers[0].stateMachine;

            foreach (string name in AnimationStateLogic.MouseOnly)
            {
                AnimationClip clip = ClipParityProbe.FindClip(CLIPS_FBX, name);
                if (clip == null)
                {
                    Debug.LogError($"[MouseGunBaker] 클립을 못 찾았다: {CLIPS_FBX} :: {name}");
                    continue;
                }

                AnimatorState state = FindState(machine, name);
                if (state == null)
                {
                    state = machine.AddState(name);
                    Debug.Log($"[MouseGunBaker] 상태 추가(쥐 전용): AC_Mouse :: {name}");
                }

                state.motion = clip;
                // 팩의 다른 상태들과 같게 — 이 컨트롤러는 파라미터 없이 CrossFade만 쓴다.
                state.writeDefaultValues = true;
            }

            EditorUtility.SetDirty(controller);
        }

        private static AnimatorState FindState(AnimatorStateMachine _machine, string _name)
        {
            foreach (ChildAnimatorState child in _machine.states)
            {
                if (child.state != null && child.state.name == _name) return child.state;
            }
            return null;
        }

        private static Transform FindDeep(Transform _root, string _name)
        {
            if (_root.name == _name) return _root;
            for (int i = 0; i < _root.childCount; i++)
            {
                Transform found = FindDeep(_root.GetChild(i), _name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
