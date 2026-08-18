using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// 지금 판에서 쓰는 격자 한 벌(2026-08-18) — 격자·탐색기·노드 예약.
    ///
    /// 정적으로 두는 이유: 길찾기(`NavRoute`)는 봇의 몸과 강아지가 각자 들고 있고 맵을 모른다.
    /// 예전 `NavMesh`도 같은 성격의 전역이었다 — 자리를 그대로 물려받는다.
    ///
    /// **한 벌만 있다.** 맵을 다시 지으면 통째로 갈린다(`MapBuilder.Build`).
    /// </summary>
    public static class NavGridService
    {
        /// <summary>지금 격자. 맵을 짓기 전에는 null이다.</summary>
        public static NavGrid Grid { get; private set; }

        /// <summary>격자 탐색기 — 봇·강아지가 공유한다(순차 호출이라 재진입이 없다).</summary>
        public static GridPathfinder Pathfinder { get; private set; }

        /// <summary>노드 예약(앨리스 19~20p) — A*가 지나갈 셀을 잡아 서로 흩어지게 한다.</summary>
        public static NavCellReservations Reservations { get; } = new NavCellReservations();

        public static bool Ready => Grid != null && Pathfinder != null;

        /// <summary>맵을 새로 지었다 — 격자를 갈아 끼운다.</summary>
        public static void Publish(NavGrid _grid)
        {
            Grid = _grid;
            Pathfinder = _grid != null ? new GridPathfinder(_grid) : null;
            Reservations.Clear();
        }

        /// <summary>플레이가 끝났다·맵이 사라졌다.</summary>
        public static void Clear() => Publish(null);
    }

    /// <summary>
    /// 판 중에 생기는 구조물이 격자에 남긴 자국. 스폰할 때 찍고 사라질 때 지운다 —
    /// **찍은 셀 목록을 그대로 들고 있다가 되돌린다**(요새는 겹쳐 설 수 있어 참조 카운트가 필요하다).
    /// </summary>
    public class NavGridStamp
    {
        private readonly List<(int cx, int cz, NavStructureKind kind)> m_Cells = new();

        public int CellCount => m_Cells.Count;

        public void Add(int _cx, int _cz, NavStructureKind _kind)
        {
            NavGrid grid = NavGridService.Grid;
            if (grid == null) return;
            grid.AddStructureCell(_cx, _cz, _kind);
            m_Cells.Add((_cx, _cz, _kind));
        }

        public void Undo()
        {
            NavGrid grid = NavGridService.Grid;
            if (grid != null)
            {
                foreach ((int cx, int cz, NavStructureKind kind) in m_Cells)
                {
                    grid.RemoveStructureCell(cx, cz, kind);
                }
            }
            m_Cells.Clear();
        }
    }
}
