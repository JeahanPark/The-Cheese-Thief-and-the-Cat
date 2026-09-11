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

        private const int RESOLUTION = 512;

        /// <summary>클립 길이를 이 비율들로 나눠 잰다 — 시작·중간·끝을 고루 본다.</summary>
        private static readonly float[] NORMALIZED_TIMES = { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f };

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
            paths.AddRange(RenderClip(prefab, packClip, "pack"));

            AnimationClip roundTripClip = ClipParityProbe.FindClip(ROUNDTRIP_FBX, CLIP_NAME);
            if (roundTripClip == null)
            {
                Debug.LogWarning(
                    $"[ClipPreviewRender] 왕복 클립을 못 찾았다(스킵): {ROUNDTRIP_FBX} :: {CLIP_NAME}");
            }
            else
            {
                paths.AddRange(RenderClip(prefab, roundTripClip, "roundtrip"));
            }

            Debug.Log($"[ClipPreviewRender] {paths.Count}장 렌더 완료:\n" + string.Join("\n", paths));
        }

        private static List<string> RenderClip(GameObject _prefab, AnimationClip _clip, string _tag)
        {
            var writtenPaths = new List<string>();
            PreviewRenderUtility preview = null;
            GameObject instance = null;

            try
            {
                preview = new PreviewRenderUtility();

                instance = UnityEngine.Object.Instantiate(_prefab);
                preview.AddSingleGO(instance);

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

                // 첫 프레임을 한 번 샘플링해서 실제 렌더러 경계를 잰다 — 크기를 몰라도 카메라가 맞는다.
                // (원래 SkinnedMeshRenderer가 아직 켜져 있을 때 재야 한다 — 꺼버린 뒤에는 bounds가 0이 된다.)
                _clip.SampleAnimation(instance, 0f);
                Bounds bounds = ComputeBounds(instance);
                Vector3 center = bounds.center;

                // 수직 FOV 기준으로 전신이 들어오도록 거리를 역산한다(여유 1.1배) — 정면/후면
                // 어느 쪽에서 봐도 잘리지 않게 3/4 각도·약간 위에서 내려다보는 구도를 쓴다.
                float halfFovRad = preview.cameraFieldOfView * 0.5f * Mathf.Deg2Rad;
                float distance = Mathf.Max(bounds.extents.magnitude * 1.1f / Mathf.Tan(halfFovRad), 0.15f);
                Vector3 dir = new Vector3(1f, 0.6f, -1.3f).normalized;

                preview.camera.transform.position = center + dir * distance;
                preview.camera.transform.LookAt(center);

                // **스킨드 메시는 이 오프스크린 루프 안에서 프레임마다 다시 스키닝되지 않는다**
                // (2026-09-12 실측 — `updateWhenOffscreen=true`를 줘도 마찬가지). 본 회전은 매
                // `SampleAnimation` 호출마다 정확히 바뀌는데(로그로 확인함) `PreviewRenderUtility`가
                // 한 메서드 호출 안에서 매번 동기적으로 `Render()`를 부르다 보니 유니티의 스키닝
                // 갱신이 정상적으로 도는 시점(플레이어 루프 한 틱)을 한 번도 못 거친다 — 여섯 장이
                // 전부 첫 포즈로 고정돼 나왔다. 그래서 `BakeMesh`로 **매 프레임 강제로 CPU 스킨을
                // 굽고**, 원래 SkinnedMeshRenderer는 끈 채 그 결과를 정적 MeshFilter/MeshRenderer로
                // 그린다 — BakeMesh는 호출 즉시 동기적으로 굽어서 이 문제를 완전히 비껴간다.
                var bakedPairs = new List<(SkinnedMeshRenderer smr, Mesh mesh)>();
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

                    smr.enabled = false;
                }

                float length = _clip.length;
                for (int i = 0; i < NORMALIZED_TIMES.Length; i++)
                {
                    float t = length * NORMALIZED_TIMES[i];
                    _clip.SampleAnimation(instance, t);

                    foreach ((SkinnedMeshRenderer smr, Mesh mesh) pair in bakedPairs)
                    {
                        pair.smr.BakeMesh(pair.mesh, true);
                    }

                    preview.BeginStaticPreview(new Rect(0, 0, RESOLUTION, RESOLUTION));
                    preview.Render();
                    Texture2D tex = preview.EndStaticPreview();

                    string fileName = $"ctc-clip-preview-{_tag}-{_clip.name}-f{i}.png";
                    string path = Path.Combine(Path.GetTempPath(), fileName);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex);

                    writtenPaths.Add(path);
                }

                foreach ((SkinnedMeshRenderer smr, Mesh mesh) pair in bakedPairs) UnityEngine.Object.DestroyImmediate(pair.mesh);
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                preview?.Cleanup();
            }

            return writtenPaths;
        }

        private static Bounds ComputeBounds(GameObject _root)
        {
            Renderer[] renderers = _root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(_root.transform.position, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}

