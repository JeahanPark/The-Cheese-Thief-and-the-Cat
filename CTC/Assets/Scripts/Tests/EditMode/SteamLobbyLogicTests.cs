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

        // ── 방금 나온 내 방 숨기기 (2026-09-20) ─────────────────────

        [Test]
        public void IsRecentlyLeft_SameLobbyInsideWindow_True()
        {
            Assert.IsTrue(SteamLobbyLogic.IsRecentlyLeft(5UL, 5UL, 100f, 100f + SteamLobbyLogic.RECENTLY_LEFT_SECONDS - 1f));
        }

        [Test]
        public void IsRecentlyLeft_WindowPassed_False()
        {
            Assert.IsFalse(SteamLobbyLogic.IsRecentlyLeft(5UL, 5UL, 100f, 100f + SteamLobbyLogic.RECENTLY_LEFT_SECONDS));
        }

        [Test]
        public void IsRecentlyLeft_OtherLobbyOrNeverLeft_False()
        {
            Assert.IsFalse(SteamLobbyLogic.IsRecentlyLeft(6UL, 5UL, 100f, 101f), "다른 방");
            Assert.IsFalse(SteamLobbyLogic.IsRecentlyLeft(0UL, 0UL, float.NegativeInfinity, 0f), "나온 적 없음(0)");
        }

        // ── 방장 이름 (2026-09-20) ─────────────────────────────────

        [Test]
        public void HostLabel_LobbyDataFirst_ThenOwner_ThenEmpty()
        {
            Assert.AreEqual("cgq", SteamLobbyLogic.HostLabel(" cgq ", "owner"));
            Assert.AreEqual("owner", SteamLobbyLogic.HostLabel("", "owner"), "안 들어간 방은 Owner가 비지만, 값이 있으면 쓴다");
            Assert.AreEqual("", SteamLobbyLogic.HostLabel(null, null));
        }
    }
}
