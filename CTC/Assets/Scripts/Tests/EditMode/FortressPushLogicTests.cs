using CTC.Runtime.Economy;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 요새 즉발 설치의 밀어내기(2026-08-15). 여기가 틀리면 고양이가 요새 안에 갇히거나
    /// 옆에 서 있던 쥐가 벽에 끼인다.
    /// </summary>
    public class FortressPushLogicTests
    {
        private const float CAT_R = 0.43f;   // 고양이 CC 반경
        private const float MOUSE_R = 0.22f; // 쥐 CC 반경
        private static readonly Vector3 C = new Vector3(10f, 0f, 5f);

        [Test]
        public void Cat_AtCenter_IsPushedOutside()
        {
            bool push = FortressPushLogic.PushDelta(C, 0f, C, CAT_R, true, out Vector3 d);
            Assert.IsTrue(push);
            Vector3 after = C + d;
            float local = Mathf.Max(Mathf.Abs(after.x - C.x), Mathf.Abs(after.z - C.z));
            Assert.Greater(local, FortressPushLogic.HALF + CAT_R, "벽 바깥이어야 한다");
        }

        [Test]
        public void Cat_NearRightEdge_LeavesThroughRight()
        {
            Vector3 body = C + new Vector3(0.9f, 0f, 0.1f);
            Assert.IsTrue(FortressPushLogic.PushDelta(C, 0f, body, CAT_R, true, out Vector3 d));
            Assert.Greater(d.x, 0f);
            Assert.AreEqual(0f, d.z, 1e-4f);
        }

        [Test]
        public void Cat_AlreadyOutside_NotPushed()
        {
            Vector3 body = C + new Vector3(FortressPushLogic.HALF + CAT_R + 0.05f, 0f, 0f);
            Assert.IsFalse(FortressPushLogic.PushDelta(C, 0f, body, CAT_R, true, out _));
        }

        [Test]
        public void Mouse_AtCenter_StaysProtected()
        {
            Assert.IsFalse(FortressPushLogic.PushDelta(C, 0f, C, MOUSE_R, false, out _));
        }

        [Test]
        public void Mouse_InSideWall_IsPushedToNearerSide()
        {
            // 벽 안쪽 면에 붙은 쥐 → 안으로
            Vector3 inner = C + new Vector3(FortressPushLogic.HALF - FortressPushLogic.WALL, 0f, 0f);
            Assert.IsTrue(FortressPushLogic.PushDelta(C, 0f, inner, MOUSE_R, false, out Vector3 d1));
            Assert.Less(d1.x, 0f);

            // 벽 바깥면에 붙은 쥐 → 밖으로
            Vector3 outer = C + new Vector3(FortressPushLogic.HALF + 0.05f, 0f, 0f);
            Assert.IsTrue(FortressPushLogic.PushDelta(C, 0f, outer, MOUSE_R, false, out Vector3 d2));
            Assert.Greater(d2.x, 0f);
        }

        [Test]
        public void Mouse_InDoorway_NotPushed()
        {
            // 앞벽(z=+HALF) 가운데는 문 — 벽이 없으니 안 민다
            Vector3 body = C + new Vector3(0f, 0f, FortressPushLogic.HALF);
            Assert.IsFalse(FortressPushLogic.PushDelta(C, 0f, body, MOUSE_R, false, out _));
        }

        [Test]
        public void Yaw_RotatesTheFrame()
        {
            // yaw 90 → 로컬 +Z(문)가 월드 +X. 월드 +X 쪽 벽 가운데는 문이라 쥐를 안 밀고,
            // 월드 +Z 쪽(로컬 -X, 통벽)은 민다.
            Vector3 doorSide = C + new Vector3(FortressPushLogic.HALF, 0f, 0f);
            Assert.IsFalse(FortressPushLogic.PushDelta(C, 90f, doorSide, MOUSE_R, false, out _));

            Vector3 solidSide = C + new Vector3(0f, 0f, FortressPushLogic.HALF);
            Assert.IsTrue(FortressPushLogic.PushDelta(C, 90f, solidSide, MOUSE_R, false, out Vector3 d));
            Assert.AreEqual(0f, d.x, 1e-3f);
            Assert.AreNotEqual(0f, d.z);
        }
    }
}
