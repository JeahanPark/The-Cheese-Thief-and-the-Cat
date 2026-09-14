using CTC.Runtime.Match;
using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>깡패 쥐(2026-09-14) — 켜지는 조건 · 몽둥이 부채꼴 · 편 가르기 · 두 발 이동 이름.</summary>
    public class GangLookLogicTests
    {
        [Test]
        public void Active_OnlyGangSideInGangPhase_NotWhenTicketPicked()
        {
            Assert.IsFalse(GangLookLogic.Active(MouseJobId.Gang, MatchPhase.Hunt), "표를 쥔 순간 바뀌면 누가 쥐었는지 드러난다");
            Assert.IsTrue(GangLookLogic.Active(MouseJobId.Gang, MatchPhase.Gang));
            Assert.IsTrue(GangLookLogic.Active(MouseJobId.Minion, MatchPhase.Gang));
            Assert.IsFalse(GangLookLogic.Active(MouseJobId.Hero, MatchPhase.Gang));
            Assert.IsFalse(GangLookLogic.Active(MouseJobId.None, MatchPhase.Gang));
        }

        [Test]
        public void InArc_RangeAndHalfAngle_ContactAlwaysHits()
        {
            Vector3 o = Vector3.zero, f = Vector3.forward;
            Assert.IsTrue(GangLookLogic.InArc(o, f, new Vector3(0f, 0f, 1.5f), 1.8f, 90f));
            Assert.IsFalse(GangLookLogic.InArc(o, f, new Vector3(0f, 0f, 2.0f), 1.8f, 90f), "사거리 밖");
            Assert.IsTrue(GangLookLogic.InArc(o, f, new Vector3(1f, 0f, 1.1f), 1.8f, 90f), "44도");
            Assert.IsFalse(GangLookLogic.InArc(o, f, new Vector3(1f, 0f, 0.8f), 1.8f, 90f), "51도");
            Assert.IsFalse(GangLookLogic.InArc(o, f, new Vector3(0f, 0f, -1f), 1.8f, 90f), "뒤");
            Assert.IsTrue(GangLookLogic.InArc(o, f, new Vector3(0f, 5f, 0.1f), 1.8f, 90f), "딱 붙으면 방향·높이 무시");
        }

        [Test]
        public void CanClub_GangDoesNotHitGang()
        {
            Assert.IsFalse(GangLookLogic.CanClub(MouseJobId.Gang, MouseJobId.Minion));
            Assert.IsFalse(GangLookLogic.CanClub(MouseJobId.Minion, MouseJobId.Gang));
            Assert.IsTrue(GangLookLogic.CanClub(MouseJobId.Gang, MouseJobId.None));
            Assert.IsTrue(GangLookLogic.CanClub(MouseJobId.Minion, MouseJobId.Hero));
        }

        [Test]
        public void FromSpeed_BipedSwapsLocomotionNames()
        {
            Assert.AreEqual(AnimationStateLogic.BipedIdle, AnimationStateLogic.FromSpeed(0f, 0.1f, 3.5f, true));
            Assert.AreEqual(AnimationStateLogic.BipedWalk, AnimationStateLogic.FromSpeed(1f, 0.1f, 3.5f, true));
            Assert.AreEqual(AnimationStateLogic.BipedRun, AnimationStateLogic.FromSpeed(5f, 0.1f, 3.5f, true));
            Assert.AreEqual(AnimationStateLogic.Walk, AnimationStateLogic.FromSpeed(1f, 0.1f, 3.5f));
            CollectionAssert.Contains(AnimationStateLogic.MouseOnly, AnimationStateLogic.ClubSwing);
            CollectionAssert.DoesNotContain(AnimationStateLogic.CatOnly, AnimationStateLogic.BipedWalk);
        }

        [Test]
        public void IsGunTech_OnlyGunAndPepper()
        {
            Assert.IsTrue(CTC.Runtime.Economy.TechLogic.IsGunTech(CTC.Runtime.Economy.TechId.Gun));
            Assert.IsTrue(CTC.Runtime.Economy.TechLogic.IsGunTech(CTC.Runtime.Economy.TechId.Pepper));
            Assert.IsFalse(CTC.Runtime.Economy.TechLogic.IsGunTech(CTC.Runtime.Economy.TechId.Dash), "대시는 깡패도 쓴다");
            Assert.IsFalse(CTC.Runtime.Economy.TechLogic.IsGunTech(CTC.Runtime.Economy.TechId.Fortress));
            StringAssert.Contains("총",
                CTC.Runtime.Economy.TechLogic.DenyText(CTC.Runtime.Economy.TechDeny.GangNoGun, CTC.Runtime.Lobby.RoleId.Mouse, 0, 5));
        }
    }
}
