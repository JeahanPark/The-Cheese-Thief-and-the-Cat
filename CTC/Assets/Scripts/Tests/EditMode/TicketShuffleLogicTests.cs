using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

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
                    Pos = new Vector2(i, 0f),
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

        // ------------------------------------------------------------------
        // D-03 — 셔플 뒤에도 물건이 그대로인가 (감사 M2·M3, 2026-09-09)
        // ------------------------------------------------------------------

        private const string WAREHOUSE = "Assets/Prefabs/Maps/Layout_Warehouse01.asset";

        /// <summary>창고 맵을 흉내 낸 벽감 여섯 — #2가 강아지(상자 없음)라 M2가 났던 그 모양이다.</summary>
        private static List<AlcovePlacement> SixAlcoves()
        {
            var at = new[]
            {
                new Vector2(-58, 40), new Vector2(-40, 58), new Vector2(58, 36),
                new Vector2(30, -58), new Vector2(-58, -40), new Vector2(58, -46),
            };
            var list = new List<AlcovePlacement>();
            for (int i = 0; i < at.Length; i++)
            {
                bool dog = i == 2;
                list.Add(new AlcovePlacement
                {
                    Pos = at[i],
                    YawDeg = 0f,
                    Payload = dog ? AlcovePayload.Dog : AlcovePayload.Box,
                    BoxContents = new List<LootEntry>(),
                });
            }

            // 굽기와 같은 규칙 — 가장 먼 두 상자 벽에 직업권, 나머지는 치즈 250
            var boxes = new List<int>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Payload == AlcovePayload.Box) boxes.Add(i);
            }
            int a = boxes[0], b = boxes[boxes.Count - 1];
            foreach (int i in boxes)
            {
                LootId id = i == a ? LootId.GangTicket : i == b ? LootId.HeroTicket : LootId.Cheese;
                AlcovePlacement p = list[i];
                p.BoxContents = new List<LootEntry>
                {
                    new LootEntry { Id = id, Amount = id == LootId.Cheese ? 250 : 1 },
                };
                list[i] = p;
            }
            return list;
        }

        /// <summary>order를 적용한 뒤의 벽감 목록 — 스포너가 <c>ServerSetBoxContents</c>로 하는 일과 같다.</summary>
        private static List<AlcovePlacement> Apply(IReadOnlyList<AlcovePlacement> _src, int[] _order)
        {
            var result = new List<AlcovePlacement>(_src.Count);
            for (int i = 0; i < _src.Count; i++)
            {
                AlcovePlacement slot = _src[i];
                slot.BoxContents = _src[_order[i]].BoxContents;
                result.Add(slot);
            }
            return result;
        }

        private static int CheeseOf(IReadOnlyList<AlcovePlacement> _list)
        {
            int sum = 0;
            foreach (AlcovePlacement a in _list)
            {
                if (a.BoxContents == null) continue;
                foreach (LootEntry e in a.BoxContents)
                {
                    if (e.Id == LootId.Cheese) sum += e.Amount;
                }
            }
            return sum;
        }

        private static List<int> TicketWalls(IReadOnlyList<AlcovePlacement> _list)
        {
            var walls = new List<int>();
            for (int i = 0; i < _list.Count; i++)
            {
                if (TicketShuffleLogic.HasTicket(_list[i].BoxContents)) walls.Add(i);
            }
            return walls;
        }

        private static float FarthestBoxDistance(IReadOnlyList<AlcovePlacement> _list, int _from)
        {
            float best = 0f;
            for (int i = 0; i < _list.Count; i++)
            {
                if (i == _from || _list[i].Payload != AlcovePayload.Box) continue;
                best = Mathf.Max(best, Vector2.Distance(_list[i].Pos, _list[_from].Pos));
            }
            return best;
        }

        [Test]
        public void ContentOrder_SeedZero_IsIdentity()
        {
            // 로비·봇 실험실엔 매치 시드가 없다 — 구운 그대로 서야 한다.
            int[] order = TicketShuffleLogic.ContentOrder(SixAlcoves(), 0);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, order);
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "D-03")]
        public void ContentOrder_DogAlcoveIsNeverShuffled()
        {
            // M2(P0-2)의 뿌리 — 강아지 벽감은 상자가 없어 내용물이 빈 목록이다.
            // 그걸 섞으면 어떤 상자 벽이 빈 손을 받고, 사라진 물건은 영영 안 나온다.
            List<AlcovePlacement> src = SixAlcoves();
            for (int seed = 1; seed <= 500; seed++)
            {
                int[] order = TicketShuffleLogic.ContentOrder(src, seed);
                Assert.AreEqual(2, order[2], $"시드 {seed}에서 강아지 벽감(#2)이 남의 내용물을 받았다");
                for (int i = 0; i < order.Length; i++)
                {
                    if (i == 2) continue;
                    Assert.AreNotEqual(2, order[i], $"시드 {seed}에서 상자 벽 #{i}가 강아지 벽의 빈 손을 받았다");
                }
            }
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "D-03")]
        public void ContentOrder_NoBoxAlcoveEndsUpEmpty()
        {
            List<AlcovePlacement> src = SixAlcoves();
            for (int seed = 1; seed <= 500; seed++)
            {
                List<AlcovePlacement> after = Apply(src, TicketShuffleLogic.ContentOrder(src, seed));
                for (int i = 0; i < after.Count; i++)
                {
                    if (after[i].Payload != AlcovePayload.Box) continue;
                    Assert.IsNotNull(after[i].BoxContents, $"시드 {seed} · 벽감 #{i}");
                    Assert.Greater(after[i].BoxContents.Count, 0, $"시드 {seed}에서 상자 벽 #{i}가 빈손이 됐다");
                }
            }
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "D-03")]
        public void ContentOrder_KeepsTotalCheeseAndBothTickets()
        {
            List<AlcovePlacement> src = SixAlcoves();
            int cheeseBefore = CheeseOf(src);
            Assert.Greater(cheeseBefore, 0, "전제: 벽 뒤에 치즈가 있다");

            for (int seed = 1; seed <= 500; seed++)
            {
                List<AlcovePlacement> after = Apply(src, TicketShuffleLogic.ContentOrder(src, seed));
                Assert.AreEqual(cheeseBefore, CheeseOf(after), $"시드 {seed}에서 치즈 총량이 변했다");
                Assert.AreEqual(2, TicketWalls(after).Count, $"시드 {seed}에서 직업권이 2장이 아니다");
            }
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "D-16")]
        public void ContentOrder_TicketsStayOnAFarthestPairOfWalls()
        {
            // M3(P0-9) — 굽기가 둘을 가장 먼 두 벽에 나눠 두는 이유는 "나란히 두면 한 사람이 둘 다 먹는다".
            // 셔플 뒤에도 한쪽에서 본 **가장 먼 상자 벽**이 다른 한 장의 자리여야 한다.
            List<AlcovePlacement> src = SixAlcoves();
            for (int seed = 1; seed <= 500; seed++)
            {
                List<AlcovePlacement> after = Apply(src, TicketShuffleLogic.ContentOrder(src, seed));
                List<int> walls = TicketWalls(after);
                Assert.AreEqual(2, walls.Count);
                Assert.AreEqual(AlcovePayload.Box, after[walls[0]].Payload);
                Assert.AreEqual(AlcovePayload.Box, after[walls[1]].Payload);

                float pair = Vector2.Distance(after[walls[0]].Pos, after[walls[1]].Pos);
                Assert.IsTrue(
                    Mathf.Approximately(pair, FarthestBoxDistance(after, walls[0]))
                    || Mathf.Approximately(pair, FarthestBoxDistance(after, walls[1])),
                    $"시드 {seed}에서 직업권 두 벽이 최원거리 쌍이 아니다 ({pair:0.#} m)");
            }
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "D-16")]
        public void ContentOrder_DifferentSeeds_MoveTheTickets()
        {
            // 셔플이 존재하는 이유 — "어느 벽을 뚫을지"가 외운 답이 되면 안 된다.
            List<AlcovePlacement> src = SixAlcoves();
            var seen = new HashSet<string>();
            for (int seed = 1; seed <= 200; seed++)
            {
                List<int> walls = TicketWalls(Apply(src, TicketShuffleLogic.ContentOrder(src, seed)));
                walls.Sort();
                seen.Add($"{walls[0]}-{walls[1]}");
            }
            Assert.Greater(seen.Count, 1, "시드를 200개 굴려도 직업권 자리가 한 쌍뿐이다 — 셔플이 안 닿는다");
        }

        [Test]
        public void WholeListPermutation_LosesContents_WhichIsWhyContentOrderExists()
        {
            // 뮤테이션 — 예전 방식(전 자리 순열)을 그대로 돌리면 위 불변식이 **실제로 깨진다**.
            // 이 테스트가 빨개지면 위 초록이 "검사가 헛돌아서"가 아니라는 근거가 사라진다.
            List<AlcovePlacement> src = SixAlcoves();
            int broken = 0;
            for (int seed = 1; seed <= 100; seed++)
            {
                List<AlcovePlacement> after = Apply(src, TicketShuffleLogic.Permutation(src.Count, seed));
                foreach (AlcovePlacement a in after)
                {
                    if (a.Payload == AlcovePayload.Box && a.BoxContents.Count == 0) { broken++; break; }
                }
            }
            Assert.Greater(broken, 50, $"예전 방식이 100판 중 {broken}판만 물건을 잃었다 — 전제(5/6)가 안 맞는다");
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "D-03")]
        public void ContentOrder_OnTheRealWarehouseLayout_KeepsEverything()
        {
            var layout = UnityEditor.AssetDatabase.LoadAssetAtPath<MapLayout>(WAREHOUSE);
            Assert.IsNotNull(layout, $"레이아웃 에셋이 없다: {WAREHOUSE}");

            IReadOnlyList<AlcovePlacement> src = layout.Alcoves;
            Assert.Greater(src.Count, 0, "전제: 창고 맵에 벽감이 있다");

            int cheeseBefore = AlcoveLogic.TotalCheese(src);
            Assert.AreEqual(2, TicketWalls(src).Count, "전제: 구운 맵에 직업권이 2장이다");

            for (int seed = 1; seed <= 300; seed++)
            {
                List<AlcovePlacement> after = Apply(src, TicketShuffleLogic.ContentOrder(src, seed));
                Assert.AreEqual(cheeseBefore, AlcoveLogic.TotalCheese(after), $"시드 {seed}: 벽 뒤 치즈 총량이 변했다");
                Assert.AreEqual(2, TicketWalls(after).Count, $"시드 {seed}: 직업권이 2장이 아니다");

                for (int i = 0; i < after.Count; i++)
                {
                    if (after[i].Payload == AlcovePayload.Box)
                        Assert.Greater(after[i].BoxContents.Count, 0, $"시드 {seed}: 상자 벽 #{i}가 빈손");
                    else
                        Assert.AreEqual(0, after[i].BoxContents.Count, $"시드 {seed}: 강아지 벽 #{i}에 물건이 갔다");
                }
            }
        }
    }
}
