using System;
using CTC.Runtime.Lobby;
using R3;
using TMPro;
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 상단 중앙 매치 타이머(mm:ss) — MVC의 View. 게임 중에만 표시.
    ///
    /// 개편 3b(2026-07-31): Update(60fps) → **1초 간격 Interval**.
    /// 초 단위로만 표시하므로 프레임마다 다시 그릴 이유가 없다.
    /// </summary>
    public class TimerHud : MonoBehaviour
    {
        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private MatchManager m_Match;
        [SerializeField] private TMP_Text m_Text;

        private readonly SerialDisposable m_Tick = new SerialDisposable();

        private void Start()
        {
            m_Text.gameObject.SetActive(false);
            m_Tick.AddTo(this);

            m_Lobby.GameStartedChanged.Subscribe(OnGameStartedChanged).AddTo(this);
        }

        private void OnGameStartedChanged(bool _started)
        {
            if (!_started)
            {
                m_Tick.Disposable = null;
                m_Text.gameObject.SetActive(false);
                return;
            }

            m_Tick.Disposable = Observable
                .Interval(TimeSpan.FromSeconds(1), UnityTimeProvider.Update)
                .Subscribe(_ => Redraw());
            Redraw();
        }

        private void Redraw()
        {
            bool show = m_Match != null && m_Match.RemainingSeconds >= 0f;
            if (m_Text.gameObject.activeSelf != show) m_Text.gameObject.SetActive(show);
            if (!show) return;

            int total = Mathf.CeilToInt(m_Match.RemainingSeconds);
            m_Text.text = $"{total / 60}:{total % 60:00}";
        }
    }
}
