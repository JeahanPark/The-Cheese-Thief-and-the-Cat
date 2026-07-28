using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Cameras
{
    /// <summary>
    /// 마우스 델타로 CinemachineOrbitalFollow 축을 구동하는 자유 궤도 카메라.
    /// 리센터 없음 — 몸과 무관하게 항상 사방을 훔쳐볼 수 있다(스텔스 핵심).
    /// 커서가 잠긴 동안만 회전(로비에선 UI 조작을 위해 커서 해제 상태).
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera), typeof(CinemachineOrbitalFollow))]
    public class OrbitCameraController : MonoBehaviour
    {
        [SerializeField] private float sensitivity = 0.15f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 70f;

        private CinemachineCamera _cam;
        private CinemachineOrbitalFollow _orbital;
        private Vector2 _angles = new Vector2(0f, 20f); // (yaw, pitch)

        private void Awake()
        {
            _cam = GetComponent<CinemachineCamera>();
            _orbital = GetComponent<CinemachineOrbitalFollow>();
        }

        /// <summary>로컬 플레이어 스폰 시 호출 — 추적 대상과 역할별 렌즈를 적용한다.</summary>
        public void Bind(Transform target, float fov, float distance)
        {
            _cam.Follow = target;
            _cam.LookAt = target;

            LensSettings lens = _cam.Lens;
            lens.FieldOfView = fov;
            _cam.Lens = lens;

            _orbital.Radius = distance;
        }

        private void Update()
        {
            if (_cam.Follow == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            _angles = CameraOrbitLogic.Step(_angles, mouse.delta.ReadValue(), sensitivity, minPitch, maxPitch);
            _orbital.HorizontalAxis.Value = _angles.x;
            _orbital.VerticalAxis.Value = _angles.y;
        }

        /// <summary>QA 전용 — 마우스 없이 시점을 지정한다(차폐 페이드 검증).</summary>
        public void DevSetAngles(float _yawDeg, float _pitchDeg)
        {
            _angles = new Vector2(
                CameraOrbitLogic.WrapYaw(_yawDeg),
                CameraOrbitLogic.ClampPitch(_pitchDeg, minPitch, maxPitch));
            _orbital.HorizontalAxis.Value = _angles.x;
            _orbital.VerticalAxis.Value = _angles.y;
        }

        /// <summary>QA 전용 — 카메라와 추적 대상의 실제 거리. 페이드 방식은 이 값이 상수여야 한다.</summary>
        public float DevDistanceToTarget()
        {
            if (_cam.Follow == null) return -1f;
            return Vector3.Distance(transform.position, _cam.Follow.position);
        }
    }
}
