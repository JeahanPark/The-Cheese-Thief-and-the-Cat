using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using CTC.Runtime.Player;
using CTC.Runtime.UI;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>능력 바(2026-08-17) — 슬롯 목록·상태·채움 비율·초 글자. 그림(View)은 이 값을 그대로 그린다.</summary>
    public class AbilityBarLogicTests
    {
        [Test]
        public void Mouse_HasDashAndAppointSlots_InOrder()
        {
            var slots = AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.None, false, CatClassId.None, 3);
            Assert.AreEqual(2, slots.Length);
            Assert.AreEqual("Space", slots[0].Key);
            Assert.AreEqual("대시", slots[0].Name);
            Assert.AreEqual("F", slots[1].Key);
            Assert.AreEqual("임명", slots[1].Name);
        }

        [Test]
        public void Mouse_DashLockedTellsShopSlot_UntilOwned()
        {
            var locked = AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.None, false, CatClassId.None, 3)[0];
            Assert.IsTrue(locked.Locked);
            Assert.AreEqual("상점 3", locked.LockReason);

            var owned = AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.None, true, CatClassId.None, 3)[0];
            Assert.IsFalse(owned.Locked);
            Assert.AreEqual("", owned.LockReason);
        }

        [Test]
        public void Mouse_AppointLockedUnlessGang()
        {
            var plain = AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.None, true, CatClassId.None, 3)[1];
            Assert.IsTrue(plain.Locked);
            Assert.AreEqual("깡패만", plain.LockReason);
            Assert.IsTrue(AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.Minion, true, CatClassId.None, 3)[1].Locked);
            Assert.IsFalse(AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.Gang, true, CatClassId.None, 3)[1].Locked);
        }

        [Test]
        public void Cat_HasSkillSlot_NamedByClass()
        {
            var none = AbilityBarLogic.Build(RoleId.Cat, MouseJobId.None, false, CatClassId.None, 0);
            Assert.AreEqual(1, none.Length);
            Assert.AreEqual("Q", none[0].Key);
            Assert.AreEqual("스킬", none[0].Name);
            Assert.IsTrue(none[0].Locked);
            Assert.AreEqual("전직 필요", none[0].LockReason);

            var dark = AbilityBarLogic.Build(RoleId.Cat, MouseJobId.None, false, CatClassId.Dark, 0)[0];
            Assert.AreEqual("어둠", dark.Name);
            Assert.IsFalse(dark.Locked);
        }

        [Test]
        public void Spectator_HasNoSlots()
        {
            Assert.AreEqual(0, AbilityBarLogic.Build(RoleId.None, MouseJobId.None, false, CatClassId.None, 0).Length);
        }

        [Test]
        public void StateOf_PrefersLocked_ThenActive_ThenCooldown()
        {
            Assert.AreEqual(AbilityState.Locked, AbilityBarLogic.StateOf(true, 2f, 1f));
            Assert.AreEqual(AbilityState.Active, AbilityBarLogic.StateOf(false, 2f, 1f));
            Assert.AreEqual(AbilityState.Cooldown, AbilityBarLogic.StateOf(false, 0f, 1f));
            Assert.AreEqual(AbilityState.Ready, AbilityBarLogic.StateOf(false, 0f, 0f));
        }

        [Test]
        public void FillRatio_ClampsAndHandlesZeroTotal()
        {
            Assert.AreEqual(0.5f, AbilityBarLogic.FillRatio(1f, 2f), 1e-4f);
            Assert.AreEqual(1f, AbilityBarLogic.FillRatio(5f, 2f), 1e-4f);
            Assert.AreEqual(0f, AbilityBarLogic.FillRatio(-1f, 2f), 1e-4f);
            Assert.AreEqual(0f, AbilityBarLogic.FillRatio(1f, 0f), 1e-4f);
        }

        [Test]
        public void SecondsText_CeilsAndBlanksAtZero()
        {
            Assert.AreEqual("3", AbilityBarLogic.SecondsText(2.01f));
            Assert.AreEqual("1", AbilityBarLogic.SecondsText(0.2f));
            Assert.AreEqual("", AbilityBarLogic.SecondsText(0f));
        }
    }
}
