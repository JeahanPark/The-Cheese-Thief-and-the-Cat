using System.IO;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// MouseGun 프리팹을 세 각도에서 오프스크린으로 렌더해 PNG로 남긴다 — 형상을 눈으로
    /// 확인하기 위한 개발 도구다. Tools > CTC > Dev > 총 메시 미리보기 렌더
    ///
    /// **왜 씬에 세우지 않고 오프스크린으로 렌더하는가**: 지금 열려 있는 씬이 저장 경로 없는
    /// `Untitled` 씬이면, 프리팹을 씬에 놓는 순간 MCP 자동 저장 경로가 파일 대화상자를 띄우고
    /// 그게 에디터 전체를 멈춘다(사람이 눌러줘야 풀린다) — <see cref="ClipPreviewRender"/>에서
    /// 이미 겪은 함정이다. <see cref="PreviewRenderUtility"/>는 자기만의 프라이빗 씬·카메라를
    /// 써서 열린 씬을 전혀 건드리지 않는다.
    /// </summary>
    public static class GunMeshPreviewRender
    {
        private const int RESOLUTION = 512;

        private const string MOUSE_PREFAB =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Prefabs/Mouse.prefab";

        [MenuItem("Tools/CTC/Dev/총 메시 미리보기 렌더")]
        public static void Render()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/MouseGun.prefab");
            if (prefab == null)
            {
                Debug.LogError("[GunMeshPreviewRender] 프리팹을 못 찾았다 — 먼저 Tools/CTC/Build Gun Mesh를 돌려라");
                return;
            }

            MeshFilter mf = prefab.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogError("[GunMeshPreviewRender] 프리팹에 메시가 없다");
                return;
            }

            // **저장된 M_MouseGun 머티리얼을 그대로 프리뷰에 쓰면 마젠타가 나온다** — URP Lit을
            // Shader.Find로 새로 만든 머티리얼은 이 PreviewRenderUtility의 사설 씬에서 렌더가 안 된다
            // (ClipPreviewRender에서 실측한 것과 같은 함정). 화면에 이미 제대로 그려지는 쥐 캐릭터의
            // 셰이더를 빌려 프리뷰 전용 임시 머티리얼을 만든다 — 저장된 에셋은 건드리지 않는다.
            Shader previewShader = null;
            var mousePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MOUSE_PREFAB);
            SkinnedMeshRenderer mouseSmr = mousePrefab != null
                ? mousePrefab.GetComponentInChildren<SkinnedMeshRenderer>(true)
                : null;
            if (mouseSmr != null && mouseSmr.sharedMaterial != null)
            {
                previewShader = mouseSmr.sharedMaterial.shader;
            }

            Bounds bounds = mf.sharedMesh.bounds;
            Vector3 center = bounds.center;

            // 세 각도 — 실루엣용 측면, 총구가 보이는 3/4, 위에서 본 실루엣.
            var views = new (string tag, Vector3 dir, Vector3 up)[]
            {
                ("side", new Vector3(1f, 0.15f, 0f), Vector3.up),
                ("threeQuarter", new Vector3(0.8f, 0.35f, 0.8f), Vector3.up),
                ("top", new Vector3(0f, 1f, 0.01f), Vector3.forward),
            };

            PreviewRenderUtility preview = null;
            GameObject instance = null;
            Material previewMaterial = null;

            try
            {
                preview = new PreviewRenderUtility();
                instance = Object.Instantiate(prefab);
                preview.AddSingleGO(instance);

                if (previewShader != null)
                {
                    MeshRenderer instanceRenderer = instance.GetComponent<MeshRenderer>();
                    if (instanceRenderer != null && instanceRenderer.sharedMaterial != null)
                    {
                        previewMaterial = new Material(previewShader) { hideFlags = HideFlags.HideAndDontSave };
                        Color baseColor = instanceRenderer.sharedMaterial.HasProperty("_BaseColor")
                            ? instanceRenderer.sharedMaterial.GetColor("_BaseColor")
                            : Color.gray;
                        if (previewMaterial.HasProperty("_BaseColor")) previewMaterial.SetColor("_BaseColor", baseColor);
                        if (previewMaterial.HasProperty("_Color")) previewMaterial.SetColor("_Color", baseColor);
                        if (previewMaterial.HasProperty("_Metallic")) previewMaterial.SetFloat("_Metallic", 0f);
                        instanceRenderer.sharedMaterial = previewMaterial;
                    }
                }

                preview.camera.farClipPlane = 10f;
                preview.camera.nearClipPlane = 0.001f;
                preview.cameraFieldOfView = 30f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.2f, 0.2f, 0.22f);
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                preview.lights[1].intensity = 0.6f;

                float halfFovRad = preview.cameraFieldOfView * 0.5f * Mathf.Deg2Rad;
                float distance = Mathf.Max(bounds.extents.magnitude * 1.6f / Mathf.Tan(halfFovRad), 0.05f);

                foreach ((string tag, Vector3 dir, Vector3 up) view in views)
                {
                    preview.camera.transform.position = center + view.dir.normalized * distance;
                    preview.camera.transform.LookAt(center, view.up);

                    preview.BeginStaticPreview(new Rect(0, 0, RESOLUTION, RESOLUTION));
                    preview.Render();
                    Texture2D tex = preview.EndStaticPreview();

                    string path = Path.Combine(Path.GetTempPath(), $"ctc-gun-preview-{view.tag}.png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);

                    Debug.Log($"[GunMeshPreviewRender] {view.tag} → {path}");
                }
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                if (previewMaterial != null) Object.DestroyImmediate(previewMaterial);
                preview?.Cleanup();
            }
        }
    }
}
