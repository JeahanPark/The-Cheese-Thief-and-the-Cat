using CTC.Runtime.Core;
using R3;
using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 붙어 있는 `AudioSource`가 옵션 창의 볼륨을 따라가게 한다 (2026-08-01).
    ///
    /// `SfxPlayer`는 원샷을 코드로 만들면서 볼륨을 곱하면 되지만, **루프 소스**(발소리·미끼
    /// 유인음·앰비언스·BGM)는 생성기가 프리팹·씬에 볼륨을 구워 넣어 두고 아무도 안 만진다.
    /// 그대로 두면 "효과음 볼륨을 줄였는데 제일 큰 소리(발소리)만 안 줄어드는" 결과가 난다.
    ///
    /// Awake에서 **구워진 볼륨을 설계값으로 기억**하고, 이후 `설계값 × 사용자 볼륨`을 쓴다.
    /// 그래서 밸런스로 정한 소리 크기 비율(쥐 0.6 / 미끼 0.6 / BGM 0.35…)은 그대로 유지된다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioVolumeFollower : MonoBehaviour
    {
        public enum Category { Sfx, Bgm }

        [SerializeField] private Category m_Category = Category.Sfx;

        /// <summary>Awake 전에만 의미가 있다 — 코드로 세우는 재생기(BgmDirector)가 쓴다.</summary>
        public void SetCategory(Category _category) => m_Category = _category;

        private AudioSource m_Source;
        private float m_DesignVolume;

        private void Awake()
        {
            m_Source = GetComponent<AudioSource>();
            m_DesignVolume = m_Source.volume;

            ReactiveProperty<float> setting =
                m_Category == Category.Bgm ? UserSettings.BgmVolume : UserSettings.SfxVolume;

            setting.Subscribe(v =>
            {
                if (m_Source != null) m_Source.volume = m_DesignVolume * v;
            }).AddTo(this);
        }
    }
}
