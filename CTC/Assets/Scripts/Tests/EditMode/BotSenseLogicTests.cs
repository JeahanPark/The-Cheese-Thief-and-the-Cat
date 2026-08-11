using CTC.Runtime.Bots;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 봇이 무엇을 알 수 있는가. **이 게임에서 가장 조심해야 하는 코드다** —
    /// 여기가 헐거우면 고양이 봇이 신이 되고, 나온 승률은 "소리 밸런스가 좋다"가 아니라
    /// "봇이 반칙했다"를 뜻하게 된다.
    /// </summary>
    public class BotSenseLogicTests
    {
        [Test]
        public void CanHear_UsesTheSameRadiusAsPlayback()
        {
            Assert.IsTrue(BotSenseLogic.CanHear(Vector3.zero, new Vector3(17f, 0f, 0f), 18f));
            Assert.IsFalse(BotSenseLogic.CanHear(Vector3.zero, new Vector3(19f, 0f, 0f), 18f));
        }

        [Test]
        public void CanHear_IgnoresHeight()
        {
            // 평면 맵이다 — y 차이로 소리가 끊기면 계단 하나에 청각이 사라진다
            Assert.IsTrue(BotSenseLogic.CanHear(Vector3.zero, new Vector3(5f, 40f, 0f), 18f));
        }

        [Test]
        public void Blur_GrowsWithDistance()
        {
            // 코앞의 소리는 거의 정확하고, 먼 소리는 방향만 남는다 — 사람의 청각과 같은 성질
            Vector3 near = BotSenseLogic.Blur(new Vector3(2f, 0f, 0f), Vector3.zero, 0.2f, 0f);
            Vector3 far = BotSenseLogic.Blur(new Vector3(40f, 0f, 0f), Vector3.zero, 0.2f, 0f);

            Assert.AreEqual(0.4f, (near - new Vector3(2f, 0f, 0f)).magnitude, 0.01f);
            Assert.AreEqual(8f, (far - new Vector3(40f, 0f, 0f)).magnitude, 0.01f);
        }

        [Test]
        public void Blur_ZeroErrorIsExact()
        {
            // 0으로 두면 고양이가 소리 나는 즉시 직선으로 덮친다 — 이 값이 곧 밸런스 손잡이다
            var heard = new Vector3(10f, 0f, 5f);
            Assert.AreEqual(heard, BotSenseLogic.Blur(heard, Vector3.zero, 0f, 1.2f));
        }

        [Test]
        public void IsInView_MatchesTheCameraCone()
        {
            // 고양이 카메라 70° → 좌우 35°
            Assert.IsTrue(BotSenseLogic.IsInView(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 10f), 70f));
            Assert.IsTrue(BotSenseLogic.IsInView(
                Vector3.zero, Vector3.forward, new Vector3(5f, 0f, 10f), 70f));   // 26°
            Assert.IsFalse(BotSenseLogic.IsInView(
                Vector3.zero, Vector3.forward, new Vector3(10f, 0f, 5f), 70f));   // 63°
        }

        [Test]
        public void IsInView_BehindIsNeverVisible()
        {
            Assert.IsFalse(BotSenseLogic.IsInView(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -10f), 179f));
        }

        [Test]
        public void IsInView_SamePositionIsVisible()
        {
            // 겹쳐 있는데 "안 보인다"고 하면 덮치기가 성립하지 않는다
            Assert.IsTrue(BotSenseLogic.IsInView(Vector3.zero, Vector3.forward, Vector3.zero, 55f));
        }
    }
}
