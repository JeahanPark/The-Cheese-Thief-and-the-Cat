using System.Collections.Generic;
using CTC.Runtime.Match;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class JailLogicTests
    {
        // ---- 가장 가까운 감옥 ----

        [Test]
        public void NearestJail_PicksClosest()
        {
            var jails = new List<Vector3> { new Vector3(10f, 0f, 0f), new Vector3(-2f, 0f, 0f) };
            Assert.AreEqual(1, JailLogic.NearestJailIndex(Vector3.zero, jails));
        }

        [Test]
        public void NearestJail_TieTakesFirst()
        {
            // 결정적이어야 QA가 재현 가능하다
            var jails = new List<Vector3> { new Vector3(5f, 0f, 0f), new Vector3(-5f, 0f, 0f) };
            Assert.AreEqual(0, JailLogic.NearestJailIndex(Vector3.zero, jails));
        }

        [Test]
        public void NearestJail_HeightIsIgnored()
        {
            // 평면 맵이라 y 차이로 감옥이 바뀌면 안 된다
            var jails = new List<Vector3> { new Vector3(3f, 50f, 0f), new Vector3(9f, 0f, 0f) };
            Assert.AreEqual(0, JailLogic.NearestJailIndex(Vector3.zero, jails));
        }

        [Test]
        public void NearestJail_NoJailsIsMinusOne()
        {
            Assert.AreEqual(-1, JailLogic.NearestJailIndex(Vector3.zero, new List<Vector3>()));
            Assert.AreEqual(-1, JailLogic.NearestJailIndex(Vector3.zero, null));
        }

        // ---- 구출 게이지 ----

        [Test]
        public void Rescue_AccumulatesWhileInside()
        {
            float p = JailLogic.AdvanceRescue(0f, 1f, _rescuerInside: true);
            Assert.AreEqual(1f, p, 0.001f);
            Assert.AreEqual(2.5f, JailLogic.AdvanceRescue(p, 1.5f, _rescuerInside: true), 0.001f);
        }

        [Test]
        public void Rescue_ResetsWhenRescuerLeaves()
        {
            // 반경을 벗어나면 처음부터 — 스치듯 지나가며 풀 수 없어야 구출이 도박이 된다
            Assert.AreEqual(0f, JailLogic.AdvanceRescue(2.9f, 0.5f, _rescuerInside: false), 0.001f);
        }

        [Test]
        public void Rescue_CompletesAtThreshold()
        {
            Assert.IsFalse(JailLogic.IsRescueComplete(2.99f, 3f));
            Assert.IsTrue(JailLogic.IsRescueComplete(3f, 3f));
        }

        [Test]
        public void Rescue_ProgressDoesNotRunAway()
        {
            // 게이지가 임계를 크게 넘어 쌓이면 다음 구출이 즉시 끝나 버린다
            float p = JailLogic.AdvanceRescue(2.9f, 10f, _rescuerInside: true);
            Assert.LessOrEqual(p, 3f + 0.001f);
        }

        // ---- 체류 시간 ----

        [Test]
        public void Hold_TicksDownAndFloorsAtZero()
        {
            Assert.AreEqual(2f, JailLogic.AdvanceHold(3f, 1f), 0.001f);
            Assert.AreEqual(0f, JailLogic.AdvanceHold(0.4f, 1f), 0.001f);
        }

        [Test]
        public void Hold_ExpiredOnlyAtZero()
        {
            Assert.IsFalse(JailLogic.IsHoldExpired(0.1f));
            Assert.IsTrue(JailLogic.IsHoldExpired(0f));
        }

        // ---- 승패 판정: 자유로운 쥐 ----

        [Test]
        public void Evaluate_AllMiceJailed_CatWin()
        {
            // 갇힌 쥐는 스스로 못 나온다 — 자유가 0이면 그 순간 끝(원작 '전원 잡힘')
            Assert.AreEqual(MatchResult.CatWin,
                MatchFlowLogic.Evaluate(_armed: true, _aliveCats: 1, _freeMice: 0, _jailedMice: 3));
        }

        [Test]
        public void Evaluate_OneFreeMouse_Continues()
        {
            Assert.AreEqual(MatchResult.None,
                MatchFlowLogic.Evaluate(_armed: true, _aliveCats: 1, _freeMice: 1, _jailedMice: 3));
        }

        [Test]
        public void Evaluate_CatDeadBeatsJailSweep()
        {
            // 동시에 성립하면 고양이 사망이 우선 — 고양이가 죽으면 감옥을 지킬 수 없다
            Assert.AreEqual(MatchResult.MouseWin,
                MatchFlowLogic.Evaluate(_armed: true, _aliveCats: 0, _freeMice: 0, _jailedMice: 2));
        }

        [Test]
        public void EvaluateAtTimeUp_OnlyJailedMice_CatWin()
        {
            // 지금까지는 '생존 쥐 > 0이면 쥐 승'이라, 전원 가둬 놓고도 시계에 졌다
            Assert.AreEqual(MatchResult.CatWin, MatchFlowLogic.EvaluateAtTimeUp(_freeMice: 0));
            Assert.AreEqual(MatchResult.MouseWin, MatchFlowLogic.EvaluateAtTimeUp(_freeMice: 1));
        }

        // ---- 석방 HP ----

        [Test]
        public void ReleaseHp_IsAFractionAndAtLeastOne()
        {
            Assert.AreEqual(50, JailLogic.ReleaseHp(100, 0.5f));
            // 비율이 0이어도 석방하자마자 죽으면 안 된다
            Assert.AreEqual(1, JailLogic.ReleaseHp(100, 0f));
        }
    }
}
