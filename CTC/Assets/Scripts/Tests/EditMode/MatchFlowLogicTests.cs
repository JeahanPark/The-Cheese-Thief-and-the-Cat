using CTC.Runtime.Match;
using NUnit.Framework;

namespace CTC.Tests
{
    public class MatchFlowLogicTests
    {
        [Test]
        public void ShouldArm_BothSidesAlive_True()
        {
            Assert.IsTrue(MatchFlowLogic.ShouldArm(_gameStarted: true, _armed: false, _aliveCats: 1, _aliveMice: 2));
        }

        [Test]
        public void ShouldArm_NotStarted_False()
        {
            Assert.IsFalse(MatchFlowLogic.ShouldArm(_gameStarted: false, _armed: false, _aliveCats: 1, _aliveMice: 1));
        }

        [Test]
        public void ShouldArm_AlreadyArmed_False()
        {
            Assert.IsFalse(MatchFlowLogic.ShouldArm(_gameStarted: true, _armed: true, _aliveCats: 1, _aliveMice: 1));
        }

        [Test]
        public void ShouldArm_MissingCat_False()
        {
            Assert.IsFalse(MatchFlowLogic.ShouldArm(_gameStarted: true, _armed: false, _aliveCats: 0, _aliveMice: 1));
        }

        [Test]
        public void ShouldArm_MissingMouse_False()
        {
            Assert.IsFalse(MatchFlowLogic.ShouldArm(_gameStarted: true, _armed: false, _aliveCats: 1, _aliveMice: 0));
        }

        [Test]
        public void Evaluate_NotArmed_None()
        {
            // armed 전에는 0/0(스폰 지연)이어도 판정하지 않는다
            Assert.AreEqual(MatchResult.None, MatchFlowLogic.Evaluate(_armed: false, _aliveCats: 0, _freeMice: 0, _jailedMice: 0));
        }

        [Test]
        public void Evaluate_MiceExtinct_CatWin()
        {
            Assert.AreEqual(MatchResult.CatWin, MatchFlowLogic.Evaluate(_armed: true, _aliveCats: 1, _freeMice: 0, _jailedMice: 0));
        }

        [Test]
        public void Evaluate_CatsExtinct_MouseWin()
        {
            Assert.AreEqual(MatchResult.MouseWin, MatchFlowLogic.Evaluate(_armed: true, _aliveCats: 0, _freeMice: 3, _jailedMice: 0));
        }

        [Test]
        public void Evaluate_BothExtinct_CatWinTakesPrecedence()
        {
            Assert.AreEqual(MatchResult.CatWin, MatchFlowLogic.Evaluate(_armed: true, _aliveCats: 0, _freeMice: 0, _jailedMice: 0));
        }

        [Test]
        public void Evaluate_BothAlive_None()
        {
            Assert.AreEqual(MatchResult.None, MatchFlowLogic.Evaluate(_armed: true, _aliveCats: 2, _freeMice: 4, _jailedMice: 0));
        }

        [Test]
        public void EvaluateAtTimeUp_MiceAlive_MouseWin()
        {
            Assert.AreEqual(MatchResult.MouseWin, MatchFlowLogic.EvaluateAtTimeUp(_freeMice: 1));
        }

        [Test]
        public void EvaluateAtTimeUp_NoMice_CatWin()
        {
            Assert.AreEqual(MatchResult.CatWin, MatchFlowLogic.EvaluateAtTimeUp(_freeMice: 0));
        }
    }
}
