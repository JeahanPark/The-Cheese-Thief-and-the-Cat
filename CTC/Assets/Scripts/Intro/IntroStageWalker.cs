using UnityEngine;

namespace CTC.Runtime.Intro
{
    /// <summary>
    /// 인트로 무대의 쥐 한 마리를 치즈 주위로 돌린다.
    ///
    /// **게임의 PlayerMouse가 아니다.** 인트로는 네트워크가 없는 씬이라 NetworkObject를 세우면
    /// 안 되고, 이동도 CharacterController가 아니라 그냥 트랜스폼이다(장식이라 충돌이 없다).
    /// 그래서 여기서는 Quirky 모델 프리팹만 세우고 이 컴포넌트가 돌린다.
    ///
    /// 새 애니메이션은 만들지 않는다 — 팩의 Walk 클립을 그대로 쓴다.
    /// </summary>
    public class IntroStageWalker : MonoBehaviour
    {
        [SerializeField] private Transform m_Center;
        [SerializeField] private float m_Radius = 2.1f;
        [SerializeField] private float m_DegreesPerSecond = 24f;
        [SerializeField] private float m_StartDegrees;

        /// <summary>걸음마다 살짝 뜨는 느낌 — 조각상처럼 미끄러지지 않게.</summary>
        [SerializeField] private float m_BobHeight = 0.015f;

        [SerializeField] private float m_BobSpeed = 7.5f;

        /// <summary>반지름을 아주 느리게 흔들어 궤도가 기계적으로 안 보이게 한다.</summary>
        [SerializeField] private float m_RadiusWobble = 0.12f;

        private float m_Angle;
        private float m_GroundY;
        private float m_Phase;

        private void Start()
        {
            m_Angle = m_StartDegrees;
            m_GroundY = transform.position.y;
            m_Phase = m_StartDegrees * Mathf.Deg2Rad;
            Apply();
        }

        private void Update()
        {
            m_Angle += m_DegreesPerSecond * Time.deltaTime;
            if (m_Angle >= 360f) m_Angle -= 360f;
            m_Phase += Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            Vector3 center = m_Center != null ? m_Center.position : Vector3.zero;

            float rad = m_Angle * Mathf.Deg2Rad;
            float radius = m_Radius + Mathf.Sin(m_Phase * 0.6f) * m_RadiusWobble;

            var next = new Vector3(
                center.x + Mathf.Cos(rad) * radius,
                m_GroundY + Mathf.Abs(Mathf.Sin(m_Phase * m_BobSpeed)) * m_BobHeight,
                center.z + Mathf.Sin(rad) * radius);

            // 진행 방향(궤도의 접선)을 본다 — 뒤로 걷는 것처럼 보이지 않게.
            var tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            transform.position = next;
            if (tangent.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
        }
    }
}
