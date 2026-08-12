using CTC.Runtime.Ai;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 꺾는 점을 벽에서 띄운다. 이게 없으면 봇이 모퉁이마다 벽을 스치며 시간을 버린다.
    /// </summary>
    public class NavCornerLogicTests
    {
        [Test]
        public void Offset_StraightLine_DoesNotMove()
        {
            Vector3 corner = NavCornerLogic.Offset(
                new Vector3(0f, 0f, 0f), new Vector3(5f, 0f, 0f), new Vector3(10f, 0f, 0f));

            Assert.AreEqual(new Vector3(5f, 0f, 0f), corner, "직진이면 꺾을 게 없다");
        }

        [Test]
        public void Offset_LeftTurn_MovesToTheOutsideOfTheTurn()
        {
            // 동쪽으로 오다 북쪽으로 꺾는다 — 벽 모서리는 왼쪽(북서)에 있다.
            // 띄우는 방향은 그 반대(남동)여야 벽에서 멀어진다.
            Vector3 corner = NavCornerLogic.Offset(
                new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f), 1f);

            Assert.Greater(corner.x, 10f, "동쪽으로 밀려야 한다");
            Assert.Less(corner.z, 0f, "남쪽으로 밀려야 한다");
        }

        [Test]
        public void Offset_KeepsTheDistanceItWasAskedFor()
        {
            Vector3 corner = NavCornerLogic.Offset(
                new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f), 2f);

            Assert.AreEqual(2f, Vector3.Distance(corner, new Vector3(10f, 0f, 0f)), 0.001f);
        }

        [Test]
        public void Offset_DegenerateInput_ReturnsTheCorner()
        {
            var same = new Vector3(3f, 0f, 3f);

            Assert.AreEqual(same, NavCornerLogic.Offset(same, same, new Vector3(9f, 0f, 0f)));
            Assert.AreEqual(same, NavCornerLogic.Offset(new Vector3(0f, 0f, 0f), same, same));
        }

        [Test]
        public void Offset_IgnoresHeight()
        {
            // 층이 없는 맵이다 — y차 때문에 꺾는 방향이 달라지면 안 된다.
            Vector3 flat = NavCornerLogic.Offset(
                new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f), 1f);
            Vector3 raised = NavCornerLogic.Offset(
                new Vector3(0f, 3f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, -2f, 10f), 1f);

            Assert.AreEqual(flat.x, raised.x, 0.001f);
            Assert.AreEqual(flat.z, raised.z, 0.001f);
        }
    }
}
