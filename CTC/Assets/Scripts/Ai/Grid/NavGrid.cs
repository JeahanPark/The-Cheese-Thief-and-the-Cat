using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>어떤 몸으로 지나가는가 — 문·부수기 규칙이 역할마다 다르다.</summary>
    public enum NavAgent
    {
        /// <summary>쥐 — 요새 문으로 들어간다. 요새 벽·창고는 못 부순다.</summary>
        Mouse = 0,

        /// <summary>고양이 — 문은 인방(0.75m)에 걸려 못 지나간다. 대신 벽을 부순다.</summary>
        Cat = 1,

        /// <summary>강아지 — 고양이와 같은 몸집. 요새도 문다(DogBrain이 표적에서 안 뺀다).</summary>
        Dog = 2,
    }

    /// <summary>셀 하나가 무엇에 막혀 있나 — 여러 구조물이 겹칠 수 있어 종류별로 센다.</summary>
    public struct NavCellStructure
    {
        /// <summary>고양이·강아지만 부술 수 있는 것(요새 벽·창고). 쥐에게는 그냥 벽이다.</summary>
        public byte CatBreakable;

        /// <summary>누구나 부술 수 있는 것(벽감·상자).</summary>
        public byte AnyBreakable;

        /// <summary>쥐만 지나가는 틈(요새 문). 고양이는 인방 높이에 걸린다.</summary>
        public byte MouseDoor;

        public bool IsEmpty => CatBreakable == 0 && AnyBreakable == 0 && MouseDoor == 0;
    }

    /// <summary>
    /// **직접 만든 이동 격자**(2026-08-18). 유니티 NavMesh를 대체한다.
    ///
    /// 왜 갈아탔나 — NavMesh는 폴리곤 내부를 못 건드려서 두 가지를 못 했다:
    /// ① **노드 비용**(기술소개서 앨리스 19~20p의 예약 시스템 원형). 폴리곤 비용에 손을 못 대
    ///    "남이 맡은 자리는 값이 떨어진다"를 목표 점수로만 흉내 내야 했다.
    /// ② **부술 수 있는 벽**. 요새는 폴리곤에 구멍(NavMeshObstacle)으로 표현돼 "못 가는 곳"이었다.
    ///    그래서 요새 안에 든 쥐를 고양이가 영영 못 쫓아갔다(2026-08-17 사용자 발견).
    ///    격자에서는 요새 벽이 **비싼 통과 가능 셀**이라, 우회로가 없으면 길이 벽을 관통해 나오고
    ///    그 셀이 곧 "여기를 부숴라"가 된다.
    /// 덤으로 폴리곤 스냅(`NavMesh.SamplePosition`)이 목표를 5m씩 당기던 문제가 사라진다.
    ///
    /// 층이 둘이다:
    /// - **지형**(`m_Blocked`·`m_Clearance`) — 맵을 지을 때 한 번 굽고 판 내내 안 바뀐다.
    ///   `m_Clearance`는 가장 가까운 막힌 셀까지의 거리(셀 단위)라, 몸 굵기가 다른 쥐·고양이가
    ///   **한 격자를 공유**하면서도 각자 지나갈 수 있는 틈이 갈린다.
    /// - **구조물**(`m_Structures`) — 판 중에 생기고 사라진다. 사전에만 담아 겹침·제거가 정확하다
    ///   (요새는 겹침 검사를 건너뛰고 서므로 참조 카운트가 필요하다).
    ///
    /// 좌표계: x-z 평면. 셀 (cx, cz)의 중심 = origin + ((cx+0.5), (cz+0.5)) * cellSize.
    /// </summary>
    public class NavGrid
    {
        /// <summary>셀 한 변(m). 요새 벽 두께 0.2·문 폭 1.0을 표현할 수 있는 크기.</summary>
        public const float CELL_SIZE = 0.5f;

        /// <summary>여유 거리 최댓값(셀) — 이보다 넓은 곳은 다 같은 값으로 본다.</summary>
        private const byte MAX_CLEARANCE = 30;

        private readonly bool[] m_Blocked;
        private readonly byte[] m_Clearance;
        private readonly Dictionary<int, NavCellStructure> m_Structures = new Dictionary<int, NavCellStructure>();

        public int Width { get; }

        public int Height { get; }

        public Vector2 Origin { get; }

        public int CellCount => Width * Height;

        /// <summary>막힌 지형 셀 수 — 진단용.</summary>
        public int BlockedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < m_Blocked.Length; i++) if (m_Blocked[i]) n++;
                return n;
            }
        }

        /// <summary>지금 구조물이 차지한 셀 수 — 진단용.</summary>
        public int StructureCellCount => m_Structures.Count;

        public NavGrid(Vector2 _origin, int _width, int _height)
        {
            Origin = _origin;
            Width = Mathf.Max(1, _width);
            Height = Mathf.Max(1, _height);
            m_Blocked = new bool[Width * Height];
            m_Clearance = new byte[Width * Height];
        }

        // ---- 좌표 ----

        public int Index(int _cx, int _cz) => _cz * Width + _cx;

        public bool InBounds(int _cx, int _cz) => _cx >= 0 && _cz >= 0 && _cx < Width && _cz < Height;

        /// <summary>월드 좌표 → 셀. 격자 밖이면 가장자리로 자른다.</summary>
        public void ToCell(Vector3 _world, out int _cx, out int _cz)
        {
            _cx = Mathf.Clamp(Mathf.FloorToInt((_world.x - Origin.x) / CELL_SIZE), 0, Width - 1);
            _cz = Mathf.Clamp(Mathf.FloorToInt((_world.z - Origin.y) / CELL_SIZE), 0, Height - 1);
        }

        /// <summary>셀 중심의 월드 좌표(y=0).</summary>
        public Vector3 ToWorld(int _cx, int _cz) => new Vector3(
            Origin.x + (_cx + 0.5f) * CELL_SIZE, 0f, Origin.y + (_cz + 0.5f) * CELL_SIZE);

        public Vector3 ToWorld(int _index) => ToWorld(_index % Width, _index / Width);

        // ---- 지형(굽기 전용) ----

        /// <summary>굽는 동안만 부른다 — 판 중에는 지형이 안 바뀐다.</summary>
        public void SetBlocked(int _cx, int _cz, bool _blocked)
        {
            if (!InBounds(_cx, _cz)) return;
            m_Blocked[Index(_cx, _cz)] = _blocked;
        }

        public bool IsTerrainBlocked(int _cx, int _cz) =>
            !InBounds(_cx, _cz) || m_Blocked[Index(_cx, _cz)];

        /// <summary>
        /// 여유 거리(가장 가까운 막힌 셀까지, 셀 단위)를 전부 계산한다 — 두 번 훑는 체비셰프 거리 변환.
        /// 몸 굵기 검사를 여기 한 번으로 끝낸다: 쥐(0.22)와 고양이(0.35)가 같은 격자를 쓰면서도
        /// "이 틈에 내 몸이 들어가나"가 갈린다.
        /// </summary>
        public void BuildClearance()
        {
            int n = Width * Height;
            for (int i = 0; i < n; i++) m_Clearance[i] = m_Blocked[i] ? (byte)0 : MAX_CLEARANCE;

            // 앞으로 훑기 — 왼쪽·아래 이웃에서 전파
            for (int z = 0; z < Height; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = Index(x, z);
                    if (m_Clearance[i] == 0) continue;
                    int best = MAX_CLEARANCE;
                    if (x > 0) best = Mathf.Min(best, m_Clearance[i - 1] + 1);
                    if (z > 0) best = Mathf.Min(best, m_Clearance[i - Width] + 1);
                    if (x > 0 && z > 0) best = Mathf.Min(best, m_Clearance[i - Width - 1] + 1);
                    if (x < Width - 1 && z > 0) best = Mathf.Min(best, m_Clearance[i - Width + 1] + 1);
                    m_Clearance[i] = (byte)Mathf.Min(best, MAX_CLEARANCE);
                }
            }

            // 뒤로 훑기 — 오른쪽·위 이웃에서 전파
            for (int z = Height - 1; z >= 0; z--)
            {
                for (int x = Width - 1; x >= 0; x--)
                {
                    int i = Index(x, z);
                    if (m_Clearance[i] == 0) continue;
                    int best = m_Clearance[i];
                    if (x < Width - 1) best = Mathf.Min(best, m_Clearance[i + 1] + 1);
                    if (z < Height - 1) best = Mathf.Min(best, m_Clearance[i + Width] + 1);
                    if (x < Width - 1 && z < Height - 1) best = Mathf.Min(best, m_Clearance[i + Width + 1] + 1);
                    if (x > 0 && z < Height - 1) best = Mathf.Min(best, m_Clearance[i + Width - 1] + 1);
                    m_Clearance[i] = (byte)Mathf.Min(best, MAX_CLEARANCE);
                }
            }
        }

        /// <summary>
        /// **닿을 수 없는 곳을 막힌 것으로 만든다** — 가장 큰 덩어리만 남기고 전부 봉인한다(2026-08-18).
        ///
        /// 격자는 맵 정사각형보다 한 칸 넓게 잡는다(경계벽까지 담으려고). 그 바깥 테두리는
        /// 아무것도 없어서 "빈 칸"으로 남는데, 실제로는 경계벽에 막혀 못 간다.
        /// 그대로 두면 **맵 밖으로 도망치려는 쥐**가 거기로 목표를 잡고 A\*가 맵 전체를 훑다 실패한다
        /// (실측 2026-08-18: 경로 요청 282번 중 256번이 이 이유로 포기).
        ///
        /// 4방 연결로 센다 — 대각으로만 이어진 틈은 실제로 못 지나간다(A\*도 양옆을 요구한다).
        /// </summary>
        public void SealUnreachable()
        {
            int n = Width * Height;
            var label = new int[n];
            var queue = new int[n];
            int bestLabel = 0, bestSize = 0, current = 0;

            for (int seed = 0; seed < n; seed++)
            {
                if (m_Blocked[seed] || label[seed] != 0) continue;

                current++;
                int head = 0, tail = 0, size = 0;
                queue[tail++] = seed;
                label[seed] = current;

                while (head < tail)
                {
                    int cell = queue[head++];
                    size++;
                    int x = cell % Width, z = cell / Width;

                    if (x > 0) Push(cell - 1);
                    if (x < Width - 1) Push(cell + 1);
                    if (z > 0) Push(cell - Width);
                    if (z < Height - 1) Push(cell + Width);

                    void Push(int _next)
                    {
                        if (m_Blocked[_next] || label[_next] != 0) return;
                        label[_next] = current;
                        queue[tail++] = _next;
                    }
                }

                if (size <= bestSize) continue;
                bestSize = size;
                bestLabel = current;
            }

            for (int i = 0; i < n; i++)
            {
                if (!m_Blocked[i] && label[i] != bestLabel) m_Blocked[i] = true;
            }
        }

        /// <summary>
        /// 이 셀 **중심에서 가장 가까운 벽 면까지**의 거리(m). 막힌 셀이면 0.
        ///
        /// 체비셰프 셀 거리 d는 "막힌 셀의 **중심**까지"라 그대로 쓰면 반 칸을 과대평가한다 —
        /// 벽 면은 그보다 반 칸 가깝다. 그 반 칸을 빼야 몸이 실제로 들어가는지가 맞는다.
        /// (2026-08-18 실측: 빼기 전에는 고양이가 벽에 붙어 걷는 경로가 나와 제자리걸음이 3배 늘었다.)
        /// </summary>
        public float ClearanceMeters(int _cx, int _cz) =>
            InBounds(_cx, _cz) ? Mathf.Max(0f, (m_Clearance[Index(_cx, _cz)] - 0.5f) * CELL_SIZE) : 0f;

        // ---- 구조물(판 중에 바뀐다) ----

        /// <summary>셀 하나에 구조물을 더한다. 같은 셀에 여럿이면 카운트가 쌓인다.</summary>
        public void AddStructureCell(int _cx, int _cz, NavStructureKind _kind)
        {
            if (!InBounds(_cx, _cz)) return;
            int i = Index(_cx, _cz);
            m_Structures.TryGetValue(i, out NavCellStructure cell);
            switch (_kind)
            {
                case NavStructureKind.CatBreakable: cell.CatBreakable++; break;
                case NavStructureKind.AnyBreakable: cell.AnyBreakable++; break;
                case NavStructureKind.MouseDoor: cell.MouseDoor++; break;
            }
            m_Structures[i] = cell;
        }

        /// <summary>더한 만큼만 뺀다 — 겹쳐 선 요새가 하나 부서져도 나머지가 남는다.</summary>
        public void RemoveStructureCell(int _cx, int _cz, NavStructureKind _kind)
        {
            if (!InBounds(_cx, _cz)) return;
            int i = Index(_cx, _cz);
            if (!m_Structures.TryGetValue(i, out NavCellStructure cell)) return;
            switch (_kind)
            {
                case NavStructureKind.CatBreakable: if (cell.CatBreakable > 0) cell.CatBreakable--; break;
                case NavStructureKind.AnyBreakable: if (cell.AnyBreakable > 0) cell.AnyBreakable--; break;
                case NavStructureKind.MouseDoor: if (cell.MouseDoor > 0) cell.MouseDoor--; break;
            }
            if (cell.IsEmpty) m_Structures.Remove(i);
            else m_Structures[i] = cell;
        }

        public NavCellStructure StructureAt(int _index)
        {
            m_Structures.TryGetValue(_index, out NavCellStructure cell);
            return cell;
        }

        public void ClearStructures() => m_Structures.Clear();

        // ---- 통행 판정 ----

        /// <summary>몸이 벽에 스치는 셀에 붙는 비용 — 못 가게 막지는 않고 **되도록 피하게** 한다.</summary>
        public const float TIGHT_COST = 3f;

        /// <summary>
        /// 이 셀을 지나갈 수 있나. `_breakCost`가 0보다 크면 **부수고 지나가는 것도 통행으로 친다**
        /// (2차 탐색) — 그때 추가 비용을 돌려준다. 0이면 부술 셀은 그냥 막힌 것이다(1차 탐색).
        ///
        /// **몸 굵기는 막는 게 아니라 값을 매긴다**(2026-08-18 실측 뒤 결정). 셀이 0.5m라
        /// 고양이 몸(0.35)은 "들어간다/안 들어간다"가 반 칸에서 갈린다 — 딱 잘라 막으면 벽 옆 치즈에
        /// 영영 못 가고(경로 요청 실패), 다 열어 주면 벽을 긁으며 걷는다(제자리걸음 3배).
        /// 그래서 통행은 느슨하게 두되 스치는 셀에 <see cref="TIGHT_COST"/>를 얹어, 여유가 있으면
        /// 저절로 복도 한가운데로 돌아가게 한다. `_strictFit`은 직선 검사 전용이다 —
        /// 코너를 지울 때는 **확실히 몸이 지나는 선**만 인정해야 벽을 스치는 지름길이 안 생긴다.
        /// </summary>
        public bool CanEnter(int _cx, int _cz, NavAgent _agent, float _bodyRadius,
            float _breakCost, out float _extraCost, bool _strictFit = false)
        {
            _extraCost = 0f;
            if (!InBounds(_cx, _cz)) return false;

            int i = Index(_cx, _cz);
            if (m_Blocked[i]) return false;

            float toWallCenter = m_Clearance[i] * CELL_SIZE;          // 막힌 셀 중심까지
            float toWallFace = (m_Clearance[i] - 0.5f) * CELL_SIZE;   // 그 벽 면까지
            if (_strictFit)
            {
                if (toWallFace < _bodyRadius) return false;
            }
            else
            {
                if (toWallCenter < _bodyRadius) return false;
                if (toWallFace < _bodyRadius) _extraCost += TIGHT_COST;
            }

            if (!m_Structures.TryGetValue(i, out NavCellStructure cell)) return true;

            // 쥐 전용 문: 쥐면 통과, 아니면 인방(0.75m)에 걸린다. 문 셀에는 벽도 같이 겹쳐 있을 수 있는데
            // (문틀), 쥐는 그 틈으로 지나가므로 문이 이긴다.
            bool blockedByCat = cell.CatBreakable > 0;
            bool blockedByAny = cell.AnyBreakable > 0;

            if (cell.MouseDoor > 0)
            {
                if (_agent == NavAgent.Mouse) return true;
                // 고양이·강아지는 인방에 막히지만 **부수면 지나간다** — 예전에는 여기서 곧장 false를
                // 돌려 문 셀이 부술 수도 없는 절대 벽이었다(2026-08-18 리뷰). 요새가 문 쪽으로만
                // 뚫려 있으면 고양이 경로가 아예 안 나왔다.
                blockedByCat = true;
            }

            if (!blockedByCat && !blockedByAny) return true;

            // 쥐는 요새 벽·창고를 못 부순다(CombatLogic이 같은 진영 구조물 피해를 막는다)
            bool canBreak = _breakCost > 0f
                            && (blockedByAny || _agent == NavAgent.Cat || _agent == NavAgent.Dog);
            if (!canBreak) return false;

            _extraCost = _breakCost;
            return true;
        }

        /// <summary>여기를 지나려면 무언가를 부숴야 하나 — 두뇌가 "공격해라"로 읽는다.</summary>
        public bool NeedsBreaking(int _cx, int _cz, NavAgent _agent)
        {
            if (!InBounds(_cx, _cz)) return false;
            if (!m_Structures.TryGetValue(Index(_cx, _cz), out NavCellStructure cell)) return false;
            if (cell.MouseDoor > 0 && _agent == NavAgent.Mouse) return false;
            if (cell.AnyBreakable > 0) return true;
            return cell.CatBreakable > 0 && _agent != NavAgent.Mouse;
        }
    }

    /// <summary>구조물이 격자에 남기는 자국의 종류.</summary>
    public enum NavStructureKind
    {
        /// <summary>요새 벽·창고 — 고양이·강아지만 부순다.</summary>
        CatBreakable = 0,

        /// <summary>벽감·상자 — 누구나 부순다.</summary>
        AnyBreakable = 1,

        /// <summary>요새 문 — 쥐만 지나간다.</summary>
        MouseDoor = 2,
    }
}
