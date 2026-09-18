// 개발 빌드에도 실린다(2026-08-31) — 빌드 E2E QA(wesqa)의 진입점이라서. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using CTC.Runtime.Ai;
using CTC.Runtime.Bots;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    /// <summary>
    /// TestManager의 일부 — **길찾기 격자**를 캐는 훅(2026-08-18).
    ///
    /// 길찾기를 NavMesh에서 자체 격자로 갈아끼우면서 만든 것들이다. 화면만 보고는
    /// "왜 저 봇이 저기서 멈췄나"를 못 밝힌다 — 격자를 글자로 떠 보면 벽 뒤에 갇힌 스폰,
    /// 끊긴 통로, 몸이 안 들어가는 틈이 한눈에 나온다(실제로 그렇게 세 건을 찾았다).
    /// </summary>
    public partial class TestManager
    {
        /// <summary>QA: 길찾기 품질 한 줄 — 갈아끼우기 전후를 같은 조건에서 비교한다.</summary>
        public string TestGetNavStats()
        {
            TestBotManager manager = TestBotManager.Instance;
            return manager != null ? manager.NavStats() : "";
        }

        /// <summary>QA: 아무 요새의 자리 — 실험실에는 로컬 플레이어가 없어 "가장 가까운"을 못 쓴다.</summary>
        public string TestGetAnyFortressPos()
        {
            var fort = FindFirstObjectByType<CTC.Runtime.Economy.FortressShield>();
            if (fort == null) return "";
            Vector3 p = fort.transform.position;
            return $"{p.x:F2},{p.z:F2},{fort.transform.rotation.eulerAngles.y:F0}";
        }

        /// <summary>QA: 격자 상태 한 줄 — 크기·막힌 비율·구조물 셀·예약 셀.</summary>
        public string TestGetNavGridInfo()
        {
            NavGrid grid = NavGridService.Grid;
            if (grid == null) return "격자 없음";

            int blocked = grid.BlockedCount;
            return $"w={grid.Width} h={grid.Height} cells={grid.CellCount} blocked={blocked} " +
                   $"({100f * blocked / grid.CellCount:F0}%) struct={grid.StructureCellCount} " +
                   $"reserved={NavGridService.Reservations.CellCount}";
        }

        /// <summary>QA: 두 점 사이 길찾기를 직접 돌려 본다 — "왜 못 가나"를 좌표로 캔다.</summary>
        public string TestTracePath(float _x1, float _z1, float _x2, float _z2, string _agent)
        {
            NavGrid grid = NavGridService.Grid;
            GridPathfinder finder = NavGridService.Pathfinder;
            if (grid == null || finder == null) return "격자 없음";

            NavAgent agent = _agent == "Cat" ? NavAgent.Cat : NavAgent.Mouse;
            float radius = agent == NavAgent.Cat ? 0.35f : 0.22f;
            var from = new Vector3(_x1, 0f, _z1);
            var to = new Vector3(_x2, 0f, _z2);

            grid.ToCell(from, out int sx, out int sz);
            grid.ToCell(to, out int gx, out int gz);
            bool startOk = grid.CanEnter(sx, sz, agent, radius, 0f, out _);
            bool goalOk = grid.CanEnter(gx, gz, agent, radius, 0f, out _, true);

            var cells = new List<int>();
            bool ok = finder.TryFindPath(from, to, agent, radius, null, cells);
            return $"start=({sx},{sz}) ok={startOk} clr={grid.ClearanceMeters(sx, sz):F2} " +
                   $"goal=({gx},{gz}) fit={goalOk} clr={grid.ClearanceMeters(gx, gz):F2} " +
                   $"path={ok} corners={cells.Count} nodes={finder.LastNodes} break={finder.LastNeededBreaking}";
        }

        /// <summary>
        /// QA: 격자를 글자 그림으로 뜬다 — 한 칸 0.5m, 위쪽이 +z.
        /// `#` 못 감 · `+` 몸이 스치는 좁은 칸 · `.` 여유 · `o` 부술 수 있는 구조물 · `d` 쥐 전용 문 · `C` 중심.
        /// </summary>
        public string TestDumpGrid(float _x, float _z, int _radiusCells, string _agent)
        {
            NavGrid grid = NavGridService.Grid;
            if (grid == null) return "격자 없음";

            NavAgent agent = _agent == "Mouse" ? NavAgent.Mouse : NavAgent.Cat;
            float radius = agent == NavAgent.Cat ? 0.35f : 0.22f;

            grid.ToCell(new Vector3(_x, 0f, _z), out int cx, out int cz);
            int r = Mathf.Clamp(_radiusCells, 2, 40);

            var sb = new StringBuilder();
            sb.AppendLine($"중심 셀 ({cx},{cz}) 월드 ({_x:F1},{_z:F1}) · {_agent} · 한 칸 0.5m · 위가 +z");

            for (int z = cz + r; z >= cz - r; z--)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    sb.Append(Glyph(grid, agent, radius, x, z, x == cx && z == cz));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static char Glyph(NavGrid _grid, NavAgent _agent, float _radius, int _x, int _z, bool _center)
        {
            if (!_grid.InBounds(_x, _z)) return ' ';
            if (_center) return 'C';

            NavCellStructure cell = _grid.StructureAt(_grid.Index(_x, _z));
            if (cell.Solid > 0) return 'x';            // 상점 부스 — 누구도 못 지난다
            if (cell.MouseDoor > 0) return 'd';        // 쥐만 지나는 문
            if (cell.CatBreakable > 0 || cell.AnyBreakable > 0) return 'o';

            bool loose = _grid.CanEnter(_x, _z, _agent, _radius, 0f, out _);
            if (!loose) return '#';
            return _grid.CanEnter(_x, _z, _agent, _radius, 0f, out _, true) ? '.' : '+';
        }
    }
}
#endif
