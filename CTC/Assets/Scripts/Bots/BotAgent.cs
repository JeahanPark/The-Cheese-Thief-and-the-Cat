#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Map;
using CTC.Runtime.Match;
using CTC.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 봇 한 마리 (2026-08-11). **MonoBehaviour가 아니다** — 봇의 몸에는 아무것도 안 붙인다.
    /// <see cref="TestBotManager"/>가 이걸 들고 매 틱 굴린다.
    ///
    /// 하는 일은 셋이다: 몸을 잡고 있고, 무엇을 아는지 걸러 내고, 사람이 쓰는 입구로 밀어 넣는다.
    /// 판단 규칙 자체는 `MouseBotLogic`·`CatBotLogic`(순수)에 있다.
    /// </summary>
    public class BotAgent
    {
        private const float DECIDE_INTERVAL = 0.5f;

        private readonly NetworkObject m_Object;
        private readonly RoleId m_Role;
        private readonly PlayerMovement m_Move;
        private readonly Sneak m_Sneak;
        private readonly JailedTag m_Jailed;
        private readonly Health m_Health;
        private readonly BotNavigator m_Nav;

        private float m_Timer;
        private int m_SpotIndex = -1;

        private readonly List<Vector3> m_Positions = new List<Vector3>();
        private readonly List<int> m_Grades = new List<int>();
        private readonly List<bool> m_Usable = new List<bool>();

        public RoleId Role => m_Role;
        public NetworkObject Body => m_Object;
        public bool Alive => m_Object != null && m_Object.IsSpawned && (m_Health == null || !m_Health.IsDead);

        /// <summary>QA·통계 — 벽에 붙박인 봇을 잡는다.</summary>
        public float TravelledMeters => m_Nav != null ? m_Nav.TravelledMeters : 0f;
        public MouseBotLogic.State State { get; private set; } = MouseBotLogic.State.Wander;

        /// <summary>QA — 길찾기 진단.</summary>
        public string NavDiagnostics => m_Nav != null ? m_Nav.Diagnostics : "";

        public BotAgent(NetworkObject _object, RoleId _role, NavGraph _graph)
        {
            m_Object = _object;
            m_Role = _role;
            m_Move = _object.GetComponent<PlayerMovement>();
            m_Sneak = _object.GetComponent<Sneak>();
            m_Jailed = _object.GetComponent<JailedTag>();
            m_Health = _object.GetComponent<Health>();
            m_Nav = new BotNavigator(_object.transform, m_Move, _graph);
        }

        public void Tick(float _deltaSeconds, CheeseSpotDirector _spots)
        {
            if (!Alive) return;

            // 갇힌 쥐는 아무것도 못 한다 — 사람도 입력이 막힌다
            if (m_Jailed != null && m_Jailed.Jailed)
            {
                m_Nav.Stop();
                m_SpotIndex = -1;
                m_Nav.Tick(_deltaSeconds);
                return;
            }

            m_Timer -= _deltaSeconds;
            if (m_Timer <= 0f)
            {
                m_Timer = DECIDE_INTERVAL;
                if (m_Role == RoleId.Mouse) DecideMouse(_spots);
            }

            m_Nav.Tick(_deltaSeconds);
        }

        /// <summary>판이 끝나거나 봇이 사라질 때 — 입력을 놓아 준다.</summary>
        public void Release()
        {
            if (m_Move != null) m_Move.DevSetMoveInput(Vector3.zero);
            if (m_Sneak != null) m_Sneak.DevSetSneak(false);
        }

        // 1단계: 쓸 만한 치즈 지점으로 가서 캔다. 위협 감지·도주는 2·3단계.
        private void DecideMouse(CheeseSpotDirector _spots)
        {
            CollectSpots(_spots);

            if (m_SpotIndex < 0 || m_SpotIndex >= m_Usable.Count || !m_Usable[m_SpotIndex])
            {
                m_SpotIndex = MouseBotLogic.ChooseSpot(
                    m_Positions, m_Grades, m_Usable, m_Object.transform.position);
            }

            bool hasTarget = m_SpotIndex >= 0;
            float distance = hasTarget
                ? Vector3.Distance(m_Object.transform.position, m_Positions[m_SpotIndex])
                : float.MaxValue;

            State = MouseBotLogic.Decide(_threatHeard: false, hasTarget, distance);

            if (State == MouseBotLogic.State.Travel) m_Nav.SetGoal(m_Positions[m_SpotIndex]);
            else m_Nav.Stop();
        }

        // 지점의 자리는 안다(고정이고 화면에 크게 표시된다). 마르거나 남이 쓰는 중인지는
        // 라벨에 적혀 있어 사람도 가서 보면 안다.
        private void CollectSpots(CheeseSpotDirector _spots)
        {
            m_Positions.Clear();
            m_Grades.Clear();
            m_Usable.Clear();
            if (_spots == null) return;

            ulong self = m_Object.NetworkObjectId;
            foreach (CheeseSpot spot in _spots.Spots)
            {
                if (spot == null) continue;
                m_Positions.Add(spot.transform.position);
                m_Grades.Add(spot.Grade);
                bool free = spot.EarnerClientId == CheeseSpotLogic.NO_EARNER
                            || spot.EarnerClientId == self;
                m_Usable.Add(!spot.Depleted && free);
            }
        }
    }
}
#endif
