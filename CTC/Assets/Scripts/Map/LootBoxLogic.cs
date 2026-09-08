using System.Collections.Generic;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 상자 규칙. MonoBehaviour와 분리된 순수 함수(EditMode TDD).
    ///
    /// **상자는 안에 뭐가 들었는지 모른다.** 여기서 하는 건 "이 목록이 치즈를 얼마나 품고 있나"를
    /// 세는 것뿐이고, 그건 맵 총량(쥐 1인당 몫 × 인원)에서 미리 떼야 하기 때문에 필요하다 —
    /// 안 떼면 총량이 늘어 고갈이 늦어지고 후반 압축이 무너진다(벽감과 같은 이유).
    /// 치즈가 아닌 물건이 늘어도 여기는 안 바뀐다.
    /// </summary>
    public static class LootBoxLogic
    {
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;

        /// <summary>상자 HP — 벽감(120)보다 훨씬 무르다. 조용히 혼자 여는 물건이라 시간을 안 뺏는다.</summary>
        public static int BoxHp => Balance.BoxHp;

        /// <summary>상자 하나에 넣을 치즈 기본값. 배치가 덮어쓸 수 있다.</summary>
        public static int CheesePerBox => Balance.BoxCheese;

        /// <summary>상자가 쏟는 덩이 수.</summary>
        public static int DropCount => Balance.BoxDropCount;

        /// <summary>목록이 품은 치즈 합계. 치즈가 아닌 항목은 무시한다.</summary>
        public static int CheeseIn(IReadOnlyList<LootEntry> _contents)
        {
            if (_contents == null) return 0;

            int sum = 0;
            for (int i = 0; i < _contents.Count; i++)
            {
                if (_contents[i].Id != LootId.Cheese) continue;
                if (_contents[i].Amount > 0) sum += _contents[i].Amount;
            }
            return sum;
        }

        /// <summary>이 목록에 그 물건이 들었나 — 직업권이 어느 자리에 갔는지 세는 데 쓴다(계획 3 Task 4).</summary>
        public static bool Contains(IReadOnlyList<LootEntry> _contents, LootId _id)
        {
            if (_contents == null) return false;

            for (int i = 0; i < _contents.Count; i++)
            {
                if (_contents[i].Id == _id) return true;
            }
            return false;
        }

        /// <summary>맵의 상자 전부가 품은 치즈 합계 — 지점 매장량에서 이만큼 뗀다.</summary>
        public static int TotalCheese(IReadOnlyList<BoxPlacement> _boxes)
        {
            if (_boxes == null) return 0;

            int sum = 0;
            for (int i = 0; i < _boxes.Count; i++) sum += CheeseIn(_boxes[i].Contents);
            return sum;
        }
    }
}
