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

        private CharacterController m_Cc;
        private DogBrain m_Brain;
        private NavGraph m_Graph;
        private System.Random m_Random;

        private readonly List<Vector2> m_Path = new List<Vector2>();
        private int m_PathIndex;
        private float m_RepathTimer;
        private float m_VerticalSpeed;

        /// <summary>서버 전용 — 스폰 직후 `Alcove`가 레이아웃을 넘긴다.</summary>
        public void ServerBindLayout(MapLayout _layout)
        {
            if (_layout == null) return;

            m_Graph = NavGraphLogic.Build(_layout.Rooms, _layout.Corridors);
            // 배회는 서버가 맵 시드로 굴린다 — 클라마다 다르면 안 되고, QA 재현성도 여기서 나온다.
            m_Random = new System.Random(_layout.Seed);
        }

        private void Awake()
        {
            m_Cc = GetComponent<CharacterController>();
            m_Brain = GetComponent<DogBrain>();
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
            }
            else if (m_Path.Count > 0 && m_PathIndex < m_Path.Count)
            {
                return; // 배회 중이고 아직 갈 데가 남았다 — 목적지를 흔들지 않는다
            }
            else
            {
                goal = RandomNode();
            }

            var from = new Vector2(transform.position.x, transform.position.z);
            List<Vector2> next = NavGraphLogic.FindPath(m_Graph, from, new Vector2(goal.x, goal.z));

            m_Path.Clear();
            m_Path.AddRange(next);
            m_PathIndex = 0;
        }

        private void Step(float _dt)
        {
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

        // 배회 목적지. 그래프가 비면 제자리.
        private Vector3 RandomNode()
        {
            if (m_Graph == null || m_Graph.Count == 0 || m_Random == null) return transform.position;

            Vector2 node = m_Graph.Nodes[m_Random.Next(m_Graph.Count)];
            return new Vector3(node.x, transform.position.y, node.y);
        }
    }
}
