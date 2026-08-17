using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 서버: 사망 처리. 쥐 = despawn(탈락·부활 없음, 원작 동일).
    /// 고양이 HP 0 = 로그만 (승패 판정은 Plan 7 매치 플로우).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerDeath : NetworkBehaviour
    {
        private Health m_Health;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            m_Health = GetComponent<Health>();
            m_Health.Died += OnDied;
        }

        public override void OnNetworkDespawn()
        {
            if (m_Health != null) m_Health.Died -= OnDied;
        }

        private void OnDied(Health _health)
        {
            RoleTag tag = GetComponent<RoleTag>();
            if (tag != null && tag.Role == RoleId.Cat)
            {
                Debug.Log($"[PlayerDeath] 고양이({OwnerClientId}) HP 0 — 승패 판정은 Plan 7");
                return;
            }

            // 쥐는 잡히면 **즉시 탈락**(관전). 감옥·구출(v3.8)은 2026-08-17에 뺐다 —
            // "죽으면 감옥으로 순간이동해 40초 버티고 동료가 구하러 온다"가 사람에게 안 읽혔다.
            Debug.Log($"[PlayerDeath] 쥐({OwnerClientId}) 탈락 — despawn");
            NetworkObject.Despawn(true);
        }
    }
}
