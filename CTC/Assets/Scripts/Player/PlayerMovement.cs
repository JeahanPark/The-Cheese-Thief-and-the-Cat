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
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float turnSpeedDegPerSec = 720f;

        private CharacterController _controller;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
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

            _controller.Move(dir * (moveSpeed * Time.deltaTime));

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, look, turnSpeedDegPerSec * Time.deltaTime);
            }
        }
    }
}
