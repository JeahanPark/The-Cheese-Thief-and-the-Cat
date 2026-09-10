using CTC.Runtime.Lobby;
using CTC.Runtime.Player;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 눈(EyeLook)의 자리·크기를 프리팹에 굽는다 — 종마다 다르고 **모델 지오메트리**라
    /// 밸런스 CSV가 아니라 여기가 원본이다.
    ///
    /// 값의 출처는 `Tools/CTC/Dev/Probe Eye Geometry`가 실제 메시를 잰 결과다
    /// (팔레트 색으로 눈 덩어리를 묶어 **머리 본 로컬** 중심·법선·반경을 뽑는다).
    /// 모델을 갈아 끼우면 그 도구를 다시 돌려 여기 숫자를 바꾼다.
    ///
    /// 씬 생성기(PlayerPrefabFactory)와 메뉴가 **같은 함수**를 부른다 —
    /// 프리팹을 다시 굽든 손으로 고치든 값이 갈리지 않게.
    /// </summary>
    public static class EyeAnchorBaker
    {
        private const string CAT_PREFAB = "Assets/Prefabs/PlayerCat.prefab";
        private const string MOUSE_PREFAB = "Assets/Prefabs/PlayerMouse.prefab";

        public static void Apply(EyeLook _eyes, RoleId _role)
        {
            if (_eyes == null) return;

            var so = new SerializedObject(_eyes);
            if (_role == RoleId.Cat)
            {
                so.FindProperty("m_EyeCenterLocal").vector3Value = new Vector3(0.00106f, 0.00179f, 0.00109f);
                so.FindProperty("m_EyeAxisLocal").vector3Value = new Vector3(0.500f, 0.476f, 0.724f);
                so.FindProperty("m_ScleraRadiusLocal").floatValue = 0.00045f;
                so.FindProperty("m_LidRadiusLocal").floatValue = 0.00075f;
            }
            else
            {
                so.FindProperty("m_EyeCenterLocal").vector3Value = new Vector3(0.00132f, 0.00100f, 0.00034f);
                so.FindProperty("m_EyeAxisLocal").vector3Value = new Vector3(0.892f, 0.396f, 0.216f);
                so.FindProperty("m_ScleraRadiusLocal").floatValue = 0.00055f;
                so.FindProperty("m_LidRadiusLocal").floatValue = 0.00090f;
            }
            BakeHeadForward(so, _eyes.transform);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 눈이 **똑바로 앞을 볼 때**의 축 — 몸 정면이 머리 본 로컬로 어느 쪽인지 **모델에서 잰다**.
        ///
        /// 손으로 적으면 안 된다. 팩의 두 FBX가 서로 다르게 리깅돼 있어서
        /// (2026-09-10 실측: 고양이 머리 본은 +z가, 쥐는 +y가 대충 앞이다) 종이 늘 때마다 틀린다.
        /// 이 값이 있어야 눈동자가 정면에서 흰자 한가운데 앉고 좌우 여유를 다 갖는다 —
        /// 전에는 쥐가 정면에서도 이미 한계(30°)에 붙어 더 돌 자리가 없었다.
        ///
        /// x는 0으로 스냅한다. 리그의 미세한 좌우 오차가 그대로 남으면 **정면이 한쪽으로 치우쳐**
        /// 두 눈동자가 가만히 있어도 비뚤어 보인다.
        /// </summary>
        private static void BakeHeadForward(SerializedObject _so, Transform _root)
        {
            Transform head = FindDeep(_root, "head");
            if (head == null)
            {
                Debug.LogWarning("[EyeAnchorBaker] 머리 본을 못 찾았다 — 정면축을 못 굽는다");
                return;
            }

            Vector3 forward = head.InverseTransformDirection(_root.forward);
            forward.x = 0f;
            if (forward.sqrMagnitude <= 1e-8f) return;

            _so.FindProperty("m_HeadForwardLocal").vector3Value = forward.normalized;
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

        /// <summary>씬을 다시 굽지 않고 두 프리팹의 눈 값만 맞춘다.</summary>
        [MenuItem("Tools/CTC/Dev/Bake Eye Anchors")]
        public static void BakeIntoPrefabs()
        {
            BakeOne(CAT_PREFAB, RoleId.Cat);
            BakeOne(MOUSE_PREFAB, RoleId.Mouse);
            AssetDatabase.SaveAssets();
        }

        private static void BakeOne(string _path, RoleId _role)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(_path);
            if (root == null)
            {
                Debug.LogError($"[EyeAnchorBaker] 프리팹 없음: {_path}");
                return;
            }

            try
            {
                var eyes = root.GetComponent<EyeLook>();
                if (eyes == null) eyes = root.AddComponent<EyeLook>();
                Apply(eyes, _role);
                PrefabUtility.SaveAsPrefabAsset(root, _path);
                Debug.Log($"[EyeAnchorBaker] 구움: {_path} ({_role})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
