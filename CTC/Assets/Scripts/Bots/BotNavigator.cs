#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.Ai;
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
    ///
    /// 길 고르기는 <see cref="NavRoute"/>가 한다 — 강아지와 **같은 코드**다. 한쪽만 고치면
    /// 강아지와 봇이 다른 맵을 걷는 셈이 되어 측정이 어긋난다.
    /// </summary>
    public class BotNavigator
    {
        // 이 시간 동안 이만큼도 못 가면 막힌 것으로 본다. 폴리곤 위를 걷는데 못 나아간다면
        // 지형이 아니라 남(다른 봇의 몸)에 막힌 것이다 — 폴리곤은 서로의 몸을 모른다.
        private const float STUCK_SECONDS = 1f;
        private const float STUCK_MIN_SPEED = 0.5f;

        /// <summary>배회 목적지를 이 반경 안에서 고른다. 맵 절반쯤은 돼야 판을 훑는다.</summary>
        private const float WANDER_RADIUS = 60f;

        private readonly Transform m_Body;
        private readonly PlayerMovement m_Move;
        private readonly NavRoute m_Route;

        private float m_StuckSeconds;
        private Vector3 m_LastPos;

        /// <summary>이 봇이 판 시작부터 실제로 걸은 거리 — "제자리에 붙박였나"를 잰다.</summary>
        public float TravelledMeters { get; private set; }

        public bool IsMoving => m_Route != null && m_Route.HasGoal;

        /// <summary>QA — 목표까지 곧장 가는 중인가(중간에 아무것도 없다).</summary>
        public bool IsStraight => m_Route != null && m_Route.IsStraight;

        /// <summary>
        /// **여기는 도저히 못 가겠다**를 한 번만 알려준다(읽으면 지워진다).
        /// 두뇌가 이걸 받으면 그 목표를 버려야 한다 — 안 버리면 같은 곳을 다시 준다.
        /// </summary>
        public bool GaveUp() => m_Route != null && m_Route.ConsumeGaveUp();

        /// <summary>QA — 길찾기가 어느 층에서 끊겼는지 본다.</summary>
        public string Diagnostics =>
            $"{(m_Route != null ? m_Route.Diagnostics : "-")} stuck={m_StuckSeconds:F1} " +
            $"@{(m_Body != null ? m_Body.position.x : 0f):F0},{(m_Body != null ? m_Body.position.z : 0f):F0}";

        public BotNavigator(Transform _body, PlayerMovement _move)
        {
            m_Body = _body;
            m_Move = _move;
            m_Route = new NavRoute(_body);
            m_LastPos = _body != null ? _body.position : Vector3.zero;
        }

        /// <summary>여기로 가라. 목표가 조금 움직인 정도면 길을 다시 짜지 않는다.</summary>
        public void SetGoal(Vector3 _goal)
        {
            if (m_Route != null) m_Route.SetGoal(_goal);
        }

        /// <summary>
        /// 갈 데가 없으면 아무 데나 간다. **제자리에 서 있으면 안 된다** —
        /// 배회하지 않는 고양이는 소리가 안 들리는 순간 판에서 사라진 것과 같고,
        /// 그러면 "소리로 찾는 데 몇 초 걸리나"가 측정되지 않는다.
        /// </summary>
        public void SetWanderGoal(System.Random _rng)
        {
            if (m_Body == null) return;
            if (NavRoute.TryRandomPoint(m_Body.position, WANDER_RADIUS, _rng, out Vector3 point))
            {
                SetGoal(point);
            }
        }

        /// <summary>멈춘다. 파밍처럼 제자리에 있어야 하는 동안 쓴다.</summary>
        public void Stop()
        {
            if (m_Route != null) m_Route.Clear();
            if (m_Move != null) m_Move.DevSetMoveInput(Vector3.zero);
        }

        /// <summary>매니저가 매 틱 부른다.</summary>
        public void Tick(float _deltaSeconds)
        {
            if (m_Body == null || m_Move == null || m_Route == null) return;

            float moved = Flat(m_Body.position - m_LastPos).magnitude;
            TravelledMeters += moved;
            m_LastPos = m_Body.position;

            if (!m_Route.HasGoal) return;

            m_StuckSeconds = moved / Mathf.Max(_deltaSeconds, 0.0001f) < STUCK_MIN_SPEED
                ? m_StuckSeconds + _deltaSeconds
                : 0f;

            if (m_StuckSeconds >= STUCK_SECONDS)
            {
                m_StuckSeconds = 0f;
                m_Route.OnStuck();
            }

            if (!m_Route.TryGetDirection(out Vector3 dir))
            {
                m_Move.DevSetMoveInput(Vector3.zero);
                return;
            }

            m_Move.DevSetMoveInput(dir);
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v;
        }
    }
}
#endif
