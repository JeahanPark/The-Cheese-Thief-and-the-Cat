using CTC.Runtime.Lobby;
using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// BGM/앰비언스 루프. 기본은 게임 중 재생(인게임 BGM·부엌 앰비언스),
    /// m_PlayWhenGameStopped면 반대로 로비·미접속 중 재생(로비 BGM).
    /// 결과 화면은 GameStarted 유지라 인게임 쪽이 계속 재생 — 재시작 시 이어짐.
    /// </summary>
    public class BgmPlayer : MonoBehaviour
    {
        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private AudioSource m_Source;
        [SerializeField] private bool m_PlayWhenGameStopped;

        private void Update()
        {
            bool gameRunning = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
            bool playing = m_PlayWhenGameStopped ? !gameRunning : gameRunning;
            if (playing && !m_Source.isPlaying)
            {
                m_Source.Play();
            }
            else if (!playing && m_Source.isPlaying)
            {
                m_Source.Stop();
            }
        }
    }
}
