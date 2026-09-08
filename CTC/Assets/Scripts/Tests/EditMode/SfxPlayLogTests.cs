// 실제 재생 횟수 계측기 자신에 붙인 검사 (2026-09-09 감사 C-18·C-19).
//
// 계측기가 거짓말하면 그 위의 결론이 전부 거짓말이 된다(감사 1단계). 특히 "무음"처럼
// **0을 기대하는 검사**는 계측기가 항상 0을 주기만 해도 초록이 된다 — 그래서 "셀 때는 센다"를
// 먼저 못 박는다.
using CTC.Runtime.Audio;
using NUnit.Framework;

namespace CTC.Tests
{
    public class SfxPlayLogTests
    {
        [SetUp]
        public void Reset() => SfxPlayLog.Reset();

        [Test]
        public void Record_CountsPerId()
        {
            SfxPlayLog.Record(SfxId.WarehouseTick);
            SfxPlayLog.Record(SfxId.WarehouseTick);
            SfxPlayLog.Record(SfxId.ShopBuy);

            Assert.AreEqual(2, SfxPlayLog.CountOf(SfxId.WarehouseTick));
            Assert.AreEqual(1, SfxPlayLog.CountOf(SfxId.ShopBuy));
        }

        [Test]
        public void CountOf_UnplayedIsZero()
        {
            SfxPlayLog.Record(SfxId.ShopBuy);
            Assert.AreEqual(0, SfxPlayLog.CountOf(SfxId.WarehouseTick),
                "안 난 소리는 0이어야 한다 — 이게 무음 검사의 바닥이다");
        }

        [Test]
        public void Reset_ClearsWindow()
        {
            SfxPlayLog.Record(SfxId.WarehouseTick);
            SfxPlayLog.Reset();
            Assert.AreEqual(0, SfxPlayLog.CountOf(SfxId.WarehouseTick));
        }

        [Test]
        public void Dump_ListsOnlyWhatActuallyPlayed()
        {
            SfxPlayLog.Record(SfxId.DogBark);
            string dump = SfxPlayLog.Dump();

            StringAssert.Contains("DogBark=1", dump);
            StringAssert.DoesNotContain("WarehouseTick", dump);
        }

        [Test]
        public void Dump_EmptyWhenNothingPlayed()
        {
            Assert.AreEqual(string.Empty, SfxPlayLog.Dump());
        }
    }
}
