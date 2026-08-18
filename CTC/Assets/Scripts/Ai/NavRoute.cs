using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// **직접 만든 격자 A\* 위로 길을 찾는다** — 강아지와 봇이 같이 쓴다 (2026-08-18 개편).
    ///
    /// 세 벌째다. ① 방·문에 점을 찍은 웨이포인트 그래프(마지막 한 구간이 검사 없는 직선이라 실패)
    /// → ② 유니티 NavMesh 폴리곤(2026-08-12) → ③ 지금의 격자.
    ///
    /// **왜 NavMesh를 버렸나** — 폴리곤 안을 못 건드려서 두 가지를 못 했다:
    /// ① 노드 비용에 손을 못 대 예약 시스템(기술소개서 앨리스 19~20p)을 목표 점수로만 흉내 냈다.
    /// ② 요새가 "구멍"이라 **부술 수 있는 벽**을 표현할 수 없었다 — 요새 안에 든 쥐를 고양이가
    ///    영영 못 쫓아갔다(2026-08-17 사용자 발견). 격자에서는 두 번 탐색해서, 우회로가 없을 때만
    ///    벽을 관통하는 경로가 나오고 그 자리가 "여기를 부숴라"가 된다.
    /// 덤: 폴리곤 스냅(`NavMesh.SamplePosition`)이 목표를 평균 4.9m 당기던 문제가 없어졌다.
    ///     격자는 막힌 목표를 **한 칸 옆**으로만 옮긴다.
    ///
    /// 순서는 셋이다:
    /// ⓪ **장애물 검사** — 목표까지 격자 직선이 뚫려 있으면 A\*를 아예 안 부른다.
    /// ① **A\*** — 1차는 부술 셀을 벽으로 보고, 실패하면 2차에서 열어 준다.
    /// ② **직선 검사**(기술소개서 14p) — 셀 사슬에서 꺾임점만 남긴다.
    ///
    /// **`NavMeshAgent`는 여전히 안 쓴다.** 길 계산만 하고, 방향은 부르는 쪽이 사람과 같은 입구로
    /// 넣는다(강아지 `CC.Move` · 봇 `DevSetMoveInput`) — 그래야 봇으로 잰 밸런스가 뜻을 갖는다.
    /// </summary>
    public class NavRoute
    {
        /// <summary>이만큼 가까워지면 그 코너는 지난 것으로 본다. 너무 작으면 모퉁이에서 맴돈다.</summary>
        public const float CORNER_REACH = 0.8f;

        /// <summary>목표에 이만큼 붙으면 도착. 0으로 두면 목표를 밀고 다닌다.</summary>
        public const float GOAL_REACH = 0.6f;

        /// <summary>목표가 이만큼 움직여야 다시 짠다. 매 틱 짜면 코너 진행이 리셋된다.</summary>
        private const float REPLAN_DISTANCE = 1.5f;

        /// <summary>
        /// 다시 짜는 최소 간격. 두뇌는 매 틱 목표를 넣는다.
        ///
        /// **카운터가 아니라 시계로 잰다**(2026-08-12). 카운터를 <see cref="TryGetDirection"/>
        /// 안에서 깎았더니, 목표에 도착해 갈 데가 없어진 순간 그 함수가 맨 앞에서 돌아가
        /// 카운터가 영영 안 깎였다. 그러면 <see cref="SetGoal"/>이 "방금 짰다"며 계속 안 짜고
        /// 봇이 그 자리에 선다 — 소리를 조사하러 가는 고양이는 경로가 짧아 늘 이 덫에 걸렸다
        /// (실측: 고양이 이동 0.33m/s, 최고속은 7.8이다).
        /// </summary>
        private const float REPLAN_COOLDOWN_SECONDS = 0.25f;

        /// <summary>몸 굵기를 못 읽었을 때의 기본값(고양이 0.35 · 쥐 0.22 사이).</summary>
        private const float DEFAULT_BODY_RADIUS = 0.3f;

        private readonly Transform m_Self;
        private readonly float m_BodyRadius;
        private readonly NavAgent m_Agent;
        private readonly ulong m_OwnerId;
        private readonly List<int> m_PathCells = new List<int>(64);
        private readonly List<Vector3> m_Corners = new List<Vector3>(64);

        private int m_Index;
        private bool m_HasGoal;
        private bool m_GaveUp;
        private bool m_StraightMode;
        private bool m_ForcePath;        // 직선으로 가다 막혔다 — 다음 계획은 직선 지름길을 건너뛴다
        private float m_NextPlanTime;
        private Vector3 m_Goal;          // 지금 향하는 곳 (쥐라면 매 틱 움직인다)
        private Vector3 m_PlannedGoal;   // 지금 들고 있는 경로를 짤 때의 곳

        public NavRoute(Transform _self)
        {
            m_Self = _self;
            m_OwnerId = _self != null ? (ulong)(uint)_self.GetEntityId().GetHashCode() : 0UL;

            // 몸 굵기는 사람과 같은 CharacterController에서 읽는다 — 격자의 '여유 거리'와 견준다
            var cc = _self != null ? _self.GetComponent<CharacterController>() : null;
            m_BodyRadius = cc != null ? cc.radius : DEFAULT_BODY_RADIUS;

            m_Agent = ResolveAgent(_self);
        }

        /// <summary>지금 갈 데가 있는가.</summary>
        public bool HasGoal => m_HasGoal;

        /// <summary>목표까지 곧장 가는 중인가(중간에 아무것도 없다).</summary>
        public bool IsStraight => m_StraightMode;

        public Vector3 Goal => m_Goal;

        /// <summary>이 길로 가려면 무언가를 부숴야 하나 — 두뇌가 "때려라"로 읽는다.</summary>
        public bool NeedsBreaking { get; private set; }

        /// <summary>부숴야 하는 자리(월드). <see cref="NeedsBreaking"/>일 때만 뜻이 있다.</summary>
        public Vector3 BreakTarget { get; private set; }

        // ---- 계측(2026-08-18) — 길찾기를 갈아끼울 때 전후를 같은 조건에서 비교한다 ----

        public int PlanCount { get; private set; }

        public int GiveUpCount { get; private set; }

        /// <summary>경로 계산에 쓴 실시간 누적(ms). 배속에서도 뜻이 변하지 않게 실시간으로 잰다.</summary>
        public double PlanMillis { get; private set; }

        /// <summary>마지막 경로의 우회율 = 경로 길이 / 도착점까지 직선. 1이면 곧장.</summary>
        public float LastDetour { get; private set; } = 1f;

        /// <summary>경로가 원본 목표에서 얼마나 못 미치고 끝났나(m).</summary>
        public float LastShortfall { get; private set; }

        /// <summary>마지막 탐색이 열어 본 노드 수.</summary>
        public int LastNodes { get; private set; }

        // ---- 디버그 그리기(BotLab) ----

        public int CornerCount => m_Corners.Count;

        public int CornerIndex => m_Index;

        public Vector3 CornerAt(int _i) => (uint)_i < (uint)m_Corners.Count ? m_Corners[_i] : m_Goal;

        /// <summary>QA — 어느 층에서 끊겼는지 한 줄로. **토큰 순서가 계약이다**(health_check.py 정규식).</summary>
        public string Diagnostics =>
            $"{(m_StraightMode ? "straight" : "path")} corners={m_Corners.Count} idx={m_Index} " +
            $"goal={m_Goal.x:F0},{m_Goal.z:F0} " +
            $"wp={(m_Index < m_Corners.Count ? $"{m_Corners[m_Index].x:F0},{m_Corners[m_Index].z:F0}" : "-")}";

        /// <summary>
        /// **여기는 도저히 못 가겠다**를 한 번만 알려준다(읽으면 지워진다).
        /// 두뇌가 이걸 받으면 그 목표를 버려야 한다 — 안 버리면 같은 곳을 다시 준다.
        /// </summary>
        public bool ConsumeGaveUp()
        {
            if (!m_GaveUp) return false;
            m_GaveUp = false;
            return true;
        }

        /// <summary>여기로 가라. 목표가 조금 움직인 정도면 들고 있던 경로를 그대로 쓴다.</summary>
        public void SetGoal(Vector3 _goal)
        {
            m_Goal = _goal;

            if (m_HasGoal && m_Index < m_Corners.Count
                && Flat(_goal - m_PlannedGoal).magnitude < REPLAN_DISTANCE)
            {
                return;
            }

            if (Time.time < m_NextPlanTime)
            {
                m_HasGoal = true;
                return;
            }

            m_HasGoal = true;
            Replan();
        }

        /// <summary>멈춘다. 파밍처럼 제자리에 있어야 하는 동안 쓴다.</summary>
        public void Clear()
        {
            m_HasGoal = false;
            m_Corners.Clear();
            m_Index = 0;
            NeedsBreaking = false;
            NavGridService.Reservations.Release(m_OwnerId);
        }

        /// <summary>
        /// 제자리걸음일 때 부른다. 격자대로 걷는데도 못 나아간다면 남(다른 몸)에 막힌 것이다 —
        /// 격자는 지형과 구조물만 알고 서로의 몸은 모른다. 코너를 하나 건너뛰고 다시 짠다.
        /// </summary>
        public void OnStuck()
        {
            // **직선으로 가다 막혔으면 이번엔 경로로 간다.** 직선 모드는 코너가 하나뿐이라
            // 그냥 다시 짜면 ⓪ 검사가 또 통과해 **완전히 같은 계획**이 나온다 — 탈출구가 없다
            // (2026-08-18 리뷰). 격자는 남의 몸을 모르므로 이 한 번의 우회가 유일한 해법이다.
            if (m_StraightMode)
            {
                m_ForcePath = true;
                Replan();
                return;
            }

            m_Index++;
            if (m_Index >= m_Corners.Count) Replan();
        }

        /// <summary>매 틱. 갈 방향(수평 단위벡터)을 돌려준다. 도착했거나 갈 데가 없으면 false.</summary>
        public bool TryGetDirection(out Vector3 _dir)
        {
            _dir = Vector3.zero;
            if (!m_HasGoal || m_Self == null) return false;

            Vector3 pos = m_Self.position;

            while (m_Index < m_Corners.Count)
            {
                Vector3 delta = Flat(m_Corners[m_Index] - pos);
                // 마지막 코너 = 목표다. 중간 코너는 스치듯 지나가야 모퉁이에서 안 맴돈다.
                float reach = m_Index == m_Corners.Count - 1 ? GOAL_REACH : CORNER_REACH;
                if (delta.magnitude > reach)
                {
                    _dir = delta.normalized;
                    return true;
                }
                m_Index++;
            }

            // 다 왔다. 들고 있던 코너를 버려야 진단이 "다 간 것"과 "못 간 것"을 구분한다.
            Clear();
            return false;
        }

        /// <summary>
        /// 격자 위의 아무 자리. 배회 목적지를 고를 때 쓴다 —
        /// 그냥 무작위 좌표를 잡으면 벽 속이 나온다.
        /// </summary>
        public static bool TryRandomPoint(
            Vector3 _around, float _radius, System.Random _rng, out Vector3 _point)
        {
            _point = _around;
            NavGrid grid = NavGridService.Grid;
            if (grid == null || _rng == null) return false;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float dist = Mathf.Sqrt((float)_rng.NextDouble()) * _radius;
                var candidate = new Vector3(
                    _around.x + Mathf.Cos(angle) * dist, 0f, _around.z + Mathf.Sin(angle) * dist);

                grid.ToCell(candidate, out int cx, out int cz);
                if (!grid.CanEnter(cx, cz, NavAgent.Mouse, DEFAULT_BODY_RADIUS, 0f, out _, true)) continue;

                _point = grid.ToWorld(cx, cz);
                return true;
            }
            return false;
        }

        /// <summary>격자 위로 당긴 좌표 — 지휘관이 수색 지점을 고를 때처럼 "갈 수 있는 자리"가 필요할 때.</summary>
        public static bool TrySnap(Vector3 _world, out Vector3 _point)
        {
            _point = _world;
            NavGrid grid = NavGridService.Grid;
            if (grid == null) return false;

            grid.ToCell(_world, out int cx, out int cz);
            if (grid.CanEnter(cx, cz, NavAgent.Mouse, DEFAULT_BODY_RADIUS, 0f, out _, true))
            {
                _point = grid.ToWorld(cx, cz);
                return true;
            }

            for (int ring = 1; ring <= 24; ring++)
            {
                for (int dz = -ring; dz <= ring; dz++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;
                        if (!grid.CanEnter(cx + dx, cz + dz, NavAgent.Mouse, DEFAULT_BODY_RADIUS, 0f, out _, true)) continue;
                        _point = grid.ToWorld(cx + dx, cz + dz);
                        return true;
                    }
                }
            }
            return false;
        }

        // ---- 안쪽 ----

        private void Replan()
        {
            PlanCount++;
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            ReplanCore();
            PlanMillis += (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks)
                          * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            MeasureDetour();
        }

        private void ReplanCore()
        {
            m_Corners.Clear();
            m_Index = 0;
            m_PlannedGoal = m_Goal;
            m_NextPlanTime = Time.time + REPLAN_COOLDOWN_SECONDS;
            NeedsBreaking = false;
            if (m_Self == null) return;

            GridPathfinder finder = NavGridService.Pathfinder;
            NavGrid grid = NavGridService.Grid;
            if (finder == null || grid == null)
            {
                GiveUp();
                return;
            }

            // **옛 예약을 먼저 놓는다.** 안 놓으면 자기가 직전에 잡아 둔 셀에 제가 비용을 물어
            // A*가 제 길을 피해 간다(2026-08-18 리뷰 — `CountAt`은 소유자를 구분하지 않는다).
            NavGridService.Reservations.Release(m_OwnerId);

            bool forcePath = m_ForcePath;
            m_ForcePath = false;

            // ⓪ 장애물 검사 — 목표까지 격자 직선이 뚫려 있으면 A*를 부를 이유가 없다.
            grid.ToCell(m_Self.position, out int sx, out int sz);
            grid.ToCell(m_Goal, out int gx, out int gz);
            if (!forcePath
                && grid.CanEnter(gx, gz, m_Agent, m_BodyRadius, 0f, out _, _strictFit: true)
                && finder.LineClear(grid.Index(sx, sz), grid.Index(gx, gz), m_Agent, m_BodyRadius))
            {
                m_Corners.Add(m_Goal);
                m_StraightMode = true;
                LastNodes = 0;
                return;
            }
            m_StraightMode = false;

            // ① A* (1차 = 안 부수고 · 실패하면 2차 = 부수고)
            if (!finder.TryFindPath(m_Self.position, m_Goal, m_Agent, m_BodyRadius,
                    NavGridService.Reservations, m_PathCells))
            {
                LastNodes = finder.LastNodes;
                GiveUp();
                return;
            }

            LastNodes = finder.LastNodes;

            // ② 지나갈 셀을 예약한다 — 다음 봇의 A*에서 이 셀들의 비용이 올라 저절로 흩어진다
            //    (기술소개서 앨리스 19~20p 원형). **꺾임점이 아니라 전체 경로 셀**을 잡는다:
            //    꺾임점만 잡으면 긴 복도에서 겹치는 칸이 양 끝 둘뿐이라 아무도 안 흩어진다.
            NavGridService.Reservations.Claim(m_OwnerId, finder.LastFullPath);

            for (int i = 0; i < m_PathCells.Count; i++)
            {
                m_Corners.Add(grid.ToWorld(m_PathCells[i]));
            }

            // 마지막 코너는 목표 그 자체로 바꾼다 — 셀 중심으로 두면 최대 반 칸(0.35m)을 못 간다.
            // 단, **몸이 실제로 설 수 있는 자리일 때만**(엄격 판정). 벽에 붙은 좌표를 그대로 두면
            // 봇이 벽을 밀며 도착 판정을 영영 못 받는다(2026-08-18 실측).
            if (m_Corners.Count > 0 && !finder.LastNeededBreaking)
            {
                grid.ToCell(m_Goal, out int mx, out int mz);
                if (grid.CanEnter(mx, mz, m_Agent, m_BodyRadius, 0f, out _, _strictFit: true))
                {
                    m_Corners[m_Corners.Count - 1] = m_Goal;
                }
            }

            if (finder.LastNeededBreaking)
            {
                // 부술 셀 중 **처음** 만나는 것이 때릴 자리다
                foreach (int cell in m_PathCells)
                {
                    if (!grid.NeedsBreaking(cell % grid.Width, cell / grid.Width, m_Agent)) continue;
                    NeedsBreaking = true;
                    BreakTarget = grid.ToWorld(cell);
                    break;
                }
            }

            if (m_Corners.Count == 0) m_HasGoal = false;   // 이미 그 자리다
        }

        // 마지막 경로가 직선 대비 얼마나 돌아가는가 — 길찾기 품질의 한 줄 요약.
        private void MeasureDetour()
        {
            if (m_Self == null || m_Corners.Count == 0)
            {
                LastDetour = 1f;
                LastShortfall = 0f;
                return;
            }

            Vector3 cursor = m_Self.position;
            float walked = 0f;
            for (int i = 0; i < m_Corners.Count; i++)
            {
                walked += Flat(m_Corners[i] - cursor).magnitude;
                cursor = m_Corners[i];
            }

            Vector3 last = m_Corners[m_Corners.Count - 1];
            float direct = Flat(last - m_Self.position).magnitude;
            LastDetour = direct > 0.5f ? walked / direct : 1f;
            LastShortfall = Flat(m_Goal - last).magnitude;
        }

        // 격자 밖이거나 길이 아예 없다 — 두뇌가 다른 목표를 잡아야 한다.
        //
        // 들고 있던 코너도 버린다. 안 버리면 **진단이 거짓말을 한다** — 갈 데가 없는데
        // "코너 N개짜리 경로를 다 쓴 상태"로 보여서, 막힘을 재는 검사가 못 가는 것과
        // 다 간 것을 구분하지 못한다(2026-08-12).
        private void GiveUp()
        {
            GiveUpCount++;
            m_GaveUp = true;
            m_HasGoal = false;
            m_StraightMode = false;
            m_Corners.Clear();
            m_Index = 0;
            NavGridService.Reservations.Release(m_OwnerId);
        }

        // 몸이 누구인가 — 문·부수기 규칙이 갈린다. 강아지는 제 두뇌를 달고 있어 바로 알아본다.
        private static NavAgent ResolveAgent(Transform _self)
        {
            if (_self == null) return NavAgent.Mouse;
            if (_self.GetComponent<DogBrain>() != null) return NavAgent.Dog;

            var tag = _self.GetComponent<Combat.RoleTag>();
            if (tag != null && tag.Role == Lobby.RoleId.Cat) return NavAgent.Cat;
            return NavAgent.Mouse;
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v;
        }
    }
}
