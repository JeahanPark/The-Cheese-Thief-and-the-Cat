using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 매치 시작에 상자를 레이아웃대로 세우는 Worker (v3.9). `AlcoveSpawner`와 같은 취급 —
    /// `Health`를 가진 NetworkObject라 서버가 스폰해야 하고, `MapBuilder`는 전 클라가
    /// 각자 짓는 로컬 작업이라 여기에 못 낀다.
    /// </summary>
    public class LootBoxSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject m_BoxPrefab;

        private readonly List<NetworkObject> m_Spawned = new List<NetworkObject>();

        /// <summary>서버 전용. 이전 판의 상자를 걷어내고 새로 세운다.</summary>
        public void ServerSpawnAll(MapLayout _layout)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            ServerDespawnAll();
            if (_layout == null || m_BoxPrefab == null) return;

            foreach (BoxPlacement b in _layout.Boxes)
            {
                var pos = new Vector3(b.Pos.x, 0f, b.Pos.y);
                GameObject go = Instantiate(m_BoxPrefab, pos, Quaternion.Euler(0f, b.YawDeg, 0f));

                // 내용물은 자리마다 다르다 — 상자는 그릇이고, 무엇이 드는지는 배치가 정한다
                var box = go.GetComponent<LootBox>();
                if (box != null) box.ServerSetContents(b.Contents);

                var no = go.GetComponent<NetworkObject>();
                no.Spawn(true);
                m_Spawned.Add(no);
            }
        }

        /// <summary>서버 전용. 로비 복귀 시 판의 흔적을 지운다.</summary>
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
