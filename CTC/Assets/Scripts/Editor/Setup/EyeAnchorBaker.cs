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
            so.ApplyModifiedPropertiesWithoutUndo();
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
