using CTC.Runtime.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 자작 FBX의 클립을 팩 컨트롤러의 Base Layer에 **상태로** 넣는다 — 쥐(<c>AC_Mouse</c>)와 고양이(<c>AC_Cat</c>) 공용(2026-09-13).
    ///
    /// 원래 <c>MouseGunBaker.AddMouseOnlyStates</c>였다. 고양이 자작 클립(광분·전직 스킬)이 생기면서 같은 일을
    /// 두 번 쓰지 않으려고 뽑았다.
    ///
    /// YAML을 손으로 쓰지 않는다 — FBX 하위 클립의 fileID는 임포터가 정하는 값이라 손으로 적으면 엉뚱한 클립을
    /// 가리킬 수 있다. 컨트롤러 API가 그 참조를 맺게 한다. 상태 이름이 없으면 <c>CrossFadeInFixedTime</c>은
    /// **에러도 경고도 없이** 아무 일도 안 한다 — 그래서 목록은 <see cref="AnimationStateLogic"/> 한 곳에서 받는다.
    /// </summary>
    public static class RoleClipStates
    {
        private const string ANIMATIONS_DIR =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Animations/";

        public const string MOUSE_CONTROLLER = ANIMATIONS_DIR + "AC_Mouse.controller";
        public const string CAT_CONTROLLER = ANIMATIONS_DIR + "AC_Cat.controller";
        public const string MOUSE_CLIPS_FBX = "Assets/Animations/Pets/CTC_Mouse_Clips.fbx";
        public const string CAT_CLIPS_FBX = "Assets/Animations/Pets/CTC_Cat_Clips.fbx";

        public static void AddMouse()
        {
            Add(MOUSE_CONTROLLER, MOUSE_CLIPS_FBX, AnimationStateLogic.MouseOnly, "쥐 전용");
            // 스턴처럼 **둘 다** 갖는 자작 상태 — 각자의 FBX에서(2026-09-19)
            Add(MOUSE_CONTROLLER, MOUSE_CLIPS_FBX, AnimationStateLogic.CustomShared, "공용 자작");
        }

        public static void AddCat()
        {
            Add(CAT_CONTROLLER, CAT_CLIPS_FBX, AnimationStateLogic.CatOnly, "고양이 전용");
            Add(CAT_CONTROLLER, CAT_CLIPS_FBX, AnimationStateLogic.CustomShared, "공용 자작");
            Remove(CAT_CONTROLLER, OBSOLETE_CAT_STATES);
        }

        /// <summary>
        /// 없어진 고양이 상태 — 남겨 두면 FBX에서 클립이 사라져 **빈 상태**가 된다(에러 없이 아무것도 안 튼다).
        /// <c>CatSkill</c>은 2026-09-13에 전직별 넷(<c>CatSkillFire</c> 등)으로 갈라졌다.
        /// </summary>
        private static readonly string[] OBSOLETE_CAT_STATES = { "CatSkill" };

        public static void Remove(string _controllerPath, string[] _names)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_controllerPath);
            if (controller == null) return;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (string name in _names)
                {
                    AnimatorState state = FindState(layer.stateMachine, name);
                    if (state == null) continue;
                    layer.stateMachine.RemoveState(state);
                    Debug.Log($"[RoleClipStates] 없어진 상태 제거: {controller.name} :: {name}");
                }
            }
            EditorUtility.SetDirty(controller);
        }

        [MenuItem("Tools/CTC/Dev/Bake Cat Clip States")]
        public static void BakeCat()
        {
            AddCat();
            AssetDatabase.SaveAssets();
        }

        public static void Add(string _controllerPath, string _clipsFbx, string[] _names, string _tag)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_controllerPath);
            if (controller == null)
            {
                Debug.LogError($"[RoleClipStates] 컨트롤러 없음: {_controllerPath}");
                return;
            }

            AnimatorStateMachine machine = null;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                machine = layer.stateMachine;
                break;
            }
            if (machine == null) machine = controller.layers[0].stateMachine;

            foreach (string name in _names)
            {
                AnimationClip clip = ClipParityProbe.FindClip(_clipsFbx, name);
                if (clip == null)
                {
                    Debug.LogError($"[RoleClipStates] 클립을 못 찾았다: {_clipsFbx} :: {name}");
                    continue;
                }

                AnimatorState state = FindState(machine, name);
                if (state == null)
                {
                    state = machine.AddState(name);
                    Debug.Log($"[RoleClipStates] 상태 추가({_tag}): {controller.name} :: {name}");
                }

                state.motion = clip;
                // 팩의 다른 상태들과 같게 — 이 컨트롤러는 파라미터 없이 CrossFade만 쓴다.
                state.writeDefaultValues = true;
            }

            EditorUtility.SetDirty(controller);
        }

        private static AnimatorState FindState(AnimatorStateMachine _machine, string _name)
        {
            foreach (ChildAnimatorState child in _machine.states)
            {
                if (child.state != null && child.state.name == _name) return child.state;
            }
            return null;
        }
    }
}
