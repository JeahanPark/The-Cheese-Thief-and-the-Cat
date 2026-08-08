using CTC.Runtime.Core;
using R3;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Cameras
{
    /// <summary>
    /// 마우스 델타로 CinemachineOrbitalFollow 축을 구동하는 자유 궤도 카메라.
    /// 리센터 없음 — 몸과 무관하게 항상 사방을 훔쳐볼 수 있다(스텔스 핵심).
    /// 커서가 잠긴 동안만 회전(로비에선 UI 조작을 위해 커서 해제 상태).
    ///
    /// **내려다보는 각도**(2026-08-01 피드백 "위쪽만 보이고 아래가 안 보인다"):
    /// `RotationComposer`가 매 프레임 LookAt 타깃을 조준점에 되돌리므로 **피치·FOV·타깃 오프셋을
    /// 아무리 만져도 캐릭터의 화면 위치는 1픽셀도 안 움직인다.** 화면 배치를 바꾸는 노브는
    /// `Composition.ScreenPosition` 하나뿐이다. 그래서 두 값을 같이 쓴다:
    /// - `CamScreenY` −0.24 → 캐릭터가 화면 위에서 43% → **67%** (앞쪽 바닥이 화면을 채운다)
    /// - `CamDefaultPitchDeg` 20° → **36°** → ScreenY가 시선을 위로 14° 밀어 올리는 걸 상쇄.
    ///   안 하면 화면만 내려가고 **바닥은 오히려 덜 보인다**(실효 시선 31.5° → 17.5°).
    /// 세 값 다 `BalanceInfo.csv`가 원본 — 씬 생성기가 굽고, 여기서 런타임 클램프에 쓴다.
    ///
    /// 감도는 밸런스가 아니라 취향이라 `UserSettings`(PlayerPrefs)에서 온다 — 옵션 창과 공유.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera), typeof(CinemachineOrbitalFollow))]
    public class OrbitCameraController : MonoBehaviour
    {
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;

        private CinemachineCamera m_Cam;
        private CinemachineOrbitalFollow m_Orbital;
        private Vector2 m_Angles;
        private float m_Sensitivity;
        private float m_MinPitch;
        private float m_MaxPitch;

        private void Awake()
        {
            m_Cam = GetComponent<CinemachineCamera>();
            m_Orbital = GetComponent<CinemachineOrbitalFollow>();

            m_MinPitch = Balance.CamMinPitchDeg;
            m_MaxPitch = Balance.CamMaxPitchDeg;
            m_Angles = new Vector2(0f, Balance.CamDefaultPitchDeg); // (yaw, pitch)

            UserSettings.MouseSensitivity.Subscribe(v => m_Sensitivity = v).AddTo(this);
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
