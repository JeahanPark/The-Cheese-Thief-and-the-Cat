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

        private const float TIMER_WARN_SECONDS = 10f; // 막판 N초 동안 초당 째깍

        private bool m_WasStarted;
        private MatchResult m_LastResult = MatchResult.None;
        private int m_LastWarnSecond = -1;

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

            // 타이머 경고 — 잔여 10초부터 초가 바뀔 때마다 1회
            float remaining = started && result == MatchResult.None && m_Match != null
                ? m_Match.RemainingSeconds : -1f;
            int second = remaining > 0f && remaining <= TIMER_WARN_SECONDS ? Mathf.CeilToInt(remaining) : -1;
            if (second != -1 && second != m_LastWarnSecond)
            {
                SfxPlayer.PlayUi(SfxId.TimerWarn, 0.8f);
            }
            m_LastWarnSecond = second;
        }
    }
}
