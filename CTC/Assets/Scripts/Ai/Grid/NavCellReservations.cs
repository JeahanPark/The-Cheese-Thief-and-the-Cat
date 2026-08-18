using System.Collections.Generic;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// **A\*가 지나갈 노드를 예약한다** — 기술소개서(앨리스 19~20p) 예약 시스템의 원형(2026-08-18).
    ///
    /// 원본: 몬스터 A가 A\*로 길을 찾으면 그 경로의 노드를 예약하고, 다음 몬스터 B의 A\*에서
    /// 그 노드의 비용을 올린다 — 넓은 지형에서는 분산해서 접근하고 미로에서는 다른 길로 돌아온다.
    ///
    /// CTC는 길찾기가 유니티 폴리곤이던 동안 이걸 못 해서, `ReservationLogic`이 **목표를 고르는 점수**로
    /// 흉내 냈다(같은 소리를 둘이 안 쫓게는 되지만, 같은 복도에 줄을 서는 건 못 막았다).
    /// 격자로 갈아탄 뒤에는 원본 그대로 노드 비용에 건다. 목표 점수 쪽(`ReservationLogic`)은 그대로 둔다 —
    /// 둘은 층이 다르다(무엇을 향해 갈까 / 어느 길로 갈까).
    ///
    /// **협동이 아니다.** 서로 신호를 주고받지 않고 각자 제 비용만 보고 고른다.
    /// </summary>
    public class NavCellReservations
    {
        private readonly Dictionary<int, int> m_Counts = new Dictionary<int, int>();
        private readonly Dictionary<ulong, List<int>> m_ByOwner = new Dictionary<ulong, List<int>>();

        /// <summary>예약된 셀 수 — 진단용.</summary>
        public int CellCount => m_Counts.Count;

        /// <summary>이 소유자의 예약을 통째로 갈아 끼운다(길을 새로 짤 때마다).</summary>
        public void Claim(ulong _owner, IReadOnlyList<int> _cells)
        {
            Release(_owner);
            if (_cells == null || _cells.Count == 0) return;

            var mine = new List<int>(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                int cell = _cells[i];
                m_Counts.TryGetValue(cell, out int n);
                m_Counts[cell] = n + 1;
                mine.Add(cell);
            }
            m_ByOwner[_owner] = mine;
        }

        /// <summary>이 소유자가 잡아 둔 셀을 놓는다(길을 버리거나 죽었을 때).</summary>
        public void Release(ulong _owner)
        {
            if (!m_ByOwner.TryGetValue(_owner, out List<int> mine)) return;

            foreach (int cell in mine)
            {
                if (!m_Counts.TryGetValue(cell, out int n)) continue;
                if (n <= 1) m_Counts.Remove(cell);
                else m_Counts[cell] = n - 1;
            }
            m_ByOwner.Remove(_owner);
        }

        public void Clear()
        {
            m_Counts.Clear();
            m_ByOwner.Clear();
        }

        /// <summary>
        /// 이 셀을 몇 명이 지나가기로 했나. **소유자를 구분하지 않는다** —
        /// 부르는 쪽(`NavRoute.ReplanCore`)이 길을 새로 짜기 전에 제 예약을 먼저 놓는다.
        /// 안 그러면 자기가 직전에 잡은 셀에 자기가 비용을 물어 제 길을 피해 간다(2026-08-18 리뷰).
        /// </summary>
        public int CountAt(int _cell) => m_Counts.TryGetValue(_cell, out int n) ? n : 0;
    }
}
