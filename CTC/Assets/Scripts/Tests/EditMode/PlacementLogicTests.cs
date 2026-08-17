using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 랜드마크(벽감)를 맵에 흩는 규칙. 여기가 무너지면 한쪽 구석에만 사건이 생기고
    /// 반대편은 아무 일도 안 일어난다 — 실제로 120×120 맵에서 벽감 6개가 한 줄에 몰렸다.
    /// </summary>
    public class PlacementLogicTests
    {
        // 3×3 격자 — 목록 순서는 아래 줄부터다(생성기가 만드는 순서와 같다)
        private static List<Vector2> Grid()
        {
            var pts = new List<Vector2>();
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                    pts.Add(new Vector2(i * 10f, j * 10f));
            return pts;
        }

        [Test]
        public void SpreadPick_DoesNotJustTakeTheFirstN()
        {
            // 앞에서부터 집으면 (-10,-10) (0,-10) (10,-10) — 맨 아랫줄만 나온다
            List<Vector2> pts = Grid();
            List<int> picked = PlacementLogic.SpreadPick(pts, 3, PlacementLogic.NearestTo(pts, Vector2.zero));

            var rows = new HashSet<float>();
            foreach (int i in picked) rows.Add(pts[i].y);
            Assert.Greater(rows.Count, 1, "전부 같은 줄에서 골랐다");
        }

        [Test]
        public void SpreadPick_KeepsThemApart()
        {
            List<Vector2> pts = Grid();
            List<int> picked = PlacementLogic.SpreadPick(pts, 3, 4);   // 한가운데에서 시작

            for (int a = 0; a < picked.Count; a++)
                for (int b = a + 1; b < picked.Count; b++)
                    Assert.Greater(Vector2.Distance(pts[picked[a]], pts[picked[b]]), 9f);
        }

        [Test]
        public void SpreadPick_NeverReturnsMoreThanAvailable()
        {
            List<Vector2> pts = Grid();
            Assert.AreEqual(pts.Count, PlacementLogic.SpreadPick(pts, 99, 0).Count);
            Assert.IsEmpty(PlacementLogic.SpreadPick(pts, 0, 0));
            Assert.IsEmpty(PlacementLogic.SpreadPick(new List<Vector2>(), 3, 0));
        }

        [Test]
        public void SpreadPick_IsDeterministic()
        {
            // 같은 레이아웃이면 같은 자리가 나와야 QA가 재현할 수 있다
            List<Vector2> pts = Grid();
            CollectionAssert.AreEqual(
                PlacementLogic.SpreadPick(pts, 4, 0), PlacementLogic.SpreadPick(pts, 4, 0));
        }

        [Test]
        public void NearestTo_PicksTheClosest()
        {
            List<Vector2> pts = Grid();
            Assert.AreEqual(Vector2.zero, pts[PlacementLogic.NearestTo(pts, Vector2.zero)]);
            Assert.AreEqual(-1, PlacementLogic.NearestTo(new List<Vector2>(), Vector2.zero));
        }

        [Test]
        public void RoomHoldsAny_SeesFortressInsideTheRoom()
        {
            // 랜드마크가 요새(=쥐 스폰) 방에 들어가는지 가려내는 판정
            var room = new RoomRect { Id = "R0", Center = new Vector2(0f, 0f), Size = new Vector2(10f, 8f) };

            Assert.IsTrue(PlacementLogic.RoomHoldsAny(room, new List<Vector2> { new Vector2(2f, 1f) }, 1f));
            Assert.IsFalse(PlacementLogic.RoomHoldsAny(room, new List<Vector2> { new Vector2(20f, 0f) }, 1f));
            Assert.IsFalse(PlacementLogic.RoomHoldsAny(room, null, 1f));
        }
    }
}
