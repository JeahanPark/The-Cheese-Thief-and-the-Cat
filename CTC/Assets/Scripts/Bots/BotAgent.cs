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
        /// <summary>판단 주기(초). 행동 트리가 한 틱에 쌓을 시간도 이 값이다.</summary>
        public const float DECIDE_INTERVAL = 0.5f;

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
        private readonly CheeseWallet m_Wallet;
        private readonly Reservations m_Reservations;
        private readonly BotOrders m_Orders;
        private readonly BtNode m_Tree;
        private readonly System.Random m_Rng;
        private readonly float m_PounceRange;
        private float m_MoveSpeed;
        private float m_PreySpeed; // 반대 역할의 이속 — 추격 경주 판정에 쓴다

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
            m_MoveSpeed = info.MoveSpeed;
            m_PreySpeed = Info.InfoManager.Instance.PlayerOf(
                _role == RoleId.Cat ? RoleId.Mouse : RoleId.Cat).MoveSpeed;

            // 배회는 봇마다 달라야 한다 — 같은 씨앗이면 전원이 같은 자리로 몰려 맵이 안 쓰인다.
            // 오브젝트 id로 씨앗을 주므로 같은 판을 다시 돌리면 같은 결과가 나온다(재현성).
            m_Rng = new System.Random((int)(_object.NetworkObjectId * 7919 + 13));

            m_Wallet = _object.GetComponent<CheeseWallet>();
            m_Shopper = new BotShopper(
                _object.transform, _object.GetComponent<ShopController>(),
                m_Wallet, _object.GetComponent<CatClass>());

            m_Tree = _role == RoleId.Cat ? BotBrainTrees.BuildCat(this) : BotBrainTrees.BuildMouse(this);
        }

        public void Tick(float _deltaSeconds, CheeseSpotDirector _spots, SoundLog _sound)
        {
            if (!Alive) return;
            m_Lived += _deltaSeconds;
            m_Spots = _spots;

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
            // 격자 셀 예약도 놓는다(2026-08-18 리뷰) — 안 놓으면 죽은 봇이 잡아 둔 칸이
            // 판이 끝날 때까지 남아, 아무도 안 다니는 복도를 산 봇들이 계속 우회한다.
            if (m_Nav != null) m_Nav.Stop();
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

        /// <summary>
        /// QA 전용 — 두뇌의 잠행 판단을 덮어쓴다. -1=평소(두뇌가 정한다) · 0=강제 해제 · 1=강제 잠행.
        ///
        /// 필요한 이유: 두뇌가 **매 틱** 잠행을 다시 정하므로(BotBrainTrees) 밖에서 한 번 켜 봐야
        /// 다음 틱에 꺼진다. 「살금살금이면 귀가 안 선다」를 재려면 잠행이 유지 시간(1.5초)보다
        /// 길게 붙어 있어야 한다(2026-09-08 실측: 안 잠그면 잠행 표본에 뛰던 순간이 섞인다).
        /// </summary>
        public static int DevSneakOverride = -1;

        public void SetSneak(bool _on)
        {
            if (DevSneakOverride >= 0) _on = DevSneakOverride == 1;
            if (m_Sneak != null) m_Sneak.DevSetSneak(_on);
        }

        /// <summary>그 자리가 위협에게 안 보이는가 — 도주 목표를 고를 때 쓴다(FleeGoal의 델리게이트).</summary>
        public static bool IsHiddenFrom(Vector3 _threat, Vector3 _goal)
            => Combat.SightLine.IsBlocked(_threat, _goal);

        /// <summary>
        /// **길이 부술 수 있는 벽에 막혔으면 그 벽을 때린다**(2026-08-18) — 요새 안에 든 쥐를 쫓는 길.
        ///
        /// 격자 A*가 1차(안 부수고)에 실패하고 2차(부수고)로 길을 냈을 때만 참이 된다. 즉
        /// **우회로가 있으면 절대 안 부순다** — 비용 상수를 만질 필요가 없다.
        /// 벽 앞까지는 평소처럼 걸어간다(경로의 마지막 꺾임점이 그 벽이다). 사거리에 들면 덮친다.
        /// </summary>
        /// <summary>포위 중인가 — 지금 길이 부술 수 있는 벽을 관통해 나 있다.</summary>
        public bool IsSieging => m_Nav != null && m_Nav.NeedsBreaking;

        /// <summary>
        /// **포위 결심을 유지할까**(2026-08-24) — 벽을 부수는 중에 쥐가 밖에 보여도,
        /// 추격이 진 경주면(쥐가 요새로 먼저 되돌아감) 벽을 계속 판다. 판정은
        /// <see cref="CatBotLogic.IsChaseWinnable"/>, 속도는 CSV 값 그대로.
        /// </summary>
        public bool ShouldKeepSieging()
        {
            if (!IsSieging) return false;
            Transform prey = VisiblePrey();
            if (prey == null) return false;
            if (!TryGetPreyRefuge(prey.position, out _, out float preyToFort)) return false;

            return !CatBotLogic.IsChaseWinnable(
                DistanceTo(prey.position), preyToFort, m_MoveSpeed, m_PreySpeed);
        }

        /// <summary>
        /// 쥐가 되돌아갈 은신처 — 쥐 기준 가장 가까운 **성한** 요새(피난 반경 안).
        /// 반쯤 깎인 요새를 거르는 기준은 쥐 봇의 피난 판정과 같다(같은 벽을 두 봇이
        /// 다르게 보면 판이 어긋난다).
        /// </summary>
        public bool TryGetPreyRefuge(Vector3 _preyPos, out Vector3 _fort, out float _preyDistance)
        {
            _fort = Vector3.zero;
            _preyDistance = float.MaxValue;
            bool found = false;
            foreach (var fort in Object.FindObjectsByType<Economy.FortressShield>(FindObjectsSortMode.None))
            {
                var hp = fort.GetComponent<Combat.Health>();
                if (hp != null && MouseBotLogic.IsRefugeSpent(hp.Hp, hp.MaxHp)) continue;

                float d = Flat(fort.transform.position - _preyPos).magnitude;
                if (d < _preyDistance)
                {
                    _preyDistance = d;
                    _fort = fort.transform.position;
                    found = true;
                }
            }
            return found && _preyDistance <= MouseBotLogic.FORT_REFUGE_RADIUS;
        }

        public bool TryBreakBlocking()
        {
            if (m_Nav == null || !m_Nav.NeedsBreaking) return false;
            if (DistanceTo(m_Nav.BreakTarget) > m_PounceRange) return false;

            Activity = "Siege";
            DoPounce();
            return true;
        }

        public void DoPounce()
        {
            if (m_Pounce != null) m_Pounce.DevPounce();
        }

        /// <summary>대시 — 안 샀거나 쿨다운이면 컴포넌트가 알아서 무시한다.</summary>
        public void DoDash()
        {
            if (m_Dash != null) m_Dash.DevDash();
        }

        // 요새는 판 값이 나가는 소모품이다 — 매 판단 틱(0.5초)마다 사면 지갑이 즉시 마른다.
        // 사람도 한 추격에 하나 세운다. 카운터가 아니라 시계로 잰다(카운터 쿨다운의 함정).
        private const float SHIELD_COOLDOWN_SECONDS = 10f;
        private float m_NextShieldTime;

        /// <summary>도주 중 요새 — 내 위치 중심 즉발 방패. 고양이는 밀려나고 3방 동안 시간을 번다.</summary>
        public void PlaceFortressForFlee()
        {
            if (Time.time < m_NextShieldTime) return;
            m_NextShieldTime = Time.time + SHIELD_COOLDOWN_SECONDS;
            m_Shopper.BuyFortressShield();
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

        // ---- 상점(계획 3 Task 6) ----

        /// <summary>상점을 목표로 잡았던 시간(초) · 그중 실제로 원반 안에 서 있었던 시간.</summary>
        public float ShopSeekSeconds { get; private set; }
        public float ShopInsideSeconds { get; private set; }

        /// <summary>상점 원반에 한 번이라도 들어갔다. **가려고 했는데 false면 도달 실패다**.</summary>
        public bool ShopReached { get; private set; }

        /// <summary>
        /// 지금 갈 상점. 없으면 false.
        ///
        /// 자리는 <see cref="Map.ShopSpot.Nearest"/>가 명부에서 준다 — 판마다 바뀌므로
        /// 좌표를 캐시하지 않는다. 갈지 말지는 <see cref="MouseBotLogic.WantsShopping"/>가 정한다.
        /// </summary>
        public bool TryGetShop(out Vector3 _shop)
        {
            _shop = Vector3.zero;
            if (m_Shopper == null) return false;

            TechId next = m_Shopper.NextWant();
            if (next == TechId.None) return false;
            if (!Map.ShopSpot.Nearest(Position, out Vector3 center)) return false;

            int cheese = m_Wallet != null ? m_Wallet.CheeseValue : 0;
            int cost = TechLogic.RowOf(next, 1).Cost;
            if (!MouseBotLogic.WantsShopping(next, cheese, cost, DistanceTo(center))) return false;

            _shop = center;
            return true;
        }

        /// <summary>
        /// 지금 상점 원반 안인가 — 도착 판정은 반경이지 <c>ARRIVE_DISTANCE</c>가 아니다.
        /// 거리로 재면 반경 밖에 서서 영원히 구매에 실패한다(서버 판정과 같은 식을 쓴다).
        /// </summary>
        public bool IsInsideShop() => Map.ShopSpot.AnyContains(Position);

        /// <summary>상점으로 가는 중이다 — 계측용 시간 적립(도달 실패를 여기서 센다).</summary>
        public void MarkShopping(bool _inside, float _seconds)
        {
            ShopSeekSeconds += _seconds;
            if (!_inside) return;

            ShopInsideSeconds += _seconds;
            ShopReached = true;
        }

        private readonly List<Vector3> m_Forts = new List<Vector3>();

        /// <summary>
        /// 도망칠 때 들어갈 요새. 서 있는 요새 중 **진입 경주에서 이길 수 있는** 가장 가까운 것 —
        /// 규칙은 <see cref="MouseBotLogic.ChooseRefuge"/>. 요새 자리는 화면에 크게 보이는
        /// 구조물이라 봇이 아는 게 반칙이 아니다.
        /// </summary>
        public bool TryGetRefugeFortress(Vector3 _threat, out Vector3 _fort)
        {
            m_Forts.Clear();
            foreach (var fort in Object.FindObjectsByType<Economy.FortressShield>(FindObjectsSortMode.None))
            {
                // **맞고 있는 요새는 은신처가 아니다**(2026-08-18). 고양이가 벽을 부수고 들어올 수
                // 있게 된 뒤로 요새는 안전지대가 아니라 '버는 시간'이다 — 반쯤 깎인 벽 뒤로
                // 뛰어들면 도망이 아니라 그 자리에서 죽는다. HP는 화면에 뜨는 값이라 봇이 알아도 된다.
                var hp = fort.GetComponent<Combat.Health>();
                if (hp != null && MouseBotLogic.IsRefugeSpent(hp.Hp, hp.MaxHp)) continue;

                m_Forts.Add(fort.transform.position);
            }

            int pick = MouseBotLogic.ChooseRefuge(m_Forts, Position, _threat);
            _fort = pick >= 0 ? m_Forts[pick] : Vector3.zero;
            return pick >= 0;
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
