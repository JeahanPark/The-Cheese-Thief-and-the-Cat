using System.Collections.Generic;
using CTC.Runtime.Lobby.Browser;
using CTC.Runtime.Networking;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>대기실 앞단(2026-09-14) — 접속 관문·비밀번호·서버 목록·빠른 참가.</summary>
    public class FrontDoorLogicTests
    {
        // ── 관문 ──────────────────────────────────────────────

        [NUnit.Framework.Property("CaseId", "A-23")]
        [Test]
        public void Gate_HostSelf_AlwaysPasses_EvenClosedFullPassworded()
        {
            Assert.IsNull(ConnectionGateLogic.Decide(true, false, 99, 10, "ABC", ""));
        }

        [NUnit.Framework.Property("CaseId", "A-23")]
        [Test]
        public void Gate_Order_ClosedBeforeFullBeforePassword()
        {
            Assert.AreEqual(ConnectionGateLogic.CLOSED, ConnectionGateLogic.Decide(false, false, 10, 10, "ABC", "X"));
            Assert.AreEqual(ConnectionGateLogic.FULL, ConnectionGateLogic.Decide(false, true, 10, 10, "ABC", "X"));
            Assert.AreEqual(ConnectionGateLogic.PW_WRONG, ConnectionGateLogic.Decide(false, true, 3, 10, "ABC", "X"));
        }

        [Test]
        public void Gate_NoHostPassword_IgnoresWhatClientSent()
        {
            Assert.IsNull(ConnectionGateLogic.Decide(false, true, 1, 10, "", "ANYTHING"));
            Assert.IsNull(ConnectionGateLogic.Decide(false, true, 1, 10, null, null));
        }

        [Test]
        public void Gate_Password_RequiredWrongRight_CaseAndSpaceInsensitive()
        {
            Assert.AreEqual(ConnectionGateLogic.PW_REQUIRED, ConnectionGateLogic.Decide(false, true, 1, 10, "cheese", ""));
            Assert.AreEqual(ConnectionGateLogic.PW_WRONG, ConnectionGateLogic.Decide(false, true, 1, 10, "cheese", "chees"));
            Assert.IsNull(ConnectionGateLogic.Decide(false, true, 1, 10, "cheese", " CHE ESE "));
        }

        [Test]
        public void Gate_Messages_KnownCodesKorean_UnknownFallsBack()
        {
            Assert.AreEqual("비밀번호가 틀렸습니다.", ConnectionGateLogic.MessageOf(ConnectionGateLogic.PW_WRONG));
            Assert.AreEqual("방에 연결하지 못했습니다.", ConnectionGateLogic.MessageOf("Transport timeout"));
            Assert.AreEqual("방에 연결하지 못했습니다.", ConnectionGateLogic.MessageOf(null));
            Assert.IsTrue(ConnectionGateLogic.AsksPassword(ConnectionGateLogic.PW_REQUIRED));
            Assert.IsTrue(ConnectionGateLogic.AsksPassword(ConnectionGateLogic.PW_WRONG));
            Assert.IsFalse(ConnectionGateLogic.AsksPassword(ConnectionGateLogic.FULL));
        }

        [NUnit.Framework.Property("CaseId", "A-23")]
        [Test]
        public void Gate_IsGateCode_OnlyOurCodes_NotTransportText()
        {
            Assert.IsTrue(ConnectionGateLogic.IsGateCode(ConnectionGateLogic.CLOSED));
            Assert.IsTrue(ConnectionGateLogic.IsGateCode(ConnectionGateLogic.PW_REQUIRED));
            Assert.IsFalse(ConnectionGateLogic.IsGateCode("[Disconnect Event From Transport] Remote client disconnected."));
            Assert.IsFalse(ConnectionGateLogic.IsGateCode(""));
            Assert.IsFalse(ConnectionGateLogic.IsGateCode(null));
        }

        // ── 비밀번호 ──────────────────────────────────────────

        [NUnit.Framework.Property("CaseId", "A-24")]
        [Test]
        public void Password_Normalize_StripsSpacesUppercasesTruncates()
        {
            Assert.AreEqual("ABCDEFGHIJ", LobbyPasswordLogic.Normalize(" abc def ghij klm"));
            Assert.AreEqual("", LobbyPasswordLogic.Normalize(null));
            Assert.AreEqual("", LobbyPasswordLogic.Normalize("   "));
            Assert.IsFalse(LobbyPasswordLogic.HasPassword(" \t"));
        }

        [NUnit.Framework.Property("CaseId", "A-24")]
        [Test]
        public void Password_PayloadRoundTrip_AndGarbageIsSafe()
        {
            Assert.AreEqual("치즈1", LobbyPasswordLogic.FromPayload(LobbyPasswordLogic.ToPayload("치즈 1")));
            Assert.AreEqual("", LobbyPasswordLogic.FromPayload(null));
            Assert.AreEqual("", LobbyPasswordLogic.FromPayload(new byte[0]));
            var huge = new byte[5000];
            for (int i = 0; i < huge.Length; i++) huge[i] = (byte)'a';
            Assert.AreEqual(LobbyPasswordLogic.MAX_LENGTH, LobbyPasswordLogic.FromPayload(huge).Length);
        }

        [NUnit.Framework.Property("CaseId", "A-24")]
        [Test]
        public void SteamPasswordFlag_OnlyOneMeansYes()
        {
            Assert.AreEqual("1", SteamLobbyLogic.PasswordFlag(true));
            Assert.AreEqual("0", SteamLobbyLogic.PasswordFlag(false));
            Assert.IsTrue(SteamLobbyLogic.IsPasswordFlag("1"));
            Assert.IsFalse(SteamLobbyLogic.IsPasswordFlag(null));
            Assert.IsFalse(SteamLobbyLogic.IsPasswordFlag("0"));
        }

        // ── 서버 목록 ─────────────────────────────────────────

        private static List<LobbyRoomInfo> Rooms()
        {
            return new List<LobbyRoomInfo>
            {
                new LobbyRoomInfo("치즈 창고 털러 갈 사람", "야옹이대장", 6, 10, 0),
                new LobbyRoomInfo("초보만 환영", "생쥐삼형제", 10, 10, 1),
                new LobbyRoomInfo("Cheese Run", "Mouse", 9, 10, 2),
                new LobbyRoomInfo("아무나", "치즈러버", 2, 10, 3),
            };
        }

        [NUnit.Framework.Property("CaseId", "A-25")]
        [Test]
        public void Filter_AllTermsMustMatch_NameOrHost_CaseInsensitive()
        {
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, ServerListLogic.Filter(Rooms(), "  "));
            CollectionAssert.AreEqual(new[] { 0, 3 }, ServerListLogic.Filter(Rooms(), "치즈"));
            CollectionAssert.AreEqual(new[] { 0 }, ServerListLogic.Filter(Rooms(), "치즈 야옹"));
            CollectionAssert.AreEqual(new[] { 2 }, ServerListLogic.Filter(Rooms(), "cheese MOUSE"));
            CollectionAssert.IsEmpty(ServerListLogic.Filter(Rooms(), "없는방"));
            CollectionAssert.IsEmpty(ServerListLogic.Filter(null, "x"));
        }

        [NUnit.Framework.Property("CaseId", "A-25")]
        [Test]
        public void Pages_CountClampStart()
        {
            Assert.AreEqual(1, ServerListLogic.PageCount(0));
            Assert.AreEqual(1, ServerListLogic.PageCount(8));
            Assert.AreEqual(2, ServerListLogic.PageCount(9));
            Assert.AreEqual(1, ServerListLogic.ClampPage(5, 9));
            Assert.AreEqual(0, ServerListLogic.ClampPage(-1, 9));
            Assert.AreEqual(8, ServerListLogic.PageStart(1, 9));
            Assert.AreEqual(0, ServerListLogic.PageStart(3, 0));
        }

        [NUnit.Framework.Property("CaseId", "A-25")]
        [Test]
        public void QuickMatch_FullestRoomWithSpace_ElseNone()
        {
            Assert.AreEqual(2, ServerListLogic.PickQuickMatch(Rooms()), "10/10은 건너뛰고 9명 방");
            var allFull = new List<LobbyRoomInfo> { new LobbyRoomInfo("a", "b", 10, 10, 0) };
            Assert.AreEqual(-1, ServerListLogic.PickQuickMatch(allFull));
            Assert.AreEqual(-1, ServerListLogic.PickQuickMatch(new List<LobbyRoomInfo>()));
        }
    }
}
