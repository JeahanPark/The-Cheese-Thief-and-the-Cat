using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests
{
    public class LobbyRoleLogicTests
    {
        [Test]
        public void CanSelect_Mouse_AlwaysAllowed()
        {
            var roles = new[] { RoleId.None, RoleId.Mouse };
            Assert.IsTrue(LobbyRoleLogic.CanSelect(roles, 0, RoleId.Mouse, 1));
        }

        [Test]
        public void CanSelect_Cat_WhenCapacityFree_Allowed()
        {
            var roles = new[] { RoleId.None, RoleId.Mouse };
            Assert.IsTrue(LobbyRoleLogic.CanSelect(roles, 0, RoleId.Cat, 1));
        }

        [Test]
        public void CanSelect_Cat_WhenAnotherIsCat_Denied()
        {
            var roles = new[] { RoleId.None, RoleId.Cat };
            Assert.IsFalse(LobbyRoleLogic.CanSelect(roles, 0, RoleId.Cat, 1));
        }

        [Test]
        public void CanSelect_Cat_ReselectBySameCat_Allowed()
        {
            // 자기 자신이 이미 고양이면 재선택은 정원을 소모하지 않는다
            var roles = new[] { RoleId.Cat, RoleId.Mouse };
            Assert.IsTrue(LobbyRoleLogic.CanSelect(roles, 0, RoleId.Cat, 1));
        }

        [Test]
        public void CanSelect_None_Denied()
        {
            var roles = new[] { RoleId.Mouse, RoleId.Mouse };
            Assert.IsFalse(LobbyRoleLogic.CanSelect(roles, 0, RoleId.None, 1));
        }

        [Test]
        public void CanSelect_InvalidIndex_Denied()
        {
            var roles = new[] { RoleId.None };
            Assert.IsFalse(LobbyRoleLogic.CanSelect(roles, 5, RoleId.Mouse, 1));
        }

        [Test]
        public void CanStart_OneCatOneMouse_True()
        {
            Assert.IsTrue(LobbyRoleLogic.CanStart(new[] { RoleId.Cat, RoleId.Mouse }));
        }

        [Test]
        public void CanStart_AnyoneUnselected_False()
        {
            Assert.IsFalse(LobbyRoleLogic.CanStart(new[] { RoleId.Cat, RoleId.None }));
        }

        [Test]
        public void CanStart_NoCat_False()
        {
            Assert.IsFalse(LobbyRoleLogic.CanStart(new[] { RoleId.Mouse, RoleId.Mouse }));
        }

        [Test]
        public void CanStart_NoMouse_False()
        {
            // 2명 이상이어도 쥐가 없으면 시작 불가 — mice >= 1 분기 검증
            Assert.IsFalse(LobbyRoleLogic.CanStart(new[] { RoleId.Cat, RoleId.Cat }));
        }

        [Test]
        public void CanStart_SinglePlayer_False()
        {
            Assert.IsFalse(LobbyRoleLogic.CanStart(new[] { RoleId.Cat }));
        }
    }
}
