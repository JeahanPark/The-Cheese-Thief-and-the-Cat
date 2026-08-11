using CTC.Runtime.Map;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// 강아지가 **어떻게 가는지** (v3.8 5번). 무엇을 쫓을지는 `DogBrain`이 정한다.
    ///
    /// 길 고르기는 <see cref="NavRoute"/>가 한다 — **곧장 가 보고 막히면 노드로 돈다**
    /// (2026-08-11). 봇과 같은 코드다. 한쪽만 고치면 강아지와 봇이 다른 맵을 걷는 셈이 되어
    /// 봇으로 잰 밸런스가 강아지에게 안 맞는다.
    ///
    /// 그래프는 방향만 주고, 정확한 회피는 `CharacterController`의 물리 충돌이 맡는다.
    /// NavMesh를 안 쓰는 이유는 <see cref="NavGraphLogic"/> 주석에 있다.
    ///
    /// 서버만 움직인다. 클라에는 `NetworkTransform`이 결과만 실어 나른다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DogMover : NetworkBehaviour
    {
        // 바닥에 붙여 두는 힘. 점프가 없으니 상수 하나면 된다.
        private const float GRAVITY = -18f;

        private CharacterController m_Cc;
        private DogBrain m_Brain;
        private NavGraph m_Graph;
        private NavRoute m_Route;
        private System.Random m_Random;

        private float m_RepathTimer;
        private float m_VerticalSpeed;
        private float m_StuckSeconds;
        private Vector3 m_LastPos;
        private int m_PrunedEdges;

        /// <summary>QA 전용 — 길찾기가 어느 층에서 끊겼는지 본다(그래프·경로·제자리 시간).</summary>
        public string DevDiagnostics =>
            $"graph={(m_Graph != null ? m_Graph.Count : -1)} " +
            $"edges={(m_Graph != null ? m_Graph.EdgeCount : -1)} pruned={m_PrunedEdges} " +
            $"{(m_Route != null ? m_Route.Diagnostics : "route=-")} stuck={m_StuckSeconds:F1} " +
            $"grounded={(m_Cc != null && m_Cc.isGrounded ? 1 : 0)}";

        /// <summary>서버 전용 — 스폰 직후 `Alcove`가 레이아웃을 넘긴다.</summary>
        public void ServerBindLayout(MapLayout _layout)
        {
            if (_layout == null) return;

            m_Graph = NavGraphLogic.Build(_layout.Rooms, _layout.Corridors);
            m_PrunedEdges = NavPruner.Prune(m_Graph, transform);
            m_Route = new NavRoute(transform, m_Graph);
            // 배회는 서버가 맵 시드로 굴린다 — 클라마다 다르면 안 되고, QA 재현성도 여기서 나온다.
            m_Random = new System.Random(_layout.Seed);
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

            // 표적이 보여 직진 중이면 매 틱 좌표를 갱신한다 — 길을 다시 짜는 게 아니라
            // 방향만 트는 것이라 공짜다. 이게 없으면 쫓는 동안 0.5초 전 자리로 달린다.
            if (m_Route != null && m_Route.IsDirect && m_Brain != null && m_Brain.Target != null)
            {
                m_Route.SetGoal(m_Brain.Target.transform.position);
            }

            if (m_RepathTimer <= 0f)
            {
                m_RepathTimer = DogAiLogic.RepathSeconds;
                ChooseGoal();
            }

            Step(dt);
        }

        /// <summary>
        /// 어디로 갈지만 정한다. **어떻게 갈지는 <see cref="NavRoute"/>가 안다** —
        /// 표적이 보이면 노드를 안 거치고 곧장 달려든다.
        /// </summary>
        private void ChooseGoal()
        {
            if (m_Route == null) return;

            // 길이 도저히 안 나오면 그 목표를 놓고 딴 데로 간다. 표적이라도 마찬가지다 —
            // 벽 너머 표적을 붙들고 있으면 벽만 밀다 판이 끝난다.
            if (m_Route.ConsumeGaveUp())
            {
                m_Route.SetGoal(RandomNode());
                return;
            }

            if (m_Brain != null && m_Brain.Target != null)
            {
                m_Route.SetGoal(m_Brain.Target.transform.position);
                return;
            }

            // 배회 중이고 아직 갈 데가 남았다 — 목적지를 흔들지 않는다
            if (m_Route.HasGoal) return;

            m_Route.SetGoal(RandomNode());
        }

        private void Step(float _dt)
        {
            // **막히면 스스로 빠져나온다.** 그래프는 방향만 주고 회피는 물리가 하는 구조라
            // 직진하다 모서리에 끼거나 경유점이 벽 뒤일 수 있다. 그때 이게 없으면 벽에 붙은 채
            // 영원히 안 움직인다(2026-08-09 실측: 배회하다 29.47m에서 굳어 12초 넘게 그대로였다).
            float moved = DogAiLogic.Flat(transform.position - m_LastPos).magnitude;
            m_LastPos = transform.position;
            m_StuckSeconds = DogAiLogic.AccumulateStuck(
                m_StuckSeconds, moved, _dt, DogAiLogic.STUCK_MIN_SPEED);

            if (m_StuckSeconds >= DogAiLogic.STUCK_SECONDS)
            {
                m_StuckSeconds = 0f;
                if (m_Route != null) m_Route.OnStuck();
            }

            Vector3 horizontal = Vector3.zero;

            if (m_Route != null && m_Route.TryGetDirection(_dt, out Vector3 dir))
            {
                horizontal = dir * DogAiLogic.MoveSpeed;
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }

            m_VerticalSpeed = m_Cc.isGrounded ? -1f : m_VerticalSpeed + GRAVITY * _dt;
            m_Cc.Move((horizontal + Vector3.up * m_VerticalSpeed) * _dt);
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
