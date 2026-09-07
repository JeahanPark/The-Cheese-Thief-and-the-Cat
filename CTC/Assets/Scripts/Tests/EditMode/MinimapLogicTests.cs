using CTC.Runtime.Lobby;
using CTC.Runtime.UI;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class MinimapLogicTests
    {
        // 창고 맵 기준(2026-09-08): 120×120 m를 256 px에 담는다.
        private const float HALF = 60f;
        private const int PX = 256;

        [Test]
        public void WorldToPixel_OriginIsTextureCenter()
        {
            Assert.AreEqual(new Vector2Int(PX / 2, PX / 2), MinimapLogic.WorldToPixel(Vector2.zero, HALF, PX));
        }

        [Test]
        public void WorldToPixel_FourCorners()
        {
            // z+가 위 — 남서(-,-)가 좌하단, 북동(+,+)이 우상단이다
            Assert.AreEqual(new Vector2Int(0, 0), MinimapLogic.WorldToPixel(new Vector2(-HALF, -HALF), HALF, PX));
            Assert.AreEqual(new Vector2Int(PX - 1, 0), MinimapLogic.WorldToPixel(new Vector2(HALF, -HALF), HALF, PX));
            Assert.AreEqual(new Vector2Int(0, PX - 1), MinimapLogic.WorldToPixel(new Vector2(-HALF, HALF), HALF, PX));
            Assert.AreEqual(new Vector2Int(PX - 1, PX - 1), MinimapLogic.WorldToPixel(new Vector2(HALF, HALF), HALF, PX));
        }

        [Test]
        public void WorldToPixel_OutsideMapClampsToEdge()
        {
            // 맵 밖으로 밀려나도 텍스처 밖을 찍으면 안 된다
            Assert.AreEqual(new Vector2Int(PX - 1, 0), MinimapLogic.WorldToPixel(new Vector2(1000f, -1000f), HALF, PX));
            Assert.AreEqual(new Vector2Int(0, PX - 1), MinimapLogic.WorldToPixel(new Vector2(-1000f, 1000f), HALF, PX));
        }

        [Test]
        public void PixelToWorld_RoundTripsWithinOnePixel()
        {
            // QA가 「저 점이 가리키는 자리」를 되물을 때 쓰는 역변환 — 픽셀 한 칸 안으로 돌아와야 한다.
            float pixelMeters = (HALF * 2f) / PX;
            foreach (Vector2 world in new[] { Vector2.zero, new Vector2(17.5f, -42.25f), new Vector2(-59f, 59f) })
            {
                Vector2Int px = MinimapLogic.WorldToPixel(world, HALF, PX);
                Vector2 back = MinimapLogic.PixelToWorld(new Vector2(px.x + 0.5f, px.y + 0.5f), HALF, PX);
                Assert.Less(Vector2.Distance(back, world), pixelMeters, $"{world} 왕복이 한 픽셀을 넘었다");
            }
        }

        [Test]
        public void PixelToWorld_CenterPixelIsOrigin()
        {
            Vector2 origin = MinimapLogic.PixelToWorld(new Vector2(PX * 0.5f, PX * 0.5f), HALF, PX);
            Assert.AreEqual(0f, origin.x, 1e-3f);
            Assert.AreEqual(0f, origin.y, 1e-3f);
        }

        [Test]
        public void RectToPixels_ThinShelfKeepsMinimumThickness()
        {
            // 0.1 m 선반은 256 px에서 0.2 px — 보장이 없으면 통째로 사라진다
            RectInt r = MinimapLogic.RectToPixels(Vector2.zero, new Vector2(0.1f, 20f), HALF, PX, MinimapLogic.MIN_THICKNESS);
            Assert.AreEqual(MinimapLogic.MIN_THICKNESS, r.width);
            Assert.Greater(r.height, MinimapLogic.MIN_THICKNESS);
            // 벌리더라도 중심은 지킨다
            Assert.AreEqual(PX / 2, r.xMin + r.width / 2);
        }

        [Test]
        public void RectToPixels_PartlyOutsideIsClipped()
        {
            // 서쪽 경계에 반쯤 걸친 사각형 — 왼쪽 절반이 잘리고 x는 0에서 시작한다
            RectInt r = MinimapLogic.RectToPixels(new Vector2(-HALF, 0f), new Vector2(20f, 20f), HALF, PX, MinimapLogic.MIN_THICKNESS);
            Assert.AreEqual(0, r.xMin);
            Assert.Greater(r.width, 0);
            Assert.Less(r.width, Mathf.RoundToInt(20f * PX / (HALF * 2f)));
            Assert.LessOrEqual(r.xMax, PX);
        }

        [Test]
        public void RectToPixels_FullyOutsideIsEmpty()
        {
            RectInt r = MinimapLogic.RectToPixels(new Vector2(200f, 0f), new Vector2(5f, 5f), HALF, PX, MinimapLogic.MIN_THICKNESS);
            Assert.AreEqual(0, r.width);
            Assert.AreEqual(0, r.height);
        }

        [Test]
        public void IsDiscovered_BoundaryCounts()
        {
            // CSV의 12 m는 「닿으면 발견」이다 — 딱 12 m가 false면 값의 뜻이 어긋난다
            Assert.IsTrue(MinimapLogic.IsDiscovered(new Vector2(12f, 0f), Vector2.zero, 12f));
            Assert.IsTrue(MinimapLogic.IsDiscovered(new Vector2(11.9f, 0f), Vector2.zero, 12f));
            Assert.IsFalse(MinimapLogic.IsDiscovered(new Vector2(12.1f, 0f), Vector2.zero, 12f));
        }

        [Test]
        public void ShowsShops_BothSidesSeeThem()
        {
            // 상점은 쥐도 고양이도 본다 — 여기서 만나라는 기획이다
            Assert.IsTrue(MinimapLogic.ShowsShops(RoleId.Mouse));
            Assert.IsTrue(MinimapLogic.ShowsShops(RoleId.Cat));
            Assert.IsFalse(MinimapLogic.ShowsShops(RoleId.None));
        }

        [Test]
        public void ShowsCheese_MouseOnly()
        {
            Assert.IsTrue(MinimapLogic.ShowsCheese(RoleId.Mouse));
            Assert.IsFalse(MinimapLogic.ShowsCheese(RoleId.Cat), "고양이가 매장지를 알면 매복이 공짜다");
            Assert.IsFalse(MinimapLogic.ShowsCheese(RoleId.None));
        }

        [Test]
        public void ShowsOwnStructures_MouseOnly()
        {
            Assert.IsTrue(MinimapLogic.ShowsOwnStructures(RoleId.Mouse));
            Assert.IsFalse(MinimapLogic.ShowsOwnStructures(RoleId.Cat));
            Assert.IsFalse(MinimapLogic.ShowsOwnStructures(RoleId.None));
        }

        [Test]
        public void ShowsTeammates_NobodyForNow()
        {
            // 밸런스 다이얼 — 지금은 전부 false다. 켜고 싶으면 MinimapLogic 한 줄만 바꾼다.
            Assert.IsFalse(MinimapLogic.ShowsTeammates(RoleId.Mouse));
            Assert.IsFalse(MinimapLogic.ShowsTeammates(RoleId.Cat));
            Assert.IsFalse(MinimapLogic.ShowsTeammates(RoleId.None));
        }

        [Test]
        public void YawToUiAngle_NorthIsUpAndEastTurnsClockwise()
        {
            Assert.AreEqual(0f, MinimapLogic.YawToUiAngle(0f), 1e-3f);
            Assert.AreEqual(270f, MinimapLogic.YawToUiAngle(90f), 1e-3f);   // 동 = UI에서 시계 90°
            Assert.AreEqual(180f, MinimapLogic.YawToUiAngle(180f), 1e-3f);
            Assert.AreEqual(90f, MinimapLogic.YawToUiAngle(270f), 1e-3f);
        }
    }
}
