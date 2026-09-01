using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 배경음의 단독 주인 — **씬을 넘어 산다** (2026-09-01).
    ///
    /// 씬마다 재생기를 새로 세우면 화면이 바뀔 때마다 소리가 끊긴다(실측: 인트로→로비→대기실).
    /// 그래서 재생기는 하나만 두고 각 씬이 <see cref="Play"/>로 "이 곡을 틀어라"만 말한다.
    /// **이미 같은 곡이면 아무 일도 안 한다** — 그래야 앞단 세 화면이 한 곡으로 이어진다.
    ///
    /// 볼륨은 <see cref="AudioVolumeFollower"/>(Bgm)가 맞춘다. 0이면 멈춘다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BgmDirector : MonoBehaviour
    {
        public static BgmDirector Instance { get; private set; }

        private AudioSource m_Source;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            m_Source = GetComponent<AudioSource>();
            m_Source.loop = true;
            m_Source.playOnAwake = false;
            m_Source.spatialBlend = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>이 곡을 튼다. 같은 곡이면 **건드리지 않는다**(끊김 방지).</summary>
        public void Play(AudioClip _clip)
        {
            if (_clip == null || m_Source == null) return;
            if (m_Source.clip == _clip && m_Source.isPlaying) return;

            m_Source.clip = _clip;
            if (m_Source.volume > 0.0001f) m_Source.Play();
        }

        private void Update()
        {
            if (m_Source == null || m_Source.clip == null) return;

            // 배경음 볼륨 0은 "끔"이다 — 소리 없이 돌리면 CPU만 쓴다.
            if (m_Source.volume <= 0.0001f)
            {
                if (m_Source.isPlaying) m_Source.Stop();
                return;
            }

            if (!m_Source.isPlaying) m_Source.Play();
        }
    }
}
