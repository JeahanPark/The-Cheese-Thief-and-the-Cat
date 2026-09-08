using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 판 시작에 상점 자리를 세우는 Worker (테크 v3). `LootBoxSpawner`와 같은 취급 —
    /// NetworkObject라 서버가 스폰한다. 자리는 <see cref="ShopSpotLogic.Pick"/>.
    /// </summary>
    public class ShopSpotSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject m_SpotPrefab;

        private readonly List<NetworkObject> m_Spawned = new List<NetworkObject>();

        /// <summary>서버 전용. 이전 판 것을 걷어내고 새로 세운다.</summary>
        public void ServerSpawnAll(MapLayout _layout)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            ServerDespawnAll();
            if (_layout == null || m_SpotPrefab == null) return;

            var placed = new List<Vector2>(_layout.Shops.Count);
            foreach (ShopPlacement shop in _layout.Shops) placed.Add(shop.Pos);

            // 손 배치 맵은 레벨디자이너 자리 그대로, 절차 맵은 방 중심 규칙 — ShopSpotLogic.Resolve.
            // 방 **중심만**이 아니라 방 사각형째로 넘긴다: 원반이 들어가는 방을 골라야
            // 상점 안에서 설치가 막히지 않는다(2026-09-08).
            Info.BalanceInfo b = Info.InfoManager.Instance.Balance;
            List<Vector2> picked = ShopSpotLogic.Resolve(_layout.HandPlaced, placed, _layout.Rooms,
                b.ShopSpotCount, b.ShopSpotMinGap, b.ShopSpotRadius, _layout.Seed);

            foreach (Vector2 p in picked)
            {
                GameObject go = Instantiate(m_SpotPrefab, new Vector3(p.x, 0f, p.y), Quaternion.identity);
                var no = go.GetComponent<NetworkObject>();
                no.Spawn(true);
                m_Spawned.Add(no);
            }
            Core.GameDebug.Log($"[ShopSpotSpawner] 상점 {picked.Count}곳 (요청 {b.ShopSpotCount})");
        }

        /// <summary>서버 전용. 로비 복귀 시 걷는다.</summary>
        public void ServerDespawnAll()
        {
            foreach (NetworkObject no in m_Spawned)
            {
                if (no != null && no.IsSpawned) no.Despawn(true);
            }
            m_Spawned.Clear();
        }
    }
}
