using CTC.Runtime.Audio;
using CTC.Runtime.Cameras;
using CTC.Runtime.Combat;
using CTC.Runtime.Info;
using CTC.Runtime.Lobby;
using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// B-09 — 두 플레이어 프리팹에 눈·귀 컴포넌트가 붙어 있고 눈 중심 상수가 베이커(EyeAnchorBaker)와 일치한다.
    /// B-10 — PlayerInfo 표의 값이 두 프리팹에 구워진 값과 같고, 보폭이 이속에서 유도된다.
    ///
    /// 씬·프리팹 재생성(`Setup Networking Scene`)이 조용히 배선을 지우거나 CSV와 갈라지는 자리다.
    /// **프리팹이 없으면 건너뛰지 않고 실패한다** — 없는 것도 사고다.
    /// </summary>
    [NUnit.Framework.Category("Asset")]
    public class PlayerPrefabBakeTests
    {
        private const string CAT_PREFAB = "Assets/Prefabs/PlayerCat.prefab";
        private const string MOUSE_PREFAB = "Assets/Prefabs/PlayerMouse.prefab";
        private const float EPS = 1e-4f;

        private static GameObject LoadPrefab(RoleId _role)
        {
            string path = _role == RoleId.Cat ? CAT_PREFAB : MOUSE_PREFAB;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsTrue(prefab != null,
                $"프리팹이 없다: {path} — Tools/CTC/Setup Networking Scene으로 굽는다");
            return prefab;
        }

        private static T Require<T>(GameObject _prefab) where T : Component
        {
            T c = _prefab.GetComponent<T>();
            Assert.IsTrue(c != null, $"{_prefab.name}에 {typeof(T).Name}가 없다 — 재생성이 배선을 지웠다");
            return c;
        }

        private static SerializedProperty Prop(Object _target, string _name)
        {
            SerializedProperty p = new SerializedObject(_target).FindProperty(_name);
            Assert.IsNotNull(p, $"{_target.GetType().Name}에 직렬화 필드 {_name}가 없다 — 이름이 바뀌면 구운 값이 날아간다");
            return p;
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

        private static void AssertVector(Vector3 _expected, Vector3 _actual, float _eps, string _message)
        {
            Assert.AreEqual(_expected.x, _actual.x, _eps, $"{_message} (x) 기대 {_expected} 실제 {_actual}");
            Assert.AreEqual(_expected.y, _actual.y, _eps, $"{_message} (y) 기대 {_expected} 실제 {_actual}");
            Assert.AreEqual(_expected.z, _actual.z, _eps, $"{_message} (z) 기대 {_expected} 실제 {_actual}");
        }

        // ───────────────────────── B-09 ─────────────────────────

        [NUnit.Framework.Property("CaseId", "B-09")]
        [TestCase(RoleId.Cat)]
        [TestCase(RoleId.Mouse)]
        public void Prefab_HasEyeEarComponents_AndTheirBonesExist(RoleId _role)
        {
            GameObject prefab = LoadPrefab(_role);

            EyeLook eyes = Require<EyeLook>(prefab);
            EarPerk ears = Require<EarPerk>(prefab);
            Require<GazeAim>(prefab);        // 없으면 남의 눈동자가 가운데 붙어 죽는다
            Require<FaceExpression>(prefab); // 원샷 표정

            // 컴포넌트가 이름으로 찾는 본이 모델에 실제로 있어야 한다 — 없으면 에러 없이 아무것도 안 한다.
            string head = Prop(eyes, "m_HeadBoneName").stringValue;
            Assert.IsTrue(FindDeep(prefab.transform, head) != null, $"{prefab.name}: 눈이 찾는 머리 본 '{head}'가 없다");
            foreach (string field in new[] { "m_EarRightBoneName", "m_EarLeftBoneName" })
            {
                string bone = Prop(ears, field).stringValue;
                Assert.IsTrue(FindDeep(prefab.transform, bone) != null, $"{prefab.name}: 귀 본 '{bone}'({field})가 없다");
            }
        }

        [NUnit.Framework.Property("CaseId", "B-09")]
        [TestCase(RoleId.Cat)]
        [TestCase(RoleId.Mouse)]
        public void Prefab_EyeCenterAndAxis_MatchBaker(RoleId _role)
        {
            GameObject prefab = LoadPrefab(_role);
            EyeLook baked = Require<EyeLook>(prefab);

            // 베이커를 임시 오브젝트에 그대로 돌려 **베이커가 지금 굽는 값**을 얻는다(씬을 더럽히지 않게 숨김 오브젝트).
            // 머리 본이 없어 정면축은 못 굽는다는 경고가 한 줄 찍힌다 — 정면축은 아래 테스트가 따로 본다.
            GameObject temp = EditorUtility.CreateGameObjectWithHideFlags("B09_EyeBakerProbe", HideFlags.HideAndDontSave);
            try
            {
                EyeLook fresh = temp.AddComponent<EyeLook>();
                global::CTC.Editor.EyeAnchorBaker.Apply(fresh, _role);

                foreach (string field in new[] { "m_EyeCenterLocal", "m_EyeAxisLocal" })
                {
                    AssertVector(Prop(fresh, field).vector3Value, Prop(baked, field).vector3Value, 1e-6f,
                        $"{prefab.name}.{field}가 EyeAnchorBaker 상수와 다르다 — Tools/CTC/Dev/Bake Eye Anchors를 돌려라");
                }
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        [NUnit.Framework.Property("CaseId", "B-09")]
        [TestCase(RoleId.Cat)]
        [TestCase(RoleId.Mouse)]
        public void Prefab_HeadForward_MatchesBakerRule(RoleId _role)
        {
            GameObject prefab = LoadPrefab(_role);
            EyeLook baked = Require<EyeLook>(prefab);

            // EyeAnchorBaker.BakeHeadForward와 같은 규칙: 몸 정면을 머리 본 로컬로, x는 0으로 스냅.
            Transform head = FindDeep(prefab.transform, "head");
            Assert.IsTrue(head != null, $"{prefab.name}: 머리 본이 없다");
            Vector3 forward = head.InverseTransformDirection(prefab.transform.forward);
            forward.x = 0f;
            Assert.Greater(forward.sqrMagnitude, 1e-8f);

            AssertVector(forward.normalized, Prop(baked, "m_HeadForwardLocal").vector3Value, 1e-3f,
                $"{prefab.name}.m_HeadForwardLocal이 모델의 머리 본 정면과 다르다 — Bake Eye Anchors를 돌려라");
        }

        // ───────────────────────── B-10 ─────────────────────────

        [NUnit.Framework.Property("CaseId", "B-10")]
        [TestCase(RoleId.Cat)]
        [TestCase(RoleId.Mouse)]
        public void Prefab_BakedValues_MatchPlayerInfo(RoleId _role)
        {
            PlayerInfo info = InfoManager.Instance.PlayerOf(_role);
            GameObject prefab = LoadPrefab(_role);
            string who = $"{prefab.name}({_role})";
            const string HINT = " — PlayerInfo.csv를 바꿨으면 씬을 재생성하라";

            CharacterController cc = Require<CharacterController>(prefab);
            Assert.AreEqual(info.CcHeight, cc.height, EPS, $"{who} CcHeight{HINT}");
            Assert.AreEqual(info.CcRadius, cc.radius, EPS, $"{who} CcRadius{HINT}");

            PlayerMovement move = Require<PlayerMovement>(prefab);
            Assert.AreEqual(info.MoveSpeed, Prop(move, "m_MoveSpeed").floatValue, EPS, $"{who} MoveSpeed{HINT}");
            Assert.AreEqual(info.TurnSpeedDegPerSec, Prop(move, "m_TurnSpeedDegPerSec").floatValue, EPS,
                $"{who} TurnSpeedDegPerSec{HINT}");

            PlayerAnimationDriver anim = Require<PlayerAnimationDriver>(prefab);
            Assert.AreEqual(info.RunThreshold, Prop(anim, "m_RunThreshold").floatValue, EPS, $"{who} RunThreshold{HINT}");

            PlayerCameraBinder cam = Require<PlayerCameraBinder>(prefab);
            Assert.AreEqual(info.Fov, Prop(cam, "m_CameraFov").floatValue, EPS, $"{who} Fov{HINT}");
            Assert.AreEqual(info.CamDistance, Prop(cam, "m_CameraDistance").floatValue, EPS, $"{who} CamDistance{HINT}");

            Health health = Require<Health>(prefab);
            Assert.AreEqual(info.MaxHp, Prop(health, "m_MaxHp").intValue, $"{who} MaxHp{HINT}");

            FootstepPlayer steps = Require<FootstepPlayer>(prefab);
            Assert.AreEqual(info.FootstepRangeMeters, Prop(steps, "m_NormalRange").floatValue, EPS,
                $"{who} FootstepRangeMeters{HINT}");
            Assert.AreEqual(info.SneakFootstepRangeMeters, Prop(steps, "m_SneakRange").floatValue, EPS,
                $"{who} SneakFootstepRangeMeters{HINT}");
            var source = Prop(steps, "m_Source").objectReferenceValue as AudioSource;
            Assert.IsTrue(source != null, $"{who} FootstepPlayer.m_Source가 비었다");
            Assert.AreEqual(info.FootstepRangeMeters, source.maxDistance, EPS, $"{who} 발소리 AudioSource.maxDistance{HINT}");

            CatPounce pounce = prefab.GetComponent<CatPounce>();
            if (_role == RoleId.Cat)
            {
                Assert.IsTrue(pounce != null, $"{who}에 CatPounce가 없다");
                Assert.AreEqual(info.PounceDamage, Prop(pounce, "m_Damage").intValue, $"{who} PounceDamage{HINT}");
                Assert.AreEqual(info.PounceCooldown, Prop(pounce, "m_CooldownSeconds").floatValue, EPS,
                    $"{who} PounceCooldown{HINT}");
                Assert.AreEqual(info.PounceRange, Prop(pounce, "m_Range").floatValue, EPS, $"{who} PounceRange{HINT}");
                Assert.AreEqual(info.PounceHalfAngleDeg, Prop(pounce, "m_HalfAngleDeg").floatValue, EPS,
                    $"{who} PounceHalfAngleDeg{HINT}");
            }
            else
            {
                Assert.IsTrue(pounce == null, $"{who}에 CatPounce가 붙어 있다 — 쥐가 덮친다");
            }
        }

        [NUnit.Framework.Property("CaseId", "B-10")]
        [TestCase(RoleId.Cat)]
        [TestCase(RoleId.Mouse)]
        public void Prefab_Stride_IsDerivedFromMoveSpeed(RoleId _role)
        {
            PlayerInfo info = InfoManager.Instance.PlayerOf(_role);
            GameObject prefab = LoadPrefab(_role);
            FootstepPlayer steps = Require<FootstepPlayer>(prefab);

            float cadence = FootstepLogic.LoopStepsPerSecondOf(_role == RoleId.Cat);
            float expected = FootstepLogic.StrideOf(info.MoveSpeed, cadence);
            float baked = Prop(steps, "m_StrideMeters").floatValue;

            Assert.AreEqual(expected, baked, EPS,
                $"{prefab.name} 보폭 {baked}가 이속 {info.MoveSpeed} ÷ 박자 {cadence} = {expected}가 아니다 — 씬을 재생성하라");
            // 기준 이속에서 원래 루프의 박자가 그대로 재현된다(초당 걸음 수 = 이속 ÷ 보폭).
            Assert.AreEqual(cadence, info.MoveSpeed / baked, 1e-3f, $"{prefab.name} 기준 이속에서 발소리 박자가 루프와 다르다");
        }
    }
}
