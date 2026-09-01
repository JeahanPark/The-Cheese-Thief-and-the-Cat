using NUnit.Framework;
using CTC.Runtime.UI.Popup;

namespace CTC.Tests
{
    public class PopupStackLogicTests
    {
        [Test]
        public void ModalOpen_Follows_Count()
        {
            Assert.IsFalse(PopupStackLogic.ModalOpen(0));
            Assert.IsTrue(PopupStackLogic.ModalOpen(1));
            Assert.IsTrue(PopupStackLogic.ModalOpen(3));
            Assert.IsFalse(PopupStackLogic.ModalOpen(-1), "음수는 없다 — 방어");
        }

        [Test]
        public void TopIndex_Is_Last()
        {
            Assert.AreEqual(-1, PopupStackLogic.TopIndex(0));
            Assert.AreEqual(0, PopupStackLogic.TopIndex(1));
            Assert.AreEqual(2, PopupStackLogic.TopIndex(3));
        }

        [Test]
        public void Address_Is_Type_Name()
        {
            // WES 규약 — Addressable 주소 = 클래스명. 어긋나면 런타임에만 드러난다.
            Assert.AreEqual("OptionsPopup", PopupStackLogic.AddressOf(typeof(FakeOptionsPopup)).Replace("Fake", ""));
        }

        [Test]
        public void CanOpen_Blocks_Duplicate_When_Single()
        {
            // 같은 팝업을 두 번 여는 것은 막는다 — 겹쳐도 보이는 건 하나뿐이라 스택만 더러워진다
            Assert.IsTrue(PopupStackLogic.CanOpen(_alreadyOpen: false));
            Assert.IsFalse(PopupStackLogic.CanOpen(_alreadyOpen: true));
        }

        private class FakeOptionsPopup { }
    }
}
