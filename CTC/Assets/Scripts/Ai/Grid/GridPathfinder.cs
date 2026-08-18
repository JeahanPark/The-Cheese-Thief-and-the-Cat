using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// 격자 A\* (2026-08-18) — 기술소개서의 세 가지를 원형대로 옮긴 것.
    ///
    /// ① **대각선 검사**(프리즌 아키텍트 53p): 대각으로 가려면 **양옆 두 칸이 다 뚫려 있어야** 한다.
    ///    안 그러면 벽 모서리를 대각으로 뚫고 지나간다.
    /// ② **직선 검사**(리그오브레전드 14p): A\*가 낸 셀 사슬을 훑어 직선으로 이어지면 중간 노드를 버린다.
    ///    남는 것은 **꺾이는 곳뿐**이라 셀 중심을 잇는 계단이 사라진다.
    /// ③ **노드 예약**(앨리스 19~20p): 남이 지나가기로 한 셀은 비용이 오른다 → 저절로 흩어진다.
    ///
    /// 그리고 CTC가 더한 것 하나 — **부술 수 있는 셀**. 두 번 탐색한다:
    /// 1차는 부술 셀을 벽으로 보고, 길이 있으면 그대로 쓴다(우회가 가능하다는 뜻).
    /// 1차가 실패했을 때만 2차에서 부술 셀을 **비싼 통과 가능 셀**로 열어 준다 —
    /// 그렇게 나온 경로에 낀 부술 셀이 곧 "여기를 부숴라"다. 비용 상수를 튜닝할 필요가 없다:
    /// 우회 가능 여부가 1차/2차로 이미 갈렸다.
    ///
    /// 배열은 한 번 잡고 재사용한다. 방문 표시는 세대 번호라 매번 안 지운다.
    /// </summary>
    public class GridPathfinder
    {
        /// <summary>노드 상한의 하한 — 작은 맵에서도 이만큼은 본다.</summary>
        public const int MIN_MAX_NODES = 8000;

        /// <summary>부수고 지나가는 셀의 추가 비용(셀 단위). 우회로가 조금이라도 있으면 안 뚫게 크게 잡는다.</summary>
        public const float BREAK_COST = 60f;

        /// <summary>남이 지나가기로 한 셀 하나당 더해지는 비용. 1.0이면 "한 칸 돌아가는 값".</summary>
        public const float RESERVE_COST = 1.5f;

        private const float DIAGONAL = 1.41421356f;
        private const float TIE_EPSILON = 1e-6f;

        /// <summary>선분이 지나는 칸을 무엇으로 판정할까 — 델리게이트를 안 쓴다(매 호출 힙 할당).</summary>
        private enum TraverseTest
        {
            /// <summary>안 부수고 못 지나가는 칸이 하나라도 있나.</summary>
            Blocked = 0,

            /// <summary>부숴야 지나가는 칸이 하나라도 있나.</summary>
            Breakable = 1,
        }

        private readonly NavGrid m_Grid;
        private readonly float[] m_G;
        private readonly int[] m_Parent;
        private readonly int[] m_Stamp;
        private readonly byte[] m_State;      // 0 = 안 봤다 · 1 = 열림 · 2 = 닫힘
        private readonly int[] m_Heap;
        private readonly float[] m_HeapKey;
        private readonly List<int> m_Cells = new List<int>(256);
        private readonly int m_MaxNodes;

        private int m_Generation;
        private int m_HeapCount;

        /// <summary>마지막 탐색에서 열어 본 노드 수 — 진단용.</summary>
        public int LastNodes { get; private set; }

        /// <summary>마지막 경로가 무언가를 부수고 지나가나(2차 탐색이 답을 냈나).</summary>
        public bool LastNeededBreaking { get; private set; }

        /// <summary>
        /// 마지막 경로의 **모든 셀**(꺾임점만이 아니라). 노드 예약(앨리스 19~20p)이 이걸 잡아야
        /// "같은 복도에 줄 서는 것"을 막는다 — 꺾임점만 잡으면 긴 복도에서 겹치는 칸이 양 끝 둘뿐이다.
        /// </summary>
        public IReadOnlyList<int> LastFullPath => m_Cells;

        public GridPathfinder(NavGrid _grid)
        {
            m_Grid = _grid;
            int n = _grid.CellCount;

            // **노드 상한은 격자 크기를 따라간다.** 대형 맵(242×242 = 58,564칸)에서 8,000은 반지름 25m짜리
            // 원에 불과해, 맵 반대편으로 가는 정상 경로를 "길 없음"으로 포기하고 2차 탐색까지 헛돌았다
            // (2026-08-18 리뷰). 절반이면 어떤 목표든 닿는다.
            m_MaxNodes = Mathf.Max(MIN_MAX_NODES, n / 2);

            m_G = new float[n];
            m_Parent = new int[n];
            m_Stamp = new int[n];
            m_State = new byte[n];
            // 지연 삭제라 같은 칸이 여러 번 들어간다 — 칸당 최대 8번 갱신될 수 있어 넉넉히 잡는다
            m_Heap = new int[n * 8 + 1];
            m_HeapKey = new float[n * 8 + 1];
        }

        /// <summary>
        /// 길을 찾는다. 성공하면 `_outCells`에 **꺾임점만** 담긴다(출발 셀 제외, 도착 셀 포함).
        /// 전체 셀은 <see cref="LastFullPath"/>에 남는다.
        /// 1차(안 부수고) → 실패하면 2차(부수고)를 자동으로 돈다.
        /// </summary>
        public bool TryFindPath(Vector3 _from, Vector3 _to, NavAgent _agent, float _bodyRadius,
            NavCellReservations _reservations, List<int> _outCells)
        {
            LastNeededBreaking = false;
            _outCells.Clear();
            m_Cells.Clear();
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
            // 대신 도착점을 **셀 중심**으로 둔다(NavRoute가 엄격 판정일 때만 원본 좌표로 바꾼다).
            if (!m_Grid.CanEnter(gx, gz, _agent, _bodyRadius, 0f, out _))
            {
                if (!TryFindNearestOpen(gx, gz, _agent, _bodyRadius, true, out goal)
                    && !TryFindNearestOpen(gx, gz, _agent, _bodyRadius, false, out goal))
                {
                    return false;
                }
            }

            if (Search(start, goal, _agent, _bodyRadius, 0f, _reservations))
            {
                BuildPath(start, goal, _agent, _bodyRadius, false, _outCells);
                return true;
            }

            // **노드 상한에 걸린 것은 "길이 없다"가 아니다.** 2차를 돌려 봐야 같은 상한에 또 걸리므로
            // 시간만 두 배로 태운다 — 그냥 포기하고 두뇌가 다른 목표를 잡게 한다.
            if (LastNodes >= m_MaxNodes) return false;

            // 1차 실패 = 안 부수고는 못 간다. 부술 수 있는 것을 열어 다시 본다.
            if (!Search(start, goal, _agent, _bodyRadius, BREAK_COST, _reservations)) return false;

            LastNeededBreaking = true;
            BuildPath(start, goal, _agent, _bodyRadius, true, _outCells);
            return true;
        }

        // ---- A* ----

        private bool Search(int _start, int _goal, NavAgent _agent, float _radius, float _breakCost,
            NavCellReservations _reservations)
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
                if (LastNodes >= m_MaxNodes) return false;

                int cx = current % width, cz = current / width;

                // **카디널 넷을 먼저 잰다.** 대각선 검사가 같은 칸을 다시 계산하지 않게 하고(확장당 8회 중복),
                // 무엇보다 **부수기 비용 0으로** 잰다 — 부술 셀을 '뚫린 옆칸'으로 인정하면 구조물 둘이
                // 대각으로 맞물린 모서리를 공짜로 관통해, 경로에 부술 셀이 하나도 없는데 "부수고 간다"는
                // 자기모순이 나온다(2026-08-18 리뷰에서 재현: 그러면 포위 행동이 영영 안 걸린다).
                bool eastOpen = m_Grid.CanEnter(cx + 1, cz, _agent, _radius, 0f, out _);
                bool westOpen = m_Grid.CanEnter(cx - 1, cz, _agent, _radius, 0f, out _);
                bool northOpen = m_Grid.CanEnter(cx, cz + 1, _agent, _radius, 0f, out _);
                bool southOpen = m_Grid.CanEnter(cx, cz - 1, _agent, _radius, 0f, out _);

                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;

                        bool diagonal = dx != 0 && dz != 0;
                        if (diagonal)
                        {
                            // 대각선 검사(53p) — 양옆이 **진짜로** 뚫려야 대각으로 간다
                            if (!(dx > 0 ? eastOpen : westOpen)) continue;
                            if (!(dz > 0 ? northOpen : southOpen)) continue;
                        }

                        int nx = cx + dx, nz = cz + dz;
                        if (!m_Grid.CanEnter(nx, nz, _agent, _radius, _breakCost, out float extra)) continue;

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
            return !Traverse(a, b, TraverseTest.Blocked, _agent, _radius);
        }

        private bool SegmentTouchesBreakable(int _fromCell, int _toCell, NavAgent _agent)
        {
            int width = m_Grid.Width;
            Vector3 a = m_Grid.ToWorld(_fromCell % width, _fromCell / width);
            Vector3 b = m_Grid.ToWorld(_toCell % width, _toCell / width);
            return Traverse(a, b, TraverseTest.Breakable, _agent, 0f);
        }

        private bool Hits(int _x, int _z, TraverseTest _test, NavAgent _agent, float _radius)
        {
            return _test == TraverseTest.Blocked
                ? !m_Grid.CanEnter(_x, _z, _agent, _radius, 0f, out _)
                : m_Grid.NeedsBreaking(_x, _z, _agent);
        }

        /// <summary>
        /// 선분이 지나는 칸을 하나도 빠뜨리지 않고 훑는다(Amanatides–Woo). 판정에 걸리면 즉시 참.
        /// 셀 중심을 잇는 계단을 지우려면 "이 직선이 벽을 스치나"를 정확히 알아야 한다 —
        /// 표본 몇 개로 때우면 모서리를 지나쳐 벽에 낀다.
        ///
        /// **정확히 45°는 따로 다룬다**(2026-08-18 리뷰). 셀 중심끼리라 tMaxX와 tMaxZ가 비트 단위로 같아지고,
        /// 그러면 한쪽 계단만 밟아 반대쪽 모서리 칸을 한 번도 안 본다 — A\*가 대각선 검사로 금지한
        /// 바로 그 이동을 직선 검사가 허가해 버린다. 동점이면 **두 모서리 칸을 다 본다**.
        /// </summary>
        private bool Traverse(Vector3 _a, Vector3 _b, TraverseTest _test, NavAgent _agent, float _radius)
        {
            m_Grid.ToCell(_a, out int x, out int z);
            m_Grid.ToCell(_b, out int ex, out int ez);
            if (Hits(x, z, _test, _agent, _radius)) return true;

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
                bool tie = tMaxX < float.MaxValue && tMaxZ < float.MaxValue
                           && Mathf.Abs(tMaxX - tMaxZ) < TIE_EPSILON;
                if (tie)
                {
                    // 꼭짓점을 정확히 지난다 — 몸은 두 모서리 칸 어느 쪽으로도 밀린다
                    if (Hits(x + stepX, z, _test, _agent, _radius)) return true;
                    if (Hits(x, z + stepZ, _test, _agent, _radius)) return true;
                    x += stepX;
                    z += stepZ;
                    tMaxX += tDeltaX;
                    tMaxZ += tDeltaZ;
                }
                else if (tMaxX < tMaxZ)
                {
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
                if (Hits(x, z, _test, _agent, _radius)) return true;
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
            if (m_HeapCount + 1 >= m_Heap.Length) return;   // 넘칠 일은 없지만 터지는 것보단 낫다

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
