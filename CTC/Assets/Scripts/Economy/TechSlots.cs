using System.Collections.Generic;
using CTC.Runtime.Core;
using R3;
using Unity.Netcode;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 플레이어가 이번 판에 산 테크와 레벨 (테크 v3, 2026-09-07). 서버만 쓰고 전원 읽는다.
    /// MVC의 Model — View(ShopHotbar)는 <see cref="Changed"/>를 구독한다.
    /// 되팔기·교체가 없으므로 지우는 API가 없다. 판이 끝나면 프리팹째 사라진다.
    /// </summary>
    public class TechSlots : NetworkBehaviour
    {
        private NetworkList<TechEntry> m_Entries;
        private readonly List<TechEntry> m_Snapshot = new List<TechEntry>();

        /// <summary>보유 목록 스냅샷(구매 순 = 칸 순). 호출마다 내부 버퍼를 다시 채운다 — 순수 로직에 넘길 때 쓴다. 보관하지 말 것.</summary>
        public IReadOnlyList<TechEntry> Owned
        {
            get
            {
                m_Snapshot.Clear();
                for (int i = 0; i < m_Entries.Count; i++) m_Snapshot.Add(m_Entries[i]);
                return m_Snapshot;
            }
        }

        public int Count => m_Entries.Count;

        /// <summary>변경 스트림 — 구독 즉시 한 번 흐른다(현재 상태를 그리라고).</summary>
        public Observable<Unit> Changed => m_Entries.AsChangedObservable().Prepend(() => Unit.Default);

        public int LevelOf(TechId _id) => TechLogic.LevelOf(Owned, _id);

        private void Awake()
        {
            // NetworkList는 필드 초기화 대신 Awake에서 생성 (NGO 권장 — 도메인 리로드 누수 방지)
            m_Entries = new NetworkList<TechEntry>();
        }

        /// <summary>서버 전용(ShopController). 미보유면 Lv1로 추가, 보유면 레벨 +1. 검증은 호출부가 끝냈다.</summary>
        public void ServerApplyNextLevel(TechId _id)
        {
            if (!IsServer) return;
            for (int i = 0; i < m_Entries.Count; i++)
            {
                if (m_Entries[i].Id != _id) continue;
                m_Entries[i] = new TechEntry(_id, m_Entries[i].Level + 1);
                return;
            }
            m_Entries.Add(new TechEntry(_id, 1));
        }
    }
}
