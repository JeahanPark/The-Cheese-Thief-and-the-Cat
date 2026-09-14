using CTC.Runtime.UI;
using System.IO;
using System.Collections.Generic;
using CTC.Runtime.Intro;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CTC.Editor
{
    /// <summary>
    /// 인트로 씬을 통째로 굽는다 — Tools > CTC > Setup Intro Scene.
    ///
    /// 연출은 "스포트라이트 안에 치즈, 그 주위를 도는 쥐들, 불빛 밖에서 보고 있는 고양이"다.
    /// 이 그림은 **새 애니메이션을 하나도 요구하지 않는다** — 쥐는 Walk, 고양이는 Idle_B를 쓴다.
    /// (앞서 확인: 이 리그는 네 발 짐승이라 두 발로 서거나 물건을 쥘 수 없다.)
    ///
    /// 게임의 PlayerMouse/PlayerCat 프리팹은 **쓰지 않는다** — NetworkObject가 붙어 있어
    /// 네트워크 없는 씬에 세우면 안 된다. Quirky 모델 프리팹만 장식으로 세운다.
    ///
    /// 다시 실행하면 씬을 새로 굽는다(멱등).
    /// </summary>
    public static class IntroSceneBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/Intro.unity";
        private const string MAT_FOLDER = "Assets/Materials/Intro";
        private const string ANIM_FOLDER = "Assets/Animations/Intro";

        private const string QUIRKY_ROOT = "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1";
        private const string MOUSE_PREFAB = QUIRKY_ROOT + "/Prefabs/Mouse.prefab";
        private const string CAT_PREFAB = QUIRKY_ROOT + "/Prefabs/Cat.prefab";
        private const string MOUSE_ANIM_FBX = QUIRKY_ROOT + "/Animations/Mouse_Animations.fbx";
        private const string CAT_ANIM_FBX = QUIRKY_ROOT + "/Animations/Cat_Animations.fbx";

        /// <summary>고양이가 불빛 밖에서 취하는 자세. 바꾸려면 여기만 고친다.</summary>
        private const string CAT_IDLE_CLIP = "Idle_A";

        /// <summary>
        /// Quirky 프리팹은 자기 셰이더 머티리얼을 들고 있어 URP에서 마젠타로 뜬다(실측).
        /// 게임이 PlayerMouse/PlayerCat에 씌우는 것과 **같은** URP 머티리얼을 여기서도 씌운다.
        /// </summary>
        private const string MOUSE_MATERIAL = "Assets/Materials/M_Mouse_URP.mat";

        private const string CAT_MATERIAL = "Assets/Materials/M_Cat_URP.mat";

        /// <summary>Tools/CTC/Build Cheese Mesh가 굽는 쐐기. 메시·머티리얼이 여기 다 들어 있다.</summary>
        private const string CHEESE_PREFAB = "Assets/Prefabs/Props/CheeseWedge.prefab";

        private static readonly Color FLOOR_COLOR = new Color(0.11f, 0.09f, 0.07f);
        private static readonly Color LIGHT_COLOR = new Color(1f, 0.91f, 0.72f);
        private static readonly Color ACCENT = UiPalette.Accent;   // 출처: CTC.Runtime.UI.UiPalette

        // 메뉴 자리(1920×1080 기준) — 왼쪽 끝을 칩·진행바·버전과 맞춘다.
        private const float MENU_LEFT = 96f;
        private const float MENU_W = 430f;
        private const float MENU_H = 64f;
        private const float MENU_TOP_Y = 500f;   // 줄이 다섯이 되며 올렸다(2026-09-14) — 실패 패널(y 168)과 안 겹치게
        private const float MENU_GAP = 70f;

        [MenuItem("Tools/CTC/Setup Intro Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureFolders();
            BuildLighting();

            Transform stage = new GameObject("Stage").transform;
            Transform cheeseCenter = BuildCheese(stage);
            BuildFloor(stage);
            BuildSpotlight(stage);
            BuildMice(stage, cheeseCenter);
            BuildCat(stage);
            BuildCamera();

            IntroDirector director = new GameObject("IntroDirector").AddComponent<IntroDirector>();

            // 2026-09-14 R.E.P.O.식 앞단 — 메인에서 바로 방을 열고 들어가므로 네트워크가 여기서 선다.
            // NetworkRoot는 이미 NetworkManager가 있으면(대기실에서 돌아옴) 아무 일도 안 한다.
            LobbySceneBuilder.BuildNetworkRoot();
            var starter = new GameObject("SessionStarter").AddComponent<CTC.Runtime.Lobby.FrontDoor.SessionStarter>();

            BuildCanvas(director, starter);

            // 배경음(2026-09-01) — 앞단 세 화면이 무음이었다. 인게임과 같은 비율(0.35)로 깐다.
            SceneBgmBuilder.Attach(SceneBgmBuilder.LOBBY_BGM);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[IntroSceneBuilder] 완료 → {SCENE_PATH}");
        }

        /// <summary>
        /// 인트로를 **0번**으로 올린다 — 빌드에서 제일 먼저 뜨는 씬이 되어야 한다.
        /// 이미 있으면 위치만 0으로 옮기고, 나머지 씬 순서는 건드리지 않는다.
        /// </summary>
        private static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == SCENE_PATH)
                {
                    if (i == 0) return;
                    EditorBuildSettingsScene mine = scenes[i];
                    scenes.RemoveAt(i);
                    scenes.Insert(0, mine);
                    EditorBuildSettings.scenes = scenes.ToArray();
                    Debug.Log("[IntroSceneBuilder] 빌드 설정에서 인트로를 0번으로 옮겼다");
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(SCENE_PATH, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[IntroSceneBuilder] 빌드 설정 0번에 인트로를 넣었다");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Materials", "Intro");
            EnsureFolder("Assets/Animations", "Intro");
            EnsureFolder("Assets", "Scenes");
        }

        private static void EnsureFolder(string _parent, string _name)
        {
            if (!AssetDatabase.IsValidFolder(_parent))
            {
                int slash = _parent.LastIndexOf('/');
                if (slash > 0) EnsureFolder(_parent.Substring(0, slash), _parent.Substring(slash + 1));
            }
            if (!AssetDatabase.IsValidFolder(_parent + "/" + _name)) AssetDatabase.CreateFolder(_parent, _name);
        }

        /// <summary>불빛 밖은 정말로 어두워야 한다 — 앰비언트를 죽인다.</summary>
        private static void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.016f, 0.020f, 0.028f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.010f, 0.014f, 0.020f);
            RenderSettings.fogDensity = 0.16f;
            RenderSettings.skybox = null;

            // 스카이박스를 지워도 반사 프로브가 남아 있으면 매끈한 바닥이 하늘색을 물고 온다
            // (실측: 바닥이 푸르스름하게 밝았다). 반사를 통째로 어둡게 눌러 둔다.
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0f;
        }

        private static void BuildFloor(Transform _parent)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(_parent, false);
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            // 매끈하면 하늘 반사를 물어 바닥이 밝아진다 — 거친 바닥이라야 불빛 밖이 검다.
            SetMaterial(floor, MakeMaterial("M_IntroFloor", FLOOR_COLOR, 0f, 0.06f));
        }

        private static Transform BuildSpotlight(Transform _parent)
        {
            var go = new GameObject("Spotlight");
            go.transform.SetParent(_parent, false);
            go.transform.position = new Vector3(0f, 4.3f, 0f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = LIGHT_COLOR;
            light.intensity = 320f;
            light.range = 11f;
            light.spotAngle = 52f;
            light.innerSpotAngle = 24f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.9f;

            // 스팟이 바로 위에서만 때리면 치즈·쥐의 옆면이 캄캄하다(실측). 아주 약한 채움광을
            // 카메라 쪽 낮은 자리에 하나 둔다 — 웅덩이 밖까지 새지 않게 range를 짧게.
            var fillGo = new GameObject("FillLight");
            fillGo.transform.SetParent(_parent, false);
            fillGo.transform.position = new Vector3(0f, 0.75f, -1.9f);
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = LIGHT_COLOR;
            fill.intensity = 3.2f;
            fill.range = 4.2f;
            fill.shadows = LightShadows.None;

            return go.transform;
        }

        /// <summary>
        /// 치즈 쐐기 3개. 메시·머티리얼은 <see cref="CheeseMeshBuilder"/>가 구운 프리팹이 들고 있다.
        /// 프리팹 쪽 머티리얼을 그대로 쓴다 — 여기서 자체 발광을 덧씌우면 파 놓은 구멍의 음영이 뭉개진다.
        /// </summary>
        private static Transform BuildCheese(Transform _parent)
        {
            var root = new GameObject("Cheese").transform;
            root.SetParent(_parent, false);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CHEESE_PREFAB);
            if (prefab == null)
            {
                Debug.LogError($"[IntroSceneBuilder] 치즈 프리팹 없음: {CHEESE_PREFAB}" +
                               " — Tools/CTC/Build Cheese Mesh를 먼저 돌린다");
                return root;
            }

            // 프리팹은 길이 1·바닥 y=0 기준이라 스케일이 곧 쐐기의 길이다.
            AddCheese(root, prefab, new Vector3(0.01f, 0f, 0.04f), 0.35f, new Vector3(0f, -24f, 0f));
            AddCheese(root, prefab, new Vector3(0.31f, 0f, -0.17f), 0.24f, new Vector3(0f, 152f, 0f));
            AddCheese(root, prefab, new Vector3(-0.29f, 0f, -0.10f), 0.20f, new Vector3(0f, 36f, 0f));
            return root;
        }

        private static void AddCheese(Transform _parent, GameObject _prefab, Vector3 _pos, float _size, Vector3 _euler)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(_prefab, _parent);
            go.name = "CheeseWedge";
            go.transform.localPosition = _pos;
            go.transform.localRotation = Quaternion.Euler(_euler);
            go.transform.localScale = Vector3.one * _size;
        }

        private static void BuildMice(Transform _parent, Transform _center)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MOUSE_PREFAB);
            if (prefab == null)
            {
                Debug.LogError($"[IntroSceneBuilder] 쥐 프리팹 없음: {MOUSE_PREFAB}");
                return;
            }

            AnimatorController walk = MakeLoopController("IntroMouseWalk", MOUSE_ANIM_FBX, "Walk");
            Material mouseMat = AssetDatabase.LoadAssetAtPath<Material>(MOUSE_MATERIAL);

            var root = new GameObject("Mice").transform;
            root.SetParent(_parent, false);

            // 반지름·속도·시작각을 다르게 줘서 넷이 한 줄로 붙어 돌지 않게 한다.
            // 스케일은 실측으로 줄였다 — 1.0이면 쥐가 화면을 다 덮는다.
            float[] radius = { 1.28f, 1.62f, 1.06f, 1.88f };
            float[] speed = { 26f, -19f, 33f, 15f };
            float[] start = { 0f, 95f, 205f, 300f };
            float[] scale = { 0.42f, 0.38f, 0.45f, 0.36f };

            for (int i = 0; i < radius.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.name = $"IntroMouse{i + 1}";
                go.transform.localScale = Vector3.one * scale[i];
                go.transform.position = new Vector3(radius[i], 0f, 0f);

                Animator animator = go.GetComponentInChildren<Animator>();
                if (animator != null && walk != null)
                {
                    animator.runtimeAnimatorController = walk;
                    animator.applyRootMotion = false;
                }

                ApplyMaterial(go, mouseMat);

                var walker = go.AddComponent<IntroStageWalker>();
                var so = new SerializedObject(walker);
                so.FindProperty("m_Center").objectReferenceValue = _center;
                so.FindProperty("m_Radius").floatValue = radius[i];
                so.FindProperty("m_DegreesPerSecond").floatValue = speed[i];
                so.FindProperty("m_StartDegrees").floatValue = start[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>고양이는 불빛 밖이다. 눈만 보이게 자체 발광을 준다.</summary>
        private static void BuildCat(Transform _parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CAT_PREFAB);
            if (prefab == null)
            {
                Debug.LogError($"[IntroSceneBuilder] 고양이 프리팹 없음: {CAT_PREFAB}");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _parent);
            go.name = "IntroCat";
            // 불빛 밖 왼쪽에서 치즈 쪽을 본다. 카메라 앞(-z)으로 오면 주인공을 가린다(실측).
            go.transform.position = new Vector3(-2.55f, 0f, 1.05f);
            go.transform.rotation = Quaternion.Euler(0f, 118f, 0f);
            go.transform.localScale = Vector3.one * 0.72f;

            AnimatorController idle = MakeLoopController("IntroCatIdle", CAT_ANIM_FBX, CAT_IDLE_CLIP);
            Animator animator = go.GetComponentInChildren<Animator>();
            if (animator != null && idle != null)
            {
                animator.runtimeAnimatorController = idle;
                animator.applyRootMotion = false;
            }

            ApplyMaterial(go, AssetDatabase.LoadAssetAtPath<Material>(CAT_MATERIAL));

            BuildEyeSparkles(go.transform);

            // 실루엣만 겨우 읽히게 — 세게 주면 고양이가 통째로 드러나 "어둠 속에서 보고 있다"가 죽는다(실측).
            var rimGo = new GameObject("CatRimLight");
            rimGo.transform.SetParent(_parent, false);
            rimGo.transform.position = go.transform.position + new Vector3(-0.7f, 0.7f, 0.9f);
            Light rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Point;
            rim.color = new Color(0.55f, 0.66f, 1f);
            rim.intensity = 0.34f;
            rim.range = 1.9f;
            rim.shadows = LightShadows.None;
        }

        private const string SPARKLE_TEXTURE = MAT_FOLDER + "/T_EyeSparkle.png";

        private const string FX_FOLDER = "Assets/Prefabs/Fx";
        private const string EYE_FX_NAME = "FX_CatEyeSparkle";

        /// <summary>
        /// 눈 반짝임을 **이펙트 무리 프리팹 하나**로 굽는다 — 별·발광이 몇 개인지, 어디에 있는지,
        /// 얼마나 큰지가 전부 그 안의 자식 트랜스폼에 박힌다. 코드는 좌표를 계산하지 않는다.
        ///
        /// 좌표는 씬에서 직접 옮겨 본 자리를 잰 값이다(2026-08-26). 머리 본 기준 **월드 단위**이며,
        /// 붙일 때 <see cref="CTC.Runtime.Fx.FxAttachToBone"/>이 리그 스케일을 상쇄해 준다.
        /// </summary>
        private static void BuildEyeSparkles(Transform _cat)
        {
            GameObject prefab = MakeEyeSparklePrefab();
            if (prefab == null) return;

            var attach = _cat.gameObject.AddComponent<CTC.Runtime.Fx.FxAttachToBone>();
            var so = new SerializedObject(attach);
            so.FindProperty("m_Prefab").objectReferenceValue = prefab;
            so.FindProperty("m_BoneName").stringValue = "head";
            // 플레이 중에 무리를 직접 옮겨 본 자리를 역산해 넣었다(2026-08-26).
            // 고양이 기준 x 옆 / y 위 / z 앞, 월드 단위.
            so.FindProperty("m_Offset").vector3Value = new Vector3(0.0943f, -0.0703f, -0.0035f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 무리가 얼마나 퍼져 있나 — 1이면 저작값 그대로, 작을수록 **중심으로 모인다**.
        /// 중심을 기준으로 줄이므로 얼굴에서 벗어나지 않는다.
        /// </summary>
        private const float FX_SPREAD = 0.4f;

        private static GameObject MakeEyeSparklePrefab()
        {
            EnsureFolder("Assets/Prefabs", "Fx");
            string prefabPath = $"{FX_FOLDER}/{EYE_FX_NAME}.prefab";

            // **이미 있으면 건드리지 않는다.** 이펙트의 자리·개수는 이제 프리팹이 주인이고,
            // 손으로 옮긴 값을 씬 재생성이 매번 지워 버리면 프리팹으로 뺀 의미가 없다.
            // 처음부터 다시 굽고 싶으면 프리팹을 지우고 이 메뉴를 돌린다.
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;

            Sprite sprite = MakeSparkleSprite();
            if (sprite == null) return null;

            // 낱개 프리팹은 폐기했다 — 무리 하나가 프리팹이다.
            AssetDatabase.DeleteAsset($"{FX_FOLDER}/FX_EyeSparkle.prefab");
            AssetDatabase.DeleteAsset($"{FX_FOLDER}/FX_EyeGlow.prefab");

            var sparkleTint = new Color(1f, 0.95f, 0.62f, 1f);
            var glowTint = new Color(1f, 0.85f, 0.30f, 0.85f);

            // 머리 본 기준 월드 단위 — 눈 자리(좌/우)와 그 둘레. 씬에서 재서 넣은 값이다.
            var eyeL = new Vector3(-0.199f, 0.206f, 0.253f);
            var eyeR = new Vector3(0.199f, 0.206f, 0.253f);

            var glowSpots = new[] { eyeL, eyeR };
            var starSpots = new[]
            {
                eyeL,
                eyeR,
                new Vector3(-0.268f, 0.251f, 0.233f),
                new Vector3(0.258f, 0.243f, 0.233f),
                new Vector3(0f, 0.299f, 0.243f)
            };

            Gather(glowSpots, starSpots, FX_SPREAD);

            var root = new GameObject(EYE_FX_NAME);

            var glows = new SpriteRenderer[glowSpots.Length];
            glows[0] = AddFxSprite(root.transform, "EyeGlow_L", sprite, glowTint, glowSpots[0], 0.075f);
            glows[1] = AddFxSprite(root.transform, "EyeGlow_R", sprite, glowTint, glowSpots[1], 0.075f);

            string[] starNames = { "Sparkle_EyeL", "Sparkle_EyeR", "Sparkle_OuterL", "Sparkle_OuterR", "Sparkle_Top" };
            float[] starSizes = { 0.20f, 0.20f, 0.10f, 0.10f, 0.085f };

            var stars = new SpriteRenderer[starSpots.Length];
            for (int i = 0; i < starSpots.Length; i++)
            {
                stars[i] = AddFxSprite(root.transform, starNames[i], sprite, sparkleTint, starSpots[i], starSizes[i]);
            }

            var fx = root.AddComponent<CTC.Runtime.Fx.FxEyeSparkle>();
            var so = new SerializedObject(fx);
            AssignRenderers(so.FindProperty("m_Glows"), glows);
            AssignRenderers(so.FindProperty("m_Sparkles"), stars);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>무리 전체를 제 중심 쪽으로 당긴다 — 위치는 유지되고 간격만 좁아진다.</summary>
        private static void Gather(Vector3[] _a, Vector3[] _b, float _spread)
        {
            if (Mathf.Approximately(_spread, 1f)) return;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < _a.Length; i++) sum += _a[i];
            for (int i = 0; i < _b.Length; i++) sum += _b[i];
            Vector3 center = sum / (_a.Length + _b.Length);

            for (int i = 0; i < _a.Length; i++) _a[i] = center + (_a[i] - center) * _spread;
            for (int i = 0; i < _b.Length; i++) _b[i] = center + (_b[i] - center) * _spread;
        }

        private static SpriteRenderer AddFxSprite(Transform _parent, string _name, Sprite _sprite,
            Color _tint, Vector3 _localPosition, float _size)
        {
            var go = new GameObject(_name);
            go.transform.SetParent(_parent, false);
            go.transform.localPosition = _localPosition;
            go.transform.localScale = Vector3.one * _size;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.color = _tint;
            return sr;
        }

        private static void AssignRenderers(SerializedProperty _prop, SpriteRenderer[] _values)
        {
            _prop.arraySize = _values.Length;
            for (int i = 0; i < _values.Length; i++)
            {
                _prop.GetArrayElementAtIndex(i).objectReferenceValue = _values[i];
            }
        }

        /// <summary>
        /// 4각 별을 픽셀로 그려 PNG로 굽고 스프라이트로 들여온다.
        /// 가로·세로 뾰족한 가시 둘과 가운데 밝은 심 — 만화에서 눈이 반짝일 때 쓰는 그 모양이다.
        /// </summary>
        private static Sprite MakeSparkleSprite()
        {
            const int SIZE = 128;
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[SIZE * SIZE];

            for (int y = 0; y < SIZE; y++)
            {
                for (int x = 0; x < SIZE; x++)
                {
                    float nx = (x + 0.5f) / SIZE * 2f - 1f;
                    float ny = (y + 0.5f) / SIZE * 2f - 1f;
                    float ax = Mathf.Abs(nx);
                    float ay = Mathf.Abs(ny);

                    // 가시는 한 축으로 길고 다른 축으로 급히 좁아진다 — 그래야 뾰족해 보인다.
                    float horiz = Mathf.Pow(Mathf.Clamp01(1f - ax), 0.55f) * Mathf.Pow(Mathf.Clamp01(1f - ay * 7.5f), 1.5f);
                    float vert = Mathf.Pow(Mathf.Clamp01(1f - ay), 0.55f) * Mathf.Pow(Mathf.Clamp01(1f - ax * 7.5f), 1.5f);
                    float core = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny) * 2.6f), 2.4f);

                    float a = Mathf.Clamp01(horiz + vert + core * 1.2f);
                    Color c = Color.Lerp(new Color(1f, 0.88f, 0.42f), Color.white, Mathf.Clamp01(core * 1.6f));
                    c.a = a;
                    pixels[y * SIZE + x] = c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            File.WriteAllBytes(SPARKLE_TEXTURE, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SPARKLE_TEXTURE, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(SPARKLE_TEXTURE) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spritePixelsPerUnit = 128f;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(SPARKLE_TEXTURE);
        }

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 1.15f, -4.1f);
            go.transform.rotation = Quaternion.Euler(12.5f, 0f, 0f);

            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.012f, 0.016f, 0.026f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            go.AddComponent<AudioListener>();
        }

        // ── 애니메이터 ────────────────────────────────────────────────────

        /// <summary>FBX 안의 클립 하나만 무한 재생하는 컨트롤러. 없으면 null(경고).</summary>
        private static AnimatorController MakeLoopController(string _name, string _fbxPath, string _clipName)
        {
            AnimationClip clip = FindClip(_fbxPath, _clipName);
            if (clip == null)
            {
                Debug.LogWarning($"[IntroSceneBuilder] 클립 못 찾음: {_clipName} ({_fbxPath})");
                return null;
            }

            string path = $"{ANIM_FOLDER}/{_name}.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            AnimatorControllerLayer layer = controller.layers[0];
            AnimatorState state = layer.stateMachine.AddState(_clipName);
            state.motion = clip;
            layer.stateMachine.defaultState = state;
            controller.layers = new[] { layer };
            return controller;
        }

        private static AnimationClip FindClip(string _fbxPath, string _clipName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(_fbxPath);
            for (int i = 0; i < assets.Length; i++)
            {
                var clip = assets[i] as AnimationClip;
                if (clip != null && clip.name == _clipName) return clip;
            }
            return null;
        }

        // ── 머티리얼 ──────────────────────────────────────────────────────

        private static Material MakeMaterial(string _name, Color _color, float _metallic, float _smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            string path = $"{MAT_FOLDER}/{_name}.mat";
            var mat = new Material(shader) { name = _name };
            mat.SetColor("_BaseColor", _color);
            mat.SetColor("_Color", _color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", _metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", _smoothness);

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void SetMaterial(GameObject _go, Material _mat)
        {
            var renderer = _go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = _mat;
        }

        /// <summary>모델 프리팹 아래의 모든 렌더러에 같은 머티리얼을 씌운다(LOD가 여러 개다).</summary>
        private static void ApplyMaterial(GameObject _root, Material _mat)
        {
            if (_mat == null)
            {
                Debug.LogWarning($"[IntroSceneBuilder] 머티리얼을 못 찾아 {_root.name}이 마젠타로 뜬다");
                return;
            }

            Renderer[] renderers = _root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var mats = new Material[renderers[i].sharedMaterials.Length];
                for (int m = 0; m < mats.Length; m++) mats[m] = _mat;
                renderers[i].sharedMaterials = mats;
            }
        }

        // ── UI ───────────────────────────────────────────────────────────

        private static void BuildCanvas(IntroDirector _director, CTC.Runtime.Lobby.FrontDoor.SessionStarter _starter)
        {
            var canvasGo = new GameObject("IntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            RectTransform root = canvasGo.GetComponent<RectTransform>();

            // 게임 이름은 텍스트가 아니라 로고 이미지다(2026-08-31 사용자 지시).
            // 원본은 스팀 라이브러리 로고(1280×720 투명 PNG) — 스토어와 인게임이 같은 얼굴을 쓴다.
            var logoSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Materials/UI/T_GameLogo.png");
            if (logoSprite != null)
            {
                var logoGo = new GameObject("Logo", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                var logoRect = logoGo.GetComponent<RectTransform>();
                logoRect.SetParent(root, false);
                logoRect.anchorMin = logoRect.anchorMax = new Vector2(0.5f, 1f);
                logoRect.pivot = new Vector2(0.5f, 1f);
                logoRect.anchoredPosition = new Vector2(0f, -60f);
                logoRect.sizeDelta = new Vector2(760f, 428f);
                var logoImage = logoGo.GetComponent<UnityEngine.UI.Image>();
                logoImage.sprite = logoSprite;
                logoImage.preserveAspect = true;
                logoImage.raycastTarget = false;
            }
            else
            {
                // 스프라이트 미임포트 시 텍스트로 후퇴 — 씬 생성이 멈추면 안 된다
                TMP_Text logo = MakeText(root, "Logo", "CANADIAN MOUSE", 96, FontStyles.Bold,
                    new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1400f, 130f));
                logo.color = new Color(1f, 0.914f, 0.639f);
                logo.alignment = TextAlignmentOptions.Center;
                logo.characterSpacing = 6f;
            }

            // ── 메인 메뉴 (좌하단 세로 목록) ──
            // 커서가 올라간 줄에만 왼쪽 강조 막대와 뒤쪽 띠가 켜진다.
            // 2026-09-14: 「게임 시작」이 R.E.P.O.식 셋으로 갈렸다(방 만들기·공개 게임·친구 방 참가).
            Button hostButton = MakeMenuItem(root, "MenuHost", "방 만들기", MENU_TOP_Y, out IntroMenuItem hostItem);
            Button publicButton = MakeMenuItem(root, "MenuPublic", "공개 게임", MENU_TOP_Y - MENU_GAP, out IntroMenuItem publicItem);
            Button friendButton = MakeMenuItem(root, "MenuFriend", "친구 방 참가", MENU_TOP_Y - MENU_GAP * 2f, out IntroMenuItem friendItem);
            Button optionsButton = MakeMenuItem(root, "MenuOptions", "설정", MENU_TOP_Y - MENU_GAP * 3f, out IntroMenuItem optionsItem);
            Button quitButton = MakeMenuItem(root, "MenuQuit", "종료", MENU_TOP_Y - MENU_GAP * 4f, out IntroMenuItem quitItem);

            // 연결 중 표시 — 메뉴 바로 위. 접속 절차가 도는 동안만 켜진다.
            TMP_Text connecting = MakeText(root, "ConnectingLabel", "연결 중…", 24, FontStyles.Bold,
                new Vector2(0f, 0f), new Vector2(MENU_LEFT + 210f, MENU_TOP_Y + 62f), new Vector2(420f, 36f));
            connecting.alignment = TextAlignmentOptions.Left;
            connecting.color = ACCENT;
            connecting.gameObject.SetActive(false);

            // ── 실패 패널 (메뉴 아래) ──
            RectTransform errorRect = MakePanel(root, "ErrorPanel", new Color(0.235f, 0.086f, 0.078f, 0.88f),
                new Vector2(0f, 0f), new Vector2(MENU_LEFT + 380f, 168f), new Vector2(760f, 68f));
            TMP_Text errorText = MakeText(errorRect, "Text", "스팀에 연결하지 못했습니다.", 22, FontStyles.Normal,
                new Vector2(0f, 0.5f), new Vector2(268f, 0f), new Vector2(490f, 56f));
            errorText.alignment = TextAlignmentOptions.Left;
            errorText.color = new Color(1f, 0.788f, 0.753f);

            RectTransform retryRect = MakePanel(errorRect, "RetryButton", new Color(0.949f, 0.439f, 0.361f),
                new Vector2(1f, 0.5f), new Vector2(-84f, 0f), new Vector2(140f, 46f));
            var retryButton = retryRect.gameObject.AddComponent<Button>();
            TMP_Text retryLabel = MakeText(retryRect, "Label", "다시 시도", 20, FontStyles.Bold,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(132f, 40f));
            retryLabel.color = new Color(0.102f, 0.059f, 0.051f);
            retryLabel.alignment = TextAlignmentOptions.Center;
            errorRect.gameObject.SetActive(false);

            // 부팅 체크 상태 표시(칩·진행바)는 두지 않는다 — 4개 중 유저가 손쓸 수 있는 건 없고,
            // 스팀 출시 뒤에는 "스팀 미실행"이라는 상태 자체가 성립하지 않는다(스팀이 실행 주체다).
            // 검사는 계속 돌지만, 화면에는 **막혔을 때 사유 한 줄**만 나온다.

            // ── 좌하단 버전 ──
            // 스팀 연결 상태는 표시하지 않는다 — 스팀으로만 실행되는 정식판에선 항상 "연결됨"이라 뜻이 없다.
            TMP_Text version = MakeText(root, "Version", "v0.9.2-dev  |  build 2026.08.26", 18, FontStyles.Normal,
                new Vector2(0f, 0f), new Vector2(MENU_LEFT + 210f, 54f), new Vector2(420f, 30f));
            version.alignment = TextAlignmentOptions.Left;
            version.color = new Color(0.373f, 0.408f, 0.455f);

            // ── 배선 ──
            var view = canvasGo.AddComponent<IntroView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("m_Director").objectReferenceValue = _director;
            SerializedProperty items = viewSo.FindProperty("m_ActionItems");
            items.arraySize = 3;
            items.GetArrayElementAtIndex(0).objectReferenceValue = hostItem;
            items.GetArrayElementAtIndex(1).objectReferenceValue = publicItem;
            items.GetArrayElementAtIndex(2).objectReferenceValue = friendItem;
            viewSo.FindProperty("m_Starter").objectReferenceValue = _starter;
            viewSo.FindProperty("m_ConnectingLabel").objectReferenceValue = connecting.gameObject;
            viewSo.FindProperty("m_ErrorPanel").objectReferenceValue = errorRect.gameObject;
            viewSo.FindProperty("m_ErrorText").objectReferenceValue = errorText;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            // 설정 창은 **프리팹**이다(2026-09-01 팝업 시스템) — 씬은 매니저와 캔버스만 준다.
            var escGo = new GameObject("EscapeRouter");
            var introEsc = escGo.AddComponent<CTC.Runtime.Core.EscapeRouter>();
            var escSo = new SerializedObject(introEsc);
            escSo.FindProperty("m_OpenOptionsOnEscape").boolValue = false;   // 메뉴에 '설정'이 있다
            escSo.ApplyModifiedPropertiesWithoutUndo();
            PopupSceneBinder.Attach(canvasGo.GetComponent<Canvas>());

            var controller = canvasGo.AddComponent<IntroController>();
            var ctrlSo = new SerializedObject(controller);
            ctrlSo.FindProperty("m_Director").objectReferenceValue = _director;
            ctrlSo.FindProperty("m_Starter").objectReferenceValue = _starter;
            ctrlSo.FindProperty("m_HostButton").objectReferenceValue = hostButton;
            ctrlSo.FindProperty("m_PublicButton").objectReferenceValue = publicButton;
            ctrlSo.FindProperty("m_FriendButton").objectReferenceValue = friendButton;
            ctrlSo.FindProperty("m_RetryButton").objectReferenceValue = retryButton;
            ctrlSo.FindProperty("m_QuitButton").objectReferenceValue = quitButton;
            ctrlSo.FindProperty("m_OptionsButton").objectReferenceValue = optionsButton;
            ctrlSo.ApplyModifiedPropertiesWithoutUndo();

            if (optionsItem != null) optionsItem.Locked = false;
            if (quitItem != null) quitItem.Locked = false;
        }

        /// <summary>
        /// 메뉴 한 줄 — 뒤쪽 띠 · 왼쪽 강조 막대 · 라벨 · 오른쪽 사유.
        /// 띠와 막대는 꺼진 채로 시작하고 <see cref="IntroMenuItem"/>이 커서에 맞춰 켠다.
        /// </summary>
        private static Button MakeMenuItem(Transform _parent, string _name, string _text, float _y,
            out IntroMenuItem _item)
        {
            RectTransform row = MakePanel(_parent, _name, new Color(0f, 0f, 0f, 0f),
                new Vector2(0f, 0f), new Vector2(MENU_LEFT + MENU_W * 0.5f, _y), new Vector2(MENU_W, MENU_H));

            Image highlight = MakeImage(row, "Highlight", new Color(1f, 0.847f, 0.302f, 0.10f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MENU_W, MENU_H));
            highlight.gameObject.SetActive(false);

            Image accent = MakeImage(row, "Accent", ACCENT,
                new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(6f, MENU_H * 0.62f));
            accent.gameObject.SetActive(false);

            TMP_Text label = MakeText(row, "Label", _text, 34, FontStyles.Bold,
                new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(260f, MENU_H));
            label.alignment = TextAlignmentOptions.Left;
            label.characterSpacing = 4f;

            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;

            _item = row.gameObject.AddComponent<IntroMenuItem>();
            var so = new SerializedObject(_item);
            so.FindProperty("m_Label").objectReferenceValue = label;
            so.FindProperty("m_AccentBar").objectReferenceValue = accent;
            so.FindProperty("m_HighlightBar").objectReferenceValue = highlight;
            so.ApplyModifiedPropertiesWithoutUndo();

            return button;
        }

        private static RectTransform MakePanel(Transform _parent, string _name, Color _color,
            Vector2 _anchor, Vector2 _pos, Vector2 _size)
        {
            Image image = MakeImage(_parent, _name, _color, _anchor, _pos, _size);
            return image.rectTransform;
        }

        private static Image MakeImage(Transform _parent, string _name, Color _color,
            Vector2 _anchor, Vector2 _pos, Vector2 _size)
        {
            var go = new GameObject(_name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = _anchor;
            rect.anchorMax = _anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = _pos;
            rect.sizeDelta = _size;

            var image = go.GetComponent<Image>();
            image.color = _color;
            return image;
        }

        private static TMP_Text MakeText(Transform _parent, string _name, string _text, float _size,
            FontStyles _style, Vector2 _anchor, Vector2 _pos, Vector2 _rect)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.transform.SetParent(_parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = _anchor;
            rect.anchorMax = _anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = _pos;
            rect.sizeDelta = _rect;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = _text;
            text.fontSize = _size;
            text.fontStyle = _style;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }
    }
}
