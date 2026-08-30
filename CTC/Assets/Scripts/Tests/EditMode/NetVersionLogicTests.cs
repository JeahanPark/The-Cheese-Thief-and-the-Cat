using CTC.Runtime.Networking;
using NUnit.Framework;

namespace CTC.Tests
{
    public class NetVersionLogicTests
    {
        [Test]
        public void Protocol_MajorMinor()
        {
            Assert.AreEqual(104, NetVersionLogic.Protocol("1.4.7"));
        }

        /// <summary>패치만 올린 판은 서로 붙어야 한다 — 버그 수정에 접속을 끊지 않는다.</summary>
        [Test]
        public void Protocol_PatchIgnored()
        {
            Assert.AreEqual(NetVersionLogic.Protocol("0.1.0"), NetVersionLogic.Protocol("0.1.9"));
        }

        [Test]
        public void Protocol_MinorBumpChangesValue()
        {
            Assert.AreNotEqual(NetVersionLogic.Protocol("0.1.0"), NetVersionLogic.Protocol("0.2.0"));
        }

        [Test]
        public void Protocol_MissingMinor_TreatedAsZero()
        {
            Assert.AreEqual(200, NetVersionLogic.Protocol("2"));
        }

        [Test]
        public void Protocol_Blank_Fallback()
        {
            Assert.AreEqual(NetVersionLogic.FALLBACK, NetVersionLogic.Protocol("   "));
        }

        [Test]
        public void Protocol_Garbage_Fallback()
        {
            Assert.AreEqual(NetVersionLogic.FALLBACK, NetVersionLogic.Protocol("dev-build"));
        }

        /// <summary>0.0.x는 0이 되는데, 0은 "설정 안 함"과 구분이 안 된다.</summary>
        [Test]
        public void Protocol_ZeroZero_FallsBackNotZero()
        {
            Assert.AreEqual(NetVersionLogic.FALLBACK, NetVersionLogic.Protocol("0.0.3"));
        }
    }
}
