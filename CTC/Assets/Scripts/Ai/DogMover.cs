using CTC.Runtime.Map;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// 강아지가 **어떻게 가는지** (v3.8 5번). 무엇을 쫓을지는 `DogBrain`이 정한다.
    ///
    /// 길 고르기는 <see cref="NavRoute"/>가 한다 — `MapBuilder`가 구운 폴리곤 위를 걷는다
    /// (2026-08-12). 봇과 같은 코드다. 한쪽만 고치면 강아지와 봇이 다른 맵을 걷는 셈이 되어
    /// 봇으로 잰 밸런스가 강아지에게 안 맞는다.
    ///
    /// 폴리곤은 방향과 코너만 주고, 정확한 회피는 `CharacterController`의 물리 충돌이 맡는다.
    ///
    /// 서버만 움직인다. 클라에는 `NetworkTransform`이 결과만 실어 나른다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DogMover : NetworkBehaviour
    {
        // 바닥에 붙여 두는 힘. 점프가 없으니 상수 하나면 된다.
        private const float GRAVITY = -18f;

        /// <summary>배회 목적지를 이 반경 안에서 고른다.</summary>
        private const float WANDER_RADIUS = 40f;

        private CharacterController m_Cc;
        private DogBrain m_Brain;
        private NavRoute m_Route;
        private System.Random m_Random;

        private float m_RepathTimer;
        private float m_VerticalSpeed;
        private float m_StuckSeconds;
        private Vector3 m_LastPos;

        /// <summary>QA 전용 — 길찾기가 어느 층에서 끊겼는지 본다(코너·제자리 시간).</summary>
        public string DevDiagnostics =>
            $"navmesh={NavMeshTriangles()} " +
            $"{(m_Route != null ? m_Route.Diagnostics : "route=-")} stuck={m_StuckSeconds:F1} " +
            $"grounded={(m_Cc != null && m_Cc.isGrounded ? 1 : 0)}";

        /// <summary>서버 전용 — 스폰 직후 `Alcove`가 레이아웃을 넘긴다.</summary>
        public void ServerBindLayout(MapLayout _layout)
        {
            if (_layout == null) return;

            m_Route = new NavRoute(transform);
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

            // 표적을 쫓는 동안에는 매 틱 좌표를 갱신한다 — 폴리곤 길찾기는 목표가 조금 움직인
            // 정도면 들고 있던 코너를 그대로 쓰므로 공짜다. 없으면 0.5초 전 자리로 달린다.
            if (m_Route != null && m_Brain != null && m_Brain.Target != null)
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
        /// 어디로 갈지만 정한다. **어떻게 갈지는 <see cref="NavRoute"/>가 안다.**
        /// </summary>
        private void ChooseGoal()
        {
            if (m_Route == null) return;

            // 길이 도저히 안 나오면 그 목표를 놓고 딴 데로 간다. 표적이라도 마찬가지다 —
            // 붙들고 있으면 벽만 밀다 판이 끝난다.
            if (m_Route.ConsumeGaveUp())
            {
                Wander();
                return;
            }

            if (m_Brain != null && m_Brain.Target != null)
            {
                m_Route.SetGoal(m_Brain.Target.transform.position);
                return;
            }

            // 배회 중이고 아직 갈 데가 남았다 — 목적지를 흔들지 않는다
            if (m_Route.HasGoal) return;

            Wander();
        }

        private void Wander()
        {
            if (NavRoute.TryRandomPoint(transform.position, WANDER_RADIUS, m_Random, out Vector3 point))
            {
                m_Route.SetGoal(point);
            }
        }

        private void Step(float _dt)
        {
            // **막히면 스스로 빠져나온다.** 폴리곤은 지형만 알고 서로의 몸은 모른다 —
            // 사람이나 다른 강아지에 막히면 이게 없으면 붙은 채로 안 움직인다
            // (2026-08-09 실측: 배회하다 29.47m에서 굳어 12초 넘게 그대로였다).
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

        // 폴리곤이 실제로 구워졌는지 — 0이면 강아지가 한 발도 못 뗀다.
        private static int NavMeshTriangles()
        {
            UnityEngine.AI.NavMeshTriangulation tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
            return tri.indices != null ? tri.indices.Length / 3 : 0;
        }
    }
}
