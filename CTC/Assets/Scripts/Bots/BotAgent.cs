#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using CTC.Runtime.Ai;
using CTC.Runtime.Ai.Bt;
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
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
    /// **무엇을 할지는 행동트리**(<see cref="BotBrainTrees"/>)가 정한다 — 그전에는 `Decide()`가
    /// if 순서로 정했는데, 규칙이 함수 안에 숨어 있어 읽기도 늘리기도 어려웠다(2026-08-12).
    ///
    /// 트리의 잎이 부를 손잡이를 여기서 공개한다. 잎은 씬을 모르고 이 손잡이만 쓴다.
    /// </summary>
    public class BotAgent
    {
        private const float DECIDE_INTERVAL = 0.5f;

        /// <summary>도망칠 때 얼마나 멀리 잡을까 — 짧으면 고양이 사거리를 못 벗어난다.</summary>
        public const float FLEE_DISTANCE = 18f;

        /// <summary>조사하러 갔는데 아무도 없었다 — 이만큼 가까워지면 그 단서를 버린다.</summary>
        public const float LEAD_ARRIVE = 2f;

        /// <summary>
        /// 길이 안 나온 단서를 이만큼 무시한다. 안 그러면 소리가 계속 나는 동안
        /// 같은 못 가는 자리를 매번 다시 잡는다 — 쥐가 지은 창고가 딱 그렇다.
        /// </summary>
        private const float BAD_LEAD_SECONDS = 15f;

        /// <summary>그 단서를 이 반경 안에서는 같은 것으로 본다(소리 위치는 매번 조금씩 흔들린다).</summary>
        private const float BAD_LEAD_RADIUS = 4f;

        private readonly NetworkObject m_Object;
        private readonly RoleId m_Role;
        private readonly PlayerMovement m_Move;
        private readonly Sneak m_Sneak;
        private readonly JailedTag m_Jailed;
        private readonly Health m_Health;
        private readonly CatPounce m_Pounce;
        private readonly MouseDash m_Dash;
        private readonly MousePopgun m_Gun;
        private readonly MouseJob m_Job;
        private readonly GangRecruit m_Recruit;
        private readonly CatSkill m_Skill;

        private readonly BotNavigator m_Nav;
        private readonly BotSenses m_Senses;
        private readonly BotShopper m_Shopper;
        private readonly Reservations m_Reservations;
        private readonly BotOrders m_Orders;
        private readonly BtNode m_Tree;
        private readonly System.Random m_Rng;
        private readonly float m_PounceRange;

        private readonly List<Vector3> m_Positions = new List<Vector3>();
        private readonly List<int> m_Grades = new List<int>();
        private readonly List<bool> m_Usable = new List<bool>();

        private float m_Timer;
        private float m_Lived;
        private int m_SpotIndex = -1;
        private float m_LeadSince;
        private Vector3 m_Lead;
        private bool m_HasLead;
        private Vector3 m_BadLead;
        private float m_BadLeadUntil;
        private CheeseSpotDirector m_Spots;

        public RoleId Role => m_Role;
        public NetworkObject Body => m_Object;
        public ulong Id => m_Object != null ? m_Object.NetworkObjectId : 0UL;
        public bool Alive => m_Object != null && m_Object.IsSpawned && (m_Health == null || !m_Health.IsDead);
        public Vector3 Position => m_Object != null ? m_Object.transform.position : Vector3.zero;

        public BotNavigator Nav => m_Nav;
        public BotSenses Senses => m_Senses;
        public System.Random Rng => m_Rng;
        public float PounceRange => m_PounceRange;

        /// <summary>QA·통계 — 벽에 붙박인 봇을 잡는다.</summary>
        public float TravelledMeters => m_Nav != null ? m_Nav.TravelledMeters : 0f;

        /// <summary>지금 무엇을 하는 중인가. 트리의 잎이 적어 넣는다(QA 표시용).</summary>
        public string Activity { get; set; } = "Idle";

        /// <summary>이 봇이 상대를 처음 찾기까지 걸린 시간(초). 못 찾았으면 -1.
        /// **소리 밸런스의 답이 이 숫자다.**</summary>
        public float FirstSightSeconds { get; private set; } = -1f;

        /// <summary>
        /// 처음 봤을 때의 거리(m). 못 봤으면 -1.
        /// **정직성 검사에 쓴다** — 맵에서 가능한 최장 직선보다 멀리서 "봤다"면 시야 판정이 새는 것이다.
        /// </summary>
        public float FirstSightDistance { get; private set; } = -1f;

        /// <summary>QA — 길찾기 진단.</summary>
        public string NavDiagnostics => m_Nav != null ? m_Nav.Diagnostics : "";

        public BotAgent(NetworkObject _object, RoleId _role, Reservations _reservations, BotOrders _orders)
        {
            m_Object = _object;
            m_Role = _role;
            m_Reservations = _reservations;
            m_Orders = _orders;

            m_Move = _object.GetComponent<PlayerMovement>();
            m_Sneak = _object.GetComponent<Sneak>();
            m_Jailed = _object.GetComponent<JailedTag>();
            m_Health = _object.GetComponent<Health>();
            m_Pounce = _object.GetComponent<CatPounce>();
            m_Dash = _object.GetComponent<MouseDash>();
            m_Gun = _object.GetComponent<MousePopgun>();
            m_Job = _object.GetComponent<MouseJob>();
            m_Recruit = _object.GetComponent<GangRecruit>();
            m_Skill = _object.GetComponent<CatSkill>();

            m_Nav = new BotNavigator(_object.transform, m_Move);

            // 시야각은 **그 역할의 카메라 그대로**다 — 봇이 못 보는 건 사람도 못 보는 것이어야 한다
            Info.PlayerInfo info = Info.InfoManager.Instance.PlayerOf(_role);
            m_Senses = new BotSenses(_object.transform, _role, info.Fov);
            m_PounceRange = info.PounceRange;

            // 배회는 봇마다 달라야 한다 — 같은 씨앗이면 전원이 같은 자리로 몰려 맵이 안 쓰인다.
            // 오브젝트 id로 씨앗을 주므로 같은 판을 다시 돌리면 같은 결과가 나온다(재현성).
            m_Rng = new System.Random((int)(_object.NetworkObjectId * 7919 + 13));

            m_Shopper = new BotShopper(
                _object.transform, _object.GetComponent<ShopController>(),
                _object.GetComponent<CheeseWallet>(), _object.GetComponent<CatClass>());

            m_Tree = _role == RoleId.Cat ? BotBrainTrees.BuildCat(this) : BotBrainTrees.BuildMouse(this);
        }

        public void Tick(float _deltaSeconds, CheeseSpotDirector _spots, SoundLog _sound)
        {
            if (!Alive) return;
            m_Lived += _deltaSeconds;
            m_Spots = _spots;

            // 갇힌 쥐는 아무것도 못 한다 — 사람도 입력이 막힌다
            if (m_Jailed != null && m_Jailed.Jailed)
            {
                Activity = "Jailed";
                m_Nav.Stop();
                m_SpotIndex = -1;
                // 예약을 놓는다 — 구출자가 갇힌 채 감옥 예약을 쥐고 있으면
                // 다른 쥐가 그 감옥을 영원히 안 간다(구출 교착)
                if (m_Reservations != null) m_Reservations.Release(Id);
                m_Nav.Tick(_deltaSeconds);
                return;
            }

            m_Timer -= _deltaSeconds;
            if (m_Timer <= 0f)
            {
                m_Timer = DECIDE_INTERVAL;
                m_Senses.Observe(_sound, Time.time);
                RefreshLead();
                m_Tree.Tick(DECIDE_INTERVAL);
            }

            m_Nav.Tick(_deltaSeconds);
        }

        /// <summary>판이 끝나거나 봇이 사라질 때 — 입력을 놓아 준다.</summary>
        public void Release()
        {
            if (m_Move != null) m_Move.DevSetMoveInput(Vector3.zero);
            if (m_Sneak != null) m_Sneak.DevSetSneak(false);
            if (m_Reservations != null) m_Reservations.Release(Id);
        }

        // ---- 트리의 잎이 쓰는 손잡이 ----

        /// <summary>지금 눈에 보이는 상대. 없으면 null. **소리로 안 것은 여기 안 나온다.**</summary>
        public Transform VisiblePrey()
        {
            Transform prey = m_Senses.NearestVisible();
            if (prey != null && FirstSightSeconds < 0f)
            {
                FirstSightSeconds = m_Lived;
                FirstSightDistance = DistanceTo(prey.position);
            }
            return prey;
        }

        public float DistanceTo(Vector3 _point) => Flat(_point - Position).magnitude;

        public bool HasLead => m_HasLead;
        public Vector3 Lead => m_Lead;

        /// <summary>지금 받은 명령. 지휘관이 없으면 <see cref="BotOrder.None"/>.</summary>
        public BotOrder Order => m_Orders != null ? m_Orders.Get(Id) : BotOrder.None;

        /// <summary>여기로 간다고 알린다 — 다른 봇이 같은 자리를 덜 고르게 된다.</summary>
        public void Reserve(Vector3 _point)
        {
            if (m_Reservations != null) m_Reservations.Claim(Id, _point);
        }

        /// <summary>남이 맡은 자리와 겹치는 정도(1이면 안 겹침).</summary>
        public float ReservationWeight(Vector3 _point)
        {
            return m_Reservations != null ? m_Reservations.WeightAt(_point, Id) : 1f;
        }

        public void SetSneak(bool _on)
        {
            if (m_Sneak != null) m_Sneak.DevSetSneak(_on);
        }

        /// <summary>그 자리가 위협에게 안 보이는가 — 도주 목표를 고를 때 쓴다(FleeGoal의 델리게이트).</summary>
        public static bool IsHiddenFrom(Vector3 _threat, Vector3 _goal)
            => Combat.SightLine.IsBlocked(_threat, _goal);

        public void DoPounce()
        {
            if (m_Pounce != null) m_Pounce.DevPounce();
        }

        /// <summary>대시 — 안 샀거나 쿨다운이면 컴포넌트가 알아서 무시한다.</summary>
        public void DoDash()
        {
            if (m_Dash != null) m_Dash.DevDash();
        }

        // 미끼는 판 값이 나가는 소모품이다 — 매 판단 틱(0.5초)마다 사면 지갑이 즉시 마른다.
        // 사람도 한 추격에 하나 던진다. 카운터가 아니라 시계로 잰다(카운터 쿨다운의 함정).
        private const float DECOY_COOLDOWN_SECONDS = 10f;
        private float m_NextDecoyTime;

        /// <summary>도주 중 미끼 — 전방(도망 방향)에 서서 뒤따르는 고양이만 막는다.</summary>
        public void PlaceDecoyForFlee()
        {
            if (Time.time < m_NextDecoyTime) return;
            m_NextDecoyTime = Time.time + DECOY_COOLDOWN_SECONDS;
            m_Shopper.BuyDecoy();
        }

        public void UseSkill()
        {
            if (m_Skill != null) m_Skill.DevUse();
        }

        public void Shoot()
        {
            if (m_Gun != null) m_Gun.DevShootAtNearestCat();
        }

        /// <summary>깡패권을 주웠으면 곁에 온 쥐를 부하로 — 2단계를 한 번도 못 열면 그 규칙을 못 잰다.</summary>
        public void RecruitIfGang()
        {
            if (m_Recruit != null && m_Job != null && m_Job.Value == MouseJobId.Gang) m_Recruit.DevRecruit();
        }

        public void BuyForMouse() => m_Shopper.BuyForMouse();
        public void BuyForCat() => m_Shopper.BuyForCat();

        /// <summary>단서를 버린다(가 봤더니 없었다 / 길이 안 나온다).</summary>
        public void DropLead(bool _markUnreachable = false)
        {
            if (_markUnreachable)
            {
                m_BadLead = m_Lead;
                m_BadLeadUntil = Time.time + BAD_LEAD_SECONDS;
            }
            m_HasLead = false;
        }

        /// <summary>
        /// 지금 갈 만한 치즈 지점. 없으면 false.
        /// 자리는 안다(고정이고 화면에 크게 표시된다) — 마르거나 남이 쓰는 중인지는 라벨에 적혀 있다.
        /// </summary>
        public bool TryGetSpot(out Vector3 _spot)
        {
            CollectSpots();

            if (m_SpotIndex < 0 || m_SpotIndex >= m_Usable.Count || !m_Usable[m_SpotIndex])
            {
                m_SpotIndex = MouseBotLogic.ChooseSpot(m_Positions, m_Grades, m_Usable, Position);
            }

            if (m_SpotIndex < 0) { _spot = Vector3.zero; return false; }

            _spot = m_Positions[m_SpotIndex];
            return true;
        }

        public void ForgetSpot() => m_SpotIndex = -1;

        /// <summary>갇힌 동료가 있는 감옥 중 가장 가까운 곳. **자리는 안다** — 맵에 고정된 구조물이다.</summary>
        public bool TryGetJailWithAlly(out Vector3 _jail)
        {
            _jail = Vector3.zero;
            float bestSqr = float.MaxValue;
            bool found = false;

            foreach (JailedTag jailed in Object.FindObjectsByType<JailedTag>(FindObjectsSortMode.None))
            {
                if (!jailed.Jailed) continue;
                if (jailed.transform == m_Object.transform) continue;

                float sqr = (jailed.transform.position - Position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                _jail = jailed.transform.position;
                found = true;
            }
            return found;
        }

        // 소리 단서는 값이 떨어질 때까지 들고 간다 — 매번 새로 잡으면 제자리에서 떤다.
        // 길이 도저히 안 나온 자리는 한동안 무시한다(창고·기둥 앞에서 벽만 밀던 증상).
        private void RefreshLead()
        {
            if (m_Nav.GaveUp()) DropLead(true);

            if (m_Senses.HasLead && !IsBadLead(m_Senses.Lead))
            {
                m_Lead = m_Senses.Lead;
                m_HasLead = true;
                m_LeadSince = Time.time;
            }
            else if (m_HasLead && Time.time - m_LeadSince > CatBotLogic.GIVE_UP_SECONDS)
            {
                m_HasLead = false;
            }
        }

        private bool IsBadLead(Vector3 _lead)
        {
            if (Time.time >= m_BadLeadUntil) return false;
            return Vector3.Distance(_lead, m_BadLead) < BAD_LEAD_RADIUS;
        }

        private void CollectSpots()
        {
            m_Positions.Clear();
            m_Grades.Clear();
            m_Usable.Clear();
            if (m_Spots == null) return;

            ulong self = m_Object.NetworkObjectId;
            foreach (CheeseSpot spot in m_Spots.Spots)
            {
                if (spot == null) continue;
                m_Positions.Add(spot.transform.position);
                m_Grades.Add(spot.Grade);
                bool free = spot.EarnerClientId == CheeseSpotLogic.NO_EARNER
                            || spot.EarnerClientId == self;
                m_Usable.Add(!spot.Depleted && free);
            }
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v;
        }
    }
}
#endif
