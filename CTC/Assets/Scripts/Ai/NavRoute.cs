using System.Collections.Generic;
using CTC.Runtime.Map;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// **일단 곧장 가 보고, 막히면 노드로 돈다** — 강아지와 봇이 같이 쓰는 이동 규칙 (2026-08-11).
    ///
    /// 그전에는 무조건 그래프 경로로만 다녔다. 그래프는 방 중심과 복도 끝점만 노드로 갖는데
    /// 방 안에 엄폐물을 놓자 **그 노드로 들어가는 간선이 잘려** 방 하나가 통째로 못 가는 곳이
    /// 되었다. 시야를 끊는 선과 길을 내는 선이 같은 선이라 생긴 충돌이다.
    ///
    /// 뒤집으면 풀린다. 그래프는 **막혔을 때만** 쓰는 우회로다.
    /// <code>
    /// 1. A체크 — 나 → 목표 직선을 몸통 굵기로 쏜다
    /// 2. 안 막힘 → 노드 무시하고 직진
    /// 3. 막힘   → 노드 경로로 우회
    /// 4. 경로 노드에 닿을 때마다 → 다시 1번
    /// </code>
    ///
    /// **4번이 핵심이다.** 목표가 쥐라면 매 순간 움직인다. 처음 길을 짤 때 막혀 있었어도
    /// 모퉁이를 하나 돌면 이미 뚫려 있을 수 있고, 그때는 남은 노드를 버리고 곧장 간다.
    /// 그래서 "노드 사이를 각지게 오가는" 움직임이 사라지고 사람처럼 질러 간다.
    ///
    /// MonoBehaviour가 아니다 — 강아지(`DogMover`)와 봇(`BotNavigator`)이 각자 들고 굴린다.
    /// 방향만 돌려주고 실제 이동은 부르는 쪽이 한다(강아지는 `CC.Move`, 봇은 `DevSetMoveInput`).
    /// </summary>
    public class NavRoute
    {
        /// <summary>이만큼 가까워지면 그 경유점은 지난 것으로 본다. 너무 작으면 모퉁이에서 맴돈다.</summary>
        public const float WAYPOINT_REACH = 0.9f;

        /// <summary>목표에 이만큼 붙으면 도착. 0으로 두면 목표를 밀고 다닌다.</summary>
        public const float GOAL_REACH = 0.6f;

        /// <summary>
        /// 직진 중 A체크 주기. 직진에는 "도착할 경유점"이 없어서 시계로 대신한다 —
        /// 목표가 벽 뒤로 들어가는 순간을 이 안에 잡는다. 매 틱 쏘면 스피어캐스트가 낭비다.
        /// </summary>
        private const float RECHECK_SECONDS = 0.25f;

        /// <summary>목표가 이만큼 움직여야 노드 경로를 다시 짠다(직진 중에는 안 짠다).</summary>
        private const float REPLAN_DISTANCE = 3f;

        /// <summary>
        /// 릴레이 후보로 물리를 쏴 볼 노드 수. 그래프가 369개라 전부 쏘면 낭비다 —
        /// "나까지 + 목표까지" 합이 짧은 것부터 이만큼만 본다.
        /// </summary>
        private const int RELAY_CANDIDATES = 24;

        /// <summary>
        /// 이만큼 연속으로 막히면 그 목표를 놓고 부르는 쪽에 알린다.
        /// 릴레이까지 실패하는 자리(구석에 완전히 낀 경우)를 위한 최후 안전장치다 —
        /// 없으면 벽을 미는 채로 판이 끝난다(실측: 고양이 둘이 8초간 1mm도 못 갔다).
        /// </summary>
        private const int GIVE_UP_STUCKS = 3;

        /// <summary>
        /// 노드 경로를 다시 짜는 최소 간격. 두뇌는 매 틱 목표를 넣는데, 갈 수 없는 곳을
        /// 가리키면 매 틱 새로 짜게 된다 — 출발 노드 고르기가 노드마다 스피어캐스트라
        /// 봇 열 마리면 그것만으로 프레임이 죽는다.
        /// </summary>
        private const float REPLAN_COOLDOWN_SECONDS = 0.3f;

        private readonly Transform m_Self;
        private NavGraph m_Graph;

        private readonly List<Vector2> m_Path = new List<Vector2>();
        private int m_Index;
        private bool m_Direct;
        private bool m_HasGoal;
        private float m_RecheckSeconds;
        private float m_ReplanCooldown;
        private int m_StuckPlans;
        private bool m_GaveUp;
        private Vector3 m_Goal;          // 지금 향하는 곳 (쥐라면 매 틱 움직인다)
        private Vector3 m_PlannedGoal;   // 지금 들고 있는 경로를 짤 때의 곳

        public NavRoute(Transform _self, NavGraph _graph)
        {
            m_Self = _self;
            m_Graph = _graph;
        }

        /// <summary>지금 갈 데가 있는가.</summary>
        public bool HasGoal => m_HasGoal;

        /// <summary>벽 없이 곧장 가는 중인가(A체크 통과). QA가 "우회 중인가"를 이걸로 본다.</summary>
        public bool IsDirect => m_HasGoal && m_Direct;

        public int PathCount => m_Path.Count;
        public int PathIndex => m_Index;
        public Vector3 Goal => m_Goal;

        /// <summary>
        /// **여기는 도저히 못 가겠다**를 한 번만 알려준다(읽으면 지워진다).
        /// 두뇌가 이걸 받으면 그 단서를 버려야 한다 — 안 버리면 다음 틱에 같은 곳을 다시 준다.
        /// </summary>
        public bool ConsumeGaveUp()
        {
            if (!m_GaveUp) return false;
            m_GaveUp = false;
            return true;
        }

        /// <summary>QA — 어느 층에서 끊겼는지 한 줄로.</summary>
        public string Diagnostics =>
            $"{(m_Direct ? "direct" : "path")} path={m_Path.Count} idx={m_Index} " +
            $"goal={m_Goal.x:F0},{m_Goal.z:F0} " +
            $"wp={(m_Index < m_Path.Count ? $"{m_Path[m_Index].x:F0},{m_Path[m_Index].y:F0}" : "-")}";

        /// <summary>그래프를 갈아 끼운다(맵이 바뀔 때). 들고 있던 경로는 버린다.</summary>
        public void SetGraph(NavGraph _graph)
        {
            m_Graph = _graph;
            Clear();
        }

        /// <summary>
        /// 여기로 가라. **직진 중이면 목표가 움직여도 그냥 따라간다** — 짤 길이 없다.
        /// 노드로 우회하는 중일 때만, 목표가 크게 움직였으면 다시 짠다.
        /// </summary>
        public void SetGoal(Vector3 _goal)
        {
            m_Goal = _goal;

            if (m_HasGoal && m_Direct) return;
            if (m_HasGoal && m_Index < m_Path.Count
                && Flat(_goal - m_PlannedGoal).magnitude < REPLAN_DISTANCE)
            {
                return;
            }

            // 방금 짰다 — 갈 수 없는 곳을 두뇌가 계속 가리켜도 매 틱 다시 짜지는 않는다
            if (m_ReplanCooldown > 0f)
            {
                m_HasGoal = true;
                return;
            }

            m_HasGoal = true;
            m_StuckPlans = 0;   // 새 목표다
            Vector3 pos = m_Self != null ? m_Self.position : Vector3.zero;

            // 처음부터 A체크 — 뚫려 있으면 그래프를 아예 안 본다.
            if (CanGoDirect(pos))
            {
                TakeDirect();
                return;
            }
            PlanPath(pos);
        }

        /// <summary>멈춘다. 파밍처럼 제자리에 있어야 하는 동안 쓴다.</summary>
        public void Clear()
        {
            m_HasGoal = false;
            m_Direct = false;
            m_Path.Clear();
            m_Index = 0;
            m_StuckPlans = 0;
        }

        /// <summary>
        /// 제자리걸음일 때 부른다. 직진하다 끼었으면 **노드로 돌아 나가고**,
        /// 노드로 가다 끼었으면 그 경유점을 포기한다.
        ///
        /// 직진은 몸통 굵기로 쏴서 고르지만 모서리를 스치면 여전히 낄 수 있다.
        /// 그때 이게 없으면 기둥에 붙어 영원히 안 움직인다.
        /// </summary>
        public void OnStuck()
        {
            Vector3 pos = m_Self != null ? m_Self.position : Vector3.zero;

            // 릴레이까지 넣었는데도 계속 막힌다 — 구석에 완전히 낀 것이다. 목표를 놓는다.
            if (++m_StuckPlans >= GIVE_UP_STUCKS)
            {
                m_GaveUp = true;
                Clear();
                return;
            }

            if (m_Direct)
            {
                PlanPath(pos);
                return;
            }

            m_Index++;
            if (m_Index >= m_Path.Count) PlanPath(pos);
        }

        /// <summary>
        /// 매 틱. 갈 방향(수평 단위벡터)을 돌려준다. 도착했거나 갈 데가 없으면 false.
        /// </summary>
        public bool TryGetDirection(float _deltaSeconds, out Vector3 _dir)
        {
            _dir = Vector3.zero;
            if (!m_HasGoal || m_Self == null) return false;

            Vector3 pos = m_Self.position;
            m_RecheckSeconds -= _deltaSeconds;
            m_ReplanCooldown -= _deltaSeconds;

            // ── 직진 중 ──
            if (m_Direct)
            {
                Vector3 toGoal = Flat(m_Goal - pos);
                if (toGoal.magnitude <= GOAL_REACH)
                {
                    m_HasGoal = false;
                    return false;
                }

                // 목표가 움직여 벽 뒤로 들어갔을 수 있다 — 주기마다 다시 본다
                if (m_RecheckSeconds > 0f)
                {
                    _dir = toGoal.normalized;
                    return true;
                }

                m_RecheckSeconds = RECHECK_SECONDS;
                if (CanGoDirect(pos))
                {
                    _dir = toGoal.normalized;
                    return true;
                }
                PlanPath(pos);   // 막혔다 → 노드로
            }

            // ── 노드로 우회 중 ──
            while (m_Index < m_Path.Count)
            {
                Vector2 wp = m_Path[m_Index];
                Vector3 delta = Flat(new Vector3(wp.x, pos.y, wp.y) - pos);
                if (delta.magnitude > WAYPOINT_REACH)
                {
                    _dir = delta.normalized;
                    return true;
                }

                // ★ 진행 노드에 닿았다 — 여기서 매번 A체크. 목표가 움직였으니 지금은 뚫렸을 수 있다.
                m_Index++;
                m_StuckPlans = 0;   // 실제로 나아갔다
                if (TryTakeDirect(pos, out _dir)) return true;
            }

            // 경로를 다 썼다 — 마지막으로 한 번 더. 그래도 막혔으면 갈 데가 없다.
            if (TryTakeDirect(pos, out _dir)) return true;

            m_HasGoal = false;
            return false;
        }

        // A체크가 통과하면 직진으로 갈아탄다. 남은 노드는 버린다.
        private bool TryTakeDirect(Vector3 _pos, out Vector3 _dir)
        {
            _dir = Vector3.zero;
            m_RecheckSeconds = RECHECK_SECONDS;
            if (!CanGoDirect(_pos)) return false;

            TakeDirect();

            Vector3 toGoal = Flat(m_Goal - _pos);
            if (toGoal.magnitude <= GOAL_REACH)
            {
                m_HasGoal = false;
                return false;
            }

            _dir = toGoal.normalized;
            return true;
        }

        private void TakeDirect()
        {
            m_Direct = true;
            m_Path.Clear();
            m_Index = 0;
            m_RecheckSeconds = RECHECK_SECONDS;
            m_StuckPlans = 0;   // 트인 길을 찾았다 — 포기 시계를 되돌린다
        }

        // 나 → 목표 직선이 몸통 굵기로 뚫려 있는가. 이게 A체크다.
        private bool CanGoDirect(Vector3 _pos)
        {
            return NavPruner.IsWalkable(
                new Vector2(_pos.x, _pos.z), new Vector2(m_Goal.x, m_Goal.z), m_Self);
        }

        private void PlanPath(Vector3 _pos)
        {
            m_Direct = false;
            m_Path.Clear();
            m_Index = 0;
            m_PlannedGoal = m_Goal;
            m_RecheckSeconds = RECHECK_SECONDS;
            m_ReplanCooldown = REPLAN_COOLDOWN_SECONDS;
            if (m_Graph == null) return;

            var goal = new Vector2(m_Goal.x, m_Goal.z);

            // 못 닿는 목표여도 그쪽으로는 붙는다 — 빈 경로를 받으면 그 자리에 굳는다
            m_Path.AddRange(NavGraphLogic.FindPathToward(m_Graph, NearestVisibleNode(_pos), goal));

            // **마지막 한 구간은 언제나 직선이다.** 길찾기는 출발과 목표가 같은 노드 담당이면
            // "다 왔으니 그냥 걸어가"라고 답하는데, 그 직선 위에 방 안 물건(쥐가 판 중에 지은
            // 창고·요새·기둥)이 있으면 걷는 쪽이 그걸 영원히 민다. 그래프는 판 시작에 한 번만
            // 만들어서 나중에 생긴 물건을 모른다 — 길이 없는 게 아니라 **안 물어본 것**이다.
            // (2026-08-11 실측: 고양이 둘이 쥐가 지은 창고 앞에서 8초간 1mm도 못 갔다.)
            //
            // 그래서 지금 자리에서 목표까지 곧장 갈 수 없고 경로도 사실상 없으면,
            // **거쳐 갈 노드를 하나 끼운다** — 모서리 링이 그러라고 있는 것이다.
            if (m_Path.Count > 1) return;
            if (m_Path.Count == 1 && !IsSameSpot(m_Path[0], goal)) return;

            if (TryFindRelay(_pos, goal, out Vector2 relay))
            {
                m_Path.Insert(0, relay);
            }
        }

        private static bool IsSameSpot(Vector2 _a, Vector2 _b) => (_a - _b).sqrMagnitude < 0.01f;

        /// <summary>
        /// 한 번 거쳐 가면 목표가 보이는 노드를 찾는다. **목표까지 트인 노드**가 1순위,
        /// 없으면 나한테서 갈 수 있으면서 목표에 제일 가까운 노드다(가서 다시 A체크한다).
        ///
        /// 물리를 쏘므로 후보를 <see cref="RELAY_CANDIDATES"/>개로 줄인다 — "나까지 + 목표까지"
        /// 합이 짧은 것부터. 노드 369개에 전부 쏘면 봇 열 마리가 프레임을 잡아먹는다.
        /// </summary>
        private bool TryFindRelay(Vector3 _pos, Vector2 _goal, out Vector2 _relay)
        {
            _relay = default;
            var self = new Vector2(_pos.x, _pos.z);
            if (m_Graph == null || m_Graph.Count == 0) return false;

            var order = new List<int>(m_Graph.Count);
            for (int i = 0; i < m_Graph.Count; i++) order.Add(i);
            order.Sort((a, b) => Detour(a).CompareTo(Detour(b)));

            int fallback = -1;
            int limit = Mathf.Min(RELAY_CANDIDATES, order.Count);
            for (int i = 0; i < limit; i++)
            {
                Vector2 node = m_Graph.Nodes[order[i]];
                if ((node - self).sqrMagnitude < 1f) continue;              // 내가 선 자리다
                if (!NavPruner.IsWalkable(self, node, m_Self)) continue;    // 거기까지도 못 간다

                if (NavPruner.IsWalkable(node, _goal, m_Self))
                {
                    _relay = node;                                          // 거기서 목표가 트인다
                    return true;
                }
                if (fallback < 0) fallback = order[i];
            }

            if (fallback < 0) return false;
            _relay = m_Graph.Nodes[fallback];
            return true;

            float Detour(int _i)
            {
                Vector2 n = m_Graph.Nodes[_i];
                return (n - self).magnitude + (n - _goal).magnitude;
            }
        }

        /// <summary>
        /// 길을 어디서 시작할 것인가. **가장 가까운 노드가 아니라, 실제로 갈 수 있는 노드**다.
        ///
        /// 그래프는 벽을 모른다. 그냥 제일 가까운 노드를 잡으면 그게 **벽 너머**일 수 있고,
        /// 그러면 첫 구간이 애초에 막혀 있어 벽을 밀며 제자리걸음을 한다
        /// (2026-08-09 강아지 · 2026-08-11 봇 여섯이 같은 증상으로 굳었다).
        /// </summary>
        private Vector2 NearestVisibleNode(Vector3 _pos)
        {
            var self = new Vector2(_pos.x, _pos.z);
            if (m_Graph == null || m_Graph.Count == 0) return self;

            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < m_Graph.Count; i++)
            {
                Vector2 node = m_Graph.Nodes[i];
                float sqr = (node - self).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                if (!NavPruner.IsWalkable(self, node, m_Self)) continue;

                bestSqr = sqr;
                best = i;
            }
            return best >= 0 ? m_Graph.Nodes[best] : self;
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v;
        }
    }
}
