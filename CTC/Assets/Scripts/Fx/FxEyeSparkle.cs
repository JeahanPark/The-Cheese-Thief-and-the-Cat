using UnityEngine;

namespace CTC.Runtime.Fx
{
    /// <summary>
    /// 눈 반짝임 이펙트 한 덩이. **프리팹 루트에 붙는다.**
    ///
    /// 이 컴포넌트는 자리를 정하지 않는다 — 별과 발광이 몇 개인지, 어디에 있는지, 얼마나 큰지는
    /// 전부 **프리팹 안의 자식 트랜스폼**이 갖는다. 여기서 하는 일은 그 자식들을 흔드는 것뿐이다.
    /// 그래서 이펙트를 고치려면 프리팹을 열어 옮기면 되고, 코드를 볼 일이 없다.
    ///
    /// 별은 하나하나 제 시계로 돈다(대기 → 커짐 → 유지 → 줄어듦). 전부 같은 박자로 뜨면
    /// 기계처럼 보여서 대기 시간과 회전을 별마다 새로 뽑는다.
    /// 발광은 꺼지지 않고 밝기만 천천히 오르내린다.
    /// </summary>
    public class FxEyeSparkle : MonoBehaviour
    {
        [Header("구성 — 프리팹의 자식들")]
        [SerializeField] private SpriteRenderer[] m_Glows;
        [SerializeField] private SpriteRenderer[] m_Sparkles;

        [Header("박자")]
        [SerializeField] private float m_GrowSeconds = 0.11f;
        [SerializeField] private float m_HoldSeconds = 0.24f;
        [SerializeField] private float m_ShrinkSeconds = 0.17f;

        /// <summary>다음 번쩍까지 쉬는 시간 — 별마다 이 범위에서 새로 뽑는다.</summary>
        [SerializeField] private Vector2 m_WaitSeconds = new Vector2(0.2f, 1.4f);

        [SerializeField] private float m_SpinDegrees = 28f;

        /// <summary>가장 클 때의 배율 — 잠깐 넘겼다 돌아와야 "톡" 하고 터진다.</summary>
        [SerializeField] private float m_Overshoot = 1.18f;

        /// <summary>눈 발광이 숨 쉬는 주기(초).</summary>
        [SerializeField] private float m_GlowPulseSeconds = 2.6f;

        private float[] m_Timer;
        private float[] m_Wait;
        private float[] m_Spin;
        private Vector3[] m_BaseScale;
        private Color[] m_GlowBase;
        private Transform m_Camera;
        private float m_GlowPhase;

        private void Start()
        {
            Camera cam = Camera.main;
            if (cam != null) m_Camera = cam.transform;

            int n = m_Sparkles != null ? m_Sparkles.Length : 0;
            m_Timer = new float[n];
            m_Wait = new float[n];
            m_Spin = new float[n];
            m_BaseScale = new Vector3[n];

            for (int i = 0; i < n; i++)
            {
                if (m_Sparkles[i] == null) continue;

                // 프리팹이 정한 크기가 곧 최대 크기다 — 코드가 크기를 정하지 않는다.
                m_BaseScale[i] = m_Sparkles[i].transform.localScale;
                m_Sparkles[i].transform.localScale = Vector3.zero;

                // 시작을 흩어 놓는다 — 안 그러면 첫 프레임에 전부 같이 터진다.
                m_Wait[i] = Random.Range(0f, m_WaitSeconds.y);
                m_Spin[i] = Random.Range(-m_SpinDegrees, m_SpinDegrees);
            }

            int g = m_Glows != null ? m_Glows.Length : 0;
            m_GlowBase = new Color[g];
            for (int i = 0; i < g; i++)
            {
                if (m_Glows[i] != null) m_GlowBase[i] = m_Glows[i].color;
            }
        }

        private void Update()
        {
            PulseGlow();
            TickSparkles();
        }

        private void PulseGlow()
        {
            if (m_Glows == null) return;

            m_GlowPhase += Time.deltaTime;
            float k = 0.72f + 0.28f * Mathf.Sin(m_GlowPhase * (Mathf.PI * 2f / Mathf.Max(0.1f, m_GlowPulseSeconds)));

            for (int i = 0; i < m_Glows.Length; i++)
            {
                if (m_Glows[i] == null) continue;

                Color c = m_GlowBase[i];
                c.a *= k;
                m_Glows[i].color = c;
                FaceCamera(m_Glows[i].transform, 0f);
            }
        }

        private void TickSparkles()
        {
            if (m_Sparkles == null) return;

            float cycle = m_GrowSeconds + m_HoldSeconds + m_ShrinkSeconds;

            for (int i = 0; i < m_Sparkles.Length; i++)
            {
                if (m_Sparkles[i] == null) continue;
                Transform sparkle = m_Sparkles[i].transform;

                m_Timer[i] += Time.deltaTime;

                if (m_Timer[i] < m_Wait[i])
                {
                    sparkle.localScale = Vector3.zero;
                    continue;
                }

                float t = m_Timer[i] - m_Wait[i];
                if (t >= cycle)
                {
                    m_Timer[i] = 0f;
                    m_Wait[i] = Random.Range(m_WaitSeconds.x, m_WaitSeconds.y);
                    m_Spin[i] = Random.Range(-m_SpinDegrees, m_SpinDegrees);
                    sparkle.localScale = Vector3.zero;
                    continue;
                }

                sparkle.localScale = m_BaseScale[i] * ScaleAt(t);
                FaceCamera(sparkle, m_Spin[i] * (t / cycle));
            }
        }

        /// <summary>커질 땐 살짝 넘겼다 돌아오고, 사라질 땐 그냥 줄어든다.</summary>
        private float ScaleAt(float _t)
        {
            if (_t < m_GrowSeconds)
            {
                float k = _t / m_GrowSeconds;
                return Mathf.Lerp(0f, m_Overshoot, k * k);
            }

            if (_t < m_GrowSeconds + m_HoldSeconds)
            {
                float k = (_t - m_GrowSeconds) / m_HoldSeconds;
                return Mathf.Lerp(m_Overshoot, 1f, k);
            }

            float s = (_t - m_GrowSeconds - m_HoldSeconds) / m_ShrinkSeconds;
            return Mathf.Lerp(1f, 0f, s * s);
        }

        private void FaceCamera(Transform _target, float _spin)
        {
            if (m_Camera == null)
            {
                _target.localRotation = Quaternion.Euler(0f, 0f, _spin);
                return;
            }

            Vector3 away = _target.position - m_Camera.position;
            if (away.sqrMagnitude < 0.0001f) return;

            _target.rotation = Quaternion.LookRotation(away, Vector3.up) * Quaternion.Euler(0f, 0f, _spin);
        }
    }
}
