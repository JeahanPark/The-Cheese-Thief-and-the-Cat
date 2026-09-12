using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CTC.Editor
{
    /// <summary>
    /// 쥐가 등 뒤 허리춤에서 뽑아 쏘고 다시 넣는 장난감 같은 권총을 통짜 메시로 굽는다 —
    /// Tools > CTC > Build Gun Mesh.
    ///
    /// 형상은 딱 둘이다 — 모서리를 둥글린 총열/슬라이드 박스와, 뒤로 기운 손잡이 박스를
    /// 스무스 유니언으로 이어 이음매를 필렛으로 만든다. 방아쇠울·조준기·별도 방아쇠는
    /// 일부러 안 넣는다 — 전장 0.2m급에서는 그런 디테일이 실루엣 노이즈로만 읽힌다
    /// (CheeseMeshBuilder에서 이미 겪은 교훈: 손으로 짠 디테일은 이 톤에 안 맞는다).
    ///
    /// 좌표 규약: **원점 = 손잡이(파지 지점), +Z = 총구 방향, +Y = 위.**
    /// 나중에 본에 붙이는 쪽이 로컬 회전 하나로 끝나게 하기 위한 약속이다 — 퍼즐이 아니라
    /// 계산이 되도록.
    ///
    /// 치수 근거(2026-09-12 실측 — GunSizeProbeTemp로 Mouse_LOD0.fbx를 직접 재고 지웠다):
    /// 쥐 전신 월드 바운드 ≈ (0.40, 0.50, 0.81)m, 앞다리(leg.F.R) 본 밑동→발끝(스킨 정점 기준)
    /// ≈ 0.19m, 머리(head 본에 매인 정점) 바운드 ≈ (0.33, 0.29, 0.23)m — 앞뒤 깊이가 약 0.23m.
    /// 목표가 "머리 길이만큼"이라 총 전장을 0.22m로 잡았다: 머리 깊이(0.23m)에 가까우면서
    /// 앞다리 길이(0.19m)와도 같은 급이라, 실루엣에서 읽히되 파지했을 때 과하게 커 보이지 않는다.
    ///
    /// 다시 실행하면 같은 에셋에 덮어쓴다(멱등) — 메시·머티리얼·프리팹 모두 GUID를 유지하므로
    /// 나중에 본에 물린 프리팹 참조가 안 끊긴다.
    /// </summary>
    public static class GunMeshBuilder
    {
        private const string PREFAB_PATH = "Assets/Prefabs/Props/MouseGun.prefab";

        // ── 총열/슬라이드 ───────────────────────────────────────────────
        // 반각(half-extent) 기준: 폭(X) 0.04m · 높이(Y) 0.056m · 길이(Z) 0.17m.
        // 1차 렌더에서 손잡이와 두께가 비슷해 부메랑처럼 보였다 — 슬라이드를 조금 더 굵혀
        // "총열 vs 손잡이"가 실루엣만으로 갈라지게 한다(2026-09-12 2차 조정).
        private static readonly Vector3 BARREL_HALF = new Vector3(0.020f, 0.028f, 0.085f);
        private const float BARREL_ROUND = 0.011f;

        /// <summary>총열 중심. Z를 손잡이보다 앞(+)으로 밀어 총구가 원점보다 한참 앞에 오게 한다.</summary>
        private static readonly Vector3 BARREL_CENTER = new Vector3(0f, 0.060f, 0.060f);

        // ── 손잡이 ──────────────────────────────────────────────────────
        // 반각 기준: 폭(X) 0.038m · 높이(Y) 0.104m · 두께(Z) 0.042m.
        // 두께(Z)를 0.017→0.021로 늘렸다 — 파지감을 위해서라기보다(그건 애니메이터 몫이다)
        // 실루엣에서 총열과 확실히 다른 "덩어리"로 읽히게 하기 위해서다.
        private static readonly Vector3 GRIP_HALF = new Vector3(0.019f, 0.052f, 0.021f);
        private const float GRIP_ROUND = 0.010f;

        /// <summary>손잡이 중심(기울이기 전, 자기 로컬 원점 기준으로 이 위치에 둔다).</summary>
        private static readonly Vector3 GRIP_CENTER = new Vector3(0f, -0.010f, -0.026f);

        /// <summary>
        /// 손잡이를 뒤로 기울이는 각. 실제 권총처럼 총열 쪽(위)이 앞으로, 손잡이 끝(아래)이
        /// 뒤로 가야 손목이 자연스럽게 꺾인 실루엣이 나온다 — X축 회전 하나로 충분하다.
        /// </summary>
        private const float GRIP_TILT_DEG = 16f;

        /// <summary>
        /// 이음매 필렛. 1차 렌더(0.016)는 총열 두께의 60%에 달해 손잡이·총열 경계가 뭉개져
        /// 보였다 — 0.010으로 줄여 "이어붙였지만 각지지는 않은" 정도로 낮췄다(2026-09-12).
        /// </summary>
        private const float UNION_FILLET = 0.010f;

        // ── 격자 ────────────────────────────────────────────────────────
        // 총 전장이 0.02m 안팎인 부재들로 이루어져, 요새(CELL_SIZE 0.055)보다 훨씬 촘촘해야
        // 필렛과 두께가 뭉개지지 않는다. 3.5mm면 가장 얇은 손잡이 두께(0.034m)에도 셀이
        // 10칸 가까이 걸쳐 표면이 부드럽게 나온다.
        private const float CELL_SIZE = 0.0035f;

        // 격자 경계 — 두 형상(회전한 손잡이 포함)을 넉넉히 감싸도록 실측 후 잡았다.
        private static readonly Vector3 GRID_MIN = new Vector3(-0.05f, -0.09f, -0.08f);
        private static readonly Vector3 GRID_MAX = new Vector3(0.05f, 0.11f, 0.17f);

        private const int RELAX_ITERATIONS = 6;
        private const float RELAX_STRENGTH = 0.5f;
        private const int NORMAL_SMOOTH_ITERATIONS = 3;

        /// <summary>무광 짙은 회색 — 팩 팔레트가 무채색을 안 쓰므로 검정도 크롬도 피한다.</summary>
        private static readonly Color GUN_COLOR = new Color(0.24f, 0.24f, 0.26f);
        private const float GUN_SMOOTHNESS = 0.35f;

        [MenuItem("Tools/CTC/Build Gun Mesh")]
        public static void Build()
        {
            Mesh built = SdfMesher.Build("Mesh_MouseGun", SampleField, GRID_MIN, GRID_MAX,
                CELL_SIZE, RELAX_ITERATIONS, RELAX_STRENGTH, NORMAL_SMOOTH_ITERATIONS);

            Mesh mesh = SdfMesher.SaveMesh(built, PropAssets.MOUSE_GUN_MESH);
            Material material = SdfMesher.SaveMaterial(PropAssets.MOUSE_GUN_MATERIAL, "M_MouseGun", GUN_COLOR, GUN_SMOOTHNESS);
            MakeTransparent(material);
            SdfMesher.Commit(PropAssets.MOUSE_GUN_MESH);

            SavePrefab(mesh, material);

            Debug.Log($"[GunMeshBuilder] 완료 → {PREFAB_PATH} " +
                      $"(정점 {mesh.vertexCount} / 삼각형 {mesh.triangles.Length / 3})");
        }

        /// <summary>
        /// 총 머티리얼을 **반투명**으로 세운다 — 총이 뽑히고 넣히는 동안 알파로 떴다 사라지기 때문이다
        /// (<see cref="CTC.Runtime.Player.GunFadeLogic"/>·<c>MouseGunVisual</c>).
        ///
        /// **왜 여기(빌더)서 하는가**: 이 에셋의 주인은 빌더다. 런타임에서 고치면 매 판 켜질 때마다
        /// 머티리얼을 손대야 하고, 저장된 <c>.mat</c>은 여전히 불투명이라 **에디터에서 본 것과
        /// 게임에서 보는 것이 갈린다.** 굽는 자리에 두면 디스크의 값이 곧 진실이다.
        ///
        /// **왜 알파만 낮추면 안 되는가**: URP/Lit은 Surface Type이 Opaque인 동안 알파를
        /// **통째로 무시한다** — 에러도 경고도 없이 총이 계속 또렷하게 보인다(조용히 깨지는 종류).
        /// 그래서 표면 타입·블렌드 팩터·ZWrite·렌더 큐·키워드를 다 같이 바꾼다. 하나라도 빠지면
        /// 안 되므로 한 함수에 모아 둔다.
        /// </summary>
        private static void MakeTransparent(Material _material)
        {
            if (_material == null) return;

            _material.SetFloat("_Surface", 1f);  // 0 = Opaque, 1 = Transparent
            _material.SetFloat("_Blend", 0f);    // 0 = Alpha (Premultiply·Additive·Multiply가 아니다)
            _material.SetFloat("_AlphaClip", 0f);

            // SrcAlpha / OneMinusSrcAlpha — 평범한 알파 블렌딩.
            // 보존 스페큘러(_BlendModePreserveSpecular)는 끈다: 켜면 URP가 SrcBlend를 One으로
            // 바꾸고 셰이더에서 미리 곱해 버려, 여기 적은 팩터와 실제가 달라진다.
            _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            _material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            _material.SetFloat("_BlendModePreserveSpecular", 0f);

            // 반투명은 깊이를 쓰지 않는다 — 쓰면 뒤에 있는 것이 지워져 총 너머가 안 보인다.
            _material.SetFloat("_ZWrite", 0f);
            _material.SetFloat("_QueueOffset", 0f);

            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _material.DisableKeyword("_ALPHATEST_ON");
            _material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            _material.renderQueue = (int)RenderQueue.Transparent; // 3000
            _material.SetOverrideTag("RenderType", "Transparent");

            EditorUtility.SetDirty(_material);
        }

        /// <summary>총열(회전 없음)과 손잡이(X축으로 기울인 로컬 프레임) 를 스무스 유니언한다.</summary>
        private static float SampleField(Vector3 _p)
        {
            float barrel = SdfMesher.RoundBox(_p, BARREL_CENTER, BARREL_HALF, BARREL_ROUND);

            // 손잡이만 기운 로컬 프레임에서 샘플한다 — 회전한 박스를 만들 때 SdfMesher에
            // 회전 API가 없으므로, 샘플 지점을 반대로 돌려 손잡이의 축 정렬 박스로 되돌린다.
            Vector3 gripLocal = Quaternion.Euler(GRIP_TILT_DEG, 0f, 0f) * (_p - GRIP_CENTER);
            float grip = SdfMesher.RoundBox(gripLocal, Vector3.zero, GRIP_HALF, GRIP_ROUND);

            return SdfMesher.SmoothUnion(barrel, grip, UNION_FILLET);
        }

        /// <summary>
        /// 콜라이더는 붙이지 않는다 — 본에 매다는 시각 전용 프롭이라 물리가 필요 없다.
        /// CheeseMeshBuilder와 같은 이유로 같은 경로에 그대로 덮어써 GUID를 지킨다.
        /// </summary>
        private static void SavePrefab(Mesh _mesh, Material _material)
        {
            var root = new GameObject("MouseGun");
            var filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;

            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            SdfMesher.EnsureFolders(PREFAB_PATH);
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            Object.DestroyImmediate(root);
        }
    }
}
