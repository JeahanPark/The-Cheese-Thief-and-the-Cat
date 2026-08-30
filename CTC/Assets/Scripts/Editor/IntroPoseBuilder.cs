using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 인트로 "치즈 치켜들기" 포즈 일습을 코드로 만든다 — 클립 · Avatar Mask · Animator Controller.
    ///
    /// **이건 스파이크다.** 앞으로 우리 MCP에 넣을 u_editor_animation(create / set_curve /
    /// create_mask / create_controller)이 할 일을 그대로 한 번 통과시켜, 도구의 면을 추측이 아니라
    /// 실측으로 정하려고 만들었다(2026-08-26). 여기서 확인된 값·순서가 그대로 핸들러 본체가 된다.
    ///
    /// 실측으로 걸린 함정 — 전부 **에러를 안 낸다**:
    ///   1. 커브 경로는 Animator 기준이다. Animator가 Model에 있으므로 "Model/" 접두를 뺀다.
    ///   2. AvatarMask.AddTransformPath는 **계층 루트 기준** 경로를 넣는다 → 기준이 어긋나
    ///      마스크가 조용히 0개를 켠다. 경로를 직접 계산해 넣어야 한다.
    ///   3. AnimationClip.SampleAnimation도 같은 기준을 요구한다 — 루트를 넘기면 아무 일도 안 난다.
    ///   4. 씬뷰 스크린샷은 갱신이 안 붙어 같은 그림을 반복해 준다 → 임시 카메라로 직접 렌더한다.
    ///
    /// 모델 사실(Dump Mouse Rig + 렌더로 확인):
    ///   - 본 13개. 앞다리는 leg.F.L / leg.F.R **관절 1개**, 손 본이 없다.
    ///   - **네 발 짐승이라 몸통이 수평이다** — 두 발로 세우려면 body를 먼저 세워야 한다.
    ///
    /// Tools > CTC > Dev > Build Intro Pose / Preview Intro Pose / Shoot Intro Pose
    /// </summary>
    public static class IntroPoseBuilder
    {
        private const string PREFAB_PATH = "Assets/Prefabs/PlayerMouse.prefab";
        private const string OUT_FOLDER = "Assets/Animations/Intro";

        private const string ANIM_FBX_PATH =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Animations/Mouse_Animations.fbx";

        /// <summary>Animator(Model) 기준 경로 — 프리팹 기준의 "Model/" 접두가 빠진다.</summary>
        private const string BODY_PATH = "Rig/root/body";

        private const string ARM_PATH = "Rig/root/body/leg.F.L";

        private const string BASE_CLIP_NAME = "Idle_B";

        // ── 포즈 정의 (스파이크라 여기만 고치면 된다) ────────────────────────
        private struct PoseBone
        {
            public string Path;
            public Vector3 Offset;
            public Vector3 Sway;

            public PoseBone(string _path, Vector3 _offset, Vector3 _sway)
            {
                Path = _path;
                Offset = _offset;
                Sway = _sway;
            }
        }

        /// <summary>
        /// 이 쥐는 네 발 짐승이라 몸통이 수평이다. "두 발로 서서 치즈를 든" 그림을 내려면
        /// body를 세운 뒤 앞발을 드는 수밖에 없다 — 실제 쥐도 뒷발로 서니 부자연스럽지 않다.
        /// </summary>
        private static readonly PoseBone[] POSE =
        {
            new PoseBone(BODY_PATH, new Vector3(-62f, 0f, 0f), new Vector3(2.5f, 0f, 0f)),
            new PoseBone(ARM_PATH, new Vector3(-70f, 0f, 0f), new Vector3(4f, 0f, 3f))
        };

        private const float LOOP_SECONDS = 2.4f;
        private const int FRAME_RATE = 30;

        /// <summary>베이스 Idle이 0.42초라 인트로엔 너무 빠르다 — 느긋하게 깐다.</summary>
        private const float BASE_LAYER_SPEED = 0.5f;
        // ────────────────────────────────────────────────────────────────

        private const string PREVIEW_NAME = "__IntroPosePreview";
        private const int SHOT_W = 360;
        private const int SHOT_H = 460;

        private static string ReportPath => Path.Combine(Path.GetTempPath(), "ctc-intro-pose.txt");
        private static string ShotPath => Path.Combine(Path.GetTempPath(), "ctc-intro-pose.png");

        [MenuItem("Tools/CTC/Dev/Build Intro Pose")]
        public static void Build()
        {
            var log = new StringBuilder();

            Transform model = LoadModel();
            if (model == null)
            {
                log.AppendLine($"[실패] Model 없음 — {PREFAB_PATH}");
                Finish(log);
                return;
            }

            EnsureFolder();

            AnimationClip clip = BuildPoseClip(model, log);
            AvatarMask mask = BuildPoseMask(model, log);
            AnimatorController controller = BuildController(clip, mask, log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine();
            log.AppendLine("=== 결과 ===");
            log.AppendLine($"  클립      {AssetDatabase.GetAssetPath(clip)}");
            log.AppendLine($"  마스크    {AssetDatabase.GetAssetPath(mask)}");
            log.AppendLine($"  컨트롤러  {AssetDatabase.GetAssetPath(controller)}");
            Finish(log);
        }

        private static Transform LoadModel()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            return prefab != null ? prefab.transform.Find("Model") : null;
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(OUT_FOLDER)) return;
            if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
            AssetDatabase.CreateFolder("Assets/Animations", "Intro");
        }

        /// <summary>POSE에 적힌 본만 건드리는 루프 클립. 나머지 본은 베이스 레이어(Idle_B)가 계속 돌린다.</summary>
        private static AnimationClip BuildPoseClip(Transform _model, StringBuilder _log)
        {
            string path = OUT_FOLDER + "/Intro_LiftCheese.anim";
            var clip = new AnimationClip { frameRate = FRAME_RATE };

            _log.AppendLine("=== 클립 ===");
            _log.AppendLine($"  {LOOP_SECONDS:0.##}s 루프 · {FRAME_RATE}fps");

            for (int i = 0; i < POSE.Length; i++)
            {
                PoseBone bone = POSE[i];
                Transform t = _model.Find(bone.Path);
                if (t == null)
                {
                    _log.AppendLine($"  [경고] 본 없음: {bone.Path}");
                    continue;
                }

                Vector3 rest = t.localEulerAngles;
                Vector3 target = rest + bone.Offset;
                SetEuler(clip, bone.Path, "x", target.x, bone.Sway.x);
                SetEuler(clip, bone.Path, "y", target.y, bone.Sway.y);
                SetEuler(clip, bone.Path, "z", target.z, bone.Sway.z);

                _log.AppendLine($"  {bone.Path}");
                _log.AppendLine($"    기본 {rest.x:0.#},{rest.y:0.#},{rest.z:0.#} → 목표 {target.x:0.#},{target.y:0.#},{target.z:0.#} (흔들 ±{bone.Sway.x:0.#},{bone.Sway.y:0.#},{bone.Sway.z:0.#})");
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        /// <summary>0 → 중간 → 끝이 같은 값으로 돌아오는 부드러운 루프 커브.</summary>
        private static void SetEuler(AnimationClip _clip, string _path, string _axis, float _center, float _amplitude)
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, _center),
                new Keyframe(LOOP_SECONDS * 0.5f, _center + _amplitude),
                new Keyframe(LOOP_SECONDS, _center));

            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }

            var binding = EditorCurveBinding.FloatCurve(_path, typeof(Transform), "localEulerAnglesRaw." + _axis);
            AnimationUtility.SetEditorCurve(_clip, binding, curve);
        }

        /// <summary>POSE의 본만 켠 Transform 마스크 — 경로를 직접 계산해 넣는다(함정 2).</summary>
        private static AvatarMask BuildPoseMask(Transform _model, StringBuilder _log)
        {
            string path = OUT_FOLDER + "/IntroPose.mask";

            var paths = new List<string>();
            CollectPaths(_model, _model, paths);

            var mask = new AvatarMask { transformCount = paths.Count };
            int on = 0;
            for (int i = 0; i < paths.Count; i++)
            {
                mask.SetTransformPath(i, paths[i]);
                bool active = false;
                for (int p = 0; p < POSE.Length; p++)
                {
                    if (paths[i] == POSE[p].Path)
                    {
                        active = true;
                        break;
                    }
                }
                mask.SetTransformActive(i, active);
                if (active) on++;
            }

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mask, path);

            _log.AppendLine();
            _log.AppendLine("=== 마스크 ===");
            _log.AppendLine($"  경로 {paths.Count}개 중 {on}개 켬 (POSE 본 {POSE.Length}개)");
            if (on != POSE.Length) _log.AppendLine("  [경고] 수가 안 맞는다 — 경로 기준이 어긋났다");
            return mask;
        }

        private static void CollectPaths(Transform _root, Transform _t, List<string> _out)
        {
            if (_t != _root) _out.Add(AnimationUtility.CalculateTransformPath(_t, _root));
            for (int i = 0; i < _t.childCount; i++) CollectPaths(_root, _t.GetChild(i), _out);
        }

        /// <summary>레이어 0에 Idle_B(느리게), 레이어 1에 포즈 본만 덮어쓰기.</summary>
        private static AnimatorController BuildController(AnimationClip _pose, AvatarMask _mask, StringBuilder _log)
        {
            string path = OUT_FOLDER + "/IntroMouse.controller";
            AssetDatabase.DeleteAsset(path);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            AnimationClip idle = FindClip(BASE_CLIP_NAME);
            AnimatorControllerLayer baseLayer = controller.layers[0];
            baseLayer.name = "Base";
            AnimatorState idleState = baseLayer.stateMachine.AddState("Idle");
            idleState.motion = idle;
            idleState.speed = BASE_LAYER_SPEED;
            baseLayer.stateMachine.defaultState = idleState;
            controller.layers = new[] { baseLayer };

            var poseLayer = new AnimatorControllerLayer
            {
                name = "Pose",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = _mask,
                stateMachine = new AnimatorStateMachine { name = "Pose", hideFlags = HideFlags.HideInHierarchy }
            };
            AssetDatabase.AddObjectToAsset(poseLayer.stateMachine, controller);

            AnimatorState poseState = poseLayer.stateMachine.AddState("LiftCheese");
            poseState.motion = _pose;
            poseLayer.stateMachine.defaultState = poseState;
            controller.AddLayer(poseLayer);

            _log.AppendLine();
            _log.AppendLine("=== 컨트롤러 ===");
            _log.AppendLine($"  레이어 0 Base — {(idle != null ? idle.name : "(없음)")} · 속도 {BASE_LAYER_SPEED:0.##}");
            _log.AppendLine("  레이어 1 Pose — Intro_LiftCheese · Override · 마스크");
            if (idle == null) _log.AppendLine($"  [경고] 베이스 클립 못 찾음: {BASE_CLIP_NAME}");
            return controller;
        }

        private static AnimationClip FindClip(string _name)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ANIM_FBX_PATH);
            for (int i = 0; i < assets.Length; i++)
            {
                var clip = assets[i] as AnimationClip;
                if (clip != null && clip.name == _name) return clip;
            }
            return null;
        }

        /// <summary>플레이모드 없이 클립을 t=0에서 샘플링해 씬에 세운다. 씬에 저장하지 않는다.</summary>
        [MenuItem("Tools/CTC/Dev/Preview Intro Pose")]
        public static void Preview()
        {
            ClearPreview();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError($"[IntroPoseBuilder] 프리팹 없음: {PREFAB_PATH}");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = PREVIEW_NAME;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;

            // SampleAnimation은 커브 경로의 **기준 오브젝트**를 받는다(함정 3).
            Transform model = go.transform.Find("Model");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(OUT_FOLDER + "/Intro_LiftCheese.anim");
            if (clip != null && model != null) clip.SampleAnimation(model.gameObject, 0f);

            var sb = new StringBuilder("[IntroPoseBuilder] 미리보기 —");
            for (int i = 0; i < POSE.Length; i++)
            {
                Transform t = go.transform.Find("Model/" + POSE[i].Path);
                sb.Append($" {POSE[i].Path}={(t != null ? t.localEulerAngles.ToString("0.#") : "(못 찾음)")}");
            }
            Debug.Log(sb.ToString());

            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/CTC/Dev/Clear Intro Preview")]
        public static void ClearPreview()
        {
            GameObject existing = GameObject.Find(PREVIEW_NAME);
            while (existing != null)
            {
                Object.DestroyImmediate(existing);
                existing = GameObject.Find(PREVIEW_NAME);
            }
        }

        /// <summary>임시 카메라로 4방향을 직접 렌더해 가로로 이어 붙인다(함정 4).</summary>
        [MenuItem("Tools/CTC/Dev/Shoot Intro Pose")]
        public static void Shoot()
        {
            Preview();
            GameObject go = GameObject.Find(PREVIEW_NAME);
            if (go == null) return;

            Bounds bounds;
            if (!TryCalcBounds(go, out bounds))
            {
                Debug.LogError("[IntroPoseBuilder] 렌더러가 없어 화각을 못 잡는다");
                return;
            }

            var camGo = new GameObject("__IntroShotCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.01f;

            float dist = bounds.extents.magnitude * 3.6f + 0.15f;
            var rt = new RenderTexture(SHOT_W, SHOT_H, 24);
            var sheet = new Texture2D(SHOT_W * 4, SHOT_H, TextureFormat.RGB24, false);

            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = Quaternion.Euler(10f, i * 90f, 0f) * Vector3.back;
                camGo.transform.position = bounds.center + dir * dist;
                camGo.transform.LookAt(bounds.center);

                cam.targetTexture = rt;
                cam.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var shot = new Texture2D(SHOT_W, SHOT_H, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, SHOT_W, SHOT_H), 0, 0);
                shot.Apply();
                RenderTexture.active = prev;

                sheet.SetPixels(i * SHOT_W, 0, SHOT_W, SHOT_H, shot.GetPixels());
                Object.DestroyImmediate(shot);
            }

            sheet.Apply();
            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            rt.Release();
            Object.DestroyImmediate(rt);

            File.WriteAllBytes(ShotPath, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log($"[IntroPoseBuilder] 4방향(yaw 0/90/180/270) → {ShotPath}");
        }

        /// <summary>
        /// 몸통 각도 후보. 첫 시도에서 X는 **피치가 아니라 롤**이라는 게 드러나서(4장이 거의 같았다)
        /// 이제 축 자체를 쓸어 본다 — 맨 왼쪽이 기본 자세(대조군)다.
        /// </summary>
        private static readonly Vector3[] BODY_SWEEP =
        {
            Vector3.zero,
            new Vector3(0f, 0f, -60f),
            new Vector3(0f, 0f, 60f),
            new Vector3(0f, -60f, 0f)
        };

        /// <summary>
        /// 같은 시점에서 몸통 각도만 바꿔 4장을 굽는다. 클립을 거치지 않고 본을 직접 돌린다 —
        /// 값을 정하는 단계라 클립까지 다시 만들 이유가 없다.
        /// </summary>
        [MenuItem("Tools/CTC/Dev/Sweep Intro Body Angle")]
        public static void SweepBodyAngle()
        {
            ClearPreview();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null) return;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = PREVIEW_NAME;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;

            Transform model = go.transform.Find("Model");
            Transform body = go.transform.Find("Model/" + BODY_PATH);
            Transform arm = go.transform.Find("Model/" + ARM_PATH);
            if (body == null || model == null)
            {
                Debug.LogError($"[IntroPoseBuilder] body 없음: {BODY_PATH}");
                return;
            }

            Vector3 bodyRest = body.localEulerAngles;
            Vector3 armRest = arm != null ? arm.localEulerAngles : Vector3.zero;

            var camGo = new GameObject("__IntroShotCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.01f;

            var rt = new RenderTexture(SHOT_W, SHOT_H, 24);
            var sheet = new Texture2D(SHOT_W * BODY_SWEEP.Length, SHOT_H, TextureFormat.RGB24, false);

            for (int i = 0; i < BODY_SWEEP.Length; i++)
            {
                // 본을 직접 돌리면 에디터에서 스킨드 메시가 다시 스키닝되지 않는다(2026-08-26 실측 —
                // 4장이 전부 기본 자세로 나왔다). 임시 클립을 만들어 샘플링해야 실제로 반영된다.
                var tmp = new AnimationClip();
                SetEulerConstant(tmp, BODY_PATH, bodyRest + BODY_SWEEP[i]);
                if (arm != null) SetEulerConstant(tmp, ARM_PATH, armRest);
                tmp.SampleAnimation(model.gameObject, 0f);
                Object.DestroyImmediate(tmp);
                Debug.Log($"[Sweep {i}] 요청 {(bodyRest + BODY_SWEEP[i]).ToString("0.#")} → 실제 {body.localEulerAngles.ToString("0.#")}");

                Bounds bounds;
                if (!TryCalcBounds(go, out bounds)) continue;

                float dist = bounds.extents.magnitude * 3.6f + 0.15f;
                Vector3 dir = Quaternion.Euler(8f, 90f, 0f) * Vector3.back;
                camGo.transform.position = bounds.center + dir * dist;
                camGo.transform.LookAt(bounds.center);

                cam.targetTexture = rt;
                cam.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var shot = new Texture2D(SHOT_W, SHOT_H, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, SHOT_W, SHOT_H), 0, 0);
                shot.Apply();
                RenderTexture.active = prev;

                sheet.SetPixels(i * SHOT_W, 0, SHOT_W, SHOT_H, shot.GetPixels());
                Object.DestroyImmediate(shot);
            }

            sheet.Apply();
            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            rt.Release();
            Object.DestroyImmediate(rt);

            string path = Path.Combine(Path.GetTempPath(), "ctc-intro-sweep.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            var names = new List<string>();
            for (int i = 0; i < BODY_SWEEP.Length; i++) names.Add(BODY_SWEEP[i].ToString("0"));
            Debug.Log($"[IntroPoseBuilder] body 기본 {bodyRest.ToString("0.#")} · 후보 {string.Join(" | ", names)} → {path}");
        }

        /// <summary>
        /// 값 하나짜리 커브 — 포즈만 세울 때 쓴다.
        /// **AnimationUtility.SetEditorCurve는 에셋이 아닌 인메모리 클립엔 안 먹는다**(2026-08-26 실측:
        /// 4장이 전부 기본 자세로 나왔다). 런타임 커브를 채우는 SetCurve를 써야 SampleAnimation이 반영한다.
        /// </summary>
        private static void SetEulerConstant(AnimationClip _clip, string _path, Vector3 _euler)
        {
            _clip.SetCurve(_path, typeof(Transform), "localEulerAnglesRaw.x", AnimationCurve.Constant(0f, 1f, _euler.x));
            _clip.SetCurve(_path, typeof(Transform), "localEulerAnglesRaw.y", AnimationCurve.Constant(0f, 1f, _euler.y));
            _clip.SetCurve(_path, typeof(Transform), "localEulerAnglesRaw.z", AnimationCurve.Constant(0f, 1f, _euler.z));
        }

        private static bool TryCalcBounds(GameObject _go, out Bounds _bounds)
        {
            _bounds = new Bounds();
            Renderer[] renderers = _go.GetComponentsInChildren<Renderer>();
            bool any = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled) continue;
                if (!any)
                {
                    _bounds = renderers[i].bounds;
                    any = true;
                }
                else
                {
                    _bounds.Encapsulate(renderers[i].bounds);
                }
            }
            return any;
        }

        private static void Finish(StringBuilder _log)
        {
            File.WriteAllText(ReportPath, _log.ToString());
            Debug.Log($"[IntroPoseBuilder] → {ReportPath}\n{_log}");
        }
    }
}
