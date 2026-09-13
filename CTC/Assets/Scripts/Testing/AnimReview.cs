#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using CTC.Runtime.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Testing
{
    /// <summary>
    /// 애니메이션 검수 씬(2026-09-13) — 쥐·고양이 모델에 클립을 하나씩 틀어 **사람이 눈으로** 본다.
    ///
    /// 왜 있나: 자작 클립은 스크립트로 키를 찍어 만든다. 수치(바닥 관통·상태 존재)는 자동으로 재지만
    /// **움직임이 보기 좋은가는 사람만 판정한다.** 게임 안에서는 원샷이 0.5초 스쳐 지나가 볼 수가 없어서
    /// 멈추고·느리게·한 프레임씩 넘겨 볼 자리를 따로 만들었다.
    ///
    /// 모델은 <c>Tools/CTC/Setup AnimReview Scene</c>이 **실제 프리팹의 Model을 복사**해 둔 것이다
    /// (무릎·눈 파생 메시·총이 게임과 같다). 프리팹을 고쳤으면 메뉴를 다시 돌린다.
    /// 네트워크·입력 가드·HUD가 없는 씬이라 게임 코드는 하나도 안 돈다 — 애니메이터만 돈다.
    ///
    /// 조작은 화면 좌상단에 늘 떠 있다(<see cref="HELP"/>).
    /// </summary>
    public class AnimReview : MonoBehaviour
    {
        [SerializeField] private Animator m_Mouse;
        [SerializeField] private Animator m_Cat;
        [SerializeField] private Camera m_Camera;

        private const string HELP =
            "← →  클립 넘기기      Tab  쥐 ↔ 고양이\n" +
            "Space  멈춤/재생      , .  한 프레임씩(멈춘 채)\n" +
            "[ ]  배속             L  반복 켜기/끄기\n" +
            "F  표정 넘기기        G  총 보이기(쥐)\n" +
            "우클릭 드래그  돌려 보기   휠  확대   R  카메라 원위치";

        private const float REPLAY_PAUSE_SECONDS = 0.35f;   // 반복할 때 끝 자세를 잠깐 보여 준다
        private const float ORBIT_SPEED = 0.25f;
        private const float ZOOM_STEP = 0.0015f;

        private bool m_ShowCat;
        private int m_ClipIndex;
        private int m_SpeedIndex = 3;   // 1x
        private bool m_Paused;
        private bool m_Loop = true;
        private int m_FaceIndex;
        private bool m_GunVisible;
        private float m_EndHold = -1f;
        private int m_Frame;

        private List<string> m_Clips = new List<string>();
        private float m_Yaw = 200f;
        private float m_Pitch = 12f;
        private float m_Distance;

        private Animator Current => m_ShowCat ? m_Cat : m_Mouse;

        private void Start()
        {
            SelectCharacter(false);
        }

        private void SelectCharacter(bool _cat)
        {
            m_ShowCat = _cat;
            if (m_Mouse != null) m_Mouse.gameObject.SetActive(!_cat);
            if (m_Cat != null) m_Cat.gameObject.SetActive(_cat);

            m_Clips = AnimReviewLogic.ClipsFor(_cat);
            Animator a = Current;
            if (a != null) m_Clips.RemoveAll(s => !a.HasState(0, Animator.StringToHash(s)));
            m_ClipIndex = 0;
            m_FaceIndex = 0;
            ResetCamera();
            PlayCurrent();
        }

        private void ResetCamera()
        {
            m_Yaw = 200f;
            m_Pitch = 12f;
            m_Distance = m_ShowCat ? 2.6f : 1.4f;
        }

        private void PlayCurrent()
        {
            Animator a = Current;
            if (a == null || m_Clips.Count == 0) return;
            m_EndHold = -1f;
            m_Frame = 0;
            a.Play(m_Clips[m_ClipIndex], 0, 0f);
            a.Update(0f);
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            Animator a = Current;
            if (kb == null || a == null) return;

            if (kb.tabKey.wasPressedThisFrame) { SelectCharacter(!m_ShowCat); return; }
            if (kb.rightArrowKey.wasPressedThisFrame) { m_ClipIndex = AnimReviewLogic.Wrap(m_ClipIndex + 1, m_Clips.Count); PlayCurrent(); }
            if (kb.leftArrowKey.wasPressedThisFrame) { m_ClipIndex = AnimReviewLogic.Wrap(m_ClipIndex - 1, m_Clips.Count); PlayCurrent(); }
            if (kb.spaceKey.wasPressedThisFrame) m_Paused = !m_Paused;
            if (kb.lKey.wasPressedThisFrame) m_Loop = !m_Loop;
            if (kb.rightBracketKey.wasPressedThisFrame) m_SpeedIndex = Mathf.Min(m_SpeedIndex + 1, AnimReviewLogic.SPEEDS.Length - 1);
            if (kb.leftBracketKey.wasPressedThisFrame) m_SpeedIndex = Mathf.Max(m_SpeedIndex - 1, 0);
            if (kb.rKey.wasPressedThisFrame) ResetCamera();
            if (kb.fKey.wasPressedThisFrame) NextFace(a);
            if (kb.gKey.wasPressedThisFrame) ToggleGun();

            int last = LastFrameOfCurrent(a);
            if (m_Paused)
            {
                a.speed = 0f;
                int step = (kb.periodKey.wasPressedThisFrame ? 1 : 0) - (kb.commaKey.wasPressedThisFrame ? 1 : 0);
                if (step != 0)
                {
                    m_Frame = Mathf.Clamp(m_Frame + step, 0, last);
                    a.Play(m_Clips[m_ClipIndex], 0, AnimReviewLogic.NormalizedOf(m_Frame, last));
                    a.Update(0f);
                }
            }
            else
            {
                a.speed = AnimReviewLogic.SPEEDS[m_SpeedIndex];
                float t = a.GetCurrentAnimatorStateInfo(0).normalizedTime;
                m_Frame = AnimReviewLogic.FrameAt(Mathf.Min(t, 1f), last);
                if (m_Loop && t >= 1f)
                {
                    if (m_EndHold < 0f) m_EndHold = Time.time;
                    else if (Time.time - m_EndHold >= REPLAY_PAUSE_SECONDS / a.speed) PlayCurrent();
                }
            }
        }

        private void LateUpdate()
        {
            Animator a = Current;
            if (m_Camera == null || a == null) return;

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    m_Yaw += d.x * ORBIT_SPEED;
                    m_Pitch = Mathf.Clamp(m_Pitch - d.y * ORBIT_SPEED, -10f, 80f);
                }
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) m_Distance = Mathf.Clamp(m_Distance * (1f - wheel * ZOOM_STEP), 0.4f, 8f);
            }

            float height = m_ShowCat ? 0.5f : 0.25f;   // 몸통 높이쯤을 본다
            Vector3 focus = a.transform.position + Vector3.up * height;
            Quaternion rot = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
            m_Camera.transform.SetPositionAndRotation(focus + rot * new Vector3(0f, 0f, -m_Distance), rot);
        }

        private int LastFrameOfCurrent(Animator _a)
        {
            if (m_Clips.Count == 0 || _a.runtimeAnimatorController == null) return 10;
            string name = m_Clips[m_ClipIndex];
            foreach (AnimationClip clip in _a.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == name) return AnimReviewLogic.LastFrame(clip.length);
            }
            return 10;
        }

        private void NextFace(Animator _a)
        {
            int layer = _a.GetLayerIndex(FaceExpressionLogic.LAYER_NAME);
            if (layer < 0) return;
            m_FaceIndex = AnimReviewLogic.Wrap(m_FaceIndex + 1, FaceExpressionLogic.Used.Length);
            _a.CrossFadeInFixedTime(FaceExpressionLogic.Used[m_FaceIndex], 0.08f, layer);
        }

        private void ToggleGun()
        {
            if (m_Mouse == null) return;
            m_GunVisible = !m_GunVisible;
            foreach (Transform t in m_Mouse.GetComponentsInChildren<Transform>(true))
            {
                // 든 총만 켠다 — 수납된 총(MouseGunHolstered)까지 켜면 두 자루가 동시에 보인다.
                if (t.name == "MouseGun") t.gameObject.SetActive(m_GunVisible);
            }
        }

        private void OnGUI()
        {
            if (m_Clips.Count == 0) return;
            string clip = m_Clips[m_ClipIndex];
            string kind = AnimReviewLogic.IsSelfMade(clip) ? "[자작]" : "[팩]";
            string status =
                $"{(m_ShowCat ? "고양이" : "쥐")}   {m_ClipIndex + 1}/{m_Clips.Count}   {kind} {clip}\n" +
                $"프레임 {m_Frame}/{LastFrameOfCurrent(Current)}   배속 {AnimReviewLogic.SPEEDS[m_SpeedIndex]:0.##}x   " +
                $"{(m_Paused ? "멈춤" : "재생")}   반복 {(m_Loop ? "켬" : "끔")}   표정 {FaceExpressionLogic.Used[m_FaceIndex]}";

            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(10, 10, 620, 190), GUIContent.none);
            GUI.Label(new Rect(20, 16, 600, 60), status, style);
            var help = new GUIStyle(style) { fontSize = 14 };
            GUI.Label(new Rect(20, 82, 600, 120), HELP, help);
        }
    }
}
#endif
