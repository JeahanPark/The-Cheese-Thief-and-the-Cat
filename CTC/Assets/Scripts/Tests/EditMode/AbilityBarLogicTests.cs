using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using CTC.Runtime.Player;
using CTC.Runtime.UI;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 능력 바(2026-08-17) — 슬롯 목록·상태·채움 비율·초 글자. 그림(View)은 이 값을 그대로 그린다.
    /// **해금 안 된 칸은 목록에 없다** — 대시는 사야, 임명은 깡패가 돼야, Q는 전직해야 생긴다.
    /// </summary>
    public class AbilityBarLogicTests
    {
        [Test]
        public void Mouse_NothingUnlocked_NoSlots()
        {
            Assert.AreEqual(0, AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.None, false, CatClassId.None).Length);
        }

        [Test]
        public void Mouse_DashAppearsWhenOwned()
        {
            var slots = AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.None, true, CatClassId.None);
            Assert.AreEqual(1, slots.Length);
            Assert.AreEqual("Space", slots[0].Key);
            Assert.AreEqual("대시", slots[0].Name);
            Assert.AreEqual("순간 가속", slots[0].Hint);
        }

        /// <summary>
        /// 용사(2026-09-19) — Space가 대시가 아니라 **무적**이다. 대시를 이미 샀어도 칸은 무적이 차지한다
        /// (누르면 무적이 켜지고, 대시는 용사가 사지도 못한다).
        /// </summary>
        [NUnit.Framework.Property("CaseId", "C-37")]
        [Test]
        public void Hero_SpaceSlotIsInvincible_EvenWithDashOwned()
        {
            foreach (bool dashOwned in new[] { false, true })
            {
                var hero = AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.Hero, dashOwned, CatClassId.None);
                Assert.AreEqual(1, hero.Length, $"대시 보유={dashOwned}");
                Assert.AreEqual("Space", hero[0].Key);
                Assert.AreEqual(AbilityBarLogic.NAME_INVINCIBLE, hero[0].Name);
            }
            Assert.AreEqual("대시", AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.None, true, CatClassId.None)[0].Name,
                "용사가 아니면 Space는 그대로 대시다");
        }

        [Test]
        public void Mouse_AppointOnlyForGang_AfterDash()
        {
            Assert.AreEqual(0, AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.Minion, false, CatClassId.None).Length);
            var gang = AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.Gang, true, CatClassId.None);
            Assert.AreEqual(2, gang.Length);
            Assert.AreEqual("Space", gang[0].Key);
            Assert.AreEqual("F", gang[1].Key);
            Assert.AreEqual("임명", gang[1].Name);
        }

        [Test]
        public void Cat_SkillSlotOnlyAfterAdvance_NamedByClass()
        {
            Assert.AreEqual(0, AbilityBarLogic.Build(RoleId.Cat, MouseJobId.None, false, CatClassId.None).Length);
            var dark = AbilityBarLogic.Build(RoleId.Cat, MouseJobId.None, false, CatClassId.Dark);
            Assert.AreEqual(1, dark.Length);
            Assert.AreEqual("Q", dark[0].Key);
            Assert.AreEqual("어둠", dark[0].Name);
            Assert.AreEqual("쥐 화면 어둡게", dark[0].Hint);
            Assert.AreEqual("", AbilityBarLogic.SkillHintOf(CatClassId.None));
        }

        /// <summary>
        /// 광분(2026-09-09 감사 P0-1) — 고양이는 상점을 안 다녀 핫바가 늘 구매 모드다.
        /// 그래서 숫자키가 안 남고 액티브 전용 키(Space)가 필요하다. 사기 전엔 칸이 없다.
        /// </summary>
        [Test]
        public void Cat_FrenzySlotOnlyWhenOwned_UsesSpace()
        {
            Assert.AreEqual(0, AbilityBarLogic.Build(RoleId.Cat, MouseJobId.None, false, CatClassId.None).Length);

            var owned = AbilityBarLogic.Build(RoleId.Cat, MouseJobId.None, false, CatClassId.None, true);
            Assert.AreEqual(1, owned.Length);
            Assert.AreEqual(AbilityBarLogic.KEY_FRENZY, owned[0].Key);
            Assert.AreEqual("Space", owned[0].Key);
            Assert.AreEqual("광분", owned[0].Name);
            Assert.IsNotEmpty(owned[0].Hint);
        }

        /// <summary>전직 스킬(Q)과 광분(Space)은 같이 뜬다 — 씬의 능력 칸 2개에 정확히 맞는다.</summary>
        [Test]
        public void Cat_SkillAndFrenzy_TwoSlots_SkillFirst()
        {
            var both = AbilityBarLogic.Build(RoleId.Cat, MouseJobId.None, false, CatClassId.Fire, true);
            Assert.AreEqual(2, both.Length);
            Assert.AreEqual(AbilityBarLogic.KEY_SKILL, both[0].Key);
            Assert.AreEqual(AbilityBarLogic.KEY_FRENZY, both[1].Key);
        }

        /// <summary>쥐에게는 광분 칸이 안 생긴다 — Space는 쥐 쪽에서 대시가 쓴다(역할이 갈려 겹치지 않는다).</summary>
        [Test]
        public void Mouse_NeverGetsFrenzySlot()
        {
            var slots = AbilityBarLogic.Build(RoleId.Mouse, MouseJobId.None, false, CatClassId.None, true);
            Assert.AreEqual(0, slots.Length);
        }

        [Test]
        public void Spectator_HasNoSlots()
        {
            Assert.AreEqual(0, AbilityBarLogic.Build(RoleId.None, MouseJobId.Gang, true, CatClassId.Fire).Length);
        }

        [Test]
        public void StateOf_PrefersActive_ThenCooldown()
        {
            Assert.AreEqual(AbilityState.Active, AbilityBarLogic.StateOf(2f, 1f));
            Assert.AreEqual(AbilityState.Cooldown, AbilityBarLogic.StateOf(0f, 1f));
            Assert.AreEqual(AbilityState.Ready, AbilityBarLogic.StateOf(0f, 0f));
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
