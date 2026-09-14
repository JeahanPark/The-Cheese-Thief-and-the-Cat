using System.Collections.Generic;
using CTC.Runtime.Combat;
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
        // 스폰된 창고 명부 — ShopSpot.s_All과 같은 이유다. 미니맵이 0.2초마다 "내 창고 어디"를
        // 물으므로 그때마다 씬을 뒤지지 않는다. **서버·클라 양쪽이 채운다**(클라도 그려야 한다).
        private static readonly List<WarehouseProduction> s_All = new List<WarehouseProduction>();

        /// <summary>스폰돼 서 있는 창고 전부(읽기 전용). 명부에 있다는 것이 곧 스폰됐다는 뜻이다.</summary>
        public static IReadOnlyList<WarehouseProduction> All => s_All;

        // 소유 쥐의 클라이언트 ID. 스폰 직후 서버가 설정한다(NGO 오너십 대신 명시 필드 —
        // 구조물은 소유자 권위 이동이 없어 오너십을 줄 이유가 없다).
        private readonly NetworkVariable<ulong> m_OwnerClientId =
            new NetworkVariable<ulong>(ulong.MaxValue);

        // 테크 레벨과 설치비를 **세운 순간에 박아 둔다**(테크 v3 계획 2a).
        // 소유 쥐가 나중에 창고/숨은 창고를 더 키워도 이미 서 있는 창고는 지어질 때의 값으로 산다:
        // 수입·소리·환급이 판 도중에 조용히 바뀌면 고양이가 들은 소리와 실제가 어긋난다.
        // 클라도 읽는다 — 틱 소리 반경은 각자 로컬에서 계산한다.
        private readonly NetworkVariable<int> m_WarehouseLevel = new NetworkVariable<int>(1);
        private readonly NetworkVariable<int> m_HiddenLevel = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> m_PlaceCost = new NetworkVariable<int>(0);

        [SerializeField] private ParticleSystem m_ProduceVfx;

        private float m_Accumulated;
        private CheeseWallet m_Wallet;
        private Health m_Health;

        public ulong OwnerMouseClientId => m_OwnerClientId.Value;

        /// <summary>연출용 — 생산이 몇 번 일어났는지(QA 검증에 쓴다).</summary>
        public int TickCount { get; private set; }

        /// <summary>서버 전용 — Spawn 직후 호출.</summary>
        public void ServerSetOwner(ulong _clientId)
        {
            if (!IsServer) return;
            m_OwnerClientId.Value = _clientId;
        }

        /// <summary>
        /// 서버 전용 — **Spawn 전에** 호출한다. 지어질 때의 테크 레벨과 낸 설치비를 새긴다.
        /// (Health.ServerSetMaxHp과 같은 이유로 스폰 전이다 — 스폰 순간의 값이 그대로 복제된다.)
        /// </summary>
        public void ServerSetTech(int _warehouseLevel, int _hiddenLevel, int _placeCost)
        {
            m_WarehouseLevel.Value = _warehouseLevel;
            m_HiddenLevel.Value = _hiddenLevel;
            m_PlaceCost.Value = _placeCost;
        }

        public override void OnNetworkSpawn()
        {
            s_All.Add(this);
            if (!IsServer) return;

            // 파괴 환급(숨은 창고 Lv3) — 부서지는 순간 서버에서만 판정한다.
            m_Health = GetComponent<Health>();
            if (m_Health != null) m_Health.Died += OnDied;
        }

        public override void OnNetworkDespawn()
        {
            s_All.Remove(this);
            if (m_Health != null) m_Health.Died -= OnDied;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer) return;

            CheeseWallet wallet = ResolveWallet();
            if (wallet == null) { m_Accumulated = 0f; return; }

            m_Accumulated += ShopLogic.WAREHOUSE_INCOME_PER_SECOND * Time.deltaTime;
            if (m_Accumulated < ShopLogic.WAREHOUSE_TICK_CHEESE) return;

            m_Accumulated -= ShopLogic.WAREHOUSE_TICK_CHEESE;
            wallet.AddCheese(WarehouseLogic.TickCheese(ShopLogic.WAREHOUSE_TICK_CHEESE, m_WarehouseLevel.Value));
            ProduceFxRpc();
        }

        // 생산 순간을 전원에게 — 소유 쥐에겐 "벌고 있다"는 피드백, 고양이에겐 창고를 찾는 단서.
        [Rpc(SendTo.Everyone)]
        private void ProduceFxRpc()
        {
            TickCount++;

            // 숨은 창고 Lv3은 반경 0 = 무음. 0m로 재생하면 바로 옆에서는 들리므로 **아예 건너뛴다**.
            float range = WarehouseLogic.SoundRange(ShopLogic.WAREHOUSE_SOUND_RANGE_METERS, m_HiddenLevel.Value);
            if (range > 0f)
            {
                Audio.SfxPlayer.PlayAt(Audio.SfxId.WarehouseTick, transform.position, _maxDistance: range);
                // 입자도 같은 조건이다 — 소리만 끄고 치즈를 뿜으면 "숨은" 창고가 눈으로 들킨다(2026-09-14 감사에서 발견).
                if (m_ProduceVfx != null) m_ProduceVfx.Play();
            }
        }

        // 부서지면 설치비 일부를 소유 쥐에게 돌려준다 — "숨은 창고"의 마지막 보험.
        // 지갑을 찾는 길은 틱 지급과 같다(ResolveWallet): 소유자가 이미 나갔으면 줄 곳이 없다.
        private void OnDied(Health _health)
        {
            if (!IsServer) return;

            // 값을 **맨 위에서 먼저 뜬다** — 같은 Died에 물린 PlayerDeath가 먼저 돌면 이 창고가
            // 이미 despawn된 뒤라 NetworkVariable을 못 읽는다. 컴포넌트 순서에 기대지 않는다.
            int placeCost = m_PlaceCost.Value;
            int hiddenLevel = m_HiddenLevel.Value;
            ulong ownerId = m_OwnerClientId.Value;

            int refund = WarehouseLogic.Refund(placeCost, hiddenLevel);
            if (refund <= 0) return;

            CheeseWallet wallet = ResolveWallet();
            if (wallet == null)
            {
                // 환급이 "안 일어난 것"과 "일어났는데 줄 곳이 없어 사라진 것"은 다르다 — 남긴다.
                Core.GameDebug.LogWarning(
                    $"[Warehouse] {ownerId} 지갑을 못 찾아 환급 {refund}을 버렸다 (나갔거나 죽은 뒤 despawn).");
                return;
            }

            // 환급은 **번 게 아니라 되돌려 받은 것**이다 — 누적 획득(전직 문턱)에 안 섞는다.
            wallet.RefundCheese(refund);
            Core.GameDebug.Log($"[Warehouse] {ownerId} 창고 파괴 환급 +{refund} (숨은 창고 Lv{hiddenLevel})");
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

        /// <summary>이 쥐가 이미 세운 창고 수 — 상한(WarehouseLogic.LimitOf) 검증용. 서버에서만 의미 있다.</summary>
        public static int CountOwnedBy(ulong _clientId)
        {
            int count = 0;
            foreach (WarehouseProduction w in s_All)
            {
                if (w.m_OwnerClientId.Value == _clientId) count++;
            }
            return count;
        }
    }
}
