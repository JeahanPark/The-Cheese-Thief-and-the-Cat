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

            IReadOnlyList<BoxPlacement> placements = _layout.Boxes;
            // 직업권이 늘 0번·1번 상자에 있으면 "어느 상자를 깨나"가 외운 답이 된다(기획서 §7-3).
            // 좌표·회전은 그대로 두고 **내용물만 통째로 교환**한다 — 맵 총 치즈량이 안 변한다.
            int[] contentOrder = TicketPermutation(placements.Count);

            for (int i = 0; i < placements.Count; i++)
            {
                BoxPlacement b = placements[i];
                var pos = new Vector3(b.Pos.x, 0f, b.Pos.y);
                GameObject go = Instantiate(m_BoxPrefab, pos, Quaternion.Euler(0f, b.YawDeg, 0f));

                // 내용물은 자리마다 다르다 — 상자는 그릇이고, 무엇이 드는지는 배치가 정한다
                var box = go.GetComponent<LootBox>();
                if (box != null) box.ServerSetContents(placements[contentOrder[i]].Contents);

                var no = go.GetComponent<NetworkObject>();
                no.Spawn(true);
                m_Spawned.Add(no);
            }
        }

        /// <summary>
        /// 내용물을 섞을 순서(<c>i</c>번 자리 → <c>perm[i]</c>번 내용물). 매치 시드가 없으면(로비·봇 실험실)
        /// **항등**이라 지금까지와 똑같이 선다.
        /// </summary>
        private static int[] TicketPermutation(int _count)
        {
            Match.MatchRandom random = Match.MatchRandom.Current;
            if (random == null || random.Seed == 0)
            {
                var identity = new int[_count];
                for (int i = 0; i < _count; i++) identity[i] = i;
                return identity;
            }
            return TicketShuffleLogic.Permutation(
                _count, Match.MatchRandom.SubSeed(random.Seed, Match.MatchRandom.SALT_TICKET));
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
