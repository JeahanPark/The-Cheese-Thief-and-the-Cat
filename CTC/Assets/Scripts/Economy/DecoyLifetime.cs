using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>미끼 수명: 서버가 30초 뒤 자동 despawn (소모품).</summary>
    public class DecoyLifetime : NetworkBehaviour
    {
        private float m_Age;

        private void Update()
        {
            if (!IsServer) return;

            m_Age += Time.deltaTime;
            if (m_Age < ShopLogic.DECOY_LIFETIME_SECONDS) return;
            if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        }
    }
}
