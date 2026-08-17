using CTC.Runtime.Combat;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// 강아지가 쓰러지면 사라진다 (v3.8 5번). 시간 제한이 없으므로 **이게 유일한 퇴장**이다.
    ///
    /// `PlayerDeath`를 쓰지 않는 이유: 그건 플레이어 규칙(쥐 탈락)이라 제3세력에 맞지 않는다.
    ///
    /// 쓰러지는 소리는 `HitFeedback`이 HP 0을 보고 전 클라에서 각자 낸다
    /// (원샷은 임시 오브젝트로 재생돼 despawn 뒤에도 끝까지 들린다 — 벽감과 같은 취급).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DogDeath : NetworkBehaviour
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
            NetworkObject.Despawn(true);
        }
    }
}
