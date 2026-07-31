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
        [SerializeField] private float m_Sensitivity = 0.15f;
        [SerializeField] private float m_MinPitch = -30f;
        [SerializeField] private float m_MaxPitch = 70f;

        private CinemachineCamera m_Cam;
        private CinemachineOrbitalFollow m_Orbital;
        private Vector2 m_Angles = new Vector2(0f, 20f); // (yaw, pitch)

        private void Awake()
        {
            m_Cam = GetComponent<CinemachineCamera>();
            m_Orbital = GetComponent<CinemachineOrbitalFollow>();
        }

        /// <summary>로컬 플레이어 스폰 시 호출 — 추적 대상과 역할별 렌즈를 적용한다.</summary>
        public void Bind(Transform _target, float _fov, float _distance)
        {
            m_Cam.Follow = _target;
            m_Cam.LookAt = _target;

            LensSettings lens = m_Cam.Lens;
            lens.FieldOfView = _fov;
            m_Cam.Lens = lens;

            m_Orbital.Radius = _distance;
        }

        private void Update()
        {
            if (m_Cam.Follow == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            m_Angles = CameraOrbitLogic.Step(m_Angles, mouse.delta.ReadValue(), m_Sensitivity, m_MinPitch, m_MaxPitch);
            m_Orbital.HorizontalAxis.Value = m_Angles.x;
            m_Orbital.VerticalAxis.Value = m_Angles.y;
        }

        /// <summary>QA 전용 — 마우스 없이 시점을 지정한다(차폐 페이드 검증).</summary>
        public void DevSetAngles(float _yawDeg, float _pitchDeg)
        {
            m_Angles = new Vector2(
                CameraOrbitLogic.WrapYaw(_yawDeg),
                CameraOrbitLogic.ClampPitch(_pitchDeg, m_MinPitch, m_MaxPitch));
            m_Orbital.HorizontalAxis.Value = m_Angles.x;
            m_Orbital.VerticalAxis.Value = m_Angles.y;
        }

        /// <summary>QA 전용 — 카메라와 추적 대상의 실제 거리. 페이드 방식은 이 값이 상수여야 한다.</summary>
        public float DevDistanceToTarget()
        {
            if (m_Cam.Follow == null) return -1f;
            return Vector3.Distance(transform.position, m_Cam.Follow.position);
        }
    }
}
