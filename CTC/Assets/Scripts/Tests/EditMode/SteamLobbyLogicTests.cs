using CTC.Runtime.Networking;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    public class SteamLobbyLogicTests
    {
        [Test]
        public void IsCtcLobby_OnlyExactTagPasses()
        {
            Assert.IsTrue(SteamLobbyLogic.IsCtcLobby("CTC"));
            Assert.IsFalse(SteamLobbyLogic.IsCtcLobby("ctc"));
            Assert.IsFalse(SteamLobbyLogic.IsCtcLobby(""));
            Assert.IsFalse(SteamLobbyLogic.IsCtcLobby(null));
        }

        [Test]
        public void RoomLabel_NameWithCount()
        {
            Assert.AreEqual("치즈방 (2/8)", SteamLobbyLogic.RoomLabel("치즈방", 2, 8));
        }

        [Test]
        public void RoomLabel_EmptyName_Fallback()
        {
            Assert.AreEqual("이름 없는 방 (1/8)", SteamLobbyLogic.RoomLabel("  ", 1, 8));
        }

        [Test]
        public void DefaultRoomName_OwnerOrFallback()
        {
            Assert.AreEqual("cgq의 방", SteamLobbyLogic.DefaultRoomName("cgq"));
            Assert.AreEqual("CTC 방", SteamLobbyLogic.DefaultRoomName(""));
        }
    }
}
