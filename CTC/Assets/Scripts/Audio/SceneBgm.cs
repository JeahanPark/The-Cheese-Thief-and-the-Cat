using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 씬에 깔리는 배경음 — **네트워크와 무관하게** 그 씬에 있는 동안 계속 돈다 (2026-09-01).
    ///
    /// <see cref="BgmPlayer"/>는 `LobbyController`(인게임의 NetworkObject)의 매치 상태를 보고
    /// 켜고 끈다. 그래서 인게임 밖(인트로·로비·대기실)에서는 **재생기가 아예 없어 무음**이었다
    /// (2026-09-01 실측: 빌드에서 인트로·대기실 볼륨 조회가 -1 = 소스 없음).
    ///
    /// 볼륨은 같은 오브젝트의 <see cref="AudioVolumeFollower"/>(Bgm)가 맞춘다 — 0이면 멈춘다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SceneBgm : MonoBehaviour
    {
        private AudioSource m_Source;

        private void Awake()
        {
            m_Source = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (m_Source == null || m_Source.clip == null) return;

            // 배경음 볼륨 0은 "끔"이다 — 볼륨만 0으로 두고 돌리면 소리 없이 CPU만 쓴다.
            if (m_Source.volume <= 0.0001f)
            {
                if (m_Source.isPlaying) m_Source.Stop();
                return;
            }

            if (!m_Source.isPlaying) m_Source.Play();
        }
    }
}
