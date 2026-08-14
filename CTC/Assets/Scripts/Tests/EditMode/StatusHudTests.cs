using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using CTC.Runtime.Player;
using CTC.Runtime.UI;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 우상단 상시 배지 조립(HUD 개편 2026-08-15). 배지는 "내가 지금 가진 것"의 단어 나열이다 —
    /// 문장이 되는 순간 상시 HUD가 다시 두꺼워진다.
    /// </summary>
    public class StatusHudTests
    {
        [Test]
        public void Mouse_WeaponTierIsAlwaysNamed()
        {
            Assert.AreEqual("딱총", StatusHud.BuildBadge(RoleId.Mouse, 0, false,
                MouseJobId.None, 0, 0, CatClassId.None));
            Assert.AreEqual("새총", StatusHud.BuildBadge(RoleId.Mouse, 1, false,
                MouseJobId.None, 0, 0, CatClassId.None));
            Assert.AreEqual("기관총", StatusHud.BuildBadge(RoleId.Mouse, 2, false,
                MouseJobId.None, 0, 0, CatClassId.None));
        }

        [Test]
        public void Mouse_DashAndJobAppendAsWords()
        {
            string badge = StatusHud.BuildBadge(RoleId.Mouse, 1, true,
                MouseJobId.Hero, 0, 0, CatClassId.None);

            Assert.AreEqual("새총 · 대시 · 용사쥐", badge);
        }

        [Test]
        public void Cat_StacksOnlyWhenPositive()
        {
            // 판 시작 직후 "공격 +0 · 이속 +0"은 정보가 아니라 소음이다
            Assert.AreEqual("", StatusHud.BuildBadge(RoleId.Cat, 0, false,
                MouseJobId.None, 0, 0, CatClassId.None));
            Assert.AreEqual("공격 +2 · 이속 +1 · 불", StatusHud.BuildBadge(RoleId.Cat, 0, false,
                MouseJobId.None, 2, 1, CatClassId.Fire));
        }

        [Test]
        public void Spectator_HasNoBadge()
        {
            Assert.AreEqual("", StatusHud.BuildBadge(RoleId.None, 0, false,
                MouseJobId.None, 0, 0, CatClassId.None));
        }
    }
}
