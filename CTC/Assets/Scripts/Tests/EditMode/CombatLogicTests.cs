using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class CombatLogicTests
    {
        [Test]
        public void ApplyDamage_Reduces()
        {
            Assert.AreEqual(40, CombatLogic.ApplyDamage(100, 60));
        }

        [Test]
        public void ApplyDamage_ClampsAtZero()
        {
            Assert.AreEqual(0, CombatLogic.ApplyDamage(30, 60));
        }

        [Test]
        public void ApplyDamage_NegativeDamage_Ignored()
        {
            Assert.AreEqual(100, CombatLogic.ApplyDamage(100, -5));
        }

        [Test]
        public void IsDead_ZeroHp_True()
        {
            Assert.IsTrue(CombatLogic.IsDead(0));
            Assert.IsFalse(CombatLogic.IsDead(1));
        }

        [Test]
        public void Cooldown_NotElapsed_False()
        {
            Assert.IsFalse(CombatLogic.IsCooldownReady(10.0, 11.0, 2.5f));
        }

        [Test]
        public void Cooldown_Elapsed_True()
        {
            Assert.IsTrue(CombatLogic.IsCooldownReady(10.0, 12.5, 2.5f));
        }

        [Test]
        public void PounceArc_InRangeInAngle_True()
        {
            Assert.IsTrue(CombatLogic.IsInPounceArc(
                Vector3.zero, Vector3.forward, new Vector3(0.5f, 0f, 1.5f), 2.2f, 60f));
        }

        [Test]
        public void PounceArc_OutOfRange_False()
        {
            Assert.IsFalse(CombatLogic.IsInPounceArc(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 3f), 2.2f, 60f));
        }

        [Test]
        public void PounceArc_BehindAttacker_False()
        {
            Assert.IsFalse(CombatLogic.IsInPounceArc(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -1f), 2.2f, 60f));
        }

        [Test]
        public void PounceArc_IgnoresHeightDifference()
        {
            // y 차이는 무시(평면 판정) — 점프/단차에서 판정 누락 방지
            Assert.IsTrue(CombatLogic.IsInPounceArc(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0.8f, 1f), 2.2f, 60f));
        }
    
        // ---- 교전 대상 (v3.8 5번 제3세력) ----

        [Test]
        public void CanAttack_HitsOpposingSide()
        {
            Assert.IsTrue(CombatLogic.CanAttack(RoleId.Cat, RoleId.Mouse));
            Assert.IsTrue(CombatLogic.CanAttack(RoleId.Mouse, RoleId.Cat));
        }

        [Test]
        public void CanAttack_NeverHitsOwnSide()
        {
            // 쥐가 쥐를, 고양이가 고양이를 때리면 안 된다. 요새·미끼는 RoleId.Mouse라 여기 걸린다.
            Assert.IsFalse(CombatLogic.CanAttack(RoleId.Mouse, RoleId.Mouse));
            Assert.IsFalse(CombatLogic.CanAttack(RoleId.Cat, RoleId.Cat));
        }

        [Test]
        public void CanAttack_BothSidesCanHitThirdFaction()
        {
            // 이게 없으면 강아지를 아무도 못 죽인다 — 실제로 그렇게 만들어 놓고 qa_v40이 잡았다.
            Assert.IsTrue(CombatLogic.CanAttack(RoleId.Cat, RoleId.None));
            Assert.IsTrue(CombatLogic.CanAttack(RoleId.Mouse, RoleId.None));
        }
}
}
