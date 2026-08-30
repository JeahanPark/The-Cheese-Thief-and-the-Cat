using CTC.Runtime.Match;
using R3;
using TMPro;
using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 시계 아래 한 줄 — `2판 / 3   고양이 1 : 쥐 0` (MVC의 View).
    ///
    /// 시리즈가 아닌 판(`Game.unity` 단독 실행)에서는 **아예 안 뜬다.**
    /// 단독 실행에 "1판 / 3"이 떠 있으면 없는 규칙을 있다고 말하는 셈이다.
    /// </summary>
    public class MatchSeriesView : MonoBehaviour
    {
        [SerializeField] private MatchSeries m_Series;
        [SerializeField] private TMP_Text m_Label;

        private void Start()
        {
            if (m_Series == null || m_Label == null) return;

            m_Label.color = UiPalette.TextDim;
            m_Series.Changed.Subscribe(_ => Render()).AddTo(this);
        }

        private void Render()
        {
            if (m_Series == null || m_Label == null) return;

            bool show = m_Series.Active;
            if (m_Label.gameObject.activeSelf != show) m_Label.gameObject.SetActive(show);
            if (!show) return;

            m_Label.text = MatchSeriesLogic.ScoreLabel(
                m_Series.Round, m_Series.CatWins, m_Series.MouseWins);
        }
    }
}
