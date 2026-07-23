using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>매치 전환 알림음(2D) — 게임 시작 팡파레, 승패 징글. 엣지 감지로 1회씩만.</summary>
    public class MatchSfx : MonoBehaviour
    {
        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private MatchManager m_Match;

        private bool m_WasStarted;
        private MatchResult m_LastResult = MatchResult.None;

        private void Update()
        {
            bool started = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
            if (started && !m_WasStarted)
            {
                SfxPlayer.PlayUi(SfxId.MatchStart);
            }
            m_WasStarted = started;

            MatchResult result = m_Match != null && m_Match.IsSpawned ? m_Match.Result : MatchResult.None;
            if (result != m_LastResult && result != MatchResult.None)
            {
                SfxPlayer.PlayUi(result == MatchResult.MouseWin ? SfxId.MouseWin : SfxId.CatWin);
            }
            m_LastResult = result;
        }
    }
}
