using System.Collections.Generic;
using CTC.Runtime.Info;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 귀가 소리를 따라 선다 — 반대편 진영의 소리가 **실제로 들릴 때만** 그쪽으로 돌고 쫑긋 선다.
    ///
    /// **없던 정보를 만들지 않는 것이 이 기능의 존재 이유다.** 동공 축소(기각)는
    /// 「고양이가 N m 안에 있다」를 새로 알려 줘서 살금살금(Sneak)을 배신했다. 귀는 반대다 —
    /// 이미 난 소리만 그림으로 바꾼다. 고양이가 살금살금 오면 발소리 반경이 20m→4m로 줄고,
    /// 그 밖의 쥐 귀는 **한 톨도 안 움직인다.** 이 성질을 깨는 최적화·편의는 넣지 않는다.
    ///
    /// **네트워크 0**: NetworkBehaviour가 아니다. 소리 이벤트(SfxPlayer.Played)도 발소리 상태
    /// (FootstepPlayer.IsStepping)도 이미 모든 클라에 있으므로, 각 클라가 제 화면의 모든 몸에 대해
    /// 같은 계산을 돌린다. 보내는 것도 받는 것도 없다.
    ///
    /// **새 에셋 0**: 팩 리그의 `ear.L`·`ear.R` 본을 LateUpdate에서 덧돌린다.
    /// 클립·메시·머티리얼을 만들지 않는다(모션 0 제약을 안 깬다).
    ///
    /// 수치(최대 각도·쫑긋 각·유지·따라가는 속도)는 BalanceInfo.csv가 원본.
    /// </summary>
    public class EarPerk : MonoBehaviour
    {
        /// <summary>
        /// 반대쪽(소리 반대편) 귀가 도는 비율. 연출 상수라 CSV로 안 내린다 — 튜닝 대상이 아니라
        /// **좌우가 똑같이 도는 뻣뻣함**과 **먼 귀가 머리를 가로지르는 것**을 같이 막는 값이다.
        /// </summary>
        private const float FAR_EAR_RATIO = 0.45f;

        /// <summary>
        /// 소리를 낼 수 있는 몸의 명부. 매 프레임 FindObjectsByType을 도는 대신 여기 등록한다
        /// (CTC 규칙 3 — Update에서 오브젝트 탐색 금지).
        ///
        /// **판 도중 들어오고 나가는 것을 감당한다**: 스폰 시 1회 캐시가 아니라 OnEnable/OnDisable로
        /// 붙고 떨어지므로, 늦게 들어온 고양이도 그 순간부터 명부에 있다(계획 2a의 StickyPatch가
        /// 남긴 「판 도중 들어온 고양이가 안 걸린다」 부채를 되풀이하지 않는다).
        ///
        /// 이 컴포넌트는 쥐·고양이 두 프리팹 **양쪽**에 붙는다 — 그래서 명부가 곧 플레이어 몸 전부다.
        /// 강아지는 제3세력이라 여기 없고, 짖음도 반대편 소리가 아니라(IsPreySound가 뺀다) 귀를 안 세운다.
        /// </summary>
        private static readonly List<EarPerk> s_All = new List<EarPerk>();

        [SerializeField] private string m_EarRightBoneName = "ear.R";
        [SerializeField] private string m_EarLeftBoneName = "ear.L";

        private Transform m_EarRight;
        private Transform m_EarLeft;
        private float m_LocalXRight;   // 몸 로컬 x — 어느 쪽 귀인지(이름이 아니라 자리가 정한다)
        private float m_LocalXLeft;

        private NetworkObject m_NetObject;
        private Audio.FootstepPlayer m_Steps;
        private RoleId m_Role = RoleId.None;
        private RoleId m_Enemy = RoleId.None;

        private float m_TurnMaxDeg;
        private float m_PerkDeg;
        private float m_HoldSeconds;
        private float m_FollowSpeed;

        private float m_Turn;       // 지금 적용 중인 좌우 각(도)
        private float m_Perk;       // 지금 적용 중인 쫑긋 세기 0~1

        private Vector3 m_HeardPosition;
        private float m_HeardAt = float.NegativeInfinity;
        private bool m_HeardIsStep;   // 지금 붙들고 있는 소리가 발소리인가(아니면 일회성) — QA 구분용
        private bool m_StepAudibleNow; // 이 프레임에 반대편 발소리가 실제로 닿았나 — QA 판정의 근거

        // 일회성 소리는 한 순간뿐이라 발소리와 나란히 비교할 수 없다 — 유지 시간 동안 살아 있는
        // 후보로 따로 들고 있다가 매 프레임 발소리와 거리로 겨룬다.
        private Vector3 m_ShotPosition;
        private float m_ShotAt = float.NegativeInfinity;

        private bool m_Ready;

        /// <summary>QA 전용 — 지금 적용 중인 좌우 각(도). 0이면 귀가 안 돌아간 것이다.</summary>
        public float DevTurnDegrees => m_Turn;

        /// <summary>QA 전용 — 지금 적용 중인 쫑긋 세기 0~1. **살금살금 검증의 숫자 근거**다.</summary>
        public float DevPerk => m_Perk;

        /// <summary>QA 전용 — 좌우 귀에 **실제로 들어간** 각(도). 먼 귀는 덜 돈다.</summary>
        public float DevTurnRight => m_Turn * EarPerkLogic.SideScale(m_Turn, m_LocalXRight, FAR_EAR_RATIO);

        /// <summary>QA 전용 — 왼귀에 실제로 들어간 각(도).</summary>
        public float DevTurnLeft => m_Turn * EarPerkLogic.SideScale(m_Turn, m_LocalXLeft, FAR_EAR_RATIO);

        /// <summary>QA 전용 — 지금 붙들고 있는 소리가 있는가.</summary>
        public bool DevHearing => EarPerkLogic.Holding(Time.time - m_HeardAt, m_HoldSeconds);

        /// <summary>
        /// QA 전용 — 붙들고 있는 소리의 종류 "step"(발소리) | "shot"(일회성) | "none".
        /// **잠행 검증에 반드시 필요하다**: 잠행은 발소리만 줄인다. 덮치기·요새 파괴음은
        /// 잠행 중에도 그대로 들리므로 "귀가 섰다"만 보면 잠행이 깨진 것처럼 오독한다.
        /// </summary>
        public string DevHeardKind => !DevHearing ? "none" : m_HeardIsStep ? "step" : "shot";

        /// <summary>
        /// QA 전용 — **이 프레임에** 반대편 발소리가 실제로 귀에 닿았는가.
        /// <see cref="DevHearing"/>은 유지 시간(1.5초) 동안 참으로 남아 「지금 들리나」와 다르다 —
        /// 잠행 판정은 순간값으로 재야 뛰던 직전의 잔상이 안 섞인다.
        /// </summary>
        public bool DevStepAudible => m_StepAudibleNow;

        /// <summary>QA 전용 — 붙들고 있는 소리의 위치(없으면 자기 자리).</summary>
        public Vector3 DevHeardPosition => DevHearing ? m_HeardPosition : transform.position;

        /// <summary>QA 전용 — 오른귀 월드 위치(고정 시점 근접 촬영의 초점).</summary>
        public Vector3 DevEarWorldRight => m_EarRight != null ? m_EarRight.position : transform.position;

        /// <summary>QA 전용 — 이 몸의 역할.</summary>
        public RoleId DevRole => m_Role;

        /// <summary>QA 전용 — 스폰돼 있는 몸 전부(반대편을 찾아 거리를 재는 쪽이 쓴다).</summary>
        public static IReadOnlyList<EarPerk> DevAll => s_All;

        private void Awake()
        {
            m_NetObject = GetComponent<NetworkObject>();
            m_Steps = GetComponentInChildren<Audio.FootstepPlayer>(true);

            var tag = GetComponent<Combat.RoleTag>();
            m_Role = tag != null ? tag.Role : RoleId.None;
            m_Enemy = m_Role == RoleId.Mouse ? RoleId.Cat : m_Role == RoleId.Cat ? RoleId.Mouse : RoleId.None;

            BalanceInfo balance = InfoManager.Instance.Balance;
            m_TurnMaxDeg = balance.EarTurnMaxDeg;
            m_PerkDeg = balance.EarPerkDeg;
            m_HoldSeconds = balance.EarHoldSeconds;
            m_FollowSpeed = balance.EarFollowSpeed;

            Build();
        }

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
        }

        private void OnDisable()
        {
            Audio.SfxPlayer.Played -= OnSfxPlayed;
            s_All.Remove(this);
        }

        /// <summary>
        /// 일회성 소리 — 반대편이 낼 수 있는 종류(<see cref="Bots.BotSenseLogic.IsPreySound"/>)만,
        /// 그것도 내 귀에 닿을 때만 받는다. 봇 청각과 **같은 규칙**을 쓴다.
        /// </summary>
        private void OnSfxPlayed(Audio.SfxId _id, Vector3 _position, float _maxDistance)
        {
            if (!m_Ready || m_Enemy == RoleId.None) return;
            if (!Bots.BotSenseLogic.IsPreySound(_id, m_Enemy)) return;
            if (!Bots.BotSenseLogic.CanHear(transform.position, _position, RangeOf(_maxDistance))) return;

            m_ShotPosition = _position;
            m_ShotAt = Time.time;
        }

        // 애니메이터가 귀 본을 돌린 뒤에 얹어야 한 프레임 늦지 않는다.
        private void LateUpdate()
        {
            if (!m_Ready) return;

            float now = Time.time;
            if (Listen(now, out Vector3 source, out bool isStep))
            {
                m_HeardPosition = source;
                m_HeardAt = now;
                m_HeardIsStep = isStep;
            }

            bool holding = EarPerkLogic.Holding(now - m_HeardAt, m_HoldSeconds);
            float targetTurn = holding
                ? EarPerkLogic.TurnDegrees(transform.forward, m_HeardPosition - transform.position, m_TurnMaxDeg)
                : 0f;
            float targetPerk = holding ? 1f : 0f;

            float dt = Time.deltaTime;
            m_Turn = EarPerkLogic.Follow(m_Turn, targetTurn, m_FollowSpeed, dt);
            m_Perk = EarPerkLogic.Follow(m_Perk, targetPerk, m_FollowSpeed, dt);

            Apply();
        }

        /// <summary>지금 들리는 것 중 **가장 가까운** 소리의 자리. 아무것도 안 들리면 false.</summary>
        private bool Listen(float _now, out Vector3 _source, out bool _isStep)
        {
            _source = Vector3.zero;
            _isStep = false;
            m_StepAudibleNow = false;
            if (m_Enemy == RoleId.None) return false;

            Vector3 me = transform.position;
            float best = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < s_All.Count; i++)
            {
                EarPerk other = s_All[i];
                if (other == null || other == this || other.m_Role != m_Enemy) continue;

                Audio.FootstepPlayer steps = other.m_Steps;
                if (steps == null || !steps.IsStepping) continue;

                Vector3 position = other.transform.position;
                if (!Bots.BotSenseLogic.CanHear(me, position, FootstepRangeOf(steps))) continue;

                float distance = Flat(position - me).magnitude;
                if (found && !EarPerkLogic.Closer(distance, best)) continue;
                found = true;
                best = distance;
                _source = position;
                _isStep = true;
                m_StepAudibleNow = true;
            }

            if (EarPerkLogic.Holding(_now - m_ShotAt, m_HoldSeconds))
            {
                float distance = Flat(m_ShotPosition - me).magnitude;
                if (!found || EarPerkLogic.Closer(distance, best))
                {
                    found = true;
                    _source = m_ShotPosition;
                    _isStep = false;
                }
            }

            return found;
        }

        /// <summary>
        /// 발소리가 이 귀에 닿는 거리. <see cref="Audio.FootstepPlayer"/>의 두 값 중 어느 쪽을 쓰나 —
        /// **내 몸이면 HeardRange, 남의 몸이면 CurrentRange.**
        ///
        /// HeardRange는 CurrentRange에 「예민한 귀」테크 배율(<see cref="Audio.ListenerRange.Multiplier"/>)을
        /// 곱한 값인데, 그 배율은 **이 클라이언트의** 테크다. 남의 몸 귀에까지 곱하면 내가 산 테크로
        /// 남이 못 들은 소리에 그 사람 귀가 서는 그림이 되고, 그건 「귀는 없던 정보를 만들지 않는다」를
        /// 정면으로 어긴다(테크를 산 고양이가 쥐 귀를 부풀려 보게 된다). 남의 테크는 복제되지 않아
        /// 애초에 알 수도 없으므로 설계값을 쓴다.
        ///
        /// 그래서 기획서대로 **테크는 태우되 내 몸에만** 태운다 — 예민한 귀를 올린 사람은
        /// 제 캐릭터의 귀가 더 자주 서는 것을 본다.
        /// </summary>
        private float FootstepRangeOf(Audio.FootstepPlayer _steps)
        {
            return Mine() ? _steps.HeardRange : _steps.CurrentRange;
        }

        /// <summary>일회성 소리 쪽 같은 규칙 — 이벤트가 주는 값은 배율이 안 곱해진 설계 반경이다.</summary>
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
        /// 거기에 안쪽으로 35°를 더 주면 기울기를 넘어서 **귀가 머리를 가로질러 접힌다.**
        /// 앞으로 세우는 축은 좌우 귀가 같은 방향이라 어느 쪽도 정중선을 넘지 않는다.
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
