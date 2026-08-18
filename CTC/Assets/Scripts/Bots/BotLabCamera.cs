#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 실험실 자유 카메라(2026-08-18). Main Camera에 직접 붙는다 — 시네머신도 궤도 카메라도 없다.
    ///
    /// BotLab 씬에는 `OrbitCameraController`를 **일부러 안 만든다**. 봇 몸은 호스트에서 IsOwner라
    /// `PlayerCameraBinder`가 스폰될 때마다 궤도 카메라를 자기에게 가져가는데, 그러면 마지막에
    /// 태어난 봇의 뒤통수만 보게 된다. 컨트롤러가 없으면 바인더는 경고 한 줄만 남기고 아무 일도 안 한다.
    ///
    /// 조작: WASD 평면 이동 · Q/E 높이 · 휠 확대 · 우클릭 드래그 회전 · Tab 봇 따라가기(순환) · F 자유.
    /// 사람 입력은 `BotLab`이 `UiFocus.SetModalOpen(true)`로 막아 두므로 이 키가 봇에 안 샌다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BotLabCamera : MonoBehaviour
    {
        private const float MOVE_SPEED = 18f;
        private const float FAST_MULTIPLIER = 3f;
        private const float ZOOM_SPEED = 6f;
        private const float LOOK_SPEED = 0.15f;
        private const float FOLLOW_HEIGHT = 14f;
        private const float FOLLOW_LAG = 8f;

        [SerializeField] private Vector3 m_StartPosition = new Vector3(0f, 30f, -18f);
        [SerializeField] private Vector3 m_StartEuler = new Vector3(58f, 0f, 0f);

        private int m_FollowIndex = -1;   // -1 = 자유 카메라
        private float m_Yaw;
        private float m_Pitch;

        private void Start()
        {
            transform.SetPositionAndRotation(m_StartPosition, Quaternion.Euler(m_StartEuler));
            m_Pitch = m_StartEuler.x;
            m_Yaw = m_StartEuler.y;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb == null) return;

            if (kb.tabKey.wasPressedThisFrame) CycleFollow();
            if (kb.fKey.wasPressedThisFrame) m_FollowIndex = -1;

            // 시간이 멈춰도(F6) 카메라는 움직여야 한다 — 그래서 unscaled
            float dt = Time.unscaledDeltaTime;

            if (m_FollowIndex >= 0 && TryFollowTarget(out Vector3 target))
            {
                Vector3 want = target + Quaternion.Euler(m_Pitch, m_Yaw, 0f) * new Vector3(0f, 0f, -FOLLOW_HEIGHT);
                transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-FOLLOW_LAG * dt));
                transform.rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
                return;
            }

            m_FollowIndex = -1;

            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                m_Yaw += delta.x * LOOK_SPEED;
                m_Pitch = Mathf.Clamp(m_Pitch - delta.y * LOOK_SPEED, -89f, 89f);
                transform.rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
            }

            float speed = MOVE_SPEED * (kb.leftShiftKey.isPressed ? FAST_MULTIPLIER : 1f);
            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += Flat(transform.forward);
            if (kb.sKey.isPressed) move -= Flat(transform.forward);
            if (kb.dKey.isPressed) move += Flat(transform.right);
            if (kb.aKey.isPressed) move -= Flat(transform.right);
            if (kb.eKey.isPressed) move += Vector3.up;
            if (kb.qKey.isPressed) move += Vector3.down;
            if (move.sqrMagnitude > 0.0001f) transform.position += move.normalized * (speed * dt);

            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    transform.position += transform.forward * (Mathf.Sign(wheel) * ZOOM_SPEED);
                }
            }
        }

        private void CycleFollow()
        {
            TestBotManager bots = TestBotManager.Instance;
            int count = bots != null ? bots.Count : 0;
            if (count == 0)
            {
                m_FollowIndex = -1;
                return;
            }
            m_FollowIndex = (m_FollowIndex + 1) % count;
        }

        private bool TryFollowTarget(out Vector3 _pos)
        {
            _pos = Vector3.zero;
            TestBotManager bots = TestBotManager.Instance;
            if (bots == null || m_FollowIndex >= bots.Count) return false;

            BotAgent agent = bots.Agents[m_FollowIndex];
            if (agent == null || !agent.Alive) return false;

            _pos = agent.Position;
            return true;
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v.sqrMagnitude < 0.0001f ? Vector3.zero : _v.normalized;
        }
    }
}
#endif
