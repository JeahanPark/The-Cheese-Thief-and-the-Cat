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

        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float turnSpeedDegPerSec = 720f;

        // 이속 강화 스택(상점, 고양이). 서버 기록·소유자 적용.
        private readonly NetworkVariable<int> _speedStacks = new NetworkVariable<int>();

        private CharacterController _controller;
        private float _verticalVelocity;

        public int SpeedStacks => _speedStacks.Value;

        /// <summary>서버 전용(ShopController). 캡은 ShopLogic.MAX_UPGRADE_STACKS.</summary>
        public void ServerAddSpeedStack()
        {
            if (!IsServer || !ShopLogic.CanAddStack(_speedStacks.Value)) return;
            _speedStacks.Value++;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        /// <summary>
        /// 소유자 권위 ClientNetworkTransform은 소유 클라이언트의 로컬 초기 위치로
        /// 서버 스폰 위치를 덮어쓴다. 그래서 소유자가 스폰 시점에 로비 목록으로
        /// 자기 자리(서버와 동일한 결정적 공식)를 직접 계산해 텔레포트한다.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            var lobby = FindFirstObjectByType<Lobby.LobbyManager>();
            if (lobby == null) return;

            int idx = lobby.IndexOfClient(OwnerClientId);
            if (idx < 0) return;

            _controller.enabled = false;
            transform.position = lobby.SpawnPositionOf(idx);
            _controller.enabled = true;
        }

        /// <summary>
        /// 재시작 스윕 시 클라 측 파괴는 프레임 말로 지연된다 — 그 프레임 동안 남은 구 몸체가
        /// 같은 자리에 스폰되는 새 CC를 밀어내(depenetration) 바닥 밑으로 사출하는 간헐 낙하의
        /// 원인. despawn 즉시 콜라이더를 꺼서 차단한다.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            _controller.enabled = false;
        }

        private void Update()
        {
            if (!IsOwner) return;

            // 프로젝트가 신형 Input System 전용(activeInputHandler=1)이라 레거시 Input 사용 불가
            var kb = Keyboard.current;
            if (kb == null) return;

            float h = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                    - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float v = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                    - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);

            float camYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;
            Vector3 dir = MovementInput.ToWorldDirection(h, v, camYaw);

            // 중력 — 접지 중엔 살짝 눌러 붙이고, 공중에선 가속 낙하
            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -1f;
            _verticalVelocity += GRAVITY * Time.deltaTime;

            Vector3 motion = dir * ShopLogic.MoveSpeed(moveSpeed, _speedStacks.Value);
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, look, turnSpeedDegPerSec * Time.deltaTime);
            }
        }
    }
}
