using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 발소리 — **한 걸음씩** 재생(2026-08-17, 예전엔 고정 박자 루프). 이동 거리를 누적해 보폭마다
    /// 한 걸음을 PlayOneShot 하므로 박자가 속도를 따라간다: 살금은 드문드문, 대시·바람은 타다닥.
    /// 클립 음높이는 건드리지 않는다(±4% 랜덤만) — 느린 걸음을 pitch로 늘이면 둔탁해진다.
    /// 소유자·원격 동일 로직(NetworkTransform 보간 위치 사용) — 네트워크 동기화 불필요.
    /// 클립·보폭은 씬 생성기가 역할별로 배선(쥐 잔걸음/고양이 쿵쿵).
    /// </summary>
    public class FootstepPlayer : MonoBehaviour
    {
        private const float PITCH_JITTER = 0.04f;

        [SerializeField] private AudioSource m_Source;
        [SerializeField] private AudioClip[] m_StepClips;   // 한 걸음짜리 여러 개 — 같은 소리 연타를 피한다
        [SerializeField] private float m_StrideMeters = 1.8f;
        [SerializeField] private float m_WalkThreshold = 0.5f;
        // 두 반경은 프리팹에 굽는다 — PlayerInfo가 원본이고, 바꾸면 씬 재생성이 필요하다.
        [SerializeField] private float m_NormalRange = 8f;
        [SerializeField] private float m_SneakRange = 4f;

        private Vector3 m_LastPosition;
        private float m_SmoothedSpeed;
        private float m_Accumulated;
        private bool m_WasMoving;
        private int m_LastClip = -1;
        private Player.Sneak m_Sneak; // 잠행 중엔 발소리가 아주 좁게만 들린다(고양이·쥐 공통)

        /// <summary>지금 걷고 있어 발소리가 나는 중인가(QA·봇 청각).</summary>
        public bool IsStepping { get; private set; }

        /// <summary>지금 발소리가 닿는 반경(m). 안 움직이면 0(QA·봇 청각).</summary>
        public float CurrentRange { get; private set; }

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
            float distance = delta.magnitude;
            m_SmoothedSpeed = Mathf.Lerp(m_SmoothedSpeed, distance / Time.deltaTime, 0.25f);

            // 잠행 상태는 복제되므로 원격 클라이언트에서도 같은 판단이 나온다
            bool sneaking = m_Sneak != null && m_Sneak.Sneaking;
            bool moving = FootstepLogic.IsMoving(m_SmoothedSpeed, m_WalkThreshold);
            float range = FootstepLogic.AudibleRange(moving, sneaking, m_NormalRange, m_SneakRange);
            IsStepping = moving;
            CurrentRange = range;

            // 반경을 바꿔 '들리는 거리'를 줄인다 — 재생을 끄면 잠행이 다시 완전 무음이 된다
            if (range > 0f && !Mathf.Approximately(m_Source.maxDistance, range)) m_Source.maxDistance = range;

            if (!moving)
            {
                m_WasMoving = false;
                m_Accumulated = 0f;
                return;
            }

            if (!m_WasMoving) m_Accumulated = m_StrideMeters;   // 첫 걸음은 바로 — 출발이 무음이면 어색하다
            m_WasMoving = true;

            m_Accumulated = FootstepLogic.Advance(m_Accumulated, distance, m_StrideMeters, out bool step);
            if (step) PlayStep();
        }

        private void PlayStep()
        {
            if (m_StepClips == null || m_StepClips.Length == 0) return;
            int index = Random.Range(0, m_StepClips.Length);
            if (m_StepClips.Length > 1 && index == m_LastClip) index = (index + 1) % m_StepClips.Length;
            m_LastClip = index;
            AudioClip clip = m_StepClips[index];
            if (clip == null) return;
            m_Source.pitch = 1f + Random.Range(-PITCH_JITTER, PITCH_JITTER);
            m_Source.PlayOneShot(clip);
        }
    }
}
