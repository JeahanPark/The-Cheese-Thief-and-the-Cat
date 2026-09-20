using CTC.Runtime.Networking;
using NUnit.Framework;

namespace CTC.Tests
{
    public class SteamInviteLogicTests
    {
        [Test]
        public void TryParse_NoArgs_False()
        {
            Assert.IsFalse(SteamInviteLogic.TryParseConnectLobby(new[] { "CTC.exe" }, out _));
        }

        [Test]
        public void TryParse_Null_False()
        {
            Assert.IsFalse(SteamInviteLogic.TryParseConnectLobby(null, out _));
        }

        [Test]
        public void TryParse_ReadsLobbyId()
        {
            string[] args = { "CTC.exe", "+connect_lobby", "109775241000000000" };

            Assert.IsTrue(SteamInviteLogic.TryParseConnectLobby(args, out ulong id));
            Assert.AreEqual(109775241000000000UL, id);
        }

        /// <summary>스팀이 실행 옵션 뒤에 붙이기도 한다 — 위치를 가정하면 안 된다.</summary>
        [Test]
        public void TryParse_ArgumentNotFirst_StillFound()
        {
            string[] args = { "CTC.exe", "-screen-fullscreen", "1", "+connect_lobby", "42" };

            Assert.IsTrue(SteamInviteLogic.TryParseConnectLobby(args, out ulong id));
            Assert.AreEqual(42UL, id);
        }

        /// <summary>인자만 있고 값이 없으면 배열 끝을 넘어 읽으면 안 된다.</summary>
        [Test]
        public void TryParse_TrailingFlagWithoutValue_False()
        {
            Assert.IsFalse(SteamInviteLogic.TryParseConnectLobby(new[] { "CTC.exe", "+connect_lobby" }, out _));
        }

        [Test]
        public void TryParse_NonNumeric_False()
        {
            Assert.IsFalse(
                SteamInviteLogic.TryParseConnectLobby(new[] { "+connect_lobby", "not-a-number" }, out _));
        }

        /// <summary>0은 방 번호가 아니다 — 기본값과 구분이 안 된다.</summary>
        [Test]
        public void TryParse_Zero_False()
        {
            Assert.IsFalse(SteamInviteLogic.TryParseConnectLobby(new[] { "+connect_lobby", "0" }, out _));
        }

        /// <summary>리치 프레즌스에 넣는 값이 곧 커맨드라인이다 — 둘이 왕복해야 한다.</summary>
        [Test]
        public void ConnectValue_RoundTripsThroughParser()
        {
            const ulong LOBBY = 109775241234567890UL;
            string[] args = SteamInviteLogic.ConnectValue(LOBBY).Split(' ');

            Assert.IsTrue(SteamInviteLogic.TryParseConnectLobby(args, out ulong id));
            Assert.AreEqual(LOBBY, id);
        }

        [Test]
        public void ConnectValue_UsesSteamsArgumentName()
        {
            StringAssert.StartsWith(SteamInviteLogic.CONNECT_ARG, SteamInviteLogic.ConnectValue(7UL));
        }

        // ── 켜진 게임에서의 친구 목록 「게임 참가」(2026-09-20) ────────

        /// <summary>리치 프레즌스 값이 그대로 콜백 문자열로 온다 — 커맨드라인 파서와 왕복해야 한다.</summary>
        [Test]
        public void TryParseConnectString_RichPresenceValue_RoundTrips()
        {
            const ulong LOBBY = 109775241234567890UL;

            Assert.IsTrue(SteamInviteLogic.TryParseConnectString(SteamInviteLogic.ConnectValue(LOBBY), out ulong id));
            Assert.AreEqual(LOBBY, id);
        }

        [Test]
        public void TryParseConnectString_EmptyOrGarbage_False()
        {
            Assert.IsFalse(SteamInviteLogic.TryParseConnectString(null, out _));
            Assert.IsFalse(SteamInviteLogic.TryParseConnectString("   ", out _));
            Assert.IsFalse(SteamInviteLogic.TryParseConnectString("+connect_lobby", out _), "값 없음");
            Assert.IsFalse(SteamInviteLogic.TryParseConnectString("join 42", out _), "다른 규약");
        }
    }
}
