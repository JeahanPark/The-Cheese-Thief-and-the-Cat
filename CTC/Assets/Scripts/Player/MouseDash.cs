using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 대시(상점 구매 영구 능력). 소유자가 Space를 누르면 몸 전방으로 짧게 돌진.
    /// 이동은 소유자 권위(ClientNetworkTransform)라 CC.Move로 로컬 실행만 하면 동기화된다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MouseDash : NetworkBehaviour
    {
        // 밸런스 원본은 BalanceInfo.csv — 재컴파일 없이 다음 플레이에 반영된다.
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;
        private static float DASH_DISTANCE => Balance.DashDistance;
        private static float DASH_DURATION_SECONDS => Balance.DashDurationSeconds;

        // 구매 여부. 서버 기록(ShopController), 소유자 읽기.
        private readonly NetworkVariable<bool> m_Owned = new NetworkVariable<bool>();

        // 대시 구르기 모션 지속(이동 애니메이션 잠금 시간) — 대시 자체(0.2초대)보다 조금 길게
        // 잡아 착지까지 보이게 한다. 클립은 팩의 Roll을 그대로 쓴다(새 클립 0).
        private const float ROLL_ANIM_SECONDS = 0.45f;

        private CharacterController m_Controller;
        private TechSlots m_TechSlots;      // Awake에서 한 번 — 쿨은 매 프레임 읽힌다
        private PlayerAnimationDriver m_AnimationDriver; // 매 대시마다 찾지 않는다
        private Vector3 m_DashDirection;
        private float m_DashRemainingSeconds;
        private double m_LastUseTime = -999.0;

        public bool Owned => m_Owned.Value;

        /// <summary>
        /// 쿨다운은 대시 레벨이 정한다(TechInfo P1 — Lv1 6초 · Lv2 4.5 · Lv3 3.5).
        /// Lv1 값은 옛 BalanceInfo 값과 같아서 1레벨 감각은 그대로다.
        /// 0이면 미보유거나 표 사고 — BalanceInfo로 후퇴한다. 쿨이 0초가 되는 일은 없다.
        /// </summary>
        private float CooldownSeconds
        {
            get
            {
                int level = m_TechSlots != null ? m_TechSlots.LevelOf(TechId.Dash) : 0;
                float seconds = TechLogic.ParamOf(TechId.Dash, level, 1);
                return seconds > 0f ? seconds : Balance.DashCooldownSeconds;
            }
        }

        /// <summary>구매 상태 스트림(HUD 칩·배지용) — 구독 즉시 현재 값이 흐른다.</summary>
        public R3.Observable<bool> OwnedChanged => m_Owned.AsObservableWithCurrent();

        /// <summary>
        /// 남은 쿨다운(초) — 소유자 로컬 값(HUD 칩 표시용). `m_LastUseTime`이 소유자에게만
        /// 쌓이므로 다른 클라에서는 항상 0으로 보인다(칩은 자기 화면에만 있으니 충분하다).
        /// </summary>
        public float CooldownRemaining => !m_Owned.Value ? 0f
            : Mathf.Max(0f, (float)(CooldownSeconds - (Time.timeAsDouble - m_LastUseTime)));

        public float CooldownTotal => CooldownSeconds;

        /// <summary>대시 진행 중 남은 시간(초) — 능력 슬롯 '지속 중' 표시용.</summary>
        public float ActiveRemaining => Mathf.Max(0f, m_DashRemainingSeconds);

        public float ActiveTotal => DASH_DURATION_SECONDS;

        /// <summary>서버 전용(ShopController).</summary>
        public void ServerSetOwned()
        {
            if (IsServer) m_Owned.Value = true;
        }

        private void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_TechSlots = GetComponent<TechSlots>();
            m_AnimationDriver = GetComponentInChildren<PlayerAnimationDriver>();
        }

        private void Update()
        {
            // 대시 이동은 봇 몸에서도 돌아야 한다(서버가 조종) — 입력만 사람의 몸에서 읽는다(2026-09-06).
            if (!LocalControl.Drives(this)) return;

            if (m_DashRemainingSeconds > 0f)
            {
                float dt = Mathf.Min(Time.deltaTime, m_DashRemainingSeconds);
                m_Controller.Move(m_DashDirection * (DASH_DISTANCE / DASH_DURATION_SECONDS) * dt);
                m_DashRemainingSeconds -= Time.deltaTime;
                return;
            }

            if (!LocalControl.Mine(this)) return;
            if (!m_Owned.Value) return;

            if (Core.UiFocus.BlocksGameplay) return; // 채팅 타이핑·옵션 창 중에는 조작하지 않는다
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.spaceKey.wasPressedThisFrame) return;

            TryDash();
        }

        /// <summary>
        /// 봇·QA 전용 — 사람의 Space와 같은 입구. 쿨다운·소유 규칙이 전부 같아야
        /// 봇으로 잰 밸런스가 뜻을 갖는다(봇이 사람보다 자주 대시하면 그건 봇의 화력이다).
        /// </summary>
        public void DevDash() => TryDash();

        private void TryDash()
        {
            if (!IsOwner) return;
            if (m_DashRemainingSeconds > 0f) return;
            if (!m_Owned.Value) return;
            if (!Combat.CombatLogic.IsCooldownReady(m_LastUseTime, Time.timeAsDouble, CooldownSeconds)) return;

            m_LastUseTime = Time.timeAsDouble;
            m_DashDirection = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            m_DashRemainingSeconds = DASH_DURATION_SECONDS;
            DashSfxRpc();
        }

        // 대시는 소유자 권위(RPC 없음)라 소리·모션만 전 클라 전파 — 고양이도 듣고 보는 도주 신호.
        // **모션을 로컬에서 걸면 소유자 화면에서만 구른다**: 몸의 이동은 ClientNetworkTransform이
        // 옮겨 주지만 애니메이터 상태는 복제되지 않는다. 그래서 이미 있던 이 소리 Rpc에 얹는다.
        [Rpc(SendTo.Everyone)]
        private void DashSfxRpc()
        {
            Audio.SfxPlayer.PlayAt(Audio.SfxId.MouseDash, transform.position);

            if (m_AnimationDriver != null)
            {
                m_AnimationDriver.PlayOneShot(AnimationStateLogic.Roll, ROLL_ANIM_SECONDS);
            }
        }


    }
}
