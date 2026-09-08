using System.Collections.Generic;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests
{
    /// <summary>오늘의 품절(계획 3 Task 3 · 기획서 §7-2) — 판정은 순수 static이라 여기서 다 덮는다.</summary>
    public class TechRandomLogicTests
    {
        // 면제 3종을 섞어 넣은 가짜 카탈로그 — 실제 CSV와 무관하게 규칙만 잰다.
        private static readonly TechId[] CATALOG =
        {
            TechId.Gun, TechId.Pepper, TechId.Fortress, TechId.Warehouse, TechId.Dash,
            TechId.CheeseShield, TechId.BigPouch, TechId.HiddenWarehouse, TechId.Sticky, TechId.FastFeet,
        };

        private static List<TechEntry> Owned(params (TechId, int)[] _e)
        {
            var list = new List<TechEntry>();
            foreach (var (id, lv) in _e) list.Add(new TechEntry(id, lv));
            return list;
        }

        [Test]
        public void SoldOut_SameSeed_SameAnswer()
        {
            // 이게 깨지면 서버와 클라가 서로 다른 품절 목록을 본다 — 시드만 복제하는 설계의 전제다.
            CollectionAssert.AreEqual(
                TechRandomLogic.SoldOut(CATALOG, 2, 12345),
                TechRandomLogic.SoldOut(CATALOG, 2, 12345));
        }

        [Test]
        public void SoldOut_DifferentSeeds_ProduceDifferentAnswers()
        {
            // 판마다 같은 답이 나오면 "무작위인데 두 판이 똑같다"가 되어 배선이 끊긴 것과 구분이 안 된다.
            IReadOnlyList<TechId> first = TechRandomLogic.SoldOut(CATALOG, 2, 1);
            bool anyDifferent = false;
            for (int seed = 2; seed <= 40 && !anyDifferent; seed++)
            {
                IReadOnlyList<TechId> other = TechRandomLogic.SoldOut(CATALOG, 2, seed);
                for (int i = 0; i < other.Count; i++)
                {
                    if (i >= first.Count || other[i] != first[i]) { anyDifferent = true; break; }
                }
            }
            Assert.IsTrue(anyDifferent, "시드를 40개 굴려도 답이 같다 — 시드가 결과에 안 닿는다");
        }

        [Test]
        public void SoldOut_NeverPicksTheExemptThree()
        {
            // 요새·대시·창고가 없는 판은 쥐가 할 줄 아는 것이 통째로 사라진다(기획서 §7-2).
            for (int seed = 1; seed <= 200; seed++)
            {
                IReadOnlyList<TechId> list = TechRandomLogic.SoldOut(CATALOG, 3, seed);
                foreach (TechId id in list)
                {
                    Assert.IsFalse(TechRandomLogic.IsExempt(id), $"시드 {seed}에서 면제 테크 {id}가 품절됐다");
                }
            }
        }

        [Test]
        public void SoldOut_HasNoDuplicates()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                IReadOnlyList<TechId> list = TechRandomLogic.SoldOut(CATALOG, 4, seed);
                var seen = new HashSet<TechId>();
                foreach (TechId id in list) Assert.IsTrue(seen.Add(id), $"시드 {seed}에서 {id}가 두 번 나왔다");
            }
        }

        [Test]
        public void SoldOut_MoreThanCandidates_ReturnsAllCandidates()
        {
            // 후보 7(10 − 면제 3) — 그보다 많이 달라고 해도 후보 전부까지다.
            IReadOnlyList<TechId> list = TechRandomLogic.SoldOut(CATALOG, 99, 7);
            Assert.AreEqual(CATALOG.Length - TechRandomLogic.NEVER_SOLD_OUT.Count, list.Count);
        }

        [Test]
        public void SoldOut_ZeroOrNegativeCount_IsEmpty()
        {
            Assert.AreEqual(0, TechRandomLogic.SoldOut(CATALOG, 0, 7).Count);
            Assert.AreEqual(0, TechRandomLogic.SoldOut(CATALOG, -3, 7).Count);
            Assert.AreEqual(0, TechRandomLogic.SoldOut(null, 2, 7).Count);
        }

        [Test]
        public void SoldOut_IsSortedInCatalogOrder()
        {
            // 토스트 문구의 순서가 시드에 따라 바뀌면 같은 판을 보는 여덟 명이 서로 다른 줄을 읽는다.
            for (int seed = 1; seed <= 50; seed++)
            {
                IReadOnlyList<TechId> list = TechRandomLogic.SoldOut(CATALOG, 3, seed);
                int previous = -1;
                foreach (TechId id in list)
                {
                    int index = System.Array.IndexOf(CATALOG, id);
                    Assert.Greater(index, previous, $"시드 {seed} — 카탈로그 순이 아니다");
                    previous = index;
                }
            }
        }

        [Test]
        public void IsSoldOut_NullList_IsNeverSoldOut()
        {
            // 로비·봇 실험실은 시드가 없다 — 그 자리에서는 지금까지와 똑같이 다 살 수 있어야 한다.
            Assert.IsFalse(TechRandomLogic.IsSoldOut(null, TechId.Pepper));
            Assert.IsFalse(TechRandomLogic.IsSoldOut(new TechId[0], TechId.Pepper));
        }

        [Test]
        public void SoldOut_RealMouseCatalog_LeavesSevenCandidates()
        {
            // 기획서 §7-2: 쥐 테크 10 − 면제 3 = 7 중 2. 카탈로그가 바뀌면 여기서 먼저 걸린다.
            IReadOnlyList<TechId> catalog = TechLogic.Catalog(RoleId.Mouse);
            int candidates = 0;
            foreach (TechId id in catalog)
            {
                if (!TechRandomLogic.IsExempt(id)) candidates++;
            }
            Assert.AreEqual(7, candidates);
        }

        [Test]
        public void CanBuy_SoldOutTech_IsDenied()
        {
            var soldOut = new[] { TechId.Pepper };
            Assert.AreEqual(TechDeny.SoldOut,
                TechLogic.CanBuy(Owned(), TechId.Pepper, RoleId.Mouse, 9999, 5, soldOut));
        }

        [Test]
        public void CanBuy_SoldOutBeatsSlotsFull()
        {
            // "칸이 없어서"가 아니라 "오늘은 없어서"다 — 이유가 바뀌면 토스트가 거짓말을 한다.
            List<TechEntry> full = Owned(
                (TechId.Gun, 1), (TechId.Fortress, 1), (TechId.Warehouse, 1),
                (TechId.Dash, 1), (TechId.Sticky, 1));
            Assert.AreEqual(TechDeny.SoldOut,
                TechLogic.CanBuy(full, TechId.Pepper, RoleId.Mouse, 9999, 5, new[] { TechId.Pepper }));
        }

        [Test]
        public void CanBuy_OwnedTech_IsNotBlockedBySoldOut()
        {
            // 품절은 판 시작에 정해지니 실제로는 Lv0만 걸린다 — 그래도 규칙으로 못 박아 둔다.
            Assert.AreEqual(TechDeny.None,
                TechLogic.CanBuy(Owned((TechId.Pepper, 1)), TechId.Pepper, RoleId.Mouse, 9999, 5,
                    new[] { TechId.Pepper }));
        }

        [Test]
        public void CanBuy_NullSoldOut_BehavesAsBefore()
        {
            Assert.AreEqual(TechDeny.None,
                TechLogic.CanBuy(Owned(), TechId.Pepper, RoleId.Mouse, 9999, 5, null));
        }

        [Test]
        public void DenyText_SoldOut_SaysToday()
        {
            Assert.AreEqual("오늘은 품절", TechLogic.DenyText(TechDeny.SoldOut, RoleId.Mouse, 0, 5));
        }
    }
}
