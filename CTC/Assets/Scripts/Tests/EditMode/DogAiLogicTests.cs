using System.Collections.Generic;
using CTC.Runtime.Ai;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 강아지의 판단 규칙. **누구 편도 아니라는 것**이 이 기능의 전부라
    /// "푼 사람은 안 문다" 같은 예외가 슬며시 들어오지 않게 여기서 못을 박는다.
    /// </summary>
    public class DogAiLogicTests
    {
        private static DogTarget Cat(ulong _id, float _x) =>
            new DogTarget(_id, new Vector3(_x, 0f, 0f), true, true);

        private static DogTarget Mouse(ulong _id, float _x, bool _visible = true) =>
            new DogTarget(_id, new Vector3(_x, 0f, 0f), false, _visible);

        [Test]
        public void ChooseTarget_NoCandidates_IsNoTarget()
        {
            Assert.AreEqual(DogAiLogic.NO_TARGET, DogAiLogic.ChooseTarget(null, Vector3.zero, 20f));
            Assert.AreEqual(DogAiLogic.NO_TARGET,
                DogAiLogic.ChooseTarget(new List<DogTarget>(), Vector3.zero, 20f));
        }

        [Test]
        public void ChooseTarget_PicksNearestRegardlessOfRole()
        {
            // 고양이가 더 가깝다 — 쥐가 문을 열어 줬어도 강아지는 가까운 쪽을 문다.
            var candidates = new List<DogTarget> { Mouse(1, 8f), Cat(2, 3f) };

            Assert.AreEqual(2ul, DogAiLogic.ChooseTarget(candidates, Vector3.zero, 20f));
        }

        [Test]
        public void ChooseTarget_IgnoresOutOfRange()
        {
            var candidates = new List<DogTarget> { Mouse(1, 30f) };

            Assert.AreEqual(DogAiLogic.NO_TARGET, DogAiLogic.ChooseTarget(candidates, Vector3.zero, 14f));
        }

        [Test]
        public void ChooseTarget_IgnoresUnseen()
        {
            // 안 보이는 게 더 가까워도 보이는 쪽을 고른다 — "눈에 보이는 것"이 규칙이다.
            var candidates = new List<DogTarget> { Mouse(1, 2f, _visible: false), Mouse(2, 9f) };

            Assert.AreEqual(2ul, DogAiLogic.ChooseTarget(candidates, Vector3.zero, 20f));
        }

        [Test]
        public void ChooseTarget_IgnoresHeightDifference()
        {
            // y를 세면 카메라 높이 차이만으로 반경 밖으로 밀려난다.
            var candidates = new List<DogTarget> { new DogTarget(7, new Vector3(3f, 40f, 0f), false, true) };

            Assert.AreEqual(7ul, DogAiLogic.ChooseTarget(candidates, Vector3.zero, 5f));
        }

        [Test]
        public void ShouldDropAggro_NeverWhileVisible()
        {
            Assert.IsFalse(DogAiLogic.ShouldDropAggro(_visibleNow: true, _secondsSinceSeen: 999f, _dropSeconds: 4f));
        }

        [Test]
        public void ShouldDropAggro_OnlyAfterLosingSightLongEnough()
        {
            Assert.IsFalse(DogAiLogic.ShouldDropAggro(false, 3.9f, 4f), "잠깐 가렸다고 놓으면 표적이 튄다");
            Assert.IsTrue(DogAiLogic.ShouldDropAggro(false, 4f, 4f));
        }

        [Test]
        public void NextState_FollowsDistance()
        {
            Assert.AreEqual(DogState.Roam, DogAiLogic.NextState(false, 0f, 2f));
            Assert.AreEqual(DogState.Chase, DogAiLogic.NextState(true, 5f, 2f));
            Assert.AreEqual(DogState.Bite, DogAiLogic.NextState(true, 2f, 2f));
        }

        [Test]
        public void BiteDamage_ScalesWithRole()
        {
            int cat = DogAiLogic.BiteDamage(_targetIsCat: true);
            int mouse = DogAiLogic.BiteDamage(_targetIsCat: false);

            Assert.Greater(cat, 0);
            Assert.Greater(mouse, 0);
            // 고양이 HP 1000 · 쥐 HP 100 — 같은 값이면 한쪽만 죽는다.
            Assert.Greater(cat, mouse, "고양이 피해가 더 커야 양쪽 다 위협이 된다");
        }

        [Test]
        public void BiteDamage_KillsNeitherInOneHit()
        {
            // 한 방에 죽으면 피할 여지가 없다 — 물린 걸 알고 도망칠 시간이 있어야 한다.
            Assert.Less(DogAiLogic.BiteDamage(true), 1000);
            Assert.Less(DogAiLogic.BiteDamage(false), 100);
        }

        [Test]
        public void TickCooldown_FloorsAtZero()
        {
            Assert.AreEqual(0.6f, DogAiLogic.TickCooldown(1f, 0.4f), 0.0001f);
            Assert.AreEqual(0f, DogAiLogic.TickCooldown(0.1f, 5f), 0.0001f);
        }

        [Test]
        public void Balance_DogIsSlowerThanCatButFasterThanMouse()
        {
            // 쥐는 겨우 못 뿌리치고 고양이는 뿌리친다 — 이 순서가 깨지면 위협이 한쪽에만 간다.
            var info = CTC.Runtime.Info.InfoManager.Instance;
            float mouse = info.PlayerOf(CTC.Runtime.Lobby.RoleId.Mouse).MoveSpeed;
            float cat = info.PlayerOf(CTC.Runtime.Lobby.RoleId.Cat).MoveSpeed;

            Assert.Greater(DogAiLogic.MoveSpeed, mouse);
            Assert.Less(DogAiLogic.MoveSpeed, cat);
        }

        [Test]
        public void Balance_DogAlcoveIsMuchTougherThanCheeseAlcove()
        {
            // 시간 게이트 대신 기회비용이 게이트다 — 벽이 안 단단하면 그 설계가 무너진다.
            Assert.Greater(CTC.Runtime.Info.InfoManager.Instance.Balance.DogAlcoveHp,
                CTC.Runtime.Map.AlcoveLogic.AlcoveHp * 2);
        }
    }
}
