using CTC.Runtime.Economy;
using NUnit.Framework;

namespace CTC.Tests
{
    /// <summary>
    /// 창고 테크 수학 (테크 v3 계획 2a) — <c>CTC.Runtime.Economy.WarehouseLogic</c>.
    /// 이름이 같은 <c>CTC.Runtime.Map.WarehouseLogic</c>(창고 평면도)와는 남남이다:
    /// 그쪽 테스트는 <c>WarehouseLogicTests</c>라서 파일·클래스 이름을 여기서 비켜 놨다.
    /// 값의 원본은 TechInfo.csv의 P열이라, 기대 숫자가 어긋나면 코드가 아니라 표가 바뀐 것이다.
    /// </summary>
    public class WarehouseTechLogicTests
    {
        [Test]
        public void Limit_ByWarehouseLevel()
        {
            Assert.AreEqual(1, WarehouseLogic.LimitOf(0));   // 설치권 없음 → 기본 1(서버가 어차피 막는다)
            Assert.AreEqual(1, WarehouseLogic.LimitOf(1));
            Assert.AreEqual(2, WarehouseLogic.LimitOf(2));
            Assert.AreEqual(2, WarehouseLogic.LimitOf(3));
        }

        [Test]
        public void Income_Lv3_Doubles()
        {
            Assert.AreEqual(10, WarehouseLogic.TickCheese(10, 1));
            Assert.AreEqual(20, WarehouseLogic.TickCheese(10, 3));
        }

        [Test]
        public void Hidden_SoundAndHp_ByLevel()
        {
            Assert.AreEqual(24f, WarehouseLogic.SoundRange(24f, 0), 1e-4f);
            Assert.AreEqual(16f, WarehouseLogic.SoundRange(24f, 1), 1e-4f);
            Assert.AreEqual(0f, WarehouseLogic.SoundRange(24f, 3), 1e-4f);
            Assert.AreEqual(150, WarehouseLogic.MaxHp(150, 0));
            Assert.AreEqual(450, WarehouseLogic.MaxHp(150, 3));
        }

        [Test]
        public void Refund_Lv3_HalfOfPlaceCost()
        {
            Assert.AreEqual(0, WarehouseLogic.Refund(100, 2));
            Assert.AreEqual(50, WarehouseLogic.Refund(100, 3));
        }
    }
}
