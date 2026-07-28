using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 창고(v3.4) — 세운 쥐에게 초당 치즈를 생산한다. 서버만 계산·지급.
    /// 치즈 지점(CheeseSpot)이 "서 있어야 버는" 능동 수입이라면 이쪽은 거점 수입이다.
    /// 위치가 드러난 자산이라 고양이가 부수면 끊긴다 — 그래서 '숨어서 버는' 수입은 아니다.
    /// </summary>
    public class WarehouseProduction : NetworkBehaviour
    {
        // 소유 쥐의 클라이언트 ID. 스폰 직후 서버가 설정한다(NGO 오너십 대신 명시 필드 —
        // 구조물은 소유자 권위 이동이 없어 오너십을 줄 이유가 없다).
        private readonly NetworkVariable<ulong> m_OwnerClientId =
            new NetworkVariable<ulong>(ulong.MaxValue);

        [SerializeField] private ParticleSystem m_ProduceVfx;

        private float m_Accumulated;
        private CheeseWallet m_Wallet;

        public ulong OwnerMouseClientId => m_OwnerClientId.Value;

        /// <summary>연출용 — 생산이 몇 번 일어났는지(QA 검증에 쓴다).</summary>
        public int TickCount { get; private set; }

        /// <summary>서버 전용 — Spawn 직후 호출.</summary>
        public void ServerSetOwner(ulong _clientId)
        {
            if (!IsServer) return;
            m_OwnerClientId.Value = _clientId;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer) return;

            CheeseWallet wallet = ResolveWallet();
            if (wallet == null) { m_Accumulated = 0f; return; }

            m_Accumulated += ShopLogic.WAREHOUSE_INCOME_PER_SECOND * Time.deltaTime;
            if (m_Accumulated < ShopLogic.WAREHOUSE_TICK_CHEESE) return;

            m_Accumulated -= ShopLogic.WAREHOUSE_TICK_CHEESE;
            wallet.AddCheese(ShopLogic.WAREHOUSE_TICK_CHEESE);
            ProduceFxRpc();
        }

        // 생산 순간을 전원에게 — 소유 쥐에겐 "벌고 있다"는 피드백, 고양이에겐 창고를 찾는 단서.
        [Rpc(SendTo.Everyone)]
        private void ProduceFxRpc()
        {
            TickCount++;
            Audio.SfxPlayer.PlayAt(Audio.SfxId.WarehouseTick, transform.position,
                _maxDistance: ShopLogic.WAREHOUSE_SOUND_RANGE_METERS);
            if (m_ProduceVfx != null) m_ProduceVfx.Play();
        }

        // 소유 쥐가 죽으면 지갑도 사라진다 — 캐시를 매번 유효성 검사한다.
        private CheeseWallet ResolveWallet()
        {
            if (m_Wallet != null && m_Wallet.IsSpawned) return m_Wallet;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || m_OwnerClientId.Value == ulong.MaxValue) return null;
            if (!nm.ConnectedClients.TryGetValue(m_OwnerClientId.Value, out NetworkClient client)) return null;

            NetworkObject player = client.PlayerObject;
            m_Wallet = player != null ? player.GetComponent<CheeseWallet>() : null;
            return m_Wallet;
        }

        /// <summary>이 쥐가 이미 세운 창고 수 — 1개 제한(ShopLogic) 검증용. 서버에서만 의미 있다.</summary>
        public static int CountOwnedBy(ulong _clientId)
        {
            int count = 0;
            foreach (WarehouseProduction w in FindObjectsByType<WarehouseProduction>(FindObjectsSortMode.None))
            {
                if (w.IsSpawned && w.m_OwnerClientId.Value == _clientId) count++;
            }
            return count;
        }
    }
}
