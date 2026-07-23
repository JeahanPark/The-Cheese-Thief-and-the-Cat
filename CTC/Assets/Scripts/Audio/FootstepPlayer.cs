using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 이동 속도 기반 발소리 루프(플레이어 프리팹 부착, 3D AudioSource).
    /// 소유자·원격 동일 로직(NetworkTransform 보간 위치 사용) — 네트워크 동기화 불필요.
    /// 클립은 씬 생성기가 역할별로 배선(쥐 잔걸음/고양이 쿵쿵).
    /// </summary>
    public class FootstepPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource m_Source;
        [SerializeField] private float m_WalkThreshold = 0.5f;

        private Vector3 m_LastPosition;
        private float m_SmoothedSpeed;

        private void Start()
        {
            m_LastPosition = transform.position;
        }

        private void Update()
        {
            if (m_Source == null || Time.deltaTime <= 0f) return;

            Vector3 delta = transform.position - m_LastPosition;
            m_LastPosition = transform.position;
            delta.y = 0f;
            m_SmoothedSpeed = Mathf.Lerp(m_SmoothedSpeed, delta.magnitude / Time.deltaTime, 0.25f);

            bool moving = m_SmoothedSpeed >= m_WalkThreshold;
            if (moving && !m_Source.isPlaying)
            {
                m_Source.Play();
            }
            else if (!moving && m_Source.isPlaying)
            {
                m_Source.Stop();
            }
        }
    }
}
