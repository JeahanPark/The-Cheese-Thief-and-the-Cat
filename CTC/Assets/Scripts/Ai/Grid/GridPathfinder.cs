using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// 격자 A\* (2026-08-18) — 기술소개서의 세 가지를 원형대로 옮긴 것.
    ///
    /// ① **대각선 검사**(프리즌 아키텍트 53p): 대각으로 가려면 **양옆 두 칸이 다 뚫려 있어야** 한다.
    ///    안 그러면 벽 모서리를 대각으로 뚫고 지나간다.
    /// ② **직선 검사**(리그오브레전드 14p): A\*가 낸 셀 사슬을 뒤에서부터 훑어, 직선으로 이어지면
    ///    중간 노드를 버린다. 남는 것은 **꺾이는 곳뿐**이라 셀 중심을 잇는 계단이 사라진다.
    /// ③ **노드 예약**(앨리스 19~20p): 남이 지나가기로 한 셀은 비용이 오른다 → 저절로 흩어진다.
    ///
    /// 그리고 CTC가 더한 것 하나 — **부술 수 있는 셀**. 두 번 탐색한다:
    /// 1차는 부술 셀을 벽으로 보고, 길이 있으면 그대로 쓴다(우회가 가능하다는 뜻).
    /// 1차가 실패했을 때만 2차에서 부술 셀을 **비싼 통과 가능 셀**로 열어 준다 —
    /// 그렇게 나온 경로에 낀 부술 셀이 곧 "여기를 부숴라"다. 비용 상수를 튜닝할 필요가 없다:
    /// 우회 가능 여부가 1차/2차로 이미 갈렸다.
    ///
    /// 배열은 한 번 잡고 재사용한다(대형 맵 240×240 = 57,600칸). 방문 표시는 세대 번호라 매번 안 지운다.
    /// </summary>
    public class GridPathfinder
    {
        /// <summary>한 번 탐색에서 열어 볼 노드 상한 — 넘으면 실패로 본다(프레임을 지키는 안전판).</summary>
        public const int MAX_NODES = 8000;

        /// <summary>부수고 지나가는 셀의 추가 비용(셀 단위). 우회로가 조금이라도 있으면 안 뚫게 크게 잡는다.</summary>
        public const float BREAK_COST = 60f;

        /// <summary>남이 지나가기로 한 셀 하나당 더해지는 비용. 1.0이면 "한 칸 돌아가는 값".</summary>
        public const float RESERVE_COST = 1.5f;

        private const float DIAGONAL = 1.41421356f;

        private readonly NavGrid m_Grid;
        private readonly float[] m_G;
        private readonly int[] m_Parent;
        private readonly int[] m_Stamp;
        private readonly byte[] m_State;      // 0 = 안 봤다 · 1 = 열림 · 2 = 닫힘
        private readonly int[] m_Heap;
        private readonly float[] m_HeapKey;
        private readonly List<int> m_Cells = new List<int>(256);

        private int m_Generation;
        private int m_HeapCount;

        /// <summary>마지막 탐색에서 열어 본 노드 수 — 진단용.</summary>
        public int LastNodes { get; private set; }

        /// <summary>마지막 경로가 무언가를 부수고 지나가나(2차 탐색이 답을 냈나).</summary>
        public bool LastNeededBreaking { get; private set; }

        public GridPathfinder(NavGrid _grid)
        {
            m_Grid = _grid;
            int n = _grid.CellCount;
            m_G = new float[n];
            m_Parent = new int[n];
            m_Stamp = new int[n];
            m_State = new byte[n];
            m_Heap = new int[n + 1];
            m_HeapKey = new float[n + 1];
        }

        /// <summary>
        /// 길을 찾는다. 성공하면 `_outCells`에 **꺾임점만** 담긴다(출발 셀 제외, 도착 셀 포함).
        /// 1차(안 부수고) → 실패하면 2차(부수고)를 자동으로 돈다.
        /// </summary>
        public bool TryFindPath(Vector3 _from, Vector3 _to, NavAgent _agent, float _bodyRadius,
            NavCellReservations _reservations, ulong _asking, List<int> _outCells)
        {
            LastNeededBreaking = false;
            _outCells.Clear();
            if (m_Grid == null) return false;

            m_Grid.ToCell(_from, out int sx, out int sz);
            m_Grid.ToCell(_to, out int gx, out int gz);
            int start = m_Grid.Index(sx, sz);
            int goal = m_Grid.Index(gx, gz);

            // 출발 셀이 막혀 있을 수 있다(요새가 발밑에 서거나 벽에 붙어 선 경우) — 가장 가까운
            // 갈 수 있는 셀에서 시작한다. 안 그러면 첫 확장부터 아무 데도 못 간다.
            if (!m_Grid.CanEnter(sx, sz, _agent, _bodyRadius, BREAK_COST, out _))
            {
                if (!TryFindNearestOpen(sx, sz, _agent, _bodyRadius, false, out start)) return false;
            }

            // 도착 셀이 아예 못 가는 곳이면 **그 옆**으로 옮긴다. 폴리곤 스냅과 달리 한두 칸이라
            // "가라는 데"에서 거의 안 멀어진다(NavMesh는 평균 4.9m를 당겼다).
            //
            // 벽에 붙은 칸이라도 **옮기지 않는다** — 옮기면 목표가 2m씩 밀려 쥐가 치즈에 못 닿는다.
            // 대신 도착점을 **셀 중심**으로 둔다(NavRoute가 엄격 판정일 때만 원본 좌표로 바꾼다):
            // 셀 중심은 벽 면에서 최소 반 칸 떨어져 있어 몸이 밀려나도 도착 반경 안에 든다.
            // 원본 좌표를 그대로 쓰면 벽 속을 향해 밀며 영영 도착 못 한다(실측: 고양이 60초에 2m).
            if (!m_Grid.CanEnter(gx, gz, _agent, _bodyRadius, 0f, out _))
            {
                if (!TryFindNearestOpen(gx, gz, _agent, _bodyRadius, true, out goal)
                    && !TryFindNearestOpen(gx, gz, _agent, _bodyRadius, false, out goal))
                {
                    return false;
                }
            }

            if (Search(start, goal, _agent, _bodyRadius, 0f, _reservations, _asking))
            {
                BuildPath(start, goal, _agent, _bodyRadius, false, _outCells);
                return true;
            }

            // 1차 실패 = 안 부수고는 못 간다. 부술 수 있는 것을 열어 다시 본다.
            if (!Search(start, goal, _agent, _bodyRadius, BREAK_COST, _reservations, _asking)) return false;

            LastNeededBreaking = true;
            BuildPath(start, goal, _agent, _bodyRadius, true, _outCells);
            return true;
        }

        // ---- A* ----

        private bool Search(int _start, int _goal, NavAgent _agent, float _radius, float _breakCost,
            NavCellReservations _reservations, ulong _asking)
        {
            m_Generation++;
            m_HeapCount = 0;
            LastNodes = 0;

            int width = m_Grid.Width;
            int gx = _goal % width, gz = _goal / width;

            Touch(_start);
            m_G[_start] = 0f;
            m_Parent[_start] = -1;
            m_State[_start] = 1;
            HeapPush(_start, Heuristic(_start % width, _start / width, gx, gz));

            while (m_HeapCount > 0)
            {
                int current = HeapPop();
                if (m_State[current] == 2) continue;
                m_State[current] = 2;
                LastNodes++;

                if (current == _goal) return true;
                if (LastNodes >= MAX_NODES) return false;

                int cx = current % width, cz = current / width;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;

                        int nx = cx + dx, nz = cz + dz;
                        if (!m_Grid.CanEnter(nx, nz, _agent, _radius, _breakCost, out float extra)) continue;

                        bool diagonal = dx != 0 && dz != 0;
                        if (diagonal)
                        {
                            // **대각선 검사**(53p) — 양옆이 다 뚫려야 대각으로 간다.
                            // 안 그러면 벽 모서리를 사선으로 통과한다.
                            if (!m_Grid.CanEnter(cx + dx, cz, _agent, _radius, _breakCost, out _)) continue;
                            if (!m_Grid.CanEnter(cx, cz + dz, _agent, _radius, _breakCost, out _)) continue;
                        }

                        int next = m_Grid.Index(nx, nz);
                        Touch(next);
                        if (m_State[next] == 2) continue;

                        float step = diagonal ? DIAGONAL : 1f;
                        if (_reservations != null) step += RESERVE_COST * _reservations.CountAt(next);
                        float g = m_G[current] + step + extra;

                        if (m_State[next] == 1 && g >= m_G[next]) continue;

                        m_G[next] = g;
                        m_Parent[next] = current;
                        m_State[next] = 1;
                        HeapPush(next, g + Heuristic(nx, nz, gx, gz));
                    }
                }
            }
            return false;
        }

        // 옥타일 거리 — 8방향 격자에서 실제로 갈 수 있는 최단이라 절대 과대평가하지 않는다.
        private static float Heuristic(int _x, int _z, int _gx, int _gz)
        {
            int dx = Mathf.Abs(_x - _gx);
            int dz = Mathf.Abs(_z - _gz);
            return Mathf.Max(dx, dz) + (DIAGONAL - 1f) * Mathf.Min(dx, dz);
        }

        // 세대 번호로 "이번 탐색에서 처음 보는 칸"을 가린다 — 매번 배열을 안 지워도 된다.
        private void Touch(int _cell)
        {
            if (m_Stamp[_cell] == m_Generation) return;
            m_Stamp[_cell] = m_Generation;
            m_State[_cell] = 0;
            m_G[_cell] = float.MaxValue;
            m_Parent[_cell] = -1;
        }

        // ---- 경로 정리 ----

        private void BuildPath(int _start, int _goal, NavAgent _agent, float _radius, bool _breaking,
            List<int> _outCells)
        {
            m_Cells.Clear();
            for (int cell = _goal; cell != -1 && cell != _start; cell = m_Parent[cell])
            {
                m_Cells.Add(cell);
            }
            m_Cells.Reverse();
            if (m_Cells.Count == 0) return;

            // **직선 검사**(14p) — 직선으로 이어지는 구간의 중간 노드를 버린다.
            // 부숴야 하는 셀은 못 버린다: 거기서 멈춰 때려야 하므로 반드시 꺾임점으로 남긴다.
            int anchorCell = _start;
            int i = 0;
            while (i < m_Cells.Count)
            {
                if (_breaking && m_Grid.NeedsBreaking(m_Cells[i] % m_Grid.Width, m_Cells[i] / m_Grid.Width, _agent))
                {
                    _outCells.Add(m_Cells[i]);
                    anchorCell = m_Cells[i];
                    i++;
                    continue;
                }

                int best = i;
                for (int j = m_Cells.Count - 1; j > i; j--)
                {
                    if (_breaking && SegmentTouchesBreakable(anchorCell, m_Cells[j], _agent)) continue;
                    if (!LineClear(anchorCell, m_Cells[j], _agent, _radius)) continue;
                    best = j;
                    break;
                }

                _outCells.Add(m_Cells[best]);
                anchorCell = m_Cells[best];
                i = best + 1;
            }
        }

        /// <summary>두 셀을 잇는 직선이 지나는 모든 칸이 (안 부수고) 갈 수 있는가.</summary>
        public bool LineClear(int _fromCell, int _toCell, NavAgent _agent, float _radius)
        {
            int width = m_Grid.Width;
            Vector3 a = m_Grid.ToWorld(_fromCell % width, _fromCell / width);
            Vector3 b = m_Grid.ToWorld(_toCell % width, _toCell / width);
            return TraverseClear(a, b, _agent, _radius);
        }

        private bool SegmentTouchesBreakable(int _fromCell, int _toCell, NavAgent _agent)
        {
            int width = m_Grid.Width;
            Vector3 a = m_Grid.ToWorld(_fromCell % width, _fromCell / width);
            Vector3 b = m_Grid.ToWorld(_toCell % width, _toCell / width);
            return Traverse(a, b, (x, z) => m_Grid.NeedsBreaking(x, z, _agent));
        }

        // 직선 검사는 **A\*와 같은 통행 기준**을 쓴다(2026-08-18 실측 뒤 결정).
        // 엄격 판정으로 바꿨더니 폭 1.6m 복도의 거의 모든 칸이 거부돼 코너가 하나도 안 지워졌다
        // (9m 경로에 코너 51개 — 0.5m 간격이라 코너 도달 반경 0.8m보다 촘촘해 봇이 제자리에서 떨었다).
        // A*가 이미 지나가도 된다고 판단한 칸이면 그 위의 직선도 같은 기준으로 봐야 한다 —
        // 벽에 붙는 것은 막는 게 아니라 TIGHT_COST로 피하게 한다.
        private bool TraverseClear(Vector3 _a, Vector3 _b, NavAgent _agent, float _radius)
        {
            return !Traverse(_a, _b, (x, z) => !m_Grid.CanEnter(x, z, _agent, _radius, 0f, out _));
        }

        /// <summary>
        /// 선분이 지나는 칸을 하나도 빠뜨리지 않고 훑는다(Amanatides–Woo). `_hit`이 참을 내면 즉시 끝낸다.
        /// 셀 중심을 잇는 계단을 지우려면 "이 직선이 벽을 스치나"를 정확히 알아야 한다 —
        /// 표본 몇 개로 때우면 모서리를 지나쳐 벽에 낀다.
        /// </summary>
        private bool Traverse(Vector3 _a, Vector3 _b, System.Func<int, int, bool> _hit)
        {
            m_Grid.ToCell(_a, out int x, out int z);
            m_Grid.ToCell(_b, out int ex, out int ez);
            if (_hit(x, z)) return true;

            float dx = _b.x - _a.x;
            float dz = _b.z - _a.z;
            int stepX = dx > 0f ? 1 : -1;
            int stepZ = dz > 0f ? 1 : -1;

            float cell = NavGrid.CELL_SIZE;
            float originX = m_Grid.Origin.x, originZ = m_Grid.Origin.y;

            float tMaxX = Mathf.Abs(dx) < 1e-6f ? float.MaxValue
                : ((x + (stepX > 0 ? 1 : 0)) * cell + originX - _a.x) / dx;
            float tMaxZ = Mathf.Abs(dz) < 1e-6f ? float.MaxValue
                : ((z + (stepZ > 0 ? 1 : 0)) * cell + originZ - _a.z) / dz;
            float tDeltaX = Mathf.Abs(dx) < 1e-6f ? float.MaxValue : cell / Mathf.Abs(dx);
            float tDeltaZ = Mathf.Abs(dz) < 1e-6f ? float.MaxValue : cell / Mathf.Abs(dz);

            int guard = m_Grid.Width + m_Grid.Height + 4;
            while ((x != ex || z != ez) && guard-- > 0)
            {
                if (tMaxX < tMaxZ)
                {
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
                if (_hit(x, z)) return true;
            }
            return false;
        }

        // 격자가 막힌 자리에서 시작·끝날 때 가장 가까운 갈 수 있는 칸을 찾는다(나선형으로 넓혀 간다).
        // `_strict`면 **몸이 완전히 들어가는 칸**만 고른다 — 도착점에 쓴다.
        private bool TryFindNearestOpen(int _cx, int _cz, NavAgent _agent, float _radius, bool _strict,
            out int _cell)
        {
            const int MAX_RING = 12;   // 6m — 이보다 멀면 "거기로 가라"가 아니게 된다
            for (int ring = 1; ring <= MAX_RING; ring++)
            {
                for (int dz = -ring; dz <= ring; dz++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;
                        int x = _cx + dx, z = _cz + dz;
                        if (!m_Grid.CanEnter(x, z, _agent, _radius, 0f, out _, _strict)) continue;
                        _cell = m_Grid.Index(x, z);
                        return true;
                    }
                }
            }
            _cell = -1;
            return false;
        }

        // ---- 이진 힙 ----

        private void HeapPush(int _cell, float _key)
        {
            int i = ++m_HeapCount;
            m_Heap[i] = _cell;
            m_HeapKey[i] = _key;
            while (i > 1)
            {
                int parent = i >> 1;
                if (m_HeapKey[parent] <= m_HeapKey[i]) break;
                Swap(parent, i);
                i = parent;
            }
        }

        private int HeapPop()
        {
            int top = m_Heap[1];
            m_Heap[1] = m_Heap[m_HeapCount];
            m_HeapKey[1] = m_HeapKey[m_HeapCount];
            m_HeapCount--;

            int i = 1;
            while (true)
            {
                int left = i << 1, right = left + 1, best = i;
                if (left <= m_HeapCount && m_HeapKey[left] < m_HeapKey[best]) best = left;
                if (right <= m_HeapCount && m_HeapKey[right] < m_HeapKey[best]) best = right;
                if (best == i) break;
                Swap(best, i);
                i = best;
            }
            return top;
        }

        private void Swap(int _a, int _b)
        {
            (m_Heap[_a], m_Heap[_b]) = (m_Heap[_b], m_Heap[_a]);
            (m_HeapKey[_a], m_HeapKey[_b]) = (m_HeapKey[_b], m_HeapKey[_a]);
        }
    }
}
