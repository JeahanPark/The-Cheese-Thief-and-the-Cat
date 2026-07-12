using CTC.Runtime.Lobby;
using TMPro;
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>상단 중앙 매치 타이머(mm:ss). 게임 중에만 표시.</summary>
    public class TimerHud : MonoBehaviour
    {
        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private MatchManager m_Match;
        [SerializeField] private TMP_Text m_Text;

        private void Update()
        {
            bool show = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted
                        && m_Match != null && m_Match.RemainingSeconds >= 0f;
            if (m_Text.gameObject.activeSelf != show) m_Text.gameObject.SetActive(show);
            if (!show) return;

            int total = Mathf.CeilToInt(m_Match.RemainingSeconds);
            m_Text.text = $"{total / 60}:{total % 60:00}";
        }
    }
}
