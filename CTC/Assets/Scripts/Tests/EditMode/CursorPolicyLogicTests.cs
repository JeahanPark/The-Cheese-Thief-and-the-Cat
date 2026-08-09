using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests
{
    public class CursorPolicyLogicTests
    {
        [Test]
        public void ShouldRelease_Lobby_Released()
        {
            Assert.IsTrue(CursorPolicyLogic.ShouldRelease(false, false, false));
        }

        [Test]
        public void ShouldRelease_InGame_Locked()
        {
            Assert.IsFalse(CursorPolicyLogic.ShouldRelease(true, false, false));
        }

        [Test]
        public void ShouldRelease_InGameResultShowing_Released()
        {
            Assert.IsTrue(CursorPolicyLogic.ShouldRelease(true, true, false));
        }

        [Test]
        public void ShouldRelease_InGameModalOpen_Released()
        {
            // 2026-08-08 버그: 이 항이 판정에 없어 옵션 창을 열어도 다음 프레임에 도로 잠겼다
            Assert.IsTrue(CursorPolicyLogic.ShouldRelease(true, false, true));
        }

        [Test]
        public void ShouldRelease_LobbyModalOpen_Released()
        {
            Assert.IsTrue(CursorPolicyLogic.ShouldRelease(false, false, true));
        }

        [Test]
        public void ShouldRelease_InGameResultAndModal_Released()
        {
            Assert.IsTrue(CursorPolicyLogic.ShouldRelease(true, true, true));
        }
    }
}
