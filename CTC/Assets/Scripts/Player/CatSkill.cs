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

        private CatClass m_Class;
        private float m_Remaining;      // 서버: 남은 지속
        private float m_Cooldown;       // 서버: 남은 쿨다운

        /// <summary>지금 스킬이 켜져 있는가(전원 읽기 — 쥐 화면 효과가 이걸 본다).</summary>
        public bool Active => m_Active.Value;

        public Observable<bool> ActiveChanged => m_Active.AsObservableWithCurrent();

        /// <summary>남은 쿨다운(소유자 HUD용). 서버 값이라 다른 클라에서는 0으로 보인다.</summary>
        public float CooldownRemaining => m_Cooldown;

        private void Awake()
        {
            m_Class = GetComponent<CatClass>();
        }

        private void Update()
        {
            if (IsServer) TickServer(Time.deltaTime);
            if (IsOwner) ReadInput();
        }

        private void TickServer(float _dt)
        {
            m_Cooldown = CatClassLogic.TickCooldown(m_Cooldown, _dt);

            if (!m_Active.Value) return;

            m_Remaining -= _dt;
            if (m_Remaining <= 0f) m_Active.Value = false;
        }

        private void ReadInput()
        {
            if (UiFocus.BlocksGameplay) return;   // 채팅 타이핑·옵션 창 중에는 조작하지 않는다
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.qKey.wasPressedThisFrame) return;

            UseRpc();
        }

        /// <summary>QA 전용 — 키 없이 쓴다.</summary>
        public void DevUse() => UseRpc();

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void UseRpc()
        {
            if (!CatClassLogic.CanUseSkill(m_Class.Value, m_Cooldown, m_Active.Value)) return;

            m_Active.Value = true;
            m_Remaining = CatClassLogic.SkillSeconds;
            m_Cooldown = CatClassLogic.SkillCooldown;

            SkillFxRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void SkillFxRpc()
        {
            // 위치가 실린 3D 소리다 — "어디서 켜졌나"까지 알려 줘야 도망칠 방향이 생긴다.
            Audio.SfxPlayer.PlayAt(Audio.SfxId.CatSkill, transform.position, 1f,
                Info.InfoManager.Instance.Balance.CatSkillSoundMeters);
        }
    }
}
