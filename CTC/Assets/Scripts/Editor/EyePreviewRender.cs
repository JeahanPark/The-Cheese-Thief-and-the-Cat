using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CTC.Editor
{
    /// <summary>
    /// 머리 본 축과 눈 지오메트리를 눈으로 확인하려고, 임시 씬에 캐릭터를 세우고 카메라로 직접 렌더한다.
    /// 씬 뷰 스크린샷은 갱신이 안 붙는 사례가 있어 카메라 렌더를 쓴다(CLAUDE.md 함정표).
    /// Tools > CTC > Dev > Render Head Preview
    /// </summary>
    public static class EyePreviewRender
    {
        private static readonly (string name, string path)[] TARGETS =
        {
            ("mouse", "Assets/Prefabs/PlayerMouse.prefab"),
            ("cat", "Assets/Prefabs/PlayerCat.prefab"),
        };

        [MenuItem("Tools/CTC/Dev/Render Head Preview")]
        public static void Render()
        {
            string outDir = Path.Combine(Path.GetTempPath(), "ctc-eye");
            Directory.CreateDirectory(outDir);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var lightGo = new GameObject("L");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 200f, 0f);

            var camGo = new GameObject("C");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.16f, 0.2f, 1f);
            cam.orthographic = true;

            for (int i = 0; i < TARGETS.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TARGETS[i].path);
                if (prefab == null) { Debug.LogError($"[EyePreview] 없음 {TARGETS[i].path}"); continue; }

                GameObject inst = Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(inst, scene);
                inst.transform.position = Vector3.zero;

                Transform head = FindDeep(inst.transform, "head");
                if (head == null) { Debug.LogError("[EyePreview] head 본 없음"); Object.DestroyImmediate(inst); continue; }

                Debug.Log($"[EyePreview] {TARGETS[i].name} head world pos={head.position} " +
                          $"right={head.right} up={head.up} fwd={head.forward} " +
                          $"lossyScale={head.lossyScale} rootScale={inst.transform.lossyScale}");

                // 머리를 화면 가운데 두고 정면(+z에서)·측면(+x에서)·위에서 세 장
                float size = 0.25f;
                Shot(cam, head.position + new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f), size,
                    Path.Combine(outDir, TARGETS[i].name + "-front.png"));
                Shot(cam, head.position + new Vector3(2f, 0f, 0f), Quaternion.Euler(0f, 270f, 0f), size,
                    Path.Combine(outDir, TARGETS[i].name + "-side.png"));
                Shot(cam, head.position + new Vector3(1.2f, 0.9f, 1.2f), Quaternion.Euler(30f, 225f, 0f), size,
                    Path.Combine(outDir, TARGETS[i].name + "-3q.png"));

                Object.DestroyImmediate(inst);
            }

            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[EyePreview] 완료 → {outDir}");
        }


        private static void Shot(Camera _cam, Vector3 _pos, Quaternion _rot, float _size, string _file)
        {
            _cam.transform.SetPositionAndRotation(_pos, _rot);
            _cam.orthographicSize = _size;
            var rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            _cam.targetTexture = rt;
            _cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            _cam.targetTexture = null;
            File.WriteAllBytes(_file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        private static Transform FindDeep(Transform _root, string _name)
        {
            if (_root.name == _name) return _root;
            for (int i = 0; i < _root.childCount; i++)
            {
                Transform f = FindDeep(_root.GetChild(i), _name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
