using System.Collections;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 피격 피드백(전 클라 로컬 연출). Health의 HP 감소를 감지해
    /// Hit 모션(PlayerAnimationDriver 있으면)과 렌더러 적색 플래시를 재생한다.
    /// 플레이어·요새·미끼 공용 — 사망(despawn)은 제외.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

        [SerializeField] private float m_FlashSeconds = 0.15f;
        [SerializeField] private Color m_FlashColor = new Color(1f, 0.25f, 0.25f);
        [SerializeField] private float m_HitAnimSeconds = 0.4f;

        private Health m_Health;
        private Renderer[] m_Renderers;
        private MaterialPropertyBlock m_Block;
        private Coroutine m_Flash;

        private void Awake()
        {
            m_Health = GetComponent<Health>();
            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_Block = new MaterialPropertyBlock();
            if (m_Health != null) m_Health.HpChanged += OnHpChanged;
        }

        private void OnDestroy()
        {
            if (m_Health != null) m_Health.HpChanged -= OnHpChanged;
        }

        private void OnHpChanged(int _previous, int _current)
        {
            // 감소만, 생존 중만 — 스폰 초기화(0→Max)·회복·사망은 연출 없음
            if (_current >= _previous || _current <= 0) return;

            var driver = GetComponent<Player.PlayerAnimationDriver>();
            if (driver != null)
            {
                driver.PlayOneShot(Player.AnimationStateLogic.Hit, m_HitAnimSeconds);
            }

            if (m_Flash != null) StopCoroutine(m_Flash);
            m_Flash = StartCoroutine(CoFlash());
        }

        private IEnumerator CoFlash()
        {
            SetTint(true);
            yield return new WaitForSeconds(m_FlashSeconds);
            SetTint(false);
            m_Flash = null;
        }

        // MaterialPropertyBlock — 머티리얼 인스턴스화 없이 렌더러 단위 틴트 (URP _BaseColor/레거시 _Color 겸용)
        private void SetTint(bool _on)
        {
            foreach (Renderer r in m_Renderers)
            {
                if (r == null) continue;

                m_Block.Clear();
                if (_on)
                {
                    m_Block.SetColor(BASE_COLOR_ID, m_FlashColor);
                    m_Block.SetColor(COLOR_ID, m_FlashColor);
                }
                r.SetPropertyBlock(m_Block);
            }
        }
    }
}
