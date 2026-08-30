using CTC.Runtime.Match;
using NUnit.Framework;

namespace CTC.Tests
{
    public class MatchSeriesLogicTests
    {
        [Test]
        public void IsFinalRound_OnlyLast()
        {
            Assert.IsFalse(MatchSeriesLogic.IsFinalRound(1));
            Assert.IsFalse(MatchSeriesLogic.IsFinalRound(2));
            Assert.IsTrue(MatchSeriesLogic.IsFinalRound(MatchSeriesLogic.ROUNDS));
        }

        [Test]
        public void NextRound_StopsAtLast()
        {
            Assert.AreEqual(2, MatchSeriesLogic.NextRound(1));
            Assert.AreEqual(3, MatchSeriesLogic.NextRound(2));
            Assert.AreEqual(3, MatchSeriesLogic.NextRound(3));
        }

        /// <summary>3판을 다 채우기 전에는 승자를 말하지 않는다 — 무조건 3판이기 때문이다.</summary>
        [Test]
        public void Winner_NotDecidedBeforeAllRounds()
        {
            Assert.AreEqual(MatchResult.None, MatchSeriesLogic.Winner(2, 0));
            Assert.AreEqual(MatchResult.None, MatchSeriesLogic.Winner(1, 1));
        }

        [Test]
        public void Winner_AfterThreeRounds()
        {
            Assert.AreEqual(MatchResult.CatWin, MatchSeriesLogic.Winner(2, 1));
            Assert.AreEqual(MatchResult.CatWin, MatchSeriesLogic.Winner(3, 0));
            Assert.AreEqual(MatchResult.MouseWin, MatchSeriesLogic.Winner(1, 2));
            Assert.AreEqual(MatchResult.MouseWin, MatchSeriesLogic.Winner(0, 3));
        }

        /// <summary>홀수 판이라 무승부가 나올 수 없다.</summary>
        [Test]
        public void Winner_NeverDraw()
        {
            for (int cat = 0; cat <= MatchSeriesLogic.ROUNDS; cat++)
            {
                int mouse = MatchSeriesLogic.ROUNDS - cat;
                Assert.AreNotEqual(MatchResult.None, MatchSeriesLogic.Winner(cat, mouse));
            }
        }

        [Test]
        public void ScoreLabel_ShowsRoundAndScore()
        {
            string label = MatchSeriesLogic.ScoreLabel(2, 1, 0);
            StringAssert.Contains("2판", label);
            StringAssert.Contains($"/ {MatchSeriesLogic.ROUNDS}", label);
            StringAssert.Contains("고양이 1", label);
            StringAssert.Contains("쥐 0", label);
        }

        /// <summary>판 번호가 범위를 벗어나도 화면에는 말이 되는 값이 떠야 한다.</summary>
        [Test]
        public void ScoreLabel_ClampsRound()
        {
            StringAssert.Contains("1판", MatchSeriesLogic.ScoreLabel(0, 0, 0));
            StringAssert.Contains($"{MatchSeriesLogic.ROUNDS}판", MatchSeriesLogic.ScoreLabel(9, 0, 0));
        }

        [Test]
        public void FinalLabel_NamesWinner()
        {
            StringAssert.Contains("고양이 승", MatchSeriesLogic.FinalLabel(2, 1));
            StringAssert.Contains("쥐 승", MatchSeriesLogic.FinalLabel(1, 2));
        }
    }
}
