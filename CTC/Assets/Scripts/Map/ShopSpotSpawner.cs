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

            var centers = new List<Vector2>(_layout.Rooms.Count);
            foreach (RoomRect room in _layout.Rooms) centers.Add(room.Center);

            Info.BalanceInfo b = Info.InfoManager.Instance.Balance;
            List<Vector2> picked = ShopSpotLogic.Pick(centers, b.ShopSpotCount, b.ShopSpotMinGap, _layout.Seed);

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
