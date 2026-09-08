using System.Collections.Generic;
using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 부술 수 있는 상자 (v3.9). **그릇이지 내용물이 아니다** — 안에 뭐가 들었는지 모르고,
    /// 부서질 때 목록을 훑어 각 항목의 드롭을 바닥에 뿌리기만 한다.
    /// 새 물건이 늘어도 이 클래스는 안 바뀐다(프리팹의 드롭 목록에 한 줄이 는다).
    ///
    /// 벽감과 역할이 다르다:
    /// - 벽감은 **경쟁 유발** — 벽면에 노출돼 있고 파괴음이 25m를 가서 다 같이 달려온다.
    /// - 상자는 **탐색 보상** — 벽 뒤·구석에 있고 소리가 거의 안 새서 찾은 사람이 조용히 먹는다.
    ///
    /// `PlayerDeath`를 안 쓰는 이유는 벽감과 같다 — 그건 죽자마자 despawn하는데,
    /// 상자는 **despawn 전에 안의 것을 꺼내야** 한다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class LootBox : NetworkBehaviour
    {
        // LootId 순서와 1:1. 배선은 프리팹 생성기가 한다 — 물건이 늘면 여기 한 칸이 는다.
        [SerializeField] private GameObject[] m_DropPrefabs;
        [SerializeField] private float m_ScatterMeters = 0.8f;

        private readonly List<LootEntry> m_Contents = new List<LootEntry>();
        private Health m_Health;

        /// <summary>서버 전용 — 스폰 직전에 배치가 내용물을 새긴다.</summary>
        public void ServerSetContents(IReadOnlyList<LootEntry> _contents)
        {
            m_Contents.Clear();
            if (_contents == null) return;
            for (int i = 0; i < _contents.Count; i++) m_Contents.Add(_contents[i]);
        }

        /// <summary>QA용 — 지금 든 물건 수.</summary>
        public int ContentCount => m_Contents.Count;

        /// <summary>서버 전용 — 이 상자가 품은 치즈(QA가 맵 총량이 안 변했는지 잴 때 쓴다).</summary>
        public int CheeseInside => LootBoxLogic.CheeseIn(m_Contents);

        /// <summary>서버 전용 — 이 상자에 그 물건이 들었나(QA가 직업권 자리를 셀 때 쓴다).</summary>
        public bool Contains(LootId _id) => LootBoxLogic.Contains(m_Contents, _id);

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
            for (int i = 0; i < m_Contents.Count; i++) SpawnEntry(m_Contents[i], i, m_Contents.Count);
            NetworkObject.Despawn(true);
        }

        private void SpawnEntry(LootEntry _entry, int _index, int _total)
        {
            GameObject prefab = PrefabFor(_entry.Id);
            if (prefab == null)
            {
                GameDebug.LogError($"[LootBox] {_entry.Id} 드롭 프리팹 미배선 — Setup Networking Scene 재실행 필요");
                return;
            }

            // 치즈만 여러 덩이로 쪼갠다 — 한 덩이면 연 사람이 독식해서 상자 앞 경쟁이 안 생긴다.
            // 다른 물건은 쪼갤 의미가 없으므로 하나로 둔다.
            int[] piles = _entry.Id == LootId.Cheese
                ? AlcoveLogic.SplitDrop(_entry.Amount, LootBoxLogic.DropCount)
                : new[] { _entry.Amount };

            for (int i = 0; i < piles.Length; i++)
            {
                Vector3 pos = ScatterPosition(_index * piles.Length + i, _total * piles.Length);
                GameObject go = Instantiate(prefab, pos, Quaternion.identity);

                var cheese = go.GetComponent<Economy.CheeseDrop>();
                if (cheese != null) cheese.ServerSetAmount(piles[i]);

                // 직업권은 "얼마나"가 없다 — 무엇인지만 새긴다.
                var ticket = go.GetComponent<JobTicketDrop>();
                if (ticket != null) ticket.ServerSetJob(JobOf(_entry.Id));

                go.GetComponent<NetworkObject>().Spawn(true);
            }
        }

        private static Match.MouseJobId JobOf(LootId _id) => _id switch
        {
            LootId.HeroTicket => Match.MouseJobId.Hero,
            LootId.GangTicket => Match.MouseJobId.Gang,
            _ => Match.MouseJobId.None,
        };

        private GameObject PrefabFor(LootId _id)
        {
            int index = (int)_id;
            if (m_DropPrefabs == null || index < 0 || index >= m_DropPrefabs.Length) return null;
            return m_DropPrefabs[index];
        }

        // 원형으로 고르게 편다 — 무작위가 아닌 이유는 서버만 스폰하므로 재현성이 QA에 유리해서다.
        private Vector3 ScatterPosition(int _index, int _count)
        {
            if (_count <= 1) return transform.position;

            float angle = Mathf.PI * 2f * _index / _count;
            return transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * m_ScatterMeters;
        }
    }
}
