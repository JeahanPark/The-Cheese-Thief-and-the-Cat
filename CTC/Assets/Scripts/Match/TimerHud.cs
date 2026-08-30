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
    ///
    /// 2단계(깡패전)에는 **다른 시계**를 본다(2026-08-10). `PhaseDirector`가 새 4분을 주는데
    /// 여기서 1단계 시계만 보고 있으면, 판이 4분 남았는데 화면에는 0:00이 박힌 채로 있다 —
    /// 언제 끝나는지 모르는 구간이 되어 몰아치는 마무리가 성립하지 않는다.
    /// </summary>
    public class TimerHud : MonoBehaviour
    {
        private static readonly Color COLOR_GANG = UI.UiPalette.Bad;

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private MatchManager m_Match;
        [SerializeField] private PhaseDirector m_Phase;
        [SerializeField] private TMP_Text m_Text;

        private Color m_HuntColor;

        private readonly SerialDisposable m_Tick = new SerialDisposable();

        private void Start()
        {
            m_HuntColor = m_Text.color;
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
            bool gang = m_Phase != null && m_Phase.IsGangPhase;
            float remaining = gang
                ? m_Phase.Remaining
                : m_Match != null ? m_Match.RemainingSeconds : -1f;

            bool show = remaining >= 0f;
            if (m_Text.gameObject.activeSelf != show) m_Text.gameObject.SetActive(show);
            if (!show) return;

            int total = Mathf.CeilToInt(remaining);
            string clock = $"{total / 60}:{total % 60:00}";
            // 2단계는 규칙이 통째로 다른 판이다 — 같은 숫자만 갈아 끼우면 바뀐 걸 모른다
            m_Text.text = gang ? $"깡패전  {clock}" : clock;
            m_Text.color = gang ? COLOR_GANG : m_HuntColor;
        }
    }
}
