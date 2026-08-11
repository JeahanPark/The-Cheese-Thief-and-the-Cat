#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using CTC.Runtime.Map;
using CTC.Runtime.Player;
using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 봇 하나가 **어떻게 가는지**. 무엇을 향해 갈지는 두뇌가 정한다 (2026-08-11).
    ///
    /// MonoBehaviour가 아니다 — 봇의 몸(플레이어 프리팹)에는 아무것도 안 붙인다.
    /// <see cref="TestBotManager"/>가 이걸 들고 매 틱 굴린다.
    ///
    /// 직접 옮기지 않고 <see cref="PlayerMovement.DevSetMoveInput"/>에 방향만 넣는 게 핵심이다.
    /// 이속·잠행 배율·바람 전직·중력·CharacterController 충돌이 **전부 사람과 같은 코드**를
    /// 타야 측정값이 뜻을 갖는다. 좌표를 직접 밀면 그 순간 봇은 다른 게임을 하는 것이 된다.
    /// </summary>
    public class BotNavigator
    {
        // 이만큼 가까워지면 그 경유점은 지난 것으로 본다. 너무 작으면 모퉁이에서 맴돈다.
        private const float WAYPOINT_REACH = 0.9f;
        // 목표가 이만큼 움직여야 길을 다시 짠다. 매 틱 다시 짜면 경유점 진행이 리셋된다.
        private const float REPLAN_DISTANCE = 2.5f;
        // 이 시간 동안 이만큼도 못 가면 막힌 것으로 본다
        private const float STUCK_SECONDS = 1.2f;
        private const float STUCK_MIN_SPEED = 0.5f;

        private readonly Transform m_Body;
        private readonly PlayerMovement m_Move;
        private readonly NavGraph m_Graph;

        private readonly List<Vector2> m_Path = new List<Vector2>();
        private int m_Index;
        private Vector3 m_PlannedGoal;
        private bool m_HasGoal;
        private float m_StuckSeconds;
        private Vector3 m_LastPos;

        /// <summary>이 봇이 판 시작부터 실제로 걸은 거리 — "제자리에 붙박였나"를 잰다.</summary>
        public float TravelledMeters { get; private set; }

        public bool IsMoving => m_HasGoal && m_Index < m_Path.Count;

        /// <summary>QA — 길찾기가 어느 층에서 끊겼는지 본다.</summary>
        public string Diagnostics =>
            $"p{m_Path.Count}/i{m_Index}/s{m_StuckSeconds:F1}" +
            $"@{(m_Body != null ? m_Body.position.x : 0f):F0},{(m_Body != null ? m_Body.position.z : 0f):F0}" +
            $"→{m_PlannedGoal.x:F0},{m_PlannedGoal.z:F0}" +
            $"{(m_Index < m_Path.Count ? $"|wp{m_Path[m_Index].x:F0},{m_Path[m_Index].y:F0}" : "|-")}";

        public BotNavigator(Transform _body, PlayerMovement _move, NavGraph _graph)
        {
            m_Body = _body;
            m_Move = _move;
            m_Graph = _graph;
            m_LastPos = _body != null ? _body.position : Vector3.zero;
        }

        /// <summary>여기로 가라. 이미 그 근처를 향하고 있으면 길을 다시 짜지 않는다.</summary>
        public void SetGoal(Vector3 _goal)
        {
            if (m_HasGoal && m_Index < m_Path.Count
                && Flat(_goal - m_PlannedGoal).magnitude < REPLAN_DISTANCE)
            {
                return;
            }

            m_PlannedGoal = _goal;
            m_HasGoal = true;
            Replan();
        }

        /// <summary>멈춘다. 파밍처럼 제자리에 있어야 하는 동안 쓴다.</summary>
        public void Stop()
        {
            m_HasGoal = false;
            m_Path.Clear();
            m_Index = 0;
            if (m_Move != null) m_Move.DevSetMoveInput(Vector3.zero);
        }

        /// <summary>매니저가 매 틱 부른다.</summary>
        public void Tick(float _deltaSeconds)
        {
            if (m_Body == null || m_Move == null) return;

            float moved = Flat(m_Body.position - m_LastPos).magnitude;
            TravelledMeters += moved;
            m_LastPos = m_Body.position;

            if (!m_HasGoal) return;

            // 막히면 그 경유점을 건너뛴다 — 그래프는 방 중심과 복도 끝점만 알아서
            // 가장 가까운 노드가 벽 너머일 수 있고, 그러면 첫 구간이 애초에 막혀 있다.
            m_StuckSeconds = moved / Mathf.Max(_deltaSeconds, 0.0001f) < STUCK_MIN_SPEED
                ? m_StuckSeconds + _deltaSeconds
                : 0f;

            if (m_StuckSeconds >= STUCK_SECONDS)
            {
                m_StuckSeconds = 0f;
                m_Index++;
                if (m_Index >= m_Path.Count) Replan();
            }

            if (m_Index >= m_Path.Count)
            {
                m_Move.DevSetMoveInput(Vector3.zero);
                m_HasGoal = false;
                return;
            }

            Vector2 wp = m_Path[m_Index];
            Vector3 delta = Flat(new Vector3(wp.x, 0f, wp.y) - m_Body.position);
            if (delta.magnitude <= WAYPOINT_REACH)
            {
                m_Index++;
                return;
            }

            m_Move.DevSetMoveInput(delta.normalized);
        }

        private void Replan()
        {
            m_Path.Clear();
            m_Index = 0;
            m_StuckSeconds = 0f;
            if (m_Graph == null || m_Body == null) return;

            var to = new Vector2(m_PlannedGoal.x, m_PlannedGoal.z);
            m_Path.AddRange(NavGraphLogic.FindPathToward(m_Graph, NearestVisibleNode(), to));
        }

        /// <summary>
        /// 길을 어디서 시작할 것인가. **가장 가까운 노드가 아니라, 실제로 보이는 노드**다.
        ///
        /// 그래프는 방 중심과 복도 끝점만 알고 벽을 모른다. 그냥 제일 가까운 노드를 잡으면
        /// 그게 **벽 너머**일 수 있고, 그러면 경로 첫 구간이 애초에 막혀 있어 벽을 밀며
        /// 제자리걸음을 한다(2026-08-11 실측: 봇 여섯이 전부 7m에서 굳었다.
        /// 강아지가 2026-08-09에 겪은 것과 같은 증상이다).
        /// </summary>
        private Vector2 NearestVisibleNode()
        {
            var self = new Vector2(m_Body.position.x, m_Body.position.z);
            if (m_Graph == null || m_Graph.Count == 0) return self;

            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < m_Graph.Count; i++)
            {
                Vector2 node = m_Graph.Nodes[i];
                float sqr = (node - self).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                if (!Ai.NavPruner.IsWalkable(self, node, m_Body)) continue;

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
#endif
