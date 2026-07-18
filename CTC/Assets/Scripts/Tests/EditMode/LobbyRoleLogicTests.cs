using System.Collections.Generic;
using CTC.Runtime.Lobby;
using NUnit.Framework;
using UnityEngine;

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

        // Vector3 정확 비교(Assert.AreEqual)는 float 오차(-12f+1.8f ≠ -10.2f 리터럴,
        // yaw 회전 epsilon)로 깨진다 — 거리 허용오차 비교 사용.
        private static void AssertPos(Vector3 _expected, Vector3 _actual)
        {
            Assert.Less(Vector3.Distance(_expected, _actual), 1e-4f,
                $"expected {_expected} but was {_actual}");
        }

        [Test]
        public void SpawnPosition_Cat_CenterLine()
        {
            var roles = new List<RoleId> { RoleId.Cat, RoleId.Mouse, RoleId.Cat };
            var noForts = new List<(Vector3 pos, float yaw)>();
            // 0번째 고양이 (0,0.1,0), 1번째 고양이 (2,0.1,0) — 요새 무관
            AssertPos(new Vector3(0f, 0.1f, 0f), LobbyManager.SpawnPosition(roles, 0, noForts));
            AssertPos(new Vector3(2f, 0.1f, 0f), LobbyManager.SpawnPosition(roles, 2, noForts));
        }

        [Test]
        public void SpawnPosition_Mouse_AtOwnFortressEntrance()
        {
            var roles = new List<RoleId> { RoleId.Cat, RoleId.Mouse, RoleId.Mouse };
            var forts = new List<(Vector3 pos, float yaw)>
            {
                (new Vector3(-11f, 0f, -12f), 0f),
                (new Vector3(9f, 0f, -12.5f), 0f),
            };
            // 0번째 쥐 = 요새0 입구 앞 1.8m — (-11,0,-12)+yaw0 전방(+Z)*1.8, y 0.1
            AssertPos(new Vector3(-11f, 0.1f, -10.2f), LobbyManager.SpawnPosition(roles, 1, forts));
            // 1번째 쥐 = 요새1 — (9,0,-12.5)+yaw0 전방*1.8
            AssertPos(new Vector3(9f, 0.1f, -10.7f), LobbyManager.SpawnPosition(roles, 2, forts));
        }
    }
}
