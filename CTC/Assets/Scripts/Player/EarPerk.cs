using System.Collections.Generic;
using CTC.Runtime.Info;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 귀가 소리를 따라 **쫑긋한다** — 반대편 진영의 소리가 **실제로 들릴 때만**, 그것도
    /// **소리 하나가 새로 날 때마다 한 번씩** 그쪽으로 돌며 튕긴다.
    ///
    /// **없던 정보를 만들지 않는 것이 이 기능의 존재 이유다.** 동공 축소(기각)는
    /// 「고양이가 N m 안에 있다」를 새로 알려 줘서 살금살금(Sneak)을 배신했다. 귀는 반대다 —
    /// 이미 난 소리만 그림으로 바꾼다. 고양이가 살금살금 오면 발소리 반경이 20m→4m로 줄고,
    /// 그 밖의 쥐 귀는 **한 번도 안 튕긴다.** 이 성질을 깨는 최적화·편의는 넣지 않는다.
    ///
    /// **왜 「걷는 중」(상태)이 아니라 「한 걸음 났다」(이벤트)인가** (2026-09-10 실플레이 요구:
    /// 「좀더 눈에 띌 정도로 쫑긋쫑긋 · 새로운 소리가 들렸을 때만 · 쫑긋도 딜레이가」).
    /// 상태로 두면 상대가 걷는 내내 귀가 **가만히 서 있다** — 움직임이 없으니 눈에 안 띄고,
    /// 「새 소리」도 아니다. 발소리는 원래 <see cref="Audio.FootstepPlayer"/>가 걸음마다 내는
    /// 이산 이벤트라, 그 **걸음 하나하나**를 트리거로 삼으면 세 요구가 한꺼번에 풀린다.
    ///
    /// **「소리 덩어리」 단위로 묶지 않는 이유도 여기 있다**: 고양이가 계속 쫓아오는데 귀가
    /// 처음 한 번만 서고 멈추면 **「아직 나를 쫓는다」가 안 읽힌다.** 이 기능의 값어치가 바로
    /// 그 채널이므로(고양이는 쥐 귀를 보고 「들켰다」를 읽는다) 반드시 걸음 단위여야 한다.
    ///
    /// **네트워크 0**: NetworkBehaviour가 아니다. 걸음 이벤트도 일회성 소리 이벤트도 이미 모든
    /// 클라에서 각자 나므로, 각 클라가 제 화면의 모든 몸에 대해 같은 계산을 돌린다.
    ///
    /// **새 에셋 0**: 팩 리그의 `ear.L`·`ear.R` 본을 LateUpdate에서 덧돌린다.
    /// 클립·메시·머티리얼을 만들지 않는다(모션 0 제약을 안 깬다).
    ///
    /// 수치(각도·딜레이·서는 시간·풀리는 시간·조준 속도)는 BalanceInfo.csv가 원본.
    /// </summary>
    public class EarPerk : MonoBehaviour
    {
        /// <summary>
        /// 반대쪽(소리 반대편) 귀가 도는 비율. 연출 상수라 CSV로 안 내린다 — 튜닝 대상이 아니라
        /// **좌우가 똑같이 도는 뻣뻣함**과 **먼 귀가 머리를 가로지르는 것**을 같이 막는 값이다.
        /// </summary>
        private const float FAR_EAR_RATIO = 0.45f;

        /// <summary>「쫑긋 한 번」으로 셀 문턱. 눈에 보이는 정점만 센다 — QA 계측 상수.</summary>
        private const float PEAK_THRESHOLD = 0.6f;

        /// <summary>
        /// 스폰돼 있는 플레이어 몸의 명부. **이제 소리를 찾는 데는 안 쓴다**(걸음이 이벤트로 온다) —
        /// 남은 용도는 QA가 「반대편 몸이 어디 있나」를 O(n)으로 훑는 색인이다.
        /// OnEnable/OnDisable로 붙고 떨어져 판 도중 난입·퇴장을 그대로 탄다.
        /// </summary>
        private static readonly List<EarPerk> s_All = new List<EarPerk>();

        [SerializeField] private string m_EarRightBoneName = "ear.R";
        [SerializeField] private string m_EarLeftBoneName = "ear.L";

        private Transform m_EarRight;
        private Transform m_EarLeft;
        private float m_LocalXRight;   // 몸 로컬 x — 어느 쪽 귀인지(이름이 아니라 자리가 정한다)
        private float m_LocalXLeft;

        private NetworkObject m_NetObject;
        private RoleId m_Role = RoleId.None;
        private RoleId m_Enemy = RoleId.None;

        private float m_TurnMaxDeg;
        private float m_PerkDeg;
        private float m_DelaySeconds;
        private float m_RiseSeconds;
        private float m_FallSeconds;
        private float m_FollowSpeed;

        private float m_Aim;        // 소리 쪽 조준각(도) — 부드럽게 따라간다
        private float m_Perk;       // 지금 쫑긋 세기 0~1 (곡선값)
        private float m_Turn;       // 실제로 적용 중인 좌우 각 = 조준각 × 쫑긋

        // 소리 하나 = 충격 하나. 곡선은 이 시각에서 다시 시작한다.
        private bool m_HasImpulse;
        private float m_ImpulseAt = float.NegativeInfinity;
        private float m_ImpulseFrom;      // 트리거 순간의 쫑긋값 — 이어서 올라간다(안 튄다)
        private Vector3 m_ImpulsePosition;
        private float m_ImpulseDistance;
        private bool m_ImpulseIsStep;

        private int m_Peaks;          // 눈에 띄게 튕긴 횟수(보조 지표)
        private int m_StepImpulses;   // 들린 걸음 수
        private int m_ShotImpulses;   // 들린 일회성 소리 수

        private bool m_Ready;

        /// <summary>QA 전용 — 지금 적용 중인 좌우 각(도). 0이면 귀가 안 돌아간 것이다.</summary>
        public float DevTurnDegrees => m_Turn;

        /// <summary>QA 전용 — 지금 쫑긋 세기 0~1. **순간값이라 걸음 사이엔 0에 가깝다.**</summary>
        public float DevPerk => m_Perk;

        /// <summary>QA 전용 — 좌우 귀에 **실제로 들어간** 각(도). 먼 귀는 덜 돈다.</summary>
        public float DevTurnRight => m_Turn * EarPerkLogic.SideScale(m_Turn, m_LocalXRight, FAR_EAR_RATIO);

        /// <summary>QA 전용 — 왼귀에 실제로 들어간 각(도).</summary>
        public float DevTurnLeft => m_Turn * EarPerkLogic.SideScale(m_Turn, m_LocalXLeft, FAR_EAR_RATIO);

        /// <summary>
        /// QA 전용 — **눈에 띄게 튕긴 횟수**(문턱 아래로 풀렸다 다시 정점에 닿은 수).
        ///
        /// 「지금 서 있나」로는 계약을 못 잰다 — 걸음 단위가 된 뒤로 순간값 표본은 대부분 0이다
        /// (걸음 사이엔 풀려 있으니까). 다만 이 값은 **소리가 아주 촘촘하면 적게 나온다**:
        /// 풀릴 틈 없이 다시 트리거되면 교차가 안 일어난다(귀는 계속 서 있고, 그건 맞는 그림이다).
        /// 그래서 **계약의 주 계측값은 <see cref="DevStepImpulses"/>**이고 이 값은 보조다.
        /// </summary>
        public int DevPerkPeaks => m_Peaks;

        /// <summary>
        /// QA 전용 — **들린 걸음 수**. 새 계약의 주 계측값이다:
        /// 뛸 때 N회 이상 · **살금살금이면 0회**. 잠행이 줄이는 것이 정확히 이 수다.
        /// </summary>
        public int DevStepImpulses => m_StepImpulses;

        /// <summary>
        /// QA 전용 — 들린 **일회성 소리** 수(덮치기·스킬 등). 잠행은 이걸 안 줄인다 —
        /// 그래서 걸음과 나눠 세야 「잠행이 깨졌다」를 오독하지 않는다.
        /// </summary>
        public int DevShotImpulses => m_ShotImpulses;

        /// <summary>QA 전용 — 마지막 충격의 종류 "step" | "shot" | "none".</summary>
        public string DevLastKind => !m_HasImpulse ? "none" : m_ImpulseIsStep ? "step" : "shot";

        /// <summary>QA 전용 — 셈을 0으로. 창을 열고 그 안에서 몇 번 쫑긋했나를 재려고.</summary>
        public void DevResetCounters()
        {
            m_Peaks = 0;
            m_StepImpulses = 0;
            m_ShotImpulses = 0;
        }

        /// <summary>QA 전용 — 오른귀 월드 위치(고정 시점 근접 촬영의 초점).</summary>
        public Vector3 DevEarWorldRight => m_EarRight != null ? m_EarRight.position : transform.position;

        /// <summary>QA 전용 — 이 몸의 역할.</summary>
        public RoleId DevRole => m_Role;

        /// <summary>QA 전용 — 스폰돼 있는 몸 전부(반대편을 찾아 거리를 재는 쪽이 쓴다).</summary>
        public static IReadOnlyList<EarPerk> DevAll => s_All;

        private void Awake()
        {
            m_NetObject = GetComponent<NetworkObject>();

            var tag = GetComponent<Combat.RoleTag>();
            m_Role = tag != null ? tag.Role : RoleId.None;
            m_Enemy = m_Role == RoleId.Mouse ? RoleId.Cat : m_Role == RoleId.Cat ? RoleId.Mouse : RoleId.None;

            ReadTuning();

            Build();
        }

        /// <summary>
        /// CSV 수치를 캐시로 옮긴다. 귀 값은 전부 **매 프레임 쓰는 것**이라 여기만 다시 부르면 된다 —
        /// 개발용 조절기 창(<c>Tools/CTC/Dev/눈·귀 수치 조절기</c>)이 그 경로를 쓴다.
        /// </summary>
        private void ReadTuning()
        {
            BalanceInfo balance = InfoManager.Instance.Balance;
            m_TurnMaxDeg = balance.EarTurnMaxDeg;
            m_PerkDeg = balance.EarPerkDeg;
            m_DelaySeconds = balance.EarPerkDelaySeconds;
            m_RiseSeconds = balance.EarPerkRiseSeconds;
            m_FallSeconds = balance.EarPerkFallSeconds;
            m_FollowSpeed = balance.EarFollowSpeed;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>**CSV 수치를 지금 다시 읽는다**(2026-09-10) — 조절기 창의 진입점.</summary>
        public void DevReloadTuning()
        {
            ReadTuning();
        }
#endif

        private void Build()
        {
            m_EarRight = FindDeep(transform, m_EarRightBoneName);
            m_EarLeft = FindDeep(transform, m_EarLeftBoneName);
            if (m_EarRight == null && m_EarLeft == null)
            {
                Debug.LogWarning("[EarPerk] 귀 본을 못 찾았다 — 귀를 안 세운다: " +
                                 m_EarRightBoneName + " / " + m_EarLeftBoneName);
                return;
            }

            // 좌우가 어느 쪽인지는 이름이 아니라 **몸 로컬 x**가 정한다 — 모델을 갈아 끼워
            // .L/.R 이름이 뒤집혀도 귀가 엇갈려 돌지 않는다.
            if (m_EarRight != null) m_LocalXRight = transform.InverseTransformPoint(m_EarRight.position).x;
            if (m_EarLeft != null) m_LocalXLeft = transform.InverseTransformPoint(m_EarLeft.position).x;

            m_Ready = true;
        }

        // 명부는 준비 여부와 무관하게 붙인다 — 귀 본이 없어도 **소리를 내는 몸**이기는 하다.
        private void OnEnable()
        {
            s_All.Add(this);
            Audio.SfxPlayer.Played += OnSfxPlayed;
            Audio.FootstepPlayer.Stepped += OnStepped;
        }

        private void OnDisable()
        {
            Audio.FootstepPlayer.Stepped -= OnStepped;
            Audio.SfxPlayer.Played -= OnSfxPlayed;
            s_All.Remove(this);
        }

        /// <summary>
        /// **걸음 하나** — 반대편 진영의 것만, 그것도 내 귀에 닿을 때만 받는다.
        /// 반경 판정은 봇 청각과 **같은 함수**를 쓴다(<see cref="Bots.BotSenseLogic.CanHear"/>).
        /// </summary>
        private void OnStepped(RoleId _role, Vector3 _position, float _designRange)
        {
            if (!m_Ready || m_Enemy == RoleId.None || _role != m_Enemy) return;
            if (!Bots.BotSenseLogic.CanHear(transform.position, _position, RangeOf(_designRange))) return;

            Trigger(_position, true);
        }

        /// <summary>
        /// 일회성 소리 — 반대편이 낼 수 있는 종류(<see cref="Bots.BotSenseLogic.IsPreySound"/>)만,
        /// 그것도 내 귀에 닿을 때만 받는다. 걸음과 **같은 트리거**다.
        /// </summary>
        private void OnSfxPlayed(Audio.SfxId _id, Vector3 _position, float _maxDistance)
        {
            if (!m_Ready || m_Enemy == RoleId.None) return;
            if (!Bots.BotSenseLogic.IsPreySound(_id, m_Enemy)) return;
            if (!Bots.BotSenseLogic.CanHear(transform.position, _position, RangeOf(_maxDistance))) return;

            Trigger(_position, false);
        }

        /// <summary>소리 하나 = 쫑긋 한 번. 곡선을 지금 값에서 다시 시작한다.</summary>
        private void Trigger(Vector3 _position, bool _isStep)
        {
            float now = Time.time;
            float distance = Flat(_position - transform.position).magnitude;

            // 같은 프레임에 둘 이상 들리면 순서에 기대면 안 된다 — 가까운 쪽이 이긴다.
            if (m_HasImpulse && Mathf.Approximately(m_ImpulseAt, now) &&
                !EarPerkLogic.Closer(distance, m_ImpulseDistance)) return;

            m_ImpulseFrom = m_Perk;
            m_ImpulseAt = now;
            m_ImpulsePosition = _position;
            m_ImpulseDistance = distance;
            m_ImpulseIsStep = _isStep;
            m_HasImpulse = true;

            if (_isStep) m_StepImpulses++;
            else m_ShotImpulses++;
        }

        // 애니메이터가 귀 본을 돌린 뒤에 얹어야 한 프레임 늦지 않는다.
        private void LateUpdate()
        {
            if (!m_Ready) return;

            float perk = m_HasImpulse
                ? EarPerkLogic.PerkEnvelope(Time.time - m_ImpulseAt, m_ImpulseFrom,
                                            m_DelaySeconds, m_RiseSeconds, m_FallSeconds)
                : 0f;
            if (EarPerkLogic.CrossedPeak(m_Perk, perk, PEAK_THRESHOLD)) m_Peaks++;
            m_Perk = perk;

            if (m_HasImpulse)
            {
                float target = EarPerkLogic.TurnDegrees(
                    transform.forward, m_ImpulsePosition - transform.position, m_TurnMaxDeg);
                m_Aim = EarPerkLogic.Follow(m_Aim, target, m_FollowSpeed, Time.deltaTime);
            }

            // 좌우 각도 쫑긋 곡선을 타고 나갔다 돌아온다 — 안 들리면 곡선이 0이라 각도 0이다.
            m_Turn = m_Aim * m_Perk;

            Apply();
        }

        /// <summary>
        /// 발소리·일회성 소리가 이 귀에 닿는 거리. **내 몸일 때만 「예민한 귀」 배율을 태운다.**
        ///
        /// <see cref="Audio.ListenerRange.Multiplier"/>는 *이 클라이언트*의 테크다. 남의 몸 귀에까지
        /// 곱하면 내가 산 테크로 남이 못 들은 소리에 그 사람 귀가 서는 그림이 되고, 그건
        /// 「귀는 없던 정보를 만들지 않는다」를 정면으로 어긴다(테크를 산 고양이가 쥐 귀를 부풀려
        /// 보게 된다). 남의 테크는 복제되지 않아 애초에 알 수도 없으므로 설계값을 쓴다.
        /// </summary>
        private float RangeOf(float _designRange)
        {
            return Mine() ? _designRange * Audio.ListenerRange.Multiplier : _designRange;
        }

        private bool Mine()
        {
            return m_NetObject != null &&
                   LocalControlLogic.Mine(m_NetObject.IsOwner, m_NetObject.IsPlayerObject);
        }

        /// <summary>
        /// 두 축으로만 돌린다 — **밑동에서 좌우로 스위블**(몸의 세로축)과 **앞으로 세우기**(몸의 좌우축).
        ///
        /// 처음엔 쫑긋을 몸의 **정면 축** 둘레 롤로 줬다가 사용자에게 「기괴한 각도로 꺾여 있다」를
        /// 들었다(2026-09-08). 실제 메시를 재 보니(`Tools/CTC/Dev/Probe Ear Geometry`) 귀는
        /// 이미 **위로** 뻗어 있고(모델 로컬 up 0.91~0.94) 바깥으로 20°쯤 기울어 있다 —
        /// 거기에 안쪽으로 롤을 주면 기울기를 넘어서 **귀가 머리를 가로질러 접힌다.**
        /// 앞으로 세우는 축은 좌우 귀가 같은 방향이라 어느 쪽도 정중선을 넘지 않는다.
        /// **각도를 키울 수 있게 된 것도 이 축 덕분이다**(2026-09-10: 26°→40° · 18°→32°) —
        /// 짧게 튕겼다 돌아오는 동작이라 큰 각도에서도 실루엣이 안 망가진다.
        /// </summary>
        private void Apply()
        {
            // 안 들리면 **한 톨도 안 건드린다** — 애니메이션 원본 그대로 둔다.
            if (Mathf.Abs(m_Turn) < 0.01f && m_Perk < 0.001f) return;

            Quaternion perk = Quaternion.AngleAxis(m_PerkDeg * m_Perk, transform.right);
            ApplyOne(m_EarRight, m_LocalXRight, perk);
            ApplyOne(m_EarLeft, m_LocalXLeft, perk);
        }

        private void ApplyOne(Transform _ear, float _localX, Quaternion _perk)
        {
            if (_ear == null) return;
            float turn = m_Turn * EarPerkLogic.SideScale(m_Turn, _localX, FAR_EAR_RATIO);
            _ear.rotation = Quaternion.AngleAxis(turn, transform.up) * _perk * _ear.rotation;
        }

        private static Transform FindDeep(Transform _root, string _name)
        {
            if (_root.name == _name) return _root;
            for (int i = 0; i < _root.childCount; i++)
            {
                Transform found = FindDeep(_root.GetChild(i), _name);
                if (found != null) return found;
            }
            return null;
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v;
        }
    }
}
