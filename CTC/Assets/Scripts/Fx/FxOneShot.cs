using UnityEngine;

namespace CTC.Runtime.Fx
{
    /// <summary>
    /// 한 번 터지고 끝나는 이펙트 프리팹의 뿌리(2026-09-14). 풀에서 다시 꺼낼 때마다 입자를 **처음부터** 다시 튼다 —
    /// 켜기만 하면 지난번 입자가 남은 채로 이어진다.
    /// 수명은 생성기가 입자 설정에서 계산해 굽는다(가장 긴 자식 기준).
    /// </summary>
    public class FxOneShot : MonoBehaviour
    {
        [SerializeField] private float m_Seconds = 1f;
        [SerializeField] private ParticleSystem[] m_Systems;

        public float Seconds => m_Seconds;

        public void Restart()
        {
            if (m_Systems == null) return;
            for (int i = 0; i < m_Systems.Length; i++)
            {
                ParticleSystem ps = m_Systems[i];
                if (ps == null) continue;
                ps.Clear(true);
                ps.Play(true);
            }
        }
    }
}
