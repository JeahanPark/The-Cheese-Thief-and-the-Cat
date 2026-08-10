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
        // 두 반경은 프리팹에 굽는다 — PlayerInfo가 원본이고, 바꾸면 씬 재생성이 필요하다.
        [SerializeField] private float m_NormalRange = 8f;
        [SerializeField] private float m_SneakRange = 4f;

        private Vector3 m_LastPosition;
        private float m_SmoothedSpeed;
        private Player.Sneak m_Sneak; // 잠행 중엔 발소리가 아주 좁게만 들린다(고양이·쥐 공통)

        private void Start()
        {
            m_LastPosition = transform.position;
            m_Sneak = GetComponentInParent<Player.Sneak>();
        }

        private void Update()
        {
            if (m_Source == null || Time.deltaTime <= 0f) return;

            Vector3 delta = transform.position - m_LastPosition;
            m_LastPosition = transform.position;
            delta.y = 0f;
            m_SmoothedSpeed = Mathf.Lerp(m_SmoothedSpeed, delta.magnitude / Time.deltaTime, 0.25f);

            // 잠행 상태는 복제되므로 원격 클라이언트에서도 같은 판단이 나온다
            bool sneaking = m_Sneak != null && m_Sneak.Sneaking;
            bool moving = FootstepLogic.IsMoving(m_SmoothedSpeed, m_WalkThreshold);
            float range = FootstepLogic.AudibleRange(moving, sneaking, m_NormalRange, m_SneakRange);

            // 반경을 바꿔 '들리는 거리'를 줄인다 — 재생을 끄면 잠행이 다시 완전 무음이 된다
            if (range > 0f && !Mathf.Approximately(m_Source.maxDistance, range)) m_Source.maxDistance = range;

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
