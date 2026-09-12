using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 클립을 몇 프레임 렌더해서 PNG로 남긴다. 팩 원본과 블렌더 왕복 클립을 나란히 눈으로
    /// 비교하기 위한 도구다. Tools > CTC > Dev > 클립 미리보기 렌더
    ///
    /// **왜 씬에 세우지 않고 오프스크린으로 렌더하는가**: MCP 경로 두 개
    /// (`McpBridgeScene.SaveCurrentModifiedScenesIfUserWantsTo`, `McpBridgeInstantiate.SaveScene`)가
    /// 씬을 자동 저장한다. 지금 열린 씬처럼 저장 경로가 없는 이름 없는 씬에서는 그 저장이
    /// **파일 대화상자를 띄우고, 그 대화상자가 에디터 전체를 멈춘다**(사람이 눌러줘야 풀린다).
    /// <see cref="PreviewRenderUtility"/>는 자기만의 프라이빗 씬·카메라를 갖고 있어서 열린 씬을
    /// 전혀 건드리지 않는다 — 그래서 이 도구는 항상 이걸로만 렌더한다.
    /// </summary>
    public static class ClipPreviewRender
    {
        private const string MOUSE_PREFAB =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Prefabs/Mouse.prefab";

        private const string PACK_FBX =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Animations/Mouse_Animations.fbx";

        private const string ROUNDTRIP_FBX = "Assets/Animations/Pets/CTC_Mouse_Clips.fbx";
        private const string CLIP_NAME = "Walk";

        /// <summary>팩에 없는 신규 클립. 왕복 FBX에서만 렌더한다.</summary>
        private const string SHOOT_CLIP_NAME = "Shoot";

        /// <summary>
        /// 총을 뽑아 첫 발을 쏘는 클립. 기관총 테크가 5배 빨라서 뽑는 동작을 매 발에
        /// 넣을 수 없어 Shoot(반동만)과 따로 존재한다 — 둘을 나란히 봐야 발사 자세가
        /// 서로 맞는지 알 수 있으므로 같은 샘플 간격으로 같이 렌더한다.
        /// </summary>
        private const string SHOOT_DRAW_CLIP_NAME = "ShootDraw";

        /// <summary>
        /// 앞발에 물리는 총. **총 없이 렌더하면 뽑는 프레임이 반쯤만 읽힌다** — 팔이 허리춤으로
        /// 갔다가 앞으로 나오는 것까지는 보이는데, 무엇을 들고 나오는지가 안 보인다.
        /// 게임에서 실제로 보이는 것과 같은 자리·회전으로 붙인다
        /// (<see cref="MouseGunBaker.Place"/>가 그 값의 유일한 원본).
        /// </summary>
        private const string GUN_PREFAB = "Assets/Prefabs/Props/MouseGun.prefab";

        private const int RESOLUTION = 768;

        /// <summary>클립 길이를 이 비율들로 나눠 잰다 — 시작·중간·끝을 고루 본다.</summary>
        private static readonly float[] NORMALIZED_TIMES = { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f };

        /// <summary>
        /// Shoot은 블렌더 프레임 1~11(10프레임)이라 0.1 간격이 **블렌더 프레임과 1:1로 맞는다**.
        /// 리코일(5프레임)이 3프레임과 별개의 박자로 읽히는지 보려면 촘촘해야 해서 11장 전부 뽑는다.
        /// </summary>
        private static readonly float[] SHOOT_NORMALIZED_TIMES =
            { 0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f };

        [MenuItem("Tools/CTC/Dev/클립 미리보기 렌더")]
        public static void Render()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MOUSE_PREFAB);
            if (prefab == null)
            {
                Debug.LogError($"[ClipPreviewRender] 프리팹을 못 찾았다: {MOUSE_PREFAB}");
                return;
            }

            AnimationClip packClip = ClipParityProbe.FindClip(PACK_FBX, CLIP_NAME);
            if (packClip == null)
            {
                Debug.LogError($"[ClipPreviewRender] 팩 클립을 못 찾았다: {PACK_FBX} :: {CLIP_NAME}");
                return;
            }

            var paths = new List<string>();
            paths.AddRange(RenderClip(prefab, packClip, "pack", NORMALIZED_TIMES));

            AnimationClip roundTripClip = ClipParityProbe.FindClip(ROUNDTRIP_FBX, CLIP_NAME);
            if (roundTripClip == null)
            {
                Debug.LogWarning(
                    $"[ClipPreviewRender] 왕복 클립을 못 찾았다(스킵): {ROUNDTRIP_FBX} :: {CLIP_NAME}");
            }
            else
            {
                paths.AddRange(RenderClip(prefab, roundTripClip, "roundtrip", NORMALIZED_TIMES));
            }

            // 사격 클립 둘은 팩에 없다 — 왕복 FBX 한쪽만 렌더한다.
            foreach (string name in new[] { SHOOT_CLIP_NAME, SHOOT_DRAW_CLIP_NAME })
            {
                AnimationClip shootClip = ClipParityProbe.FindClip(ROUNDTRIP_FBX, name);
                if (shootClip == null)
                {
                    Debug.LogWarning(
                        $"[ClipPreviewRender] {name} 클립을 못 찾았다(스킵): {ROUNDTRIP_FBX} :: {name}");
                    continue;
                }

                paths.AddRange(RenderClip(prefab, shootClip, "roundtrip", SHOOT_NORMALIZED_TIMES));
            }

            Debug.Log($"[ClipPreviewRender] {paths.Count}장 렌더 완료:\n" + string.Join("\n", paths));
        }

        private static List<string> RenderClip(
            GameObject _prefab, AnimationClip _clip, string _tag, float[] _normalizedTimes)
        {
            var writtenPaths = new List<string>();
            PreviewRenderUtility preview = null;
            GameObject instance = null;
            // try 밖에서 선언한다 — 오브젝트(GameObject)를 지워도 그 MeshFilter가 물고 있던
            // 독립된 Mesh 애셋은 따로 남는다. 굽는 도중 예외가 나도 finally에서 반드시 지운다.
            var bakedPairs = new List<(SkinnedMeshRenderer smr, Mesh mesh)>();
            var bakedGos = new List<GameObject>();
            GameObject ground = null;
            Material groundMat = null;
            Renderer gunRenderer = null;
            Material gunMat = null;

            try
            {
                preview = new PreviewRenderUtility();

                instance = UnityEngine.Object.Instantiate(_prefab);
                preview.AddSingleGO(instance);

                // 총을 앞발에 물린다. 본의 자식이라 SampleAnimation이 본을 돌리면 그대로 따라간다 —
                // 스킨드 메시와 달리 정적 MeshRenderer는 다시 스키닝할 게 없어 BakeMesh가 필요 없다.
                gunRenderer = AttachGun(instance);
                gunMat = RepaintGunForPreview(gunRenderer, instance);

                // LOD0으로 고정 — 카메라 거리 계산에 따라 다른 LOD가 뽑히면 비교가 안 맞는다.
                var lodGroup = instance.GetComponentInChildren<LODGroup>();
                if (lodGroup != null) lodGroup.ForceLOD(0);

                preview.camera.farClipPlane = 100f;
                preview.camera.nearClipPlane = 0.01f;
                preview.cameraFieldOfView = 35f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.2f, 0.2f, 0.22f);
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                preview.lights[1].intensity = 0.6f;

                // **스킨드 메시는 이 오프스크린 루프 안에서 프레임마다 다시 스키닝되지 않는다**
                // (2026-09-12 실측 — `updateWhenOffscreen=true`를 줘도 마찬가지). 본 회전은 매
                // `SampleAnimation` 호출마다 정확히 바뀌는데(로그로 확인함) `PreviewRenderUtility`가
                // 한 메서드 호출 안에서 매번 동기적으로 `Render()`를 부르다 보니 유니티의 스키닝
                // 갱신이 정상적으로 도는 시점(플레이어 루프 한 틱)을 한 번도 못 거친다 — 여섯 장이
                // 전부 첫 포즈로 고정돼 나왔다. 그래서 `BakeMesh`로 **매 프레임 강제로 CPU 스킨을
                // 굽고**, 원래 SkinnedMeshRenderer는 끈 채 그 결과를 정적 MeshFilter/MeshRenderer로
                // 그린다 — BakeMesh는 호출 즉시 동기적으로 굽어서 이 문제를 완전히 비껴간다.
                // **`useScale: true`로 굽는다 — 실측 확인함(2026-09-12).** MOUSE_PREFAB은 단위
                // 스케일이 아니다: SkinnedMeshRenderer 자신의 트랜스폼(Mesh_LOD0..3)이 45배,
                // 별도 가지인 본(Rig)은 100배다 — 본 쪽 100배는 바인드포즈가 흡수하므로 무관하고,
                // `useScale`이 실제로 상관하는 건 SMR 자신의 45배뿐이다. `BakeMesh(mesh, true)`는
                // 그 45배를 **뺀** 축소 메시를 주고(측정: bounds.size가 원래의 1/45), 아래서 복사하는
                // `smr.transform.localScale`(45)이 그걸 다시 곱해 원래 크기로 되돌린다 — 굽기 전
                // `smr.bounds`와 대조해 확인했다. **`useScale: false`를 썼다면** 이미 다 큰 메시에
                // 45배가 다시 곱해져 실제보다 **45배 커졌을 것**(측정치로 실제 그렇게 나왔다).
                foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var baked = new Mesh { name = smr.name + "_baked" };
                    bakedPairs.Add((smr, baked));

                    var bakedGo = new GameObject(smr.name + "_Baked");
                    bakedGo.transform.SetParent(smr.transform.parent, false);
                    bakedGo.transform.localPosition = smr.transform.localPosition;
                    bakedGo.transform.localRotation = smr.transform.localRotation;
                    bakedGo.transform.localScale = smr.transform.localScale;

                    var mf = bakedGo.AddComponent<MeshFilter>();
                    mf.sharedMesh = baked;
                    var mr = bakedGo.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = smr.sharedMaterials;

                    bakedGos.Add(bakedGo);
                    smr.enabled = false;
                }

                float length = _clip.length;

                // **카메라 틀은 클립 전 구간을 한 번 구워 보고 정한다.** 첫 프레임만 재면 Shoot처럼
                // 중간에 일어서는 클립이 위로 잘린다 — 첫 프레임은 네발로 엎드린 자세라 낮다.
                // SkinnedMeshRenderer.bounds는 여기서 갱신되지 않으므로(위 주석) 구운 메시의
                // bounds를 직접 월드로 옮겨 합친다 — BakeMesh가 동기적으로 채워 주는 값이다.
                Bounds bounds = BakeAndEncapsulate(
                    _clip, instance, bakedPairs, bakedGos, gunRenderer,
                    _normalizedTimes, length, out float floorY);
                Vector3 center = bounds.center;

                // **거의 눈높이의 앞 3/4**로 본다 — 쥐는 +Z를 보고 서 있다.
                // 두 번 틀리고 나서 여기로 왔다(2026-09-12 실측):
                //   부감(위에서)  → 반쯤 선 프레임에서 **다리가 몸통에 가려 전부 사라졌다**.
                //   정측면        → 앞다리가 머리 실루엣과 정확히 겹쳐 **앞발이 안 보였다**
                //                   (앞다리 뿌리 X=±0.087, 머리 반지름도 그쯤이다).
                // 정면 가까이(40도)도 틀렸다 — 원근 압축에 몸통 피치가 뭉개진다.
                // 측면에서 **25도만** 앞으로 튼 각이 둘 다 살린다.
                float halfFovRad = preview.cameraFieldOfView * 0.5f * Mathf.Deg2Rad;
                float distance = Mathf.Max(bounds.extents.magnitude * 0.85f / Mathf.Tan(halfFovRad), 0.15f);
                Vector3 dir = new Vector3(1f, 0.20f, 0.45f).normalized;

                preview.camera.transform.position = center + dir * distance;
                preview.camera.transform.LookAt(center);

                // 바닥판 — 「발이 붙어 있나」·「정말 일어섰나」는 기준선이 있어야 보인다.
                // 셰이더는 **캐릭터가 실제로 쓰는 것**을 빌려 쓴다. `Shader.Find("Universal
                // Render Pipeline/Lit")`으로 만들면 이 오프스크린 씬에서 마젠타로 나왔다
                // (2026-09-12 실측). 이미 화면에 제대로 그려지고 있는 셰이더면 확실하다.
                Shader groundShader = null;
                if (bakedPairs.Count > 0)
                {
                    Material src = bakedPairs[0].smr.sharedMaterial;
                    if (src != null) groundShader = src.shader;
                }

                ground = MakeGround(bounds, floorY, groundShader, out groundMat);
                if (ground != null) preview.AddSingleGO(ground);

                for (int i = 0; i < _normalizedTimes.Length; i++)
                {
                    float t = length * _normalizedTimes[i];
                    _clip.SampleAnimation(instance, t);

                    foreach ((SkinnedMeshRenderer smr, Mesh mesh) pair in bakedPairs)
                    {
                        pair.smr.BakeMesh(pair.mesh, true);
                    }

                    preview.BeginStaticPreview(new Rect(0, 0, RESOLUTION, RESOLUTION));
                    preview.Render();
                    Texture2D tex = preview.EndStaticPreview();

                    // 파일 이름에 **순서와 블렌더 프레임**을 같이 박는다. 0채움이라 사전순 정렬이
                    // 곧 재생 순서다 — f10이 f2 앞으로 튀어나오지 않는다.
                    int blenderFrame = 1 + Mathf.RoundToInt(_normalizedTimes[i] * 10f);
                    string fileName =
                        $"ctc-clip-preview-{_tag}-{_clip.name}-{i:D2}-bf{blenderFrame:D2}.png";
                    string path = Path.Combine(Path.GetTempPath(), fileName);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex);

                    writtenPaths.Add(path);
                }
            }
            finally
            {
                // 오브젝트(GameObject)를 지워도 Mesh는 따로 남는다 — 굽는 도중 예외가 나서
                // try 본문의 나머지가 안 돌아도 여기서는 반드시 지운다.
                foreach ((SkinnedMeshRenderer smr, Mesh mesh) pair in bakedPairs)
                {
                    if (pair.mesh != null) UnityEngine.Object.DestroyImmediate(pair.mesh);
                }

                if (ground != null) UnityEngine.Object.DestroyImmediate(ground);
                // 머티리얼은 오브젝트와 따로 남는다 — 코드로 만든 건 코드로 지운다.
                if (groundMat != null) UnityEngine.Object.DestroyImmediate(groundMat);
                if (gunMat != null) UnityEngine.Object.DestroyImmediate(gunMat);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                preview?.Cleanup();
            }

            return writtenPaths;
        }

        /// <summary>
        /// 미리보기 인스턴스의 `leg.F.R`에 총을 매단다 — 게임에서 보이는 것과 **같은 자리**로.
        /// 프리팹 연결은 맺지 않는다(<c>Instantiate</c>): 이건 렌더가 끝나면 지울 임시 오브젝트다.
        /// </summary>
        private static Renderer AttachGun(GameObject _instance)
        {
            Transform bone = MouseGunBaker.FindGunBone(_instance.transform);
            if (bone == null)
            {
                Debug.LogWarning(
                    $"[ClipPreviewRender] '{MouseGunBaker.GUN_BONE}' 본을 못 찾았다 — 총 없이 렌더한다");
                return null;
            }

            var gunAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GUN_PREFAB);
            if (gunAsset == null)
            {
                Debug.LogWarning($"[ClipPreviewRender] 총 프리팹이 없다(스킵): {GUN_PREFAB}");
                return null;
            }

            var gun = UnityEngine.Object.Instantiate(gunAsset);
            gun.name = "MouseGun";
            MouseGunBaker.Place(gun.transform, bone);
            // **여기서는 켠 채로 둔다.** 게임에서는 쏠 때만 보이지만, 미리보기의 목적이
            // "쏘는 동안 무엇이 보이는가"를 확인하는 것이라 그 구간을 그대로 그려야 한다.
            gun.SetActive(true);

            return gun.GetComponentInChildren<Renderer>(true);
        }

        /// <summary>
        /// 총을 **이 오프스크린 씬에서 실제로 그려지는 셰이더**로 다시 칠한다.
        ///
        /// 총의 진짜 머티리얼(M_MouseGun)은 URP/Lit인데, <see cref="PreviewRenderUtility"/>의
        /// 프라이빗 씬에서는 그게 **마젠타로 나온다**(2026-09-12 실측 — 바닥판에서 이미 겪은 것과
        /// 같은 증상이라 같은 처방을 쓴다). 캐릭터가 지금 멀쩡히 그려지고 있는 셰이더를 빌려
        /// 총의 원래 색만 옮겨 담는다 — 게임에서 보이는 색과 같고, 미리보기에서도 보인다.
        ///
        /// 색을 손으로 적지 않는다: 진짜 머티리얼에서 읽어 온다. 총 색을 바꾸면 여기도 따라온다.
        /// </summary>
        private static Material RepaintGunForPreview(Renderer _gun, GameObject _instance)
        {
            if (_gun == null) return null;

            // `Sprites/Default`를 쓴다. 캐릭터 셰이더를 빌려 보니(1차 시도) 그건 팩의
            // 셰이더그래프라 `_BaseColor`·`_Color`를 **안 받는다** — 텍스처를 비운 순간
            // 총이 흰 덩어리로 나와 바닥과 구분이 안 갔다(2026-09-12 실측).
            // 이 셰이더는 이 프로젝트에서 이미 검증된 것이고(MousePopgun 궤적선),
            // 조명 없이 색을 그대로 찍어 줘서 **실루엣을 읽는** 이 용도에 오히려 맞는다.
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                foreach (SkinnedMeshRenderer smr in _instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Material src = smr.sharedMaterial;
                    if (src != null && src.shader != null) { shader = src.shader; break; }
                }
            }

            // 널이면 만들지 않는다 — `new Material(null)`은 예외도 경고도 없이 마젠타를 굽는다.
            if (shader == null)
            {
                Debug.LogWarning("[ClipPreviewRender] 총을 칠할 셰이더를 못 구했다 — 원래 머티리얼로 둔다");
                return null;
            }

            var color = new Color(0.24f, 0.24f, 0.26f);
            Material real = _gun.sharedMaterial;
            if (real != null)
            {
                if (real.HasProperty("_BaseColor")) color = real.GetColor("_BaseColor");
                else if (real.HasProperty("_Color")) color = real.GetColor("_Color");
            }

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", null);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            _gun.sharedMaterial = mat;
            return mat;
        }

        /// <summary>
        /// 샘플 지점마다 굽고 그 메시의 월드 경계를 전부 합친다 — 클립 어느 순간에도 안 잘리는 틀.
        /// </summary>
        private static Bounds BakeAndEncapsulate(
            AnimationClip _clip,
            GameObject _instance,
            List<(SkinnedMeshRenderer smr, Mesh mesh)> _bakedPairs,
            List<GameObject> _bakedGos,
            Renderer _gunRenderer,
            float[] _normalizedTimes,
            float _length,
            out float _floorY)
        {
            var bounds = new Bounds();
            bool seeded = false;

            // 바닥 높이는 **첫 프레임(중립 자세)** 기준이다 — 그 자세에서 네 발이 땅에 붙어 있다.
            // 클립 전체의 최저점을 쓰면 꼬리가 처지는 프레임에 바닥이 끌려 내려가 기준이 안 된다.
            _floorY = float.MaxValue;

            for (int i = 0; i < _normalizedTimes.Length; i++)
            {
                _clip.SampleAnimation(_instance, _length * _normalizedTimes[i]);

                for (int b = 0; b < _bakedPairs.Count; b++)
                {
                    _bakedPairs[b].smr.BakeMesh(_bakedPairs[b].mesh, true);

                    Bounds local = _bakedPairs[b].mesh.bounds;
                    Transform tr = _bakedGos[b].transform;
                    Vector3 e = local.extents;

                    for (int c = 0; c < 8; c++)
                    {
                        var corner = new Vector3(
                            (c & 1) == 0 ? -e.x : e.x,
                            (c & 2) == 0 ? -e.y : e.y,
                            (c & 4) == 0 ? -e.z : e.z);
                        Vector3 world = tr.TransformPoint(local.center + corner);

                        if (i == 0) _floorY = Mathf.Min(_floorY, world.y);

                        if (!seeded)
                        {
                            bounds = new Bounds(world, Vector3.zero);
                            seeded = true;
                        }
                        else
                        {
                            bounds.Encapsulate(world);
                        }
                    }
                }

                // 총도 틀 안에 들어와야 한다 — 뽑아 든 프레임에서 총구가 잘리면 그림이 거짓말을 한다.
                // 정적 메시라 Renderer.bounds가 매 샘플 정확히 갱신된다(스킨드와 달리).
                if (_gunRenderer != null && seeded) bounds.Encapsulate(_gunRenderer.bounds);
            }

            if (!seeded)
            {
                _floorY = _instance.transform.position.y;
                return new Bounds(_instance.transform.position, Vector3.one);
            }

            return bounds;
        }

        /// <summary>
        /// 기준선용 바닥판. 발이 붙어 있는지·정말 섰는지는 바닥이 있어야 눈으로 읽힌다.
        /// </summary>
        private static GameObject MakeGround(
            Bounds _bounds, float _floorY, Shader _shader, out Material _material)
        {
            _material = null;

            // 셰이더는 반드시 널 검사한다 — `new Material(null)`은 예외도 경고도 없이
            // **마젠타**를 굽는다(CLAUDE.md 조용히 깨지는 함정 6번).
            if (_shader == null)
            {
                Debug.LogWarning("[ClipPreviewRender] 바닥판 셰이더를 못 구했다 — 바닥 없이 렌더한다");
                return null;
            }

            _material = new Material(_shader) { hideFlags = HideFlags.HideAndDontSave };
            // 캐릭터 머티리얼에서 셰이더만 빌려 왔으므로 텍스처는 비운다 — 무늬가 있으면
            // 바닥이 기준선 구실을 못 한다. 이름 두 개를 다 시도한다(URP/빌트인).
            var grey = new Color(0.40f, 0.40f, 0.44f);
            if (_material.HasProperty("_BaseMap")) _material.SetTexture("_BaseMap", null);
            if (_material.HasProperty("_MainTex")) _material.SetTexture("_MainTex", null);
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", grey);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", grey);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ClipPreviewGround";
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = _material;

            // 캐릭터보다 넉넉히 넓게, 두께는 아주 얇게 — 윗면이 정확히 _floorY에 오도록 내린다.
            float span = Mathf.Max(_bounds.size.x, _bounds.size.z) * 6f + 0.5f;
            float thickness = Mathf.Max(_bounds.size.y * 0.02f, 0.001f);
            go.transform.localScale = new Vector3(span, thickness, span);
            go.transform.position = new Vector3(_bounds.center.x, _floorY - thickness * 0.5f, _bounds.center.z);

            return go;
        }
    }
}

