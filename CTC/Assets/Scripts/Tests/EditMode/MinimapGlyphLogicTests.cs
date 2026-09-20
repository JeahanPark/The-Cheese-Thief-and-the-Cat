using NUnit.Framework;
using CTC.Runtime.UI;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>미니맵 내 삼각형(끝 = 몸 방향)과 시선 부채꼴의 모양(2026-09-20 사용자 요청).</summary>
    public class MinimapGlyphLogicTests
    {
        private const float EPS = 1e-4f;

        [Test]
        [NUnit.Framework.Property("CaseId", "C-44")]
        public void Triangle_PointsUp_AndIsCentered()
        {
            MinimapGlyphLogic.Triangle(10f, 20f, out Vector2 tip, out Vector2 left, out Vector2 right);
            Assert.AreEqual(new Vector2(0f, 10f), tip, "끝이 위(+Y)를 향해야 회전이 yaw와 맞는다");
            Assert.AreEqual(new Vector2(-5f, -10f), left);
            Assert.AreEqual(new Vector2(5f, -10f), right);
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "C-44")]
        public void TipCut_SplitsTheRedTipFromTheBody()
        {
            MinimapGlyphLogic.Triangle(10f, 20f, out Vector2 tip, out Vector2 left, out Vector2 right);
            MinimapGlyphLogic.TipCut(tip, left, right, 0.5f, out Vector2 cutLeft, out Vector2 cutRight);
            Assert.AreEqual(new Vector2(-2.5f, 0f), cutLeft);
            Assert.AreEqual(new Vector2(2.5f, 0f), cutRight);
            Assert.Less(MinimapGlyphLogic.TIP_FRACTION, 0.5f, "빨간 끝은 삼각형의 절반보다 작아야 몸이 흰색으로 읽힌다");
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "C-44")]
        public void FanPoints_SpreadEvenlyAroundUp()
        {
            const int segments = 16;
            Vector2 first = MinimapGlyphLogic.FanPoint(0, segments, 90f, 10f);
            Vector2 middle = MinimapGlyphLogic.FanPoint(segments / 2, segments, 90f, 10f);
            Vector2 last = MinimapGlyphLogic.FanPoint(segments, segments, 90f, 10f);

            Assert.AreEqual(0f, middle.x, EPS, "가운데 선은 위를 본다");
            Assert.AreEqual(10f, middle.y, EPS);
            Assert.AreEqual(-first.x, last.x, EPS, "좌우가 대칭이다");
            Assert.AreEqual(first.y, last.y, EPS);
            Assert.AreEqual(45f, Vector2.Angle(Vector2.up, last), 0.01f, "각의 절반씩 벌어진다");
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "C-44")]
        public void ScaleAbout_KeepsTheCenter()
        {
            Vector2 c = new Vector2(1f, 1f);
            Assert.AreEqual(c, MinimapGlyphLogic.ScaleAbout(c, c, 2f));
            Assert.AreEqual(new Vector2(5f, 1f), MinimapGlyphLogic.ScaleAbout(new Vector2(3f, 1f), c, 2f));
        }
    }
}
