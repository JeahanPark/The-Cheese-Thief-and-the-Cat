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
        // 밸런스 원본은 BalanceInfo.csv — 재컴파일 없이 다음 플레이에 반영된다.
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;
        private static float DASH_DISTANCE => Balance.DashDistance;
        private static float DASH_DURATION_SECONDS => Balance.DashDurationSeconds;
        private static float COOLDOWN_SECONDS => Balance.DashCooldownSeconds;

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

            if (Core.UiFocus.BlocksGameplay) return; // 채팅 타이핑·옵션 창 중에는 조작하지 않는다
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.leftShiftKey.wasPressedThisFrame) return;
            if (!Combat.CombatLogic.IsCooldownReady(m_LastUseTime, Time.timeAsDouble, COOLDOWN_SECONDS)) return;

            m_LastUseTime = Time.timeAsDouble;
            m_DashDirection = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            m_DashRemainingSeconds = DASH_DURATION_SECONDS;
            DashSfxRpc();
        }

        // 대시는 소유자 권위(RPC 없음)라 소리만 전 클라 전파 — 고양이도 들리는 도주 신호
        [Rpc(SendTo.Everyone)]
        private void DashSfxRpc()
        {
            Audio.SfxPlayer.PlayAt(Audio.SfxId.MouseDash, transform.position);
        }
    }
}
