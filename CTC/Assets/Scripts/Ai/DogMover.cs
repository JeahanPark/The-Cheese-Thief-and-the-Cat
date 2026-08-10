using System.Collections.Generic;
using CTC.Runtime.Map;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// 강아지가 **어떻게 가는지** (v3.8 5번). 무엇을 쫓을지는 `DogBrain`이 정한다.
    ///
    /// 길은 레이아웃에서 뽑은 `NavGraph`가 준다 — NavMesh를 안 쓰는 이유는
    /// <see cref="NavGraphLogic"/> 주석에 있다. 그래프는 방향만 주고, 정확한 회피는
    /// `CharacterController`의 물리 충돌이 맡는다.
    ///
    /// 서버만 움직인다. 클라에는 `NetworkTransform`이 결과만 실어 나른다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DogMover : NetworkBehaviour
    {
        // 이만큼 가까워지면 그 경유점은 지난 것으로 본다. 너무 작으면 모퉁이에서 맴돈다.
        private const float WAYPOINT_REACH = 0.9f;
        // 바닥에 붙여 두는 힘. 점프가 없으니 상수 하나면 된다.
        private const float GRAVITY = -18f;
        // 표적이 이만큼 움직여야 길을 다시 짠다. 매 틱 다시 짜면 경유점 진행이 리셋된다.
        private const float REPLAN_DISTANCE = 3f;
        // 간선을 검사할 때 쏘는 몸통(프리팹의 CharacterController와 맞춘다).
        private const float BODY_CENTER_HEIGHT = 0.4f;
        private const float BODY_RADIUS = 0.35f;

        private CharacterController m_Cc;
        private DogBrain m_Brain;
        private NavGraph m_Graph;
        private System.Random m_Random;

        private readonly List<Vector2> m_Path = new List<Vector2>();
        private int m_PathIndex;
        private float m_RepathTimer;
        private float m_VerticalSpeed;
        private float m_StuckSeconds;
        private Vector3 m_LastPos;
        private Vector3 m_PlannedGoal;
        private int m_PrunedEdges;

        /// <summary>QA 전용 — 길찾기가 어느 층에서 끊겼는지 본다(그래프·경로·제자리 시간).</summary>
        public string DevDiagnostics =>
            $"graph={(m_Graph != null ? m_Graph.Count : -1)} " +
            $"edges={(m_Graph != null ? m_Graph.EdgeCount : -1)} pruned={m_PrunedEdges} " +
            $"path={m_Path.Count} idx={m_PathIndex} stuck={m_StuckSeconds:F1} " +
            $"wp={(m_PathIndex < m_Path.Count ? $"{m_Path[m_PathIndex].x:F1},{m_Path[m_PathIndex].y:F1}" : "-")} " +
            $"grounded={(m_Cc != null && m_Cc.isGrounded ? 1 : 0)}";

        /// <summary>서버 전용 — 스폰 직후 `Alcove`가 레이아웃을 넘긴다.</summary>
        public void ServerBindLayout(MapLayout _layout)
        {
            if (_layout == null) return;

            m_Graph = NavGraphLogic.Build(_layout.Rooms, _layout.Corridors);
            PruneUnwalkableEdges();
            // 배회는 서버가 맵 시드로 굴린다 — 클라마다 다르면 안 되고, QA 재현성도 여기서 나온다.
            m_Random = new System.Random(_layout.Seed);
        }

        /// <summary>
        /// **벽을 지나는 간선을 걷어낸다.** 이걸 안 하면 길찾기가 "이 선으로 갈 수 있다"고
        /// 답해 놓고 실제로는 벽이 막고 있어, 강아지가 코앞 경유점을 두고 벽만 민다
        /// (2026-08-10 실측: 1.3m 앞 경유점을 두고 z가 4.17에 못 박혔다).
        ///
        /// 판 시작에 한 번만 돈다(간선 100개 남짓). 몸통 굵기로 쏘므로 **끼어서 못 지나는
        /// 틈**도 같이 걸러진다 — 점 하나로 쏘면 통과한다고 나오고 실제로는 낀다.
        /// </summary>
        private void PruneUnwalkableEdges()
        {
            m_PrunedEdges = 0;
            if (m_Graph == null) return;

            for (int a = 0; a < m_Graph.Count; a++)
            {
                var neighbors = new List<int>(m_Graph.NeighborsOf(a));
                foreach (int b in neighbors)
                {
                    if (b < a) continue;                    // 양방향이라 한 번만 본다
                    if (IsWalkable(m_Graph.Nodes[a], m_Graph.Nodes[b])) continue;

                    m_Graph.Disconnect(a, b);
                    m_PrunedEdges++;
                }
            }
        }

        private bool IsWalkable(Vector2 _a, Vector2 _b)
        {
            var from = new Vector3(_a.x, BODY_CENTER_HEIGHT, _a.y);
            var to = new Vector3(_b.x, BODY_CENTER_HEIGHT, _b.y);
            Vector3 dir = to - from;
            float distance = dir.magnitude;
            if (distance < 0.01f) return true;

            foreach (RaycastHit hit in Physics.SphereCastAll(
                         from, BODY_RADIUS, dir / distance, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform root = hit.transform.root;
                if (root == transform.root) continue;
                // 사람·강아지는 지나갈 수 있다(비켜 준다) — 막는 건 지형과 구조물뿐이다
                if (root.GetComponent<CharacterController>() != null) continue;
                return false;
            }
            return true;
        }

        private void Awake()
        {
            m_Cc = GetComponent<CharacterController>();
            m_Brain = GetComponent<DogBrain>();
            m_LastPos = transform.position;
        }

        private void Update()
        {
            if (!IsServer) return;

            float dt = Time.deltaTime;
            m_RepathTimer -= dt;

            if (m_RepathTimer <= 0f)
            {
                m_RepathTimer = DogAiLogic.RepathSeconds;
                Repath();
            }

            Step(dt);
        }

        private void Repath()
        {
            if (m_Graph == null) return;

            Vector3 goal;
            if (m_Brain != null && m_Brain.Target != null)
            {
                goal = m_Brain.Target.transform.position;

                // **표적이 그 자리면 다시 짜지 않는다.** 매번 새로 짜면 `m_PathIndex`가 0으로
                // 돌아가 지나온 경유점으로 되돌아가고, 벽에 막힌 경로를 무한히 반복한다
                // (2026-08-09 실측: Chase 상태로 path=3 idx=0에 붙박여 12초 넘게 안 움직였다).
                if (m_Path.Count > 0 && m_PathIndex < m_Path.Count
                    && DogAiLogic.Flat(goal - m_PlannedGoal).magnitude < REPLAN_DISTANCE)
                {
                    return;
                }
            }
            else if (m_Path.Count > 0 && m_PathIndex < m_Path.Count)
            {
                return; // 배회 중이고 아직 갈 데가 남았다 — 목적지를 흔들지 않는다
            }
            else
            {
                goal = RandomNode();
            }

            m_PlannedGoal = goal;

            // 못 닿는 표적이어도 그쪽으로는 붙는다 — 빈 경로를 받으면 강아지가 그 자리에 굳는다
            List<Vector2> next = NavGraphLogic.FindPathToward(
                m_Graph, NearestVisibleNode(), new Vector2(goal.x, goal.z));

            m_Path.Clear();
            m_Path.AddRange(next);
            m_PathIndex = 0;
            m_StuckSeconds = 0f;
        }

        private void Step(float _dt)
        {
            // **막히면 스스로 길을 다시 짠다.** 그래프는 방향만 주고 회피는 물리가 하는 구조라
            // 웨이포인트가 벽 뒤일 수 있다. 그때 이게 없으면 벽에 붙은 채 영원히 안 움직인다
            // (2026-08-09 실측: 배회하다 29.47m에서 굳어 12초 넘게 그대로였다).
            float moved = DogAiLogic.Flat(transform.position - m_LastPos).magnitude;
            m_LastPos = transform.position;
            m_StuckSeconds = DogAiLogic.AccumulateStuck(
                m_StuckSeconds, moved, _dt, DogAiLogic.STUCK_MIN_SPEED);

            if (m_StuckSeconds >= DogAiLogic.STUCK_SECONDS)
            {
                // 그 경유점은 못 간다 — 건너뛴다. 그래프는 방 중심과 복도 끝점만 알아서
                // 가장 가까운 노드가 **벽 너머**일 수 있고, 그러면 첫 구간이 애초에 막혀 있다.
                m_StuckSeconds = 0f;
                m_PathIndex++;
                if (m_PathIndex >= m_Path.Count) m_Path.Clear(); // 다음 Repath가 새 목적지를 잡는다
            }

            Vector3 horizontal = Vector3.zero;

            if (m_PathIndex < m_Path.Count)
            {
                Vector2 wp = m_Path[m_PathIndex];
                var target = new Vector3(wp.x, transform.position.y, wp.y);
                Vector3 delta = target - transform.position;
                delta.y = 0f;

                if (delta.magnitude <= WAYPOINT_REACH)
                {
                    m_PathIndex++;
                }
                else
                {
                    Vector3 dir = delta.normalized;
                    horizontal = dir * DogAiLogic.MoveSpeed;
                    transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                }
            }

            m_VerticalSpeed = m_Cc.isGrounded ? -1f : m_VerticalSpeed + GRAVITY * _dt;
            m_Cc.Move((horizontal + Vector3.up * m_VerticalSpeed) * _dt);
        }

        /// <summary>
        /// 길을 어디서 시작할 것인가. **가장 가까운 노드가 아니라, 실제로 보이는 노드**다.
        ///
        /// 그래프는 방 중심과 복도 끝점만 알고 벽을 모른다. 그냥 제일 가까운 노드를 잡으면
        /// 그게 **벽 너머**일 수 있고, 그러면 경로 첫 구간이 애초에 막혀 있어 강아지가 벽을
        /// 밀며 제자리걸음을 한다(2026-08-09 실측: z가 4.17에 못 박힌 채 x로만 미끄러졌다).
        ///
        /// 보이는 노드가 하나도 없으면(구석에 끼면) 원래대로 제 위치를 쓴다 — 그때는
        /// 경유점 건너뛰기가 빠져나오게 해 준다.
        /// </summary>
        private Vector2 NearestVisibleNode()
        {
            var self = new Vector2(transform.position.x, transform.position.z);
            if (m_Graph == null || m_Graph.Count == 0) return self;

            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < m_Graph.Count; i++)
            {
                Vector2 node = m_Graph.Nodes[i];
                float sqr = (node - self).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                if (IsNodeBlocked(node)) continue;

                bestSqr = sqr;
                best = i;
            }
            return best >= 0 ? m_Graph.Nodes[best] : self;
        }

        // 노드에는 콜라이더가 없으므로 좌표로 직접 쏜다(SightLine은 Transform을 받는다).
        private bool IsNodeBlocked(Vector2 _node)
        {
            Vector3 from = transform.position + Vector3.up * Combat.SightLine.RAY_HEIGHT;
            var to = new Vector3(_node.x, from.y, _node.y);
            Vector3 dir = to - from;
            if (dir.sqrMagnitude < 0.01f) return false;

            foreach (RaycastHit hit in Physics.RaycastAll(
                         from, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform root = hit.transform.root;
                if (root == transform.root) continue;
                if (root.GetComponent<CharacterController>() != null) continue;
                return true;
            }
            return false;
        }

        // 배회 목적지. 그래프가 비면 제자리.
        private Vector3 RandomNode()
        {
            if (m_Graph == null || m_Graph.Count == 0 || m_Random == null) return transform.position;

            Vector2 node = m_Graph.Nodes[m_Random.Next(m_Graph.Count)];
            return new Vector3(node.x, transform.position.y, node.y);
        }
    }
}
