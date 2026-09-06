using CTC.Runtime.Player;
using NUnit.Framework;

namespace CTC.Tests
{
    /// <summary>
    /// "이 몸을 누가 움직이나" 판정(2026-09-06). 2026-09-02에 입력 게이트를 `Mine`으로 통일하자
    /// **봇 몸의 Update가 통째로 막혀 봇이 한 발도 못 뗐다** — 봇은 서버가 조종하는 몸이라
    /// 입력은 안 읽되 이동은 돌아야 한다.
    /// </summary>
    public class LocalControlLogicTests
    {
        [Test]
        public void Mine_RequiresOwnerAndPlayerObject()
        {
            Assert.IsTrue(LocalControlLogic.Mine(_isOwner: true, _isPlayerObject: true));
            Assert.IsFalse(LocalControlLogic.Mine(_isOwner: true, _isPlayerObject: false), "호스트의 봇");
            Assert.IsFalse(LocalControlLogic.Mine(_isOwner: false, _isPlayerObject: true), "남의 몸");
        }

        [Test]
        public void Drives_MyBody_True()
        {
            Assert.IsTrue(LocalControlLogic.Drives(_isOwner: true, _isPlayerObject: true, _isServer: false));
        }

        /// <summary>호스트에서 봇: 소유자지만 플레이어 오브젝트가 아니다 — 서버가 움직인다.</summary>
        [Test]
        public void Drives_ServerBot_True()
        {
            Assert.IsTrue(LocalControlLogic.Drives(_isOwner: true, _isPlayerObject: false, _isServer: true));
        }

        /// <summary>클라이언트가 보는 봇: 서버가 아니라 움직이지 않는다(NetworkTransform이 받아 온다).</summary>
        [Test]
        public void Drives_BotOnClient_False()
        {
            Assert.IsFalse(LocalControlLogic.Drives(_isOwner: false, _isPlayerObject: false, _isServer: false));
        }

        /// <summary>서버가 보는 남의 몸: 그 사람이 움직인다 — 서버는 손대지 않는다.</summary>
        [Test]
        public void Drives_RemoteHumanOnServer_False()
        {
            Assert.IsFalse(LocalControlLogic.Drives(_isOwner: false, _isPlayerObject: true, _isServer: true));
        }
    }
}
