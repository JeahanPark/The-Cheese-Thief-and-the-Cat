using CTC.Runtime.Lobby;
using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 인게임 BGM. 게임 시작 시 루프 재생, 로비 복귀 시 정지.
    /// 결과 화면에서는 계속 재생(GameStarted 유지) — 재시작 시 이어짐.
    /// </summary>
    public class BgmPlayer : MonoBehaviour
    {
        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private AudioSource m_Source;

        private void Update()
        {
            bool playing = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
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
