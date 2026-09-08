using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 매치 시작에 벽감을 레이아웃대로 세우는 Worker (v3.8, 2026-08-09).
    ///
    /// `MapBuilder`가 짓지 않는 이유: 벽감은 `Health`를 가진 **NetworkObject**라 서버가
    /// 스폰해야 하는데, `MapBuilder`는 전 클라가 각자 지오메트리를 짓는 로컬 작업이다.
    /// 요새·창고와 같은 취급 — 다만 저건 쥐가 사고, 이건 판이 시작할 때 맵이 깐다.
    ///
    /// 치즈 지점(`CheeseSpot`)처럼 로컬 오브젝트 + 상태 복제로 갈 수도 있었지만,
    /// 그러면 피격·파괴 판정을 새로 짜야 한다. NetworkObject로 두면 팝건·덮치기가 그냥 붙는다.
    /// </summary>
    public class AlcoveSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject m_AlcovePrefab;

        private readonly List<NetworkObject> m_Spawned = new List<NetworkObject>();

        /// <summary>서버 전용. 이전 판의 벽감을 걷어내고 새로 세운다.</summary>
        public void ServerSpawnAll(MapLayout _layout)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            ServerDespawnAll();
            if (_layout == null || m_AlcovePrefab == null) return;

            IReadOnlyList<AlcovePlacement> placements = _layout.Alcoves;
            // 벽 뒤 내용물만 판마다 섞는다 — 좌표·회전·페이로드(강아지 벽감)는 그대로다.
            // 강아지가 든 벽이 판마다 옮겨 다니면 낑낑거리는 소리 단서가 거짓말이 된다(계획 3 Task 4).
            int[] contentOrder = TicketPermutation(placements.Count);

            for (int i = 0; i < placements.Count; i++)
            {
                AlcovePlacement a = placements[i];
                var pos = new Vector3(a.Pos.x, 0f, a.Pos.y);
                GameObject go = Instantiate(m_AlcovePrefab, pos, Quaternion.Euler(0f, a.YawDeg, 0f));

                // 페이로드는 자리마다 다르다 — 프리팹 기본값(치즈)을 레이아웃이 덮는다.
                // 레이아웃도 같이 준다: 강아지 벽감이 열리면 강아지가 이 맵에서 길을 찾아야 한다.
                var alcove = go.GetComponent<Alcove>();
                if (alcove != null)
                {
                    alcove.ServerSetPayload(a.Payload);
                    // 벽 뒤 상자가 품을 것 — **통째로 교환**한다(직업권은 두 벽에만 있다).
                    // 항목을 만들거나 지우지 않으므로 맵 총 치즈량이 정의상 안 변한다.
                    alcove.ServerSetBoxContents(placements[contentOrder[i]].BoxContents);
                    alcove.ServerSetLayout(_layout);
                }

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
