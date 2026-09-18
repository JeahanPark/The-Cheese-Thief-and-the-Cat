using CTC.Runtime.Match;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 결과 패널 문구. 사소해 보이지만 실제로 **깡패 팀이 이겨도 "CAT WINS!"가 떴다**
    /// (2026-08-10 실측) — "쥐 승이 아니면 고양이 승"이라는 삼항이 남아 있었다.
    /// 승자가 셋인 게임이므로 결과가 늘어날 때마다 여기가 먼저 깨져야 한다.
    /// </summary>
    public class ResultLabelTests
    {
        /// <summary>
        /// 단판제의 결과 넷(2026-09-15): 고양이 승 · 깡패 없이 쥐 전원 승 · 깡패 승 · 깡패가 아닌 쥐 승.
        /// </summary>
        [NUnit.Framework.Property("CaseId", "A-27")]
        [Test]
        public void LabelOf_EveryWinnerHasItsOwnLine()
        {
            Assert.AreEqual("MICE WIN!", ResultHud.LabelOf(MatchResult.MouseWin));
            Assert.AreEqual("CAT WINS!", ResultHud.LabelOf(MatchResult.CatWin));
            Assert.AreEqual("GANG WINS!", ResultHud.LabelOf(MatchResult.GangWin));
            Assert.AreEqual("GANG DOWN! MICE WIN", ResultHud.LabelOf(MatchResult.MiceBeatGang));
        }

        [NUnit.Framework.Property("CaseId", "A-27")]
        [Test]
        public void FourResults_ComeFromTheRightPhase()
        {
            // 1단계: 고양이가 죽고 깡패가 없으면 쥐 전원 승, 쥐가 다 잡히면 고양이 승
            Assert.AreEqual(MatchResult.MouseWin, MatchFlowLogic.Evaluate(_armed: true, _aliveCats: 0, _aliveMice: 2));
            Assert.AreEqual(MatchResult.CatWin, MatchFlowLogic.Evaluate(_armed: true, _aliveCats: 1, _aliveMice: 0));
            // 2단계: 깡패 팀이 나머지를 다 잡으면 깡패 승, 깡패 팀이 전멸하거나 시간을 버티면 깡패가 아닌 쥐 승
            Assert.AreEqual(MatchResult.GangWin, MouseJobLogic.EvaluateGangPhase(1, 0, false));
            Assert.AreEqual(MatchResult.MiceBeatGang, MouseJobLogic.EvaluateGangPhase(0, 2, false));
            Assert.AreEqual(MatchResult.MiceBeatGang, MouseJobLogic.EvaluateGangPhase(1, 2, true));
        }

        [Test]
        public void LabelOf_NoneShowsNothing()
        {
            // 판이 안 끝났는데 승자 문구가 나오면 안 된다
            Assert.AreEqual("", ResultHud.LabelOf(MatchResult.None));
        }

        [Test]
        public void LabelOf_AllResultsAreDistinct()
        {
            // 결과를 새로 추가하고 문구를 안 붙이면 다른 승자와 같은 줄이 나온다 — 그 순간 실패한다
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (MatchResult r in System.Enum.GetValues(typeof(MatchResult)))
            {
                if (r == MatchResult.None) continue;
                string label = ResultHud.LabelOf(r);
                Assert.IsNotEmpty(label, $"{r} 문구가 비었다");
                Assert.IsTrue(seen.Add(label), $"{r}가 다른 승자와 같은 문구를 쓴다: {label}");
            }
        }
    }
}
