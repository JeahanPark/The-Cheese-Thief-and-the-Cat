using CTC.Runtime.Core;
using NUnit.Framework;

namespace CTC.Tests
{
    public class EnterRouteLogicTests
    {
        [Test]
        public void ChatOpen_Submits_EvenWhenShopCouldBuy()
        {
            // 열려 있는 입력창이 언제나 우선 — 타이핑 중 Enter가 구매로 새면 안 된다
            Assert.AreEqual(EnterRoute.SubmitChat, EnterRouteLogic.Route(true, true));
        }

        [Test]
        public void ShopCanConfirm_Buys()
        {
            Assert.AreEqual(EnterRoute.ConfirmBuy, EnterRouteLogic.Route(false, true));
        }

        [Test]
        public void NothingToBuy_OpensChat()
        {
            Assert.AreEqual(EnterRoute.OpenChat, EnterRouteLogic.Route(false, false));
        }
    }
}
