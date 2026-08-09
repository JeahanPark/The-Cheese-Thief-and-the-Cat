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

            // v3.8: 쥐는 죽지 않고 갇힌다 — 3분에 죽으면 남은 7분을 관전만 하던 문제.
            // 진짜 탈락은 감옥 체류가 만료될 때뿐이고, 그건 JailDirector가 처리한다.
            var jail = FindFirstObjectByType<Match.JailDirector>();
            if (jail != null && jail.ServerJail(NetworkObject)) return;

            // 감옥이 없는 맵(구 레이아웃)에서는 예전대로 탈락한다
            Debug.Log($"[PlayerDeath] 쥐({OwnerClientId}) 탈락 — 감옥 없음, despawn");
            NetworkObject.Despawn(true);
        }
    }
}
