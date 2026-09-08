using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;

namespace CTC.Tests
{
    /// <summary>직업권 자리 셔플(계획 3 Task 4 · 기획서 §7-3) — 순수 순열이라 여기서 다 덮는다.</summary>
    public class TicketShuffleLogicTests
    {
        [Test]
        public void Permutation_SameSeed_SameOrder()
        {
            CollectionAssert.AreEqual(
                TicketShuffleLogic.Permutation(6, 4242),
                TicketShuffleLogic.Permutation(6, 4242));
        }

        [Test]
        public void Permutation_DifferentSeeds_DifferInSomeOrder()
        {
            int[] first = TicketShuffleLogic.Permutation(6, 1);
            bool anyDifferent = false;
            for (int seed = 2; seed <= 40 && !anyDifferent; seed++)
            {
                int[] other = TicketShuffleLogic.Permutation(6, seed);
                for (int i = 0; i < other.Length; i++)
                {
                    if (other[i] != first[i]) { anyDifferent = true; break; }
                }
            }
            Assert.IsTrue(anyDifferent, "시드를 40개 굴려도 순열이 같다 — 시드가 결과에 안 닿는다");
        }

        [Test]
        public void Permutation_IsAPermutation()
        {
            // 이게 깨지면 내용물이 사라지거나 두 번 복사된다 — 맵 총 치즈량이 그대로 어긋난다.
            for (int seed = 1; seed <= 100; seed++)
            {
                int[] order = TicketShuffleLogic.Permutation(9, seed);
                Assert.AreEqual(9, order.Length);

                var seen = new HashSet<int>();
                foreach (int index in order)
                {
                    Assert.GreaterOrEqual(index, 0);
                    Assert.Less(index, 9);
                    Assert.IsTrue(seen.Add(index), $"시드 {seed}에서 {index}가 두 번 나왔다");
                }
            }
        }

        [Test]
        public void Permutation_ZeroOrOne_IsIdentity()
        {
            // 상자가 없는 맵(창고)·하나뿐인 맵에서 섞을 것이 없다.
            Assert.AreEqual(0, TicketShuffleLogic.Permutation(0, 7).Length);
            CollectionAssert.AreEqual(new[] { 0 }, TicketShuffleLogic.Permutation(1, 7));
        }

        [Test]
        public void Permutation_NegativeCount_IsEmpty()
        {
            Assert.AreEqual(0, TicketShuffleLogic.Permutation(-3, 7).Length);
        }

        [Test]
        public void Permutation_TotalCheeseIsUnchanged_WhenContentsAreSwappedWholesale()
        {
            // 이 계획이 자리만 바꾸는 이유 그 자체 — 맵 총 치즈량이 지점 매장량에서 이미 떼어져 있어
            // 하나라도 늘거나 줄면 고갈 시점이 밀린다.
            var boxes = new List<BoxPlacement>();
            for (int i = 0; i < 6; i++)
            {
                boxes.Add(new BoxPlacement
                {
                    Pos = new UnityEngine.Vector2(i, 0f),
                    Contents = new List<LootEntry>
                    {
                        new LootEntry { Id = i == 0 ? LootId.GangTicket : LootId.Cheese, Amount = 10 * (i + 1) },
                    },
                });
            }
            int before = LootBoxLogic.TotalCheese(boxes);

            int[] order = TicketShuffleLogic.Permutation(boxes.Count, 31337);
            var shuffled = new List<BoxPlacement>(boxes.Count);
            for (int i = 0; i < boxes.Count; i++)
            {
                BoxPlacement slot = boxes[i];
                slot.Contents = boxes[order[i]].Contents;   // 자리는 그대로, 내용물만 교환
                shuffled.Add(slot);
            }

            Assert.AreEqual(before, LootBoxLogic.TotalCheese(shuffled));
        }
    }
}
