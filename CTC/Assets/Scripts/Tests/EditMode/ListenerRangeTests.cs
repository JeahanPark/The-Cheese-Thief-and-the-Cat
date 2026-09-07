using CTC.Runtime.Audio;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 예민한 귀의 배율 홀더. **0이 새면 소리가 통째로 죽는다** — 미보유(ParamOf 0)가
    /// 그대로 흘러 들어오는 경로가 실제로 있어서, 1로 막는 게 이 파일의 존재 이유다.
    /// </summary>
    public class ListenerRangeTests
    {
        // 정적 홀더라 테스트끼리 값이 샌다 — 앞뒤로 비운다
        [SetUp]
        public void SetUp() => ListenerRange.Reset();

        [TearDown]
        public void TearDown() => ListenerRange.Reset();

        [Test]
        public void Multiplier_DefaultsToOne()
        {
            Assert.AreEqual(1f, ListenerRange.Multiplier);
        }

        [Test]
        public void Set_AppliesPositiveMultiplier()
        {
            ListenerRange.Set(1.3f);
            Assert.AreEqual(1.3f, ListenerRange.Multiplier, 1e-5f);
            ListenerRange.Set(2f);
            Assert.AreEqual(2f, ListenerRange.Multiplier, 1e-5f);
        }

        [Test]
        public void Set_ZeroOrNegativeFallsBackToOne()
        {
            ListenerRange.Set(2f);
            ListenerRange.Set(0f);
            Assert.AreEqual(1f, ListenerRange.Multiplier, "미보유(0)가 소리를 죽이면 안 된다");

            ListenerRange.Set(2f);
            ListenerRange.Set(-3f);
            Assert.AreEqual(1f, ListenerRange.Multiplier, "음수는 뒤집지 않고 1로 막는다");
        }

        [Test]
        public void Reset_ReturnsToOne()
        {
            ListenerRange.Set(2.5f);
            ListenerRange.Reset();
            Assert.AreEqual(1f, ListenerRange.Multiplier);
        }
    }
}
