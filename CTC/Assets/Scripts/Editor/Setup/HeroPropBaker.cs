using CTC.Runtime.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CTC.Editor
{
    /// <summary>
    /// 용사 쥐 소품(2026-09-19) — **칼**을 SDF로 굽고 쥐 프리팹의 오른 앞발에 꺼진 채로 단다. 무적 테두리 머티리얼도 여기서 배선한다.
    /// 메뉴 `Tools/CTC/Dev/Bake Hero Props (칼)`. 설계: docs/superpowers/specs/2026-09-19-hero-sword-design.md §5-4.
    ///
    /// - 칼은 **날(강철)·자루(금)** 두 조각이다 — 머티리얼이 달라서. 원점 = 손잡이 가운데, +Z = 날.
    /// - 앞발 뼈(<see cref="MouseGunBaker.GUN_BONE"/>) 끝에 붙인다. **방향은 굽는 값이 아니라 게임이 매 프레임 준다**
    ///   (<see cref="HeroLook"/> — 리그에 손목이 없다). 그래서 여기서는 자리만 정한다.
    /// - 머티리얼은 **반투명으로 굽는다** — 알파로 나타나야 하므로(불투명이면 URP가 알파를 조용히 무시한다).
    /// - 길이 0.40m(날 0.29) — 쥐 몸(0.57m)의 70%. 기획의 60%보다 조금 길다: 뒤 카메라에서 큰 둥근 몸 너머로 칼끝이 보여야 한다(렌더 확인).
    /// </summary>
    public static class HeroPropBaker
    {
        private const string MOUSE_PREFAB = "Assets/Prefabs/PlayerMouse.prefab";
        private const string BLADE_MESH = PropAssets.PROP_FOLDER + "/Mesh_HeroBlade.asset";
        private const string HILT_MESH = PropAssets.PROP_FOLDER + "/Mesh_HeroHilt.asset";
        private const string BLADE_MAT = "Assets/Materials/Props/M_HeroBlade.mat";
        private const string HILT_MAT = "Assets/Materials/Props/M_HeroHilt.mat";
        private const string RIM_MAT = "Assets/Materials/Props/M_HeroRim.mat";
        private const string RIM_SHADER = "CTC/HeroRim";

        // 무적 테두리 세기 — 첫 판(2.2 · 1.4)은 둥근 몸이 통째로 하얗게 떴다(인게임 촬영, 2026-09-19). 가장자리만 빛나게 좁혔다.
        private const float RIM_POWER = 3.2f;
        private const float RIM_INTENSITY = 1.1f;
        private static readonly Color RIM_COLOR = new Color(1f, 0.97f, 0.85f);

        private const string BLADE_NAME = "Blade";
        private const string HILT_NAME = "Hilt";

        private static readonly Color BLADE_COLOR = new Color(0.86f, 0.90f, 0.96f);   // 푸른 기 도는 강철 — 밤 맵에서 회색으로 묻히지 않게 밝게
        private static readonly Color HILT_COLOR = new Color(0.95f, 0.72f, 0.22f);    // 금 — 「용사의 칼」

        /// <summary>
        /// 손잡이 가운데 = 앞발 뼈 끝 + 15mm(뼈 로컬, 단위 1/100m — 뼈 스케일이 100이다).
        /// 앞다리 뼈는 155.2mm(어깨 → 발끝)라 0.0017 = 170mm. 블렌더 미리보기(`sword_swing.py`)와 같은 자리다.
        /// </summary>
        private static readonly Vector3 GRIP_LOCAL_POSITION = new Vector3(0f, 0.0017f, 0f);

        // 치수(m) — 원점 = 손잡이 가운데, +Z = 날.
        private const float BLADE_START = 0.05f;
        private const float BLADE_TIP = 0.34f;
        private const float BLADE_HALF_WIDTH = 0.016f;
        private const float BLADE_HALF_THICK = 0.0035f;
        private const float BLADE_POINT = 0.055f;           // 끝을 뾰족하게 깎는 길이
        private const float GRIP_HALF = 0.035f;
        private const float GRIP_RADIUS = 0.0095f;
        private const float GUARD_Z = 0.043f;
        private const float GUARD_HALF_WIDTH = 0.046f;

        [MenuItem("Tools/CTC/Dev/Bake Hero Props (칼)")]
        public static void BakeAll()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(MOUSE_PREFAB);
            if (root == null)
            {
                Debug.LogError($"[HeroPropBaker] 프리팹 없음: {MOUSE_PREFAB}");
                return;
            }
            try
            {
                Attach(root);
                PrefabUtility.SaveAsPrefabAsset(root, MOUSE_PREFAB);
                Debug.Log($"[HeroPropBaker] 칼·무적 테두리를 달았다: {MOUSE_PREFAB}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>프리팹 계층에 단다 — 생성기(<c>PlayerPrefabFactory</c>)가 갓 만든 계층에도 같이 쓴다. 멱등.</summary>
        public static void Attach(GameObject _root)
        {
            if (_root == null) return;

            Transform paw = MouseGunBaker.FindBone(_root.transform, MouseGunBaker.GUN_BONE);
            if (paw == null)
            {
                Debug.LogError($"[HeroPropBaker] '{MouseGunBaker.GUN_BONE}' 본이 없다 — 칼을 못 단다");
                return;
            }

            Mesh blade = BuildBladeMesh();
            Mesh hilt = BuildHiltMesh();
            Material bladeMat = SaveMetal(BLADE_MAT, "M_HeroBlade", BLADE_COLOR, 0.82f, 0.35f);
            Material hiltMat = SaveMetal(HILT_MAT, "M_HeroHilt", HILT_COLOR, 0.6f, 0.3f);

            // 멱등 — 자리의 주인이 이 코드라 「있으면 안 덮어쓴다」 규칙의 대상이 아니다(MouseGunBaker와 같다).
            Transform old = paw.Find(HeroLook.SWORD_NAME);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var sword = new GameObject(HeroLook.SWORD_NAME);
            MouseGunBaker.Place(sword.transform, paw, GRIP_LOCAL_POSITION, Vector3.zero);
            sword.tag = paw.gameObject.tag;
            sword.layer = paw.gameObject.layer;
            AddPart(sword.transform, BLADE_NAME, blade, bladeMat);
            AddPart(sword.transform, HILT_NAME, hilt, hiltMat);
            sword.SetActive(false);   // 쓸 때만 보인다(HeroLook)

            if (_root.GetComponent<HeroLook>() == null) _root.AddComponent<HeroLook>();

            Material rim = SaveRimMaterial();
            var visual = _root.GetComponent<HeroInvincibleVisual>();
            if (visual == null) visual = _root.AddComponent<HeroInvincibleVisual>();
            var so = new SerializedObject(visual);
            so.FindProperty("m_RimMaterial").objectReferenceValue = rim;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddPart(Transform _parent, string _name, Mesh _mesh, Material _mat)
        {
            var go = new GameObject(_name);
            go.transform.SetParent(_parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            go.tag = _parent.gameObject.tag;
            go.layer = _parent.gameObject.layer;
        }

        private static Material SaveMetal(string _path, string _name, Color _color, float _smoothness, float _metallic)
        {
            Material mat = SdfMesher.SaveMaterial(_path, _name, _color, _smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", _metallic);
            GunMeshBuilder.MakeTransparent(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// 무적 테두리(<c>CTC/HeroRim</c>). **셰이더 로드를 널 검사한다** — 대규모 리임포트 중엔 null이 오고
        /// `new Material(null)`은 마젠타를 조용히 굽는다(함정 6).
        /// </summary>
        private static Material SaveRimMaterial()
        {
            Shader shader = Shader.Find(RIM_SHADER);
            if (shader == null)
            {
                Debug.LogError($"[HeroPropBaker] '{RIM_SHADER}' 셰이더가 없다 — Assets/Shaders/CTC_HeroRim.shader 확인(리임포트 중이면 끝난 뒤 다시)");
                return AssetDatabase.LoadAssetAtPath<Material>(RIM_MAT);
            }
            SdfMesher.EnsureFolders(RIM_MAT);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(RIM_MAT);
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_HeroRim" };
                AssetDatabase.CreateAsset(mat, RIM_MAT);
            }
            mat.shader = shader;
            // 셰이더 기본값이 아니라 여기 값이 주인이다 — 이미 있는 머티리얼은 셰이더 기본값을 바꿔도 안 따라온다.
            mat.SetColor("_BaseColor", RIM_COLOR);
            mat.SetFloat("_Power", RIM_POWER);
            mat.SetFloat("_Intensity", RIM_INTENSITY);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ── 메시 ─────────────────────────────────────────────

        /// <summary>곧은 양날 — 얇은 둥근 판을 끝에서 두 평면으로 깎아 뾰족하게 만든다.</summary>
        private static Mesh BuildBladeMesh()
        {
            float mid = (BLADE_START + BLADE_TIP) * 0.5f;
            float half = (BLADE_TIP - BLADE_START) * 0.5f;
            float slope = BLADE_HALF_WIDTH / BLADE_POINT;           // 끝 깎는 기울기
            float norm = Mathf.Sqrt(1f + slope * slope);
            System.Func<Vector3, float> field = _p =>
            {
                float body = SdfMesher.RoundBox(_p, new Vector3(0f, 0f, mid),
                    new Vector3(BLADE_HALF_WIDTH, BLADE_HALF_THICK, half), BLADE_HALF_THICK * 0.8f);
                // |x| ≤ (tip − z)·slope — 끝으로 갈수록 좁아진다
                float point = (Mathf.Abs(_p.x) + (_p.z - BLADE_TIP) * slope) / norm;
                return SdfMesher.Intersect(body, point);
            };
            float pad = 0.006f;
            Mesh built = SdfMesher.Build("Mesh_HeroBlade", field,
                new Vector3(-BLADE_HALF_WIDTH - pad, -BLADE_HALF_THICK - pad, BLADE_START - pad),
                new Vector3(BLADE_HALF_WIDTH + pad, BLADE_HALF_THICK + pad, BLADE_TIP + pad),
                0.0014f, 2, 0.3f, 2);
            Mesh mesh = SdfMesher.SaveMesh(built, BLADE_MESH);
            SdfMesher.Commit(BLADE_MESH);
            return mesh;
        }

        /// <summary>자루 — 둥근 손잡이 + 가로 코등이(양끝이 둥글다) + 끝 구슬.</summary>
        private static Mesh BuildHiltMesh()
        {
            System.Func<Vector3, float> field = _p =>
            {
                Vector3 q = new Vector3(_p.x, _p.z, -_p.y);   // 원기둥을 Z축으로 쓰려고 Z → Y
                float grip = SdfMesher.RoundCylinderY(q, Vector3.zero, GRIP_RADIUS, GRIP_HALF, 0.003f);
                float pommel = SdfMesher.Sphere(_p, new Vector3(0f, 0f, -GRIP_HALF - 0.008f), 0.014f);
                float guard = SdfMesher.RoundBox(_p, new Vector3(0f, 0f, GUARD_Z),
                    new Vector3(GUARD_HALF_WIDTH, 0.008f, 0.007f), 0.005f);
                float knobL = SdfMesher.Sphere(_p, new Vector3(-GUARD_HALF_WIDTH, 0f, GUARD_Z), 0.011f);
                float knobR = SdfMesher.Sphere(_p, new Vector3(GUARD_HALF_WIDTH, 0f, GUARD_Z), 0.011f);
                float d = SdfMesher.SmoothUnion(grip, pommel, 0.004f);
                d = SdfMesher.SmoothUnion(d, guard, 0.004f);
                d = SdfMesher.SmoothUnion(d, knobL, 0.003f);
                return SdfMesher.SmoothUnion(d, knobR, 0.003f);
            };
            Mesh built = SdfMesher.Build("Mesh_HeroHilt", field,
                new Vector3(-GUARD_HALF_WIDTH - 0.016f, -0.02f, -GRIP_HALF - 0.03f),
                new Vector3(GUARD_HALF_WIDTH + 0.016f, 0.02f, GUARD_Z + 0.02f),
                0.0022f, 3, 0.4f, 2);
            Mesh mesh = SdfMesher.SaveMesh(built, HILT_MESH);
            SdfMesher.Commit(HILT_MESH);
            return mesh;
        }
    }
}
