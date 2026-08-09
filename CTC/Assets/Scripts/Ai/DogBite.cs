using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// 강아지의 물기 (v3.8 5번). 표적 선정은 `DogBrain`이 끝내 놓았고 여기는 때리기만 한다.
    ///
    /// **피해만 역할을 본다.** 쥐 HP 100 · 고양이 HP 1000이라 한 값으로 물면 한쪽만 죽는다.
    /// 구조물은 `RoleTag.Mouse`라 쥐 값을 받는다 — 규칙을 하나로 유지한다.
    ///
    /// 죽인 뒤 처리는 손대지 않는다. 쥐는 `PlayerDeath`가 감옥으로 보내고, 요새는 그냥 부서진다 —
    /// 강아지가 특별 대우를 받으면 "누가 죽였나"에 따라 규칙이 갈라진다.
    /// </summary>
    public class DogBite : NetworkBehaviour
    {
        private DogBrain m_Brain;
        private float m_Cooldown;

        private void Awake()
        {
            m_Brain = GetComponent<DogBrain>();
        }

        private void Update()
        {
            if (!IsServer || m_Brain == null) return;

            m_Cooldown = DogAiLogic.TickCooldown(m_Cooldown, Time.deltaTime);
            if (m_Brain.State != DogState.Bite || m_Cooldown > 0f) return;

            Health target = m_Brain.Target;
            if (target == null || target.IsDead) return;

            m_Cooldown = DogAiLogic.BiteCooldown;

            RoleTag tag = target.GetComponent<RoleTag>();
            target.TakeDamage(DogAiLogic.BiteDamage(tag != null && tag.Role == RoleId.Cat));

            BiteFxRpc();
        }

        // 물기 모션 지속 — 덮치기(0.6)와 같은 급으로 둔다. AC_Dog에도 Attack 상태가 있다.
        private const float ATTACK_ANIM_SECONDS = 0.6f;

        [Rpc(SendTo.Everyone)]
        private void BiteFxRpc()
        {
            Audio.SfxPlayer.PlayAt(Audio.SfxId.DogBite, transform.position);

            Player.PlayerAnimationDriver driver = GetComponent<Player.PlayerAnimationDriver>();
            if (driver != null)
            {
                driver.PlayOneShot(Player.AnimationStateLogic.Attack, ATTACK_ANIM_SECONDS);
            }
        }
    }
}
