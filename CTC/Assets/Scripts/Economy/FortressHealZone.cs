using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 쥐 요새(원작 벙커)의 회복 거점: 서버가 1초마다 반경 내 살아있는 쥐 플레이어를 회복.
    /// 요새 자체는 Health+RoleTag(Mouse)+PlayerDeath — 고양이가 부수면 despawn.
    /// </summary>
    public class FortressHealZone : NetworkBehaviour
    {
        private const float TICK_SECONDS = 1f;

        private float m_Timer;

        private void Update()
        {
            if (!IsServer) return;

            m_Timer += Time.deltaTime;
            if (m_Timer < TICK_SECONDS) return;
            m_Timer -= TICK_SECONDS;

            foreach (Health target in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (target.IsDead) continue;
                if (target.NetworkObject == null || !target.NetworkObject.IsPlayerObject) continue;

                RoleTag tag = target.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Mouse) continue;

                Vector3 to = target.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > ShopLogic.FORTRESS_HEAL_RADIUS * ShopLogic.FORTRESS_HEAL_RADIUS) continue;

                target.Heal(ShopLogic.FORTRESS_HEAL_PER_TICK);
            }
        }
    }
}
