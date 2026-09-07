using System.Collections.Generic;
using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 소유 클라이언트에서만 입력을 읽어 CharacterController로 이동한다.
    /// 이동은 카메라 yaw 기준(camera-relative). 몸은 이동 입력이 있을 때만
    /// 이동 방향으로 회전한다 — 정지 시 카메라만 도는 '훔쳐보기' 성립.
    /// 위치 동기화는 ClientNetworkTransform(소유자 권위)이 담당한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : NetworkBehaviour
    {
        private const float GRAVITY = -20f;

        // 둘 다 PlayerInfo.csv가 원본 — 생성기가 프리팹에 굽는다(씬 재생성 필요).
        [SerializeField] private float m_MoveSpeed = 5f;
        [SerializeField] private float m_TurnSpeedDegPerSec = 720f;

        /// <summary>프리팹에 구운 기본 이속(강화·잠행 전) — QA가 CSV가 아니라 실물을 재는 데 쓴다.</summary>
        public float BaseSpeed => m_MoveSpeed;

        // 이속 강화 스택(상점, 고양이). 서버 기록·소유자 적용.
        private readonly NetworkVariable<int> m_SpeedStacks = new NetworkVariable<int>();

        // 바깥에서 거는 이속 배율(끈끈이 등). **서버가 쓰고 소유자가 읽는다** —
        // 이동은 소유자 권위(ClientNetworkTransform)라 값이 복제돼야 그 몸이 실제로 느려진다.
        private readonly NetworkVariable<float> m_ExternalSpeedMul = new NetworkVariable<float>(1f);

        // 감속을 건 쪽마다 한 칸(서버 전용 — 복제되는 건 위의 합산 결과 하나뿐이다).
        // 칸이 하나뿐이던 시절엔 겹친 장판 둘 중 나중에 나간 쪽이 1을 덮어써서, 아직 밟고 있는
        // 장판 위에서 고양이가 제 속도로 달렸다. 이제 건 쪽(NetworkObjectId)마다 기록하고
        // 가장 센 것 하나를 고른다.
        private readonly Dictionary<ulong, float> m_SlowSources = new Dictionary<ulong, float>();

        private CharacterController m_Controller;
        private Sneak m_Sneak;      // 고양이·쥐 공통(2026-08-10 쥐로 확장)
        private TechSlots m_TechSlots;  // Awake에서 한 번 — 속도 식은 매 프레임 돈다
        private float m_VerticalVelocity;
        private Vector3 m_DevMoveInput; // QA 전용 자동 이동 방향(월드, 정규화)

        public int SpeedStacks => m_SpeedStacks.Value;

        /// <summary>강화 중첩 스트림 — 전직 창이 "다 올렸나"를 폴링 없이 안다(규칙 3).</summary>
        public R3.Observable<int> SpeedStacksChanged =>
            m_SpeedStacks.AsObservableWithCurrent();

        /// <summary>QA 전용 — 키보드 대신 이 방향으로 걷는다. 영벡터면 해제.</summary>
        public void DevSetMoveInput(Vector3 _worldDirection)
        {
            m_DevMoveInput = _worldDirection;
        }

        /// <summary>바깥 감속 배율(1 = 없음) — QA·핫바가 실제로 걸렸는지 본다.</summary>
        public float ExternalSpeedMultiplier => m_ExternalSpeedMul.Value;

        /// <summary>
        /// 서버 전용(끈끈이 장판 등). 건 쪽의 <c>NetworkObjectId</c>를 열쇠로 준다 —
        /// 같은 열쇠로 다시 걸면 값만 바뀐다. 나갈 때 <see cref="ServerRemoveSlowSource"/>로 반드시 뗀다.
        /// </summary>
        public void ServerAddSlowSource(ulong _sourceId, float _mul)
        {
            if (!IsServer) return;
            m_SlowSources[_sourceId] = _mul;
            ApplySlowSources();
        }

        /// <summary>서버 전용 — 그 쪽이 건 감속만 뗀다. 남은 게 있으면 그 중 가장 센 것이 이어진다.</summary>
        public void ServerRemoveSlowSource(ulong _sourceId)
        {
            if (!IsServer) return;
            if (!m_SlowSources.Remove(_sourceId)) return;
            ApplySlowSources();
        }

        // 복제되는 값은 언제나 "지금 걸린 것들 중 가장 센 배율" 하나다(하나도 없으면 1).
        private void ApplySlowSources()
        {
            m_ExternalSpeedMul.Value = StickyLogic.MinMultiplier(m_SlowSources.Values);
        }

        /// <summary>서버 전용(ShopController). 캡은 ShopLogic.MAX_UPGRADE_STACKS.</summary>
        public void ServerAddSpeedStack()
        {
            if (!IsServer || !ShopLogic.CanAddStack(m_SpeedStacks.Value)) return;
            m_SpeedStacks.Value++;
        }

        private void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_Sneak = GetComponent<Sneak>();
            m_TechSlots = GetComponent<TechSlots>();
        }

        /// <summary>
        /// 소유자 권위 ClientNetworkTransform은 소유 클라이언트의 로컬 초기 위치로
        /// 서버 스폰 위치를 덮어쓴다. 그래서 소유자가 스폰 시점에 로비 목록으로
        /// 자기 자리(서버와 동일한 결정적 공식)를 직접 계산해 텔레포트한다.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            // **로비 참가자에게만 해당한다**(2026-08-11). 측정용 봇은 붙일 클라이언트가 없어
            // 서버가 소유하는데, 그러면 `IndexOfClient(OwnerClientId)`가 **호스트의 자리**를
            // 가리켜 봇 전원이 호스트 스폰 지점으로 순간이동한다(실측: 봇 셋이 원점에 겹쳤다).
            // 봇의 자리는 세운 쪽이 이미 정해서 놓았으므로 건드리면 안 된다.
            if (!NetworkObject.IsPlayerObject) return;

            var lobby = FindFirstObjectByType<Lobby.LobbyController>();
            if (lobby == null) return;

            int idx = lobby.IndexOfClient(OwnerClientId);
            if (idx < 0) return;

            m_Controller.enabled = false;
            transform.position = lobby.SpawnPositionOf(idx);
            m_Controller.enabled = true;
        }

        /// <summary>
        /// 재시작 스윕 시 클라 측 파괴는 프레임 말로 지연된다 — 그 프레임 동안 남은 구 몸체가
        /// 같은 자리에 스폰되는 새 CC를 밀어내(depenetration) 바닥 밑으로 사출하는 간헐 낙하의
        /// 원인. despawn 즉시 콜라이더를 꺼서 차단한다.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            m_Controller.enabled = false;
        }

        private void Update()
        {
            // 내 몸이거나 서버가 조종하는 봇만 움직인다. 봇은 서버 소유라 IsOwner가 true인데
            // Mine으로만 막으면 봇이 한 발도 못 뗀다(2026-09-06 실측: 길은 있는데 이동 0m).
            if (!LocalControl.Drives(this)) return;

            // **키보드는 사람의 몸만 읽는다**(2026-09-01) — 안 그러면 내 WASD를 봇이 같이 먹는다.
            // 채팅 타이핑·옵션 창 중에는 WASD가 이동으로 새면 안 된다. 중력은 계속 먹어야 하므로
            // early-return이 아니라 입력만 0으로 둔다(공중에서 채팅 열면 떠 있게 되는 걸 막는다).
            float h = 0f, v = 0f;
            var kb = Keyboard.current;   // 신형 Input System 전용(activeInputHandler=1)
            if (LocalControl.Mine(this) && kb != null && !Core.UiFocus.BlocksGameplay)
            {
                h = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                    - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                v = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                    - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            }

            float camYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;
            Vector3 dir = MovementInput.ToWorldDirection(h, v, camYaw);

            // QA 자동 이동이 걸려 있으면 그 방향이 우선 — 지오메트리 통행 검수는
            // 텔레포트가 아니라 실제로 걸어봐야만 알 수 있다(벽을 통과하지 않으므로)
            if (m_DevMoveInput.sqrMagnitude > 0.0001f) dir = m_DevMoveInput;

            // 중력 — 접지 중엔 살짝 눌러 붙이고, 공중에선 가속 낙하
            if (m_Controller.isGrounded && m_VerticalVelocity < 0f) m_VerticalVelocity = -1f;
            m_VerticalVelocity += GRAVITY * Time.deltaTime;

            float speed = CurrentSpeed;
            if (m_Sneak != null && m_Sneak.Sneaking) speed *= m_Sneak.SpeedMultiplier;
            speed *= WindMultiplier();   // 바람 전직(v3.9) — 스킬 중에만 1이 아니다

            Vector3 motion = dir * speed;
            motion.y = m_VerticalVelocity;
            m_Controller.Move(motion * Time.deltaTime);

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, look, m_TurnSpeedDegPerSec * Time.deltaTime);
            }
        }


    
        /// <summary>
        /// 지금 걸을 수 있는 이속. 순서가 뜻이다: 기본 + 강화 + 빠른 발을 다 더한 **뒤에**
        /// 바깥 배율을 곱한다 — 끈끈이는 "지금 낼 수 있는 속도의 40%를 뺀다"지 "기본 속도만
        /// 깎는다"가 아니다. 잠행·바람 전직은 여기 없다: 둘 다 누르고 있는 동안만인 순간 값이라
        /// Update가 따로 곱한다(QA가 재는 것도 이 값이다 — TestGetMoveSpeedOf).
        /// </summary>
        public float CurrentSpeed =>
            (ShopLogic.MoveSpeed(m_MoveSpeed, m_SpeedStacks.Value) + FastFeetBonus()) * m_ExternalSpeedMul.Value;

        /// <summary>
        /// 빠른 발(테크 v3) — 이속 가산(TechInfo P1: +0.3/0.7/1.2). 미보유·컴포넌트 없음은 0.
        /// 만렙이어도 고양이 기본 이속을 못 넘는다(가위바위보 표 — 발 싸움은 고양이가 이긴다).
        /// </summary>
        private float FastFeetBonus()
        {
            if (m_TechSlots == null) return 0f;
            return TechLogic.ParamOf(TechId.FastFeet, m_TechSlots.LevelOf(TechId.FastFeet), 1);
        }

        // 바람 전직 + 스킬 활성일 때만 1이 아니다. 컴포넌트가 없으면(쥐·구 프리팹) 항상 1이라
        // 전직을 모르는 쪽에서도 그대로 돈다.
        private float WindMultiplier()
        {
            var cls = GetComponent<CatClass>();
            var skill = GetComponent<CatSkill>();
            if (cls == null || skill == null) return 1f;
            if (cls.Value != CatClassId.Wind || !skill.Active) return 1f;

            return CatClassLogic.WindSpeedMultiplier;
        }
}
}
