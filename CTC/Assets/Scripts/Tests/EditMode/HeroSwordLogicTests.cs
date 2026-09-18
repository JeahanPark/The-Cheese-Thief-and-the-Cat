using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Match;
using CTC.Runtime.Player;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>용사 칼·스턴·무적(2026-09-19) — 순수 판정 전수.</summary>
    public class HeroSwordLogicTests
    {
        // ───────────── 스턴 ─────────────

        [NUnit.Framework.Property("CaseId", "B-28")]
        [Test]
        public void Stunned_UntilTheStampPasses()
        {
            Assert.IsTrue(StunLogic.Stunned(_until: 10.5f, _now: 10.0f));
            Assert.IsFalse(StunLogic.Stunned(10.5f, 10.5f), "끝나는 순간은 풀린 것이다");
            Assert.IsFalse(StunLogic.Stunned(0f, 0f), "한 번도 안 맞았으면 0이다");
        }

        [NUnit.Framework.Property("CaseId", "B-28")]
        [Test]
        public void NextUntil_RefreshesNeverAdds()
        {
            // 남은 0.5초에 0.8초를 또 맞으면 1.3이 아니라 0.8이다 — 연쇄 스턴을 막는다.
            Assert.AreEqual(10.8f, StunLogic.NextUntil(_current: 10.5f, _now: 10.0f, _seconds: 0.8f), 1e-4f);
            // 남은 시간이 더 길면 줄이지 않는다.
            Assert.AreEqual(11.5f, StunLogic.NextUntil(11.5f, 10.0f, 0.8f), 1e-4f);
        }

        // ───────────── 칼 ─────────────

        [NUnit.Framework.Property("CaseId", "B-29")]
        [Test]
        public void SwordActive_OnlyForHero_RegardlessOfPhase()
        {
            Assert.IsTrue(HeroSwordLogic.Active(MouseJobId.Hero, MatchPhase.Hunt), "표를 밟는 순간부터다");
            Assert.IsTrue(HeroSwordLogic.Active(MouseJobId.Hero, MatchPhase.Gang));
            Assert.IsFalse(HeroSwordLogic.Active(MouseJobId.Gang, MatchPhase.Gang), "그쪽은 몽둥이다");
            Assert.IsFalse(HeroSwordLogic.Active(MouseJobId.None, MatchPhase.Hunt));
        }

        [NUnit.Framework.Property("CaseId", "B-29")]
        [Test]
        public void CanCut_EveryoneButAnotherHero()
        {
            Assert.IsTrue(HeroSwordLogic.CanCut(MouseJobId.Hero, MouseJobId.Gang), "2단계의 깡패도 벤다");
            Assert.IsTrue(HeroSwordLogic.CanCut(MouseJobId.Hero, MouseJobId.None));
            Assert.IsFalse(HeroSwordLogic.CanCut(MouseJobId.Hero, MouseJobId.Hero));
        }

        [NUnit.Framework.Property("CaseId", "B-29")]
        [Test]
        public void HeroCannotBuyGunPepperOrDash()
        {
            Assert.IsTrue(HeroSwordLogic.Denied(TechId.Gun));
            Assert.IsTrue(HeroSwordLogic.Denied(TechId.Pepper));
            Assert.IsTrue(HeroSwordLogic.Denied(TechId.Dash), "Space가 무적으로 찼다");
            Assert.IsFalse(HeroSwordLogic.Denied(TechId.Fortress));
            Assert.IsFalse(HeroSwordLogic.Denied(TechId.FastFeet));
        }

        // ───────────── 무적 ─────────────

        [NUnit.Framework.Property("CaseId", "B-30")]
        [Test]
        public void Absorb_EverythingWhileOn_NothingAfter()
        {
            Assert.AreEqual(0, HeroInvincibleLogic.AfterAbsorb(_damage: 90, _on: true));
            Assert.AreEqual(90, HeroInvincibleLogic.AfterAbsorb(90, false));
        }

        [NUnit.Framework.Property("CaseId", "B-30")]
        [Test]
        public void On_ForItsDurationOnly_AndReadyAfterCooldown()
        {
            Assert.IsTrue(HeroInvincibleLogic.On(_startedAt: 5f, _now: 7.9f, _seconds: 3f));
            Assert.IsFalse(HeroInvincibleLogic.On(5f, 8f, 3f), "3초째에 꺼진다");
            Assert.IsFalse(HeroInvincibleLogic.On(0f, 1f, 3f), "한 번도 안 켰으면 꺼져 있다");

            Assert.IsTrue(HeroInvincibleLogic.Ready(_lastUsed: 0f, _now: 10f, _cooldown: 10f));
            Assert.IsFalse(HeroInvincibleLogic.Ready(0f, 9.9f, 10f));
        }
    }
}
