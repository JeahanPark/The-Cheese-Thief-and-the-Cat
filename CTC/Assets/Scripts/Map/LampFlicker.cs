using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 전등 하나의 아주 약한 깜빡임(2026-09-14 맵 분위기). 빛 세기와 전구 발광을 같이 흔든다.
    ///
    /// **스텔스 판정에 안 닿는다** — 빛 자리·반경은 그대로고 세기만 ±<see cref="AMPLITUDE"/>다.
    /// 전구는 머티리얼을 복제하지 않고 MaterialPropertyBlock으로 밝기만 바꾼다(SRP Batcher 유지).
    /// 가끔 한 번 "툭" 떨어졌다 돌아온다 — 오래된 창고 전등의 인상. 주기는 전등마다 달라 박자가 안 맞는다.
    /// </summary>
    public class LampFlicker : MonoBehaviour
    {
        private const float AMPLITUDE = 0.12f;
        private const float DIP_DEPTH = 0.45f;
        private const float DIP_SECONDS = 0.12f;
        private static readonly int EMISSION_COLOR = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Renderer m_Bulb;
        [SerializeField] private Color m_BulbEmission = new Color(4f, 2.6f, 0.9f);

        private Light m_Light;
        private float m_BaseIntensity;
        private float m_Phase;
        private float m_NextDip;
        private float m_DipUntil;
        private MaterialPropertyBlock m_Block;

        /// <summary>전등을 붙인 쪽이 켠다 — 광원과 시드(위치 해시)를 준다.</summary>
        public void Bind(Light _light, int _seed)
        {
            m_Light = _light;
            m_BaseIntensity = _light != null ? _light.intensity : 1f;
            m_Phase = (_seed & 1023) * 0.37f;
            m_NextDip = Time.time + 3f + (_seed & 63) * 0.15f;
            enabled = true;
        }

        private void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            enabled = false;   // Bind 전에는 안 돈다
        }

        private void Update()
        {
            float t = Time.time + m_Phase;
            float noise = Mathf.PerlinNoise(t * 2.3f, m_Phase) * 2f - 1f;
            float k = 1f + noise * AMPLITUDE;

            if (Time.time >= m_NextDip)
            {
                m_DipUntil = Time.time + DIP_SECONDS;
                m_NextDip = Time.time + 4f + Mathf.PerlinNoise(m_Phase, t) * 9f;
            }
            if (Time.time < m_DipUntil) k *= 1f - DIP_DEPTH;

            if (m_Light != null) m_Light.intensity = m_BaseIntensity * k;
            if (m_Bulb != null)
            {
                m_Bulb.GetPropertyBlock(m_Block);
                m_Block.SetColor(EMISSION_COLOR, m_BulbEmission * k);
                m_Bulb.SetPropertyBlock(m_Block);
            }
        }
    }
}
