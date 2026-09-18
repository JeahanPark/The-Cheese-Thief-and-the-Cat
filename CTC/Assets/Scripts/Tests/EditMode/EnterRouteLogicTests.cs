using CTC.Runtime.Core;
using NUnit.Framework;

namespace CTC.Tests
{
    /// <summary>
    /// Enter 한 번이 갈 곳. 확정 구매는 2026-09-19 상점 팝업으로 옮겨서 **채팅 둘뿐**이다 —
    /// 그전에는 상점 안에서 Enter가 「산다」를 겸해, 채팅을 열려면 상점 밖으로 나가야 했다.
    /// </summary>
    public class EnterRouteLogicTests
    {
        [Test]
        public void ChatOpen_Submits()
        {
            // 열려 있는 입력창이 언제나 우선 — 타이핑 중 Enter가 다른 데로 새면 안 된다
            Assert.AreEqual(EnterRoute.SubmitChat, EnterRouteLogic.Route(true));
        }

        [Test]
        public void ChatClosed_OpensChat()
        {
            Assert.AreEqual(EnterRoute.OpenChat, EnterRouteLogic.Route(false));
        }
    }
}
