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
        private readonly BotSenses m_Senses;
        private readonly CatPounce m_Pounce;
        private readonly float m_PounceRange;
        private readonly ShopController m_Shop;
        private readonly CheeseWallet m_Wallet;
        private readonly MousePopgun m_Gun;
        private readonly MouseJob m_Job;
        private readonly GangRecruit m_Recruit;
        private readonly CatClass m_Class;
        private readonly CatSkill m_Skill;

        private bool m_BoughtDash, m_BoughtGun, m_BoughtWarehouse, m_BoughtFortress;

        /// <summary>도망칠 때 얼마나 멀리 잡을까 — 짧으면 고양이 사거리를 못 벗어난다.</summary>
        private const float FLEE_DISTANCE = 18f;

        private float m_Timer;
        private int m_SpotIndex = -1;
        private float m_LeadSince;
        private Vector3 m_Lead;
        private bool m_HasLead;

        private readonly List<Vector3> m_Positions = new List<Vector3>();
        private readonly List<int> m_Grades = new List<int>();
        private readonly List<bool> m_Usable = new List<bool>();

        public RoleId Role => m_Role;
        public NetworkObject Body => m_Object;
        public bool Alive => m_Object != null && m_Object.IsSpawned && (m_Health == null || !m_Health.IsDead);

        /// <summary>QA·통계 — 벽에 붙박인 봇을 잡는다.</summary>
        public float TravelledMeters => m_Nav != null ? m_Nav.TravelledMeters : 0f;
        public MouseBotLogic.State State { get; private set; } = MouseBotLogic.State.Wander;

        /// <summary>고양이 봇의 상태.</summary>
        public CatBotLogic.State CatState { get; private set; } = CatBotLogic.State.Patrol;

        /// <summary>이 봇이 상대를 처음 찾기까지 걸린 시간(초). 못 찾았으면 -1.
        /// **소리 밸런스의 답이 이 숫자다.**</summary>
        public float FirstSightSeconds { get; private set; } = -1f;

        private float m_Lived;

        // 배회는 봇마다 달라야 한다 — 같은 씨앗이면 전원이 같은 자리로 몰려 맵이 안 쓰인다.
        // 오브젝트 id로 씨앗을 주므로 같은 판을 다시 돌리면 같은 결과가 나온다(재현성).
        private System.Random m_Rng;

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
            m_Pounce = _object.GetComponent<CatPounce>();
            m_Nav = new BotNavigator(_object.transform, m_Move, _graph);

            // 시야각은 **그 역할의 카메라 그대로**다 — 봇이 못 보는 건 사람도 못 보는 것이어야 한다
            Info.PlayerInfo info = Info.InfoManager.Instance.PlayerOf(_role);
            m_Senses = new BotSenses(_object.transform, _role, info.Fov);
            m_PounceRange = info.PounceRange;
            m_Rng = new System.Random((int)(_object.NetworkObjectId * 7919 + 13));
            m_Shop = _object.GetComponent<ShopController>();
            m_Wallet = _object.GetComponent<CheeseWallet>();
            m_Gun = _object.GetComponent<MousePopgun>();
            m_Job = _object.GetComponent<MouseJob>();
            m_Recruit = _object.GetComponent<GangRecruit>();
            m_Class = _object.GetComponent<CatClass>();
            m_Skill = _object.GetComponent<CatSkill>();
        }

        public void Tick(float _deltaSeconds, CheeseSpotDirector _spots, SoundLog _sound)
        {
            if (!Alive) return;
            m_Lived += _deltaSeconds;

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
                m_Senses.Observe(_sound, Time.time);
                if (m_Role == RoleId.Mouse) DecideMouse(_spots);
                else DecideCat();
            }

            m_Nav.Tick(_deltaSeconds);
        }

        /// <summary>판이 끝나거나 봇이 사라질 때 — 입력을 놓아 준다.</summary>
        public void Release()
        {
            if (m_Move != null) m_Move.DevSetMoveInput(Vector3.zero);
            if (m_Sneak != null) m_Sneak.DevSetSneak(false);
        }

        /// <summary>
        /// 고양이 봇 — 보이면 쫓고, 사거리면 덮치고, 아니면 소리가 났던 쪽으로 간다.
        ///
        /// **좌표를 직접 읽지 않는다.** `BotSenses`를 통과한 것만 쓴다 — 그게 이 봇으로
        /// 소리 밸런스를 잴 수 있는 유일한 이유다.
        /// </summary>
        private void DecideCat()
        {
            Transform prey = m_Senses.NearestVisible();
            bool sees = prey != null;
            float distance = sees
                ? Vector3.Distance(m_Object.transform.position, prey.position)
                : float.MaxValue;

            // 소리 단서는 값이 떨어질 때까지 들고 간다 — 매번 새로 잡으면 제자리에서 떤다
            if (m_Senses.HasLead) { m_Lead = m_Senses.Lead; m_HasLead = true; m_LeadSince = Time.time; }
            else if (m_HasLead && Time.time - m_LeadSince > CatBotLogic.GIVE_UP_SECONDS) m_HasLead = false;

            CatState = CatBotLogic.Decide(sees, distance, m_PounceRange, m_HasLead);
            if (sees && FirstSightSeconds < 0f) FirstSightSeconds = m_Lived;

            BuyCatUpgrades();
            // 스킬은 적이 보일 때만 — 아무 때나 켜면 쿨다운만 태우고 소리로 위치만 알린다
            if (sees && m_Skill != null) m_Skill.DevUse();

            if (m_Sneak != null) m_Sneak.DevSetSneak(CatBotLogic.ShouldSneak(CatState));

            switch (CatState)
            {
                case CatBotLogic.State.Pounce:
                    m_Nav.Stop();
                    // 덮치기는 앞을 봐야 맞는다 — 표적 쪽으로 몸을 밀어 방향을 맞춘 뒤 친다
                    m_Nav.SetGoal(prey.position);
                    if (m_Pounce != null) m_Pounce.DevPounce();
                    break;
                case CatBotLogic.State.Chase:
                    m_Nav.SetGoal(prey.position);
                    break;
                case CatBotLogic.State.Investigate:
                    m_Nav.SetGoal(m_Lead);
                    // 도착했는데 아무도 없으면 단서를 버린다
                    if (Vector3.Distance(m_Object.transform.position, m_Lead) < 2f) m_HasLead = false;
                    break;
                default:
                    // 단서가 없으면 돌아다닌다 — 서 있으면 판에서 사라진 것과 같다
                    if (!m_Nav.IsMoving) m_Nav.SetWanderGoal(m_Rng);
                    break;
            }
        }

        // 쓸 만한 치즈 지점으로 가서 캔다. 위협이 보이면 도망치고, 갇힌 동료가 있으면 구하러 간다.
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

            Transform threat = m_Senses.NearestVisible();
            bool threatNear = threat != null || m_Senses.HasLead;
            Vector3 threatPos = threat != null ? threat.position : m_Senses.Lead;
            bool allyJailed = NearestJailWithAlly(out Vector3 jail);

            State = MouseBotLogic.Decide(threatNear, allyJailed, hasTarget, distance);

            // 잠행은 도망칠 때만 — 평소에 켜면 이속이 반이라 아무것도 못 한다
            if (m_Sneak != null) m_Sneak.DevSetSneak(State == MouseBotLogic.State.Flee);

            switch (State)
            {
                case MouseBotLogic.State.Flee:
                    Vector3 away = MouseBotLogic.FleeDirection(m_Object.transform.position, threatPos);
                    m_Nav.SetGoal(m_Object.transform.position + away * FLEE_DISTANCE);
                    if (m_Gun != null && threat != null) m_Gun.DevShootAtNearestCat();
                    break;
                case MouseBotLogic.State.Rescue:
                    m_Nav.SetGoal(jail);   // 반경 안에 5초 서 있으면 JailDirector가 알아서 푼다
                    break;
                case MouseBotLogic.State.Travel:
                    m_Nav.SetGoal(m_Positions[m_SpotIndex]);
                    break;
                case MouseBotLogic.State.Farm:
                    m_Nav.Stop();
                    break;
                default:
                    // 쓸 자리가 하나도 없다(전부 마르거나 남이 쓴다) — 돌아다니며 빈자리를 찾는다
                    if (!m_Nav.IsMoving) m_Nav.SetWanderGoal(m_Rng);
                    break;
            }

            BuyMouseItems();
            // 깡패권을 주웠으면 곁에 온 쥐를 부하로 — 2단계가 한 번도 안 열리면 그 규칙을 못 잰다
            if (m_Recruit != null && m_Job != null && m_Job.Value == MouseJobId.Gang) m_Recruit.DevRecruit();
        }

        // 갇힌 동료가 있는 감옥 중 가장 가까운 곳. **자리는 안다** — 감옥은 맵에 고정된 구조물이다.
        private bool NearestJailWithAlly(out Vector3 _jail)
        {
            _jail = Vector3.zero;
            float bestSqr = float.MaxValue;
            bool found = false;

            foreach (JailedTag jailed in Object.FindObjectsByType<JailedTag>(FindObjectsSortMode.None))
            {
                if (!jailed.Jailed) continue;
                if (jailed.transform == m_Object.transform) continue;

                float sqr = (jailed.transform.position - m_Object.transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                _jail = jailed.transform.position;
                found = true;
            }
            return found;
        }

        // 살 수 있으면 산다. 설치물은 제 자리에 둔다 — 자리 고르기는 사람의 판단이라 흉내 내지 않는다.
        private void BuyMouseItems()
        {
            if (m_Shop == null || m_Wallet == null) return;

            ShopItemId next = MouseBotLogic.NextPurchase(
                m_Wallet.CheeseValue, m_BoughtDash, m_BoughtGun, m_BoughtWarehouse, m_BoughtFortress);
            if (next == ShopItemId.None) return;

            if (ShopLogic.IsPlaceable(next)) m_Shop.BuyAt(next, m_Object.transform.position);
            else m_Shop.Buy(next);

            if (next == ShopItemId.Dash) m_BoughtDash = true;
            else if (next == ShopItemId.Slingshot) m_BoughtGun = true;
            else if (next == ShopItemId.Warehouse) m_BoughtWarehouse = true;
            else if (next == ShopItemId.Fortress) m_BoughtFortress = true;
        }

        // 공격·이속을 번갈아 올린다. 둘 다 만렙이면 전직 — 불은 판단이 필요 없는 유일한 전직이다.
        private void BuyCatUpgrades()
        {
            if (m_Shop == null || m_Wallet == null) return;

            if (m_Class != null && m_Class.CanAdvance && m_Class.Value == CatClassId.None)
            {
                m_Class.ChooseRpc(CatClassId.Fire);
                return;
            }

            if (m_Shop.CanApply(ShopItemId.CatAttack)
                && m_Wallet.CheeseValue >= ShopLogic.Find(ShopItemId.CatAttack).Cost)
            {
                m_Shop.Buy(ShopItemId.CatAttack);
                return;
            }
            if (m_Shop.CanApply(ShopItemId.CatSpeed)
                && m_Wallet.CheeseValue >= ShopLogic.Find(ShopItemId.CatSpeed).Cost)
            {
                m_Shop.Buy(ShopItemId.CatSpeed);
            }
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
