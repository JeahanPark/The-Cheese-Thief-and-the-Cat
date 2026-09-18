using CTC.Runtime.Core;
using R3;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 전직 스킬 (v3.9). **전부 액티브다** — Q로 쓰고 쿨다운이 돈다.
    ///
    /// 여기는 "켜졌다/꺼졌다"만 복제한다. 실제 효과는 각자 이 값을 보고 판단한다:
    /// - 불·바람은 `CatPounce`·`PlayerMovement`가 배율을 곱한다
    /// - 빛·어둠은 보는 쪽(쥐 화면)이 그린다 — 서버가 화면을 그릴 수는 없다
    ///
    /// **켜질 때 소리가 난다.** 이 게임은 큰 효과에 항상 소리가 붙는다 —
    /// 쥐가 "지금 뭔가 켜졌다"를 알아야 숨거나 흩어질 수 있고, 그게 액티브로 만든 이유다.
    /// </summary>
    [RequireComponent(typeof(CatClass))]
    public class CatSkill : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> m_Active = new NetworkVariable<bool>();
        // 서버 시각(NetworkManager.ServerTime) 기준 시각 두 개. 소유자 HUD(능력 바)가 남은 초를 스스로
        // 계산한다 — 서버 float였을 때는 클라 고양이에게 쿨다운이 항상 0으로 보였다(2026-08-17).
        private readonly NetworkVariable<double> m_ReadyAt = new NetworkVariable<double>();
        private readonly NetworkVariable<double> m_ActiveUntil = new NetworkVariable<double>();

        /// <summary>켤 때 나는 소리 — C-21 테스트가 광분 소리(<see cref="CatFrenzy.USE_SFX"/>)와 비교한다.</summary>
        public const Audio.SfxId USE_SFX = Audio.SfxId.CatSkill;

        private CatClass m_Class;
        private PlayerAnimationDriver m_AnimationDriver; // 쓸 때마다 찾지 않는다

        // 전직 스킬 동작(자작, 2026-09-13) — 예전엔 몸이 아무것도 안 했다. 켜졌다를 알리는 한 번짜리.
        // 전직마다 클립이 다르다(AnimationStateLogic.CatSkillFor).
        private const float SKILL_ANIM_SECONDS = 0.5f;

        /// <summary>지금 스킬이 켜져 있는가(전원 읽기 — 쥐 화면 효과가 이걸 본다).</summary>
        public bool Active => m_Active.Value;

        public Observable<bool> ActiveChanged => m_Active.AsObservableWithCurrent();

        /// <summary>남은 쿨다운(초) — 전 클라에서 같은 값(서버 시각 기준).</summary>
        public float CooldownRemaining => RemainingUntil(m_ReadyAt.Value);

        public float CooldownTotal => CatClassLogic.SkillCooldown;

        /// <summary>남은 지속(초). 꺼져 있으면 0.</summary>
        public float ActiveRemaining => m_Active.Value ? RemainingUntil(m_ActiveUntil.Value) : 0f;

        public float ActiveTotal => CatClassLogic.SkillSeconds;

        private float RemainingUntil(double _serverTime)
        {
            if (!IsSpawned) return 0f;
            return Mathf.Max(0f, (float)(_serverTime - NetworkManager.ServerTime.Time));
        }

        private void Awake()
        {
            m_Class = GetComponent<CatClass>();
            m_AnimationDriver = GetComponent<PlayerAnimationDriver>();
        }

        private void Update()
        {
            if (IsServer) TickServer();
            if (LocalControl.Mine(this)) ReadInput();
        }

        private void TickServer()
        {
            if (m_Active.Value && NetworkManager.ServerTime.Time >= m_ActiveUntil.Value)
            {
                m_Active.Value = false;
            }
        }

        private void ReadInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.qKey.wasPressedThisFrame) return;

            PressSkillKey();
        }

        /// <summary>
        /// Q 한 번 — **사람이 누르는 그 길**. 키를 읽는 곳과 가드를 나눈 이유는 QA다
        /// (<c>ShopHotbar.PressSlot</c>과 같은 규약): <see cref="DevUse"/>는 가드를 건너뛰므로
        /// 「채팅 중에도 스킬이 나간다」 같은 사고를 초록으로 통과시킨다.
        /// <c>false</c> = 가드에 막혔다(입력창 포커스·커서 미잠금).
        /// </summary>
        public bool PressSkillKey()
        {
            if (UiFocus.BlocksGameplay) return false;   // 채팅 타이핑·옵션 창 중에는 조작하지 않는다
            if (Cursor.lockState != CursorLockMode.Locked) return false;
            if (ActionLock.Locked(this)) return false;   // 용사 칼에 굳었다(2026-09-19)
            UseRpc();
            return true;
        }

        /// <summary>QA 전용 — 키도 가드도 없이 쓴다. 입력 계약을 재는 스위트는 <see cref="PressSkillKey"/>를 쓴다.</summary>
        public void DevUse() => UseRpc();

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void UseRpc()
        {
            if (ActionLock.Locked(this)) return;   // 굳은 고양이는 스킬도 못 쓴다(서버가 보는 값)
            double now = NetworkManager.ServerTime.Time;
            float cooldown = (float)System.Math.Max(0.0, m_ReadyAt.Value - now);
            if (!CatClassLogic.CanUseSkill(m_Class.Value, cooldown, m_Active.Value)) return;

            m_Active.Value = true;
            m_ActiveUntil.Value = now + CatClassLogic.SkillSeconds;
            m_ReadyAt.Value = now + CatClassLogic.SkillCooldown;

            SkillFxRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void SkillFxRpc()
        {
            // 위치가 실린 3D 소리다 — "어디서 켜졌나"까지 알려 줘야 도망칠 방향이 생긴다.
            Audio.SfxPlayer.PlayAt(USE_SFX, transform.position, 1f,
                Info.InfoManager.Instance.Balance.CatSkillSoundMeters);

            // 소리와 같은 이유로 모션도 전 클라다(CatFrenzy.FrenzyFxRpc와 같은 규칙).
            // **전직마다 동작이 다르다**(2026-09-13 사용자 결정) — 전직은 복제값(CatClass)이라 받는 쪽이 제 화면에서 고른다.
            string clip = m_Class != null ? AnimationStateLogic.CatSkillFor(m_Class.Value) : null;

            // 발동 한 번만 터진다(2026-09-14) — 켜져 있는 동안 도는 오라는 "아직 켜져 있다"를 흘린다(기획서 §11).
            Fx.FxId? fx = m_Class != null ? Fx.FxCatalogLogic.SkillFor(m_Class.Value) : null;
            if (fx.HasValue) Fx.FxPlayer.PlayAt(fx.Value, transform.position);
            if (m_AnimationDriver != null && clip != null)
            {
                m_AnimationDriver.PlayOneShot(clip, SKILL_ANIM_SECONDS);
            }
        }
    }
}
