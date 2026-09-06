using CTC.Runtime.Economy;
using CTC.Runtime.Info;
using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests
{
    public class TechInfoTests
    {
        [Test]
        public void TechInfo_LoadsTwelveRows()
        {
            // 계획 1: 기존 8항목 = 쥐 6행(총 2레벨) + 고양이 6행(스택 2종 × 3레벨)
            Assert.AreEqual(12, InfoManager.Instance.Tech.Count);
        }

        [Test]
        public void TechInfo_GunLevel2_IsMachineGun600()
        {
            TechInfo found = null;
            foreach (TechInfo row in InfoManager.Instance.Tech)
                if (row.TechId == TechId.Gun && row.Level == 2) found = row;
            Assert.IsNotNull(found);
            Assert.AreEqual(600, found.Cost);
            Assert.AreEqual(RoleId.Mouse, found.Role);
            Assert.AreEqual(TechAxis.Attack, found.Axis);
        }

        [Test]
        public void Balance_HasTechColumns()
        {
            BalanceInfo b = InfoManager.Instance.Balance;
            Assert.AreEqual(5, b.TechSlotCap);
            Assert.AreEqual(80, b.FortressPlaceCost);
            Assert.AreEqual(4, b.ShopSpotCount);
            Assert.AreEqual(3f, b.ShopSpotRadius, 1e-4f);
            Assert.AreEqual(25f, b.ShopSpotMinGap, 1e-4f);
            Assert.AreEqual(12f, b.ShopBuySoundMeters, 1e-4f);
        }
    }
}
