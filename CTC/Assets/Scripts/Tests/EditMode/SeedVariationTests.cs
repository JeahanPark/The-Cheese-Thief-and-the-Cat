using System.Collections.Generic;
using CTC.Runtime.Ai.Bt;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 결정성 테스트의 **짝** — 「다른 씨앗 → 다른 결과」(케이스 E-31).
    ///
    /// 같은 씨앗으로 두 번 불러 같은 값이 나오는 것만으로는, 씨앗이 결과에 **아예 안 닿아도** 통과한다.
    /// 여기 있는 것은 원래 테스트 파일에 짝이 없던 함수들이다. 시드 하나(1)와 2~40을 견줘
    /// **한 번이라도** 다르면 통과 — 해시 충돌 같은 우연에는 흔들리지 않는다.
    /// </summary>
    public class SeedVariationTests
    {
        private const int FIRST_SEED = 1;
        private const int LAST_SEED = 40;

        [NUnit.Framework.Property("CaseId", "E-31")]
        [Test]
        public void MapDressPick_DifferentSeeds_DifferSomewhere()
        {
            // 짝 판정(E-31 메타 테스트)이 본문의 `MapDressLogic.Pick(` 호출을 보므로 도우미로 빼지 않는다
            bool differs = false;
            for (int seed = FIRST_SEED + 1; seed <= LAST_SEED && !differs; seed++)
            {
                for (int slot = 0; slot < 50 && !differs; slot++)
                    differs = MapDressLogic.Pick(slot, 7, seed) != MapDressLogic.Pick(slot, 7, FIRST_SEED);
            }
            Assert.IsTrue(differs, "씨앗을 40개 굴려도 변종 배치가 같다 — 씨앗이 결과에 안 닿는다");
        }

        [NUnit.Framework.Property("CaseId", "E-31")]
        [Test]
        public void RoomModulePickModule_DifferentSeeds_DifferSomewhere()
        {
            var ids = new List<string> { "A", "B", "C" };
            bool differs = false;
            for (int seed = FIRST_SEED + 1; seed <= LAST_SEED && !differs; seed++)
            {
                for (int room = 0; room < 10 && !differs; room++)
                    differs = RoomModuleLogic.PickModule(ids, room, seed) != RoomModuleLogic.PickModule(ids, room, FIRST_SEED);
            }
            Assert.IsTrue(differs, "씨앗을 40개 굴려도 방마다 고른 모듈이 같다");
        }

        [NUnit.Framework.Property("CaseId", "E-31")]
        [Test]
        public void PantryPlankColor_DifferentSeeds_DifferSomewhere()
        {
            var baseColor = Color.white;
            Color first = PantryTextureLogic.PlankColor(0.37f, 0.61f, 10, FIRST_SEED, baseColor);
            bool differs = false;
            for (int seed = FIRST_SEED + 1; seed <= LAST_SEED && !differs; seed++)
            {
                Color other = PantryTextureLogic.PlankColor(0.37f, 0.61f, 10, seed, baseColor);
                differs = Mathf.Abs(other.r - first.r) > 1e-4f;
            }
            Assert.IsTrue(differs, "씨앗을 40개 굴려도 널 색이 같다");
        }

        [NUnit.Framework.Property("CaseId", "E-31")]
        [Test]
        public void MapCeilingHangSlots_DifferentSeeds_DifferSomewhere()
        {
            var lamps = new List<Vector2> { new Vector2(0f, 0f) };
            var moons = new List<Vector2> { new Vector2(10f, 10f) };
            string first = HangSignature(MapCeilingLogic.HangSlots(30f, FIRST_SEED, 5, lamps, moons));
            bool differs = false;
            for (int seed = FIRST_SEED + 1; seed <= LAST_SEED && !differs; seed++)
                differs = HangSignature(MapCeilingLogic.HangSlots(30f, seed, 5, lamps, moons)) != first;
            Assert.IsTrue(differs, "씨앗을 40개 굴려도 매단 자리가 같다");
        }

        private static string HangSignature(List<MapCeilingLogic.HangSlot> _slots)
        {
            var sb = new System.Text.StringBuilder();
            foreach (MapCeilingLogic.HangSlot s in _slots) sb.Append(s.Pos).Append(s.Kind).Append(';');
            return sb.ToString();
        }

        [NUnit.Framework.Property("CaseId", "E-31")]
        [Test]
        public void ShopSpotPick_DifferentSeeds_DifferSomewhere()
        {
            List<RoomRect> rooms = Grid(7, 20f);
            List<Vector2> first = ShopSpotLogic.Pick(rooms, 4, 25f, 3f, FIRST_SEED);
            Assert.AreEqual(4, first.Count, "넉넉한 49칸에서 넷을 못 골랐다 — 전제가 깨졌다");
            bool differs = false;
            for (int seed = FIRST_SEED + 1; seed <= LAST_SEED && !differs; seed++)
            {
                List<Vector2> other = ShopSpotLogic.Pick(rooms, 4, 25f, 3f, seed);
                foreach (Vector2 p in other)
                {
                    if (!first.Contains(p)) { differs = true; break; }
                }
            }
            Assert.IsTrue(differs, "씨앗을 40개 굴려도 상점 자리가 같다");
        }

        private static List<RoomRect> Grid(int _n, float _step)
        {
            var list = new List<RoomRect>();
            for (int i = 0; i < _n; i++)
                for (int j = 0; j < _n; j++)
                    list.Add(new RoomRect { Id = $"R{i}_{j}", Center = new Vector2(i * _step, j * _step), Size = new Vector2(9f, 9f) });
            return list;
        }

        [NUnit.Framework.Property("CaseId", "E-31")]
        [Test]
        public void BtRandomSelector_DifferentSeeds_PickDifferentChildren()
        {
            var picked = new HashSet<int>();
            for (int seed = FIRST_SEED; seed <= LAST_SEED; seed++)
            {
                int which = -1;
                var a = new BtAction(_ => { which = 0; return BtStatus.Success; });
                var b = new BtAction(_ => { which = 1; return BtStatus.Success; });
                new BtRandomSelector(new System.Random(seed), a, b).Tick(0f);
                picked.Add(which);
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, picked,
                "씨앗을 40개 굴려도 한쪽 자식만 고른다 — 씨앗이 선택에 안 닿는다");
        }
    }
}
