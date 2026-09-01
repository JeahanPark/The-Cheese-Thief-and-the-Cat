using NUnit.Framework;
using CTC.Runtime.Room;

namespace CTC.Tests
{
    public class RoomBotLogicTests
    {
        [Test]
        public void ClampBots_Fits_Remaining_Seats()
        {
            // 방 정원 10 — 사람 3이면 봇은 7까지
            Assert.AreEqual(7, RoomBotLogic.ClampBots(9, 3));
            Assert.AreEqual(0, RoomBotLogic.ClampBots(-2, 3));
            Assert.AreEqual(4, RoomBotLogic.ClampBots(4, 3));
        }

        [Test]
        public void ClampBots_Full_Room_Allows_None()
        {
            Assert.AreEqual(0, RoomBotLogic.ClampBots(5, RoomLogic.MAX_PLAYERS));
        }

        [Test]
        public void CanStart_Counts_Bots_As_Players()
        {
            // 혼자여도 봇 1을 채우면 2명 조건을 만족한다 — 봇 테스트의 핵심
            Assert.IsFalse(RoomBotLogic.CanStartWithBots(1, 0, _isHost: true));
            Assert.IsTrue(RoomBotLogic.CanStartWithBots(1, 1, _isHost: true));
            Assert.IsFalse(RoomBotLogic.CanStartWithBots(1, 5, _isHost: false), "방장만 시작한다");
        }

        [Test]
        public void BotHint_Says_Total()
        {
            StringAssert.Contains("3", RoomBotLogic.BotHint(1, 2));   // 사람1+봇2 = 3
        }
    }
}
