using System;
using System.Collections.Generic;
using CTC.Runtime.Map;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 맵 천장 재료(2026-09-14) — 판·들보 머티리얼 둘과 들보에 매다는 식료품 프리팹 다섯을 굽고 <see cref="MapAtmosphereProfile"/>에 꽂는다.
    /// 메뉴 `Tools/CTC/Map/Build Ceiling (천장)` — `Build Atmosphere`도 끝에 이걸 부른다(재생성 때 천장이 빠지지 않게).
    /// 계획: docs/superpowers/plans/2026-09-14-map-ceiling.md
    ///
    /// 매달린 것은 피벗이 **꼭대기(0)**이고 아래로 자란다 — 키는 <see cref="MapCeilingLogic.ITEM_MAX_HEIGHT"/> 안.
    /// 그보다 길면 카메라 최고 높이(≈4m)에 닿는다.
    /// </summary>
    public static class MapCeilingBuilder
    {
        private const string MESH_DIR = "Assets/Models/Atmosphere/Ceiling";
        private const string MAT_DIR = "Assets/Materials/Atmosphere/Ceiling";
        private const string PREFAB_DIR = "Assets/Prefabs/Fx/Map/Ceiling";
        private const string PROFILE_PATH = "Assets/Resources/Map/MapAtmosphere.asset";
        private const string WOOD_TEX = "Assets/Materials/Props/Textures/T_WoodCrate.png";
        private const string CORD_MESH = "Assets/Models/Atmosphere/LampCord.asset";
        private const string CORD_MAT = "Assets/Materials/Atmosphere/M_LampCord.mat";

        private const float CELL = 0.007f;

        // 채움광 — 밤 환경광은 아래를 향한 면에 거의 0이라 천장이 새까만 구멍이었다(2026-09-14 첫 촬영).
        // 전등 빛웅덩이보다는 한참 어두워야 한다 — 천장이 밝으면 "빛 = 위험" 읽기가 흐려진다.
        // 1차(0.22 · 0.30)는 전등에서 먼 천장이 여전히 캄캄해 **덮였다는 게 안 읽혔다**(2026-09-14 사용자: 조금 밝히자) → 약 1.9배.
        private static readonly Color FILL_DECK = new Color(0.42f, 0.36f, 0.31f);
        private static readonly Color FILL_BEAM = new Color(0.56f, 0.47f, 0.39f);
        private const float HANG_SELF_GLOW = 0.16f;

        [MenuItem("Tools/CTC/Map/Build Ceiling (천장)")]
        public static void Build()
        {
            var profile = AssetDatabase.LoadAssetAtPath<MapAtmosphereProfile>(PROFILE_PATH);
            if (profile == null)
            {
                Debug.LogError($"[MapCeiling] 프로필이 없다: {PROFILE_PATH} — Tools/CTC/Map/Build Atmosphere를 먼저 돌려라");
                return;
            }
            Shader triplanar = Shader.Find(MapAssets.SHADER_NAME);
            var wood = AssetDatabase.LoadAssetAtPath<Texture2D>(WOOD_TEX);
            // 함정 6 — 대규모 리임포트 중엔 셰이더가 null로 온다. 마젠타를 굽느니 멈춘다.
            if (triplanar == null || wood == null)
            {
                Debug.LogError($"[MapCeiling] 셰이더·텍스처를 못 찾았다 shader={triplanar} wood={wood}");
                return;
            }
            SdfMesher.EnsureFolders(MESH_DIR + "/x");
            SdfMesher.EnsureFolders(MAT_DIR + "/x");
            SdfMesher.EnsureFolders(PREFAB_DIR + "/x");

            // 판은 밝은 쪽, 들보는 더 짙게 — 전등 빛을 받으면 들보 줄무늬가 판 위로 읽힌다.
            Material deck = Triplanar(triplanar, wood, "M_CeilingDeck", new Color(0.62f, 0.50f, 0.40f), 2.4f, FILL_DECK);
            Material beam = Triplanar(triplanar, wood, "M_CeilingBeam", new Color(0.46f, 0.34f, 0.25f), 1.2f, FILL_BEAM);
            Material window = WindowMaterial();
            if (window == null) return;

            var prefabs = new List<GameObject>
            {
                Hanging("Hang_GarlicBraid", GarlicBraid, new Color(0.90f, 0.86f, 0.76f), 0.35f),
                Hanging("Hang_OnionString", OnionString, new Color(0.72f, 0.42f, 0.20f), 0.45f),
                Hanging("Hang_Sausages", Sausages, new Color(0.55f, 0.22f, 0.16f), 0.4f),
                Hanging("Hang_Ham", Ham, new Color(0.62f, 0.34f, 0.23f), 0.35f),
                Hanging("Hang_CheeseWheel", CheeseWheel, new Color(0.95f, 0.74f, 0.26f), 0.3f),
            };

            var so = new SerializedObject(profile);
            so.FindProperty("m_CeilingDeckMaterial").objectReferenceValue = deck;
            so.FindProperty("m_CeilingBeamMaterial").objectReferenceValue = beam;
            so.FindProperty("m_CeilingWindowMaterial").objectReferenceValue = window;
            SerializedProperty arr = so.FindProperty("m_HangingPrefabs");
            arr.arraySize = prefabs.Count;
            for (int i = 0; i < prefabs.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            so.FindProperty("m_HangCordMesh").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Mesh>(CORD_MESH);
            so.FindProperty("m_HangCordMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(CORD_MAT);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MapCeiling] 완료 — 머티리얼 3(판·들보·채광창) · 매달린 것 {prefabs.Count} → {PROFILE_PATH}");
        }

        private static Material Triplanar(Shader _shader, Texture2D _tex, string _name, Color _tint, float _tileMeters, Color _fill)
        {
            string path = $"{MAT_DIR}/{_name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(_shader) { name = _name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = _shader;
            mat.SetTexture("_BaseMap", _tex);
            mat.SetColor("_BaseColor", _tint);
            mat.SetFloat("_TileMeters", _tileMeters);
            mat.SetFloat("_SkirtMeters", 0f);
            mat.SetColor("_FillLight", _fill);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// 닫힌 채광창 유리 — 조명과 무관하게 **옅은 달빛 색으로 스스로 빛난다**(Unlit). 천장은 다 덮였지만 달빛 기둥의 출처가 읽혀야 한다.
        /// 블룸 임계(1.0)는 안 넘긴다 — 천장이 번쩍이면 전등보다 눈을 끈다.
        /// </summary>
        private static Material WindowMaterial()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null)
            {
                Debug.LogError("[MapCeiling] URP Unlit 셰이더를 못 찾았다 — 리임포트가 끝난 뒤 다시 돌려라(함정 6)");
                return null;
            }
            string path = $"{MAT_DIR}/M_CeilingWindow.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(unlit) { name = "M_CeilingWindow" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = unlit;
            // 0.30·0.42·0.62는 후보정(노출 1.7) 뒤 거의 흰 하늘로 떠서 **구멍처럼** 읽혔다(2026-09-14 촬영) — 덮인 천장이 우선이다.
            mat.SetColor("_BaseColor", new Color(0.10f, 0.15f, 0.24f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ── 매달린 것 ─────────────────────────────────────────

        private static GameObject Hanging(string _name, Func<Vector3, float> _field, Color _color, float _smooth)
        {
            float h = MapCeilingLogic.ITEM_MAX_HEIGHT;
            Mesh built = SdfMesher.Build(_name, _field, new Vector3(-0.2f, -h - 0.02f, -0.2f), new Vector3(0.2f, 0.03f, 0.2f), CELL, 4, 0.5f, 2);
            string meshPath = $"{MESH_DIR}/Mesh_{_name}.asset";
            Mesh mesh = SdfMesher.SaveMesh(built, meshPath);
            SdfMesher.Commit(meshPath);
            Material mat = SdfMesher.SaveMaterial($"{MAT_DIR}/M_{_name}.mat", "M_" + _name, _color, _smooth);
            // 밤 환경광이 푸르고 약해 제 색이 안 나왔다(양파가 보랏빛 덩어리) — 제 색을 조금 스스로 낸다.
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", _color * HANG_SELF_GLOW);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(mat);

            var root = new GameObject(_name);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = mat;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PREFAB_DIR}/{_name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // 매듭(꼭대기) — 줄이 붙는 자리를 조금 굵게.
        private static float Knot(Vector3 _p) => SdfMesher.Sphere(_p, new Vector3(0f, -0.015f, 0f), 0.022f);

        private static float GarlicBraid(Vector3 _p)
        {
            float d = Knot(_p);
            // 땋은 줄기 + 좌우로 엇갈린 통마늘 다섯
            d = SdfMesher.SmoothUnion(d, SdfMesher.RoundCylinderY(_p, new Vector3(0f, -0.2f, 0f), 0.018f, 0.19f, 0.012f), 0.02f);
            for (int i = 0; i < 5; i++)
            {
                float y = -0.08f - i * 0.075f;
                float x = (i % 2 == 0 ? 1f : -1f) * 0.045f;
                Vector3 c = new Vector3(x, y, (i % 3 - 1) * 0.02f);
                float bulb = SdfMesher.Sphere(Vector3.Scale(_p - c, new Vector3(1f, 1.15f, 1f)) + c, c, 0.05f);
                float tip = SdfMesher.Sphere(_p, c + new Vector3(0f, -0.05f, 0f), 0.012f);
                d = SdfMesher.SmoothUnion(d, SdfMesher.SmoothUnion(bulb, tip, 0.02f), 0.015f);
            }
            return d;
        }

        private static float OnionString(Vector3 _p)
        {
            float d = SdfMesher.SmoothUnion(Knot(_p), SdfMesher.RoundCylinderY(_p, new Vector3(0f, -0.13f, 0f), 0.006f, 0.12f, 0.004f), 0.01f);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 1.7f;
                Vector3 c = new Vector3(Mathf.Cos(angle) * 0.05f, -0.13f - i * 0.08f, Mathf.Sin(angle) * 0.05f);
                float onion = SdfMesher.Sphere(_p, c, 0.062f);
                float top = SdfMesher.Sphere(_p, c + new Vector3(0f, 0.058f, 0f), 0.016f);
                d = SdfMesher.SmoothUnion(d, SdfMesher.SmoothUnion(onion, top, 0.025f), 0.02f);
            }
            return d;
        }

        private static float Sausages(Vector3 _p)
        {
            float d = Knot(_p);
            // 고리 모양으로 늘어진 소시지 줄 — 두 줄기로 갈라져 내려오며 마디마다 매듭
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 c = new Vector3(side * (0.03f + i * 0.02f), -0.08f - i * 0.12f, 0f);
                    float link = SdfMesher.RoundCylinderY(_p, c, 0.028f, 0.055f, 0.026f);
                    d = SdfMesher.SmoothUnion(d, link, 0.012f);
                }
            }
            return d;
        }

        private static float Ham(Vector3 _p)
        {
            float d = SdfMesher.SmoothUnion(Knot(_p), SdfMesher.RoundCylinderY(_p, new Vector3(0f, -0.06f, 0f), 0.016f, 0.05f, 0.01f), 0.015f);
            // 뼈(위) → 살(아래로 부푼 물방울)
            float bone = SdfMesher.RoundCylinderY(_p, new Vector3(0f, -0.13f, 0f), 0.022f, 0.05f, 0.015f);
            Vector3 q = _p - new Vector3(0f, -0.32f, 0f);
            float meat = new Vector3(q.x, q.y * 0.8f, q.z).magnitude - 0.13f;
            d = SdfMesher.SmoothUnion(d, SdfMesher.SmoothUnion(bone, meat, 0.06f), 0.02f);
            return d;
        }

        private static float CheeseWheel(Vector3 _p)
        {
            float d = SdfMesher.SmoothUnion(Knot(_p), SdfMesher.RoundCylinderY(_p, new Vector3(0f, -0.07f, 0f), 0.006f, 0.06f, 0.004f), 0.01f);
            // 세워 매단 치즈 바퀴 — Y 원기둥을 X축으로 눕혀 샘플한다
            Vector3 c = new Vector3(0f, -0.26f, 0f);
            Vector3 q = _p - c;
            Vector3 lying = new Vector3(q.x, q.z, -q.y);
            float wheel = SdfMesher.RoundCylinderY(lying, Vector3.zero, 0.14f, 0.05f, 0.02f);
            // 한 조각 떼어낸 자리 — 치즈 도둑 쥐의 흔적
            float bite = SdfMesher.Box(q, new Vector3(0.1f, -0.1f, 0f), new Vector3(0.08f, 0.08f, 0.08f));
            return SdfMesher.SmoothUnion(d, SdfMesher.SmoothSubtract(wheel, bite, 0.01f), 0.015f);
        }
    }
}
