using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CTC.Editor
{
    /// <summary>
    /// 프롭 진열장 — Tools > CTC > Dev > 프롭 진열장 (메시 확인). 개발 확인용이다.
    ///
    /// 구운 메시를 **눈으로 보려면** 지금까지는 판을 열고 치즈를 넣고 사서 설치해야 했다.
    /// 여기서는 한 줄로 세워 놓고 씬 뷰를 돌려 보면 된다.
    ///
    /// 씬은 `Assets/Scenes/Dev/`에 둔다 — **빌드 설정에 넣지 않는다.** 계측기가 배포에 섞이면 안 된다.
    /// 프리팹에 `NetworkObject`가 붙어 있어도 상관없다. 여기서는 스폰되지 않고 서 있기만 한다.
    /// </summary>
    public static class PropShowcase
    {
        private const string SCENE_PATH = "Assets/Scenes/Dev/PropShowcase.unity";
        // `Builds/`는 .gitignore가 통째로 무시한다 — 문서가 가리킬 그림을 거기 두면 남한테는 안 보인다.
        private const string SHOT_PATH = "docs/design-vault/images/prop-showcase.png";

        /// <summary>프롭 사이 여백(m). 자리는 **각자의 실제 폭**으로 잡는다 — 요새는 2.4m고 직업권은 0.35m다.</summary>
        private const float GAP = 0.9f;

        private const int SHOT_WIDTH = 1920;
        private const int SHOT_HEIGHT = 700;

        private static readonly Color BACKDROP = new Color(0.055f, 0.062f, 0.078f);
        private static readonly Color LIGHT_WARM = new Color(1f, 0.94f, 0.84f);

        /// <summary>진열 순서 — 왼쪽부터. 라벨은 사람이 부르는 이름으로 쓴다.</summary>
        private static readonly (string label, string path)[] PROPS =
        {
            ("요새", "Assets/Prefabs/CheeseFortress.prefab"),
            ("창고", "Assets/Prefabs/CheeseWarehouse.prefab"),
            ("벽감", "Assets/Prefabs/Alcove.prefab"),
            ("상자", "Assets/Prefabs/LootBox.prefab"),
            ("영웅권", "Assets/Prefabs/HeroTicket.prefab"),
            ("깡패권", "Assets/Prefabs/GangTicket.prefab"),
            ("치즈 드롭", "Assets/Prefabs/CheeseDrop.prefab"),
            ("치즈 쐐기", "Assets/Prefabs/Props/CheeseWedge.prefab"),
            ("쥐", "Assets/Prefabs/PlayerMouse.prefab"),
            ("고양이", "Assets/Prefabs/PlayerCat.prefab"),
            ("강아지", "Assets/Prefabs/Dog.prefab"),
        };

        [MenuItem("Tools/CTC/Dev/프롭 진열장 (메시 확인)")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildFloor();

            var placed = new List<GameObject>();
            var spans = new List<(GameObject go, Bounds bounds)>();

            // 1) 일단 원점에 세워 두고 각자의 크기를 잰다. 폭을 모르면 자리를 못 잡는다.
            for (int i = 0; i < PROPS.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PROPS[i].path);
                if (asset == null)
                {
                    Debug.LogWarning($"[PropShowcase] 없다(건너뜀): {PROPS[i].path}");
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                go.transform.position = Vector3.zero;
                // 정면이 아니라 살짝 돌려 세운다 — 옆면·두께가 같이 보여야 메시를 판단할 수 있다.
                go.transform.rotation = Quaternion.Euler(0f, 205f, 0f);

                if (!TryMeasure(go, out Bounds bounds))
                {
                    Debug.LogWarning($"[PropShowcase] 렌더러가 없다(건너뜀): {PROPS[i].path}");
                    Object.DestroyImmediate(go);
                    continue;
                }

                placed.Add(go);
                spans.Add((go, bounds));
            }

            // 2) 폭을 합쳐 전체 길이를 구하고, 가운데가 원점에 오도록 왼쪽부터 늘어놓는다.
            float total = 0f;
            for (int i = 0; i < spans.Count; i++) total += spans[i].bounds.size.x + GAP;
            total -= GAP;

            float cursor = -total * 0.5f;
            for (int i = 0; i < spans.Count; i++)
            {
                (GameObject go, Bounds bounds) = spans[i];
                float half = bounds.size.x * 0.5f;

                // 프리팹 원점이 메시 한가운데가 아닐 수 있다(요새는 문 기준이다) — 잰 중심으로 맞춘다.
                float shift = cursor + half - bounds.center.x;
                go.transform.position = new Vector3(shift, -bounds.min.y, 0f);

                Label(PROPS[i].label, new Vector3(cursor + half, bounds.size.y + 0.45f, 0f));
                cursor += bounds.size.x + GAP;
            }

            Camera cam = BuildCamera(total);

            EnsureFolder();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SCENE_PATH);

            Shoot(cam);
            Debug.Log($"[PropShowcase] 완료 → {SCENE_PATH} (프롭 {placed.Count}개) · 사진 {SHOT_PATH}");
        }

        private static void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.20f);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0f;

            var keyGo = new GameObject("KeyLight");
            keyGo.transform.rotation = Quaternion.Euler(38f, 205f, 0f);
            Light key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = LIGHT_WARM;
            key.intensity = 1.5f;
            key.shadows = LightShadows.Soft;

            // 뒤쪽에서 한 장 더 — 실루엣만 보이면 구멍이 파였는지 알 수가 없다.
            var fillGo = new GameObject("FillLight");
            fillGo.transform.rotation = Quaternion.Euler(20f, 25f, 0f);
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.72f, 0.80f, 1f);
            fill.intensity = 0.45f;
            fill.shadows = LightShadows.None;
        }

        private static void BuildFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(8f, 1f, 1.2f);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader) { name = "M_ShowcaseFloor" };
            mat.SetColor("_BaseColor", new Color(0.20f, 0.21f, 0.24f));
            mat.SetColor("_Color", new Color(0.20f, 0.21f, 0.24f));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            floor.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>진열 폭에 맞춰 물러선다 — 프롭이 늘어도 사진에서 잘리지 않는다.</summary>
        private static Camera BuildCamera(float _totalWidth)
        {
            const float FOV = 42f;
            float aspect = (float)SHOT_WIDTH / SHOT_HEIGHT;

            // 가로를 담으려면 세로 FOV를 가로로 환산해 거리를 구한다.
            float halfHorizontalRad = Mathf.Atan(Mathf.Tan(FOV * 0.5f * Mathf.Deg2Rad) * aspect);
            float distance = _totalWidth * 0.5f / Mathf.Tan(halfHorizontalRad) + 1.5f;

            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 1.6f, -distance);
            go.transform.rotation = Quaternion.Euler(6f, 0f, 0f);

            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BACKDROP;
            cam.fieldOfView = FOV;
            cam.farClipPlane = 200f;
            return cam;
        }

        /// <summary>보이는 것의 크기 = 렌더러 경계. 콜라이더로 재면 안 보이는 벽까지 폭에 든다.</summary>
        private static bool TryMeasure(GameObject _go, out Bounds _bounds)
        {
            _bounds = default;

            Renderer[] renderers = _go.GetComponentsInChildren<Renderer>(false);
            bool any = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer) continue;   // 파티클은 크기가 없다

                if (!any) { _bounds = renderers[i].bounds; any = true; }
                else _bounds.Encapsulate(renderers[i].bounds);
            }
            return any;
        }

        private static void Label(string _text, Vector3 _position)
        {
            var go = new GameObject($"Label_{_text}");
            go.transform.position = _position;
            // **회전을 주지 않는다.** TMP는 자기 +Z가 바깥(= 카메라 반대편)을 볼 때 바로 읽힌다.
            // 카메라 쪽으로 180° 돌리면 글자가 좌우로 뒤집힌다(2026-08-30 실측).

            var text = go.AddComponent<TextMeshPro>();
            text.text = _text;
            text.fontSize = 2.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.86f, 0.88f, 0.92f);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(2.4f, 0.6f);
        }

        /// <summary>
        /// 카메라로 **직접 렌더해서** 파일로 뽑는다.
        /// 씬 뷰 스크린샷은 갱신이 안 붙어 같은 그림을 반복해 주는 일이 있다(2026-08-30 실측).
        /// </summary>
        private static void Shoot(Camera _cam)
        {
            if (_cam == null) return;

            var rt = new RenderTexture(SHOT_WIDTH, SHOT_HEIGHT, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
            };

            RenderTexture previous = RenderTexture.active;
            try
            {
                _cam.targetTexture = rt;
                _cam.Render();

                RenderTexture.active = rt;
                var shot = new Texture2D(SHOT_WIDTH, SHOT_HEIGHT, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0f, 0f, SHOT_WIDTH, SHOT_HEIGHT), 0, 0);
                shot.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(SHOT_PATH));
                File.WriteAllBytes(SHOT_PATH, shot.EncodeToPNG());
                Object.DestroyImmediate(shot);
            }
            finally
            {
                _cam.targetTexture = null;
                RenderTexture.active = previous;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Dev"))
                AssetDatabase.CreateFolder("Assets/Scenes", "Dev");
        }
    }
}
