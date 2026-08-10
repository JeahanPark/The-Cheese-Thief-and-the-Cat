using CTC.Runtime.Match;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 쥐 직업과 2단계 규칙. **승리 조건을 갈아엎는 부분**이라 여기가 무너지면
    /// 판이 안 끝나거나 엉뚱한 쪽이 이긴다.
    /// </summary>
    public class MouseJobLogicTests
    {
        [Test]
        public void IsGangSide_BossAndMinionAreOneTeam()
        {
            Assert.IsTrue(MouseJobLogic.IsGangSide(MouseJobId.Gang));
            Assert.IsTrue(MouseJobLogic.IsGangSide(MouseJobId.Minion));
            Assert.IsFalse(MouseJobLogic.IsGangSide(MouseJobId.Hero));
            Assert.IsFalse(MouseJobLogic.IsGangSide(MouseJobId.None));
        }

        [Test]
        public void CanRecruit_OnlyGangRecruits()
        {
            Assert.IsFalse(MouseJobLogic.CanRecruit(MouseJobId.Hero, MouseJobId.None, 1f, 2.5f));
            Assert.IsFalse(MouseJobLogic.CanRecruit(MouseJobId.None, MouseJobId.None, 1f, 2.5f));
            Assert.IsTrue(MouseJobLogic.CanRecruit(MouseJobId.Gang, MouseJobId.None, 1f, 2.5f));
        }

        [Test]
        public void CanRecruit_NeverStealsFromOwnTeam()
        {
            // 보스를 부하로 만들거나 부하를 다시 임명하면 팀 구조가 뒤집힌다.
            Assert.IsFalse(MouseJobLogic.CanRecruit(MouseJobId.Gang, MouseJobId.Gang, 1f, 2.5f));
            Assert.IsFalse(MouseJobLogic.CanRecruit(MouseJobId.Gang, MouseJobId.Minion, 1f, 2.5f));
        }

        [Test]
        public void CanRecruit_RespectsDistance()
        {
            // 거리 판정이 없으면 맵 반대편에서도 임명한다.
            Assert.IsTrue(MouseJobLogic.CanRecruit(MouseJobId.Gang, MouseJobId.None, 2.5f, 2.5f));
            Assert.IsFalse(MouseJobLogic.CanRecruit(MouseJobId.Gang, MouseJobId.None, 2.6f, 2.5f));
        }

        [Test]
        public void ShouldEnterGangPhase_OnlyWhenAGangExists()
        {
            // 깡패가 없으면 지금까지의 규칙대로 쥐 승리다 — 항상 2단계면 깡패권이 특별하지 않다.
            Assert.IsFalse(MouseJobLogic.ShouldEnterGangPhase(0));
            Assert.IsTrue(MouseJobLogic.ShouldEnterGangPhase(1));
        }

        [Test]
        public void EvaluateGangPhase_GangWinsByWipingTheRest()
        {
            Assert.AreEqual(MatchResult.GangWin,
                MouseJobLogic.EvaluateGangPhase(_gangAlive: 2, _otherAlive: 0, _timeUp: false));
        }

        [Test]
        public void EvaluateGangPhase_RestWinsWhenTheGangIsWipedOut()
        {
            Assert.AreEqual(MatchResult.MouseWin,
                MouseJobLogic.EvaluateGangPhase(_gangAlive: 0, _otherAlive: 1, _timeUp: false));
        }

        [Test]
        public void EvaluateGangPhase_RestWinsOnTimeUp()
        {
            // 시계를 쥔 쪽이 쫓는 쪽이다 — 버티면 나머지 쥐가 이긴다.
            Assert.AreEqual(MatchResult.MouseWin,
                MouseJobLogic.EvaluateGangPhase(_gangAlive: 2, _otherAlive: 1, _timeUp: true));
        }

        [Test]
        public void EvaluateGangPhase_UndecidedWhileBothSidesLive()
        {
            Assert.AreEqual(MatchResult.None,
                MouseJobLogic.EvaluateGangPhase(_gangAlive: 2, _otherAlive: 1, _timeUp: false));
        }

        [Test]
        public void EvaluateGangPhase_WipeBeatsTimeUp()
        {
            // 같은 틱에 둘 다 성립하면 전멸이 먼저다 — 시간이 다 됐어도 이미 이긴 판이다.
            Assert.AreEqual(MatchResult.GangWin,
                MouseJobLogic.EvaluateGangPhase(_gangAlive: 1, _otherAlive: 0, _timeUp: true));
        }

        [Test]
        public void Balance_GangPhaseIsShortEnoughToStayTense()
        {
            // 본 판보다 훨씬 짧아야 '몰아치는 마무리'가 된다. 다만 120×120에서는 3분이면
            // 깡패 팀이 나머지 쥐를 찾지도 못한 채 시계가 끝난다 — 그래서 4분이다(2026-08-10).
            Assert.GreaterOrEqual(MouseJobLogic.GangPhaseSeconds, 180f);
            Assert.Less(MouseJobLogic.GangPhaseSeconds, MatchFlowLogic.MATCH_DURATION_SECONDS * 0.5f);
        }

        [Test]
        public void Balance_HeroIsStrongerButNotUnkillable()
        {
            Assert.Greater(MouseJobLogic.HeroHpMultiplier, 1f);
            Assert.Less(MouseJobLogic.HeroHpMultiplier, 4f);
            Assert.Greater(MouseJobLogic.HeroDamageMultiplier, 1f);
        }
    }
}
