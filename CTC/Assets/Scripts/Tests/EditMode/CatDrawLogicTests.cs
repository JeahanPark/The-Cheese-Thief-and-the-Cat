using System.Collections.Generic;
using CTC.Runtime.Lobby;
using CTC.Runtime.Room;
using NUnit.Framework;

namespace CTC.Tests
{
    public class CatDrawLogicTests
    {
        private static List<ulong> Members(params ulong[] _ids) => new List<ulong>(_ids);

        [Test]
        public void Pick_Empty_ReturnsFallback()
        {
            Assert.AreEqual(7UL, CatDrawLogic.Pick(Members(), 0UL, 0, 7UL));
        }

        [Test]
        public void Pick_RollWrapsAroundCandidates()
        {
            List<ulong> members = Members(1, 2, 3);
            Assert.AreEqual(1UL, CatDrawLogic.Pick(members, 99UL, 3, 0UL));
            Assert.AreEqual(2UL, CatDrawLogic.Pick(members, 99UL, 4, 0UL));
        }

        [Test]
        public void Pick_NegativeRoll_StillInRange()
        {
            List<ulong> members = Members(1, 2, 3);
            ulong picked = CatDrawLogic.Pick(members, 99UL, -1, 0UL);
            CollectionAssert.Contains(members, picked);
        }

        /// <summary>연속으로 걸리면 재미가 없다 — 3명 이상이면 직전 고양이를 뺀다.</summary>
        [Test]
        public void Candidates_ThreePlayers_ExcludesPreviousCat()
        {
            List<ulong> candidates = CatDrawLogic.Candidates(Members(1, 2, 3), 2UL);
            CollectionAssert.AreEquivalent(new ulong[] { 1, 3 }, candidates);
        }

        [Test]
        public void Pick_ThreePlayers_NeverRepeatsPreviousCat()
        {
            List<ulong> members = Members(1, 2, 3);
            for (int roll = 0; roll < 12; roll++)
            {
                Assert.AreNotEqual(2UL, CatDrawLogic.Pick(members, 2UL, roll, 0UL));
            }
        }

        /// <summary>2명이면 빼는 순간 무작위가 아니게 된다 — 그대로 둔다.</summary>
        [Test]
        public void Candidates_TwoPlayers_KeepsPreviousCat()
        {
            List<ulong> candidates = CatDrawLogic.Candidates(Members(1, 2), 2UL);
            CollectionAssert.AreEquivalent(new ulong[] { 1, 2 }, candidates);
        }

        [Test]
        public void Candidates_AllArePreviousCat_FallsBackToEveryone()
        {
            List<ulong> candidates = CatDrawLogic.Candidates(Members(5, 5, 5), 5UL);
            Assert.AreEqual(3, candidates.Count);
        }

        [Test]
        public void RoleTable_ExactlyOneCat()
        {
            Dictionary<ulong, RoleId> table = CatDrawLogic.RoleTable(Members(1, 2, 3, 4), 3UL);

            Assert.AreEqual(4, table.Count);
            Assert.AreEqual(RoleId.Cat, table[3UL]);

            int cats = 0;
            foreach (KeyValuePair<ulong, RoleId> pair in table)
            {
                if (pair.Value == RoleId.Cat) cats++;
            }
            Assert.AreEqual(RoomLogic.CAT_COUNT, cats);
        }

        [Test]
        public void RoleTable_EveryoneElseIsMouse()
        {
            Dictionary<ulong, RoleId> table = CatDrawLogic.RoleTable(Members(1, 2, 3), 1UL);
            Assert.AreEqual(RoleId.Mouse, table[2UL]);
            Assert.AreEqual(RoleId.Mouse, table[3UL]);
        }
        // ---- 봇 자리 (2026-09-06) — 혼자+봇이면 사람이 늘 고양이였다. 봇도 후보다. ----

        [Test]
        public void WithBots_AppendsOneSeatPerBot()
        {
            List<ulong> pool = CatDrawLogic.WithBots(Members(1), 2);
            CollectionAssert.AreEqual(
                new ulong[] { 1, CatDrawLogic.BOT_CAT, CatDrawLogic.BOT_CAT }, pool);
        }

        [Test]
        public void WithBots_Zero_KeepsHumansOnly()
        {
            CollectionAssert.AreEqual(new ulong[] { 1, 2 }, CatDrawLogic.WithBots(Members(1, 2), 0));
        }

        [Test]
        public void Pick_SoloWithBots_CanPickBot()
        {
            List<ulong> pool = CatDrawLogic.WithBots(Members(1), 2);
            bool botPicked = false;
            for (int roll = 0; roll < 3; roll++)
            {
                if (CatDrawLogic.IsBot(CatDrawLogic.Pick(pool, 0UL, roll, 1UL))) botPicked = true;
            }
            Assert.IsTrue(botPicked);
        }

        [Test]
        public void IsBot_OnlyForBotSeat()
        {
            Assert.IsTrue(CatDrawLogic.IsBot(CatDrawLogic.BOT_CAT));
            Assert.IsFalse(CatDrawLogic.IsBot(0UL));
        }

        /// <summary>봇이 걸리면 사람은 전원 쥐 — 역할표에 봇 자리는 안 들어간다.</summary>
        [Test]
        public void RoleTable_BotCat_EveryHumanIsMouse()
        {
            Dictionary<ulong, RoleId> table = CatDrawLogic.RoleTable(Members(1, 2), CatDrawLogic.BOT_CAT);
            Assert.AreEqual(2, table.Count);
            Assert.AreEqual(RoleId.Mouse, table[1UL]);
            Assert.AreEqual(RoleId.Mouse, table[2UL]);
        }

        [Test]
        public void BotSplit_BotCat_TakesOneSeatFromMice()
        {
            Assert.AreEqual((2, 1), CatDrawLogic.BotSplit(3, CatDrawLogic.BOT_CAT));
            Assert.AreEqual((3, 0), CatDrawLogic.BotSplit(3, 5UL));
            Assert.AreEqual((0, 0), CatDrawLogic.BotSplit(0, CatDrawLogic.BOT_CAT));
        }
    }
}
