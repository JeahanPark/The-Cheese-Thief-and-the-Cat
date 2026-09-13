using CTC.Runtime.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CTC.Editor
{
    /// <summary>
    /// 애니메이션 검수 씬을 굽는다 — Tools > CTC > Setup AnimReview Scene (애니메이션 검수)(2026-09-13).
    ///
    /// **모델은 실제 플레이어 프리팹의 <c>Model</c>을 복사한다.** 팩 모델을 그냥 놓으면 무릎(<c>shin.B.*</c>)·
    /// 눈 파생 메시·총이 없어 게임과 다른 것을 보게 된다. 프리팹을 다시 구웠으면 이 메뉴도 다시 돌린다
    /// (씬은 매번 새로 만든다 — 손으로 고친 것이 없는 생성물이다).
    ///
    /// 빌드 목록에는 **안 넣는다** — 검수 도구다. 조작은 <see cref="AnimReview"/>.
    /// </summary>
    public static class AnimReviewSceneBuilder
    {
        public const string SCENE_PATH = "Assets/Scenes/AnimReview.unity";
        private const string MOUSE_PREFAB = "Assets/Prefabs/PlayerMouse.prefab";
        private const string CAT_PREFAB = "Assets/Prefabs/PlayerCat.prefab";

        [MenuItem("Tools/CTC/Setup AnimReview Scene (애니메이션 검수)")]
        public static void Build()
        {
            // 열린 씬에 저장 안 한 변경이 있으면 멈춘다 — 새 씬을 열면 그 변경이 말없이 사라지고,
            // 「저장할까?」 대화상자는 에디터를 멈춰 MCP를 죽인다(CLAUDE.md 함정 11).
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene open = SceneManager.GetSceneAt(i);
                if (open.isDirty)
                {
                    Debug.LogError($"[AnimReviewSceneBuilder] '{open.name}'에 저장 안 한 변경이 있다 — 저장하거나 되돌린 뒤 다시 돌려라");
                    return;
                }
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(45f, 150f, 0f);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.19f, 0.22f);
            camera.nearClipPlane = 0.02f;
            camera.fieldOfView = 40f;

            // 바닥 — 발이 바닥을 뚫는지·뜨는지를 눈으로 보는 기준선이다(y = 0).
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            Animator mouse = ExtractModel(MOUSE_PREFAB, "Mouse");
            Animator cat = ExtractModel(CAT_PREFAB, "Cat");
            if (mouse == null || cat == null)
            {
                Debug.LogError("[AnimReviewSceneBuilder] 모델을 못 꺼냈다 — 씬을 저장하지 않는다");
                return;
            }

            var review = new GameObject("AnimReview").AddComponent<AnimReview>();
            var so = new SerializedObject(review);
            so.FindProperty("m_Mouse").objectReferenceValue = mouse;
            so.FindProperty("m_Cat").objectReferenceValue = cat;
            so.FindProperty("m_Camera").objectReferenceValue = camera;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            Debug.Log($"[AnimReviewSceneBuilder] 구웠다: {SCENE_PATH} — 플레이를 누르면 된다");
        }

        /// <summary>프리팹을 놓고 완전히 풀어 <c>Model</c>만 남긴다. Animator를 돌려준다.</summary>
        private static Animator ExtractModel(string _prefabPath, string _name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[AnimReviewSceneBuilder] 프리팹이 없다: {_prefabPath}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            Transform model = instance.transform.Find("Model");
            if (model == null)
            {
                Debug.LogError($"[AnimReviewSceneBuilder] '{_prefabPath}'에 Model 자식이 없다");
                Object.DestroyImmediate(instance);
                return null;
            }

            model.SetParent(null, true);
            model.name = _name;
            model.position = Vector3.zero;
            Object.DestroyImmediate(instance);

            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null) Debug.LogError($"[AnimReviewSceneBuilder] '{_name}'에 Animator가 없다");
            return animator;
        }
    }
}
