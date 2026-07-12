using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 대시(상점 구매 영구 능력). 소유자가 Shift를 누르면 몸 전방으로 짧게 돌진.
    /// 이동은 소유자 권위(ClientNetworkTransform)라 CC.Move로 로컬 실행만 하면 동기화된다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MouseDash : NetworkBehaviour
    {
        private const float DASH_DISTANCE = 4f;
        private const float DASH_DURATION_SECONDS = 0.2f;
        private const float COOLDOWN_SECONDS = 5f;

        // 구매 여부. 서버 기록(ShopController), 소유자 읽기.
        private readonly NetworkVariable<bool> m_Owned = new NetworkVariable<bool>();

        private CharacterController m_Controller;
        private Vector3 m_DashDirection;
        private float m_DashRemainingSeconds;
        private double m_LastUseTime = -999.0;

        public bool Owned => m_Owned.Value;

        /// <summary>서버 전용(ShopController).</summary>
        public void ServerSetOwned()
        {
            if (IsServer) m_Owned.Value = true;
        }

        private void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (m_DashRemainingSeconds > 0f)
            {
                float dt = Mathf.Min(Time.deltaTime, m_DashRemainingSeconds);
                m_Controller.Move(m_DashDirection * (DASH_DISTANCE / DASH_DURATION_SECONDS) * dt);
                m_DashRemainingSeconds -= Time.deltaTime;
                return;
            }

            if (!m_Owned.Value) return;

            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.leftShiftKey.wasPressedThisFrame) return;
            if (!Combat.CombatLogic.IsCooldownReady(m_LastUseTime, Time.timeAsDouble, COOLDOWN_SECONDS)) return;

            m_LastUseTime = Time.timeAsDouble;
            m_DashDirection = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            m_DashRemainingSeconds = DASH_DURATION_SECONDS;
        }
    }
}
