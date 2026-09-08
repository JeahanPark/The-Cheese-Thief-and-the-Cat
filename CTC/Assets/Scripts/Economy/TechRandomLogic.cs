using System.Collections.Generic;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 오늘의 품절 추첨 (테크 v3 계획 3 Task 3, 2026-09-08 · 기획서 §7-2).
    ///
    /// 판마다 쥐 테크 몇 개가 **그 판엔 없다**. 늘 하던 조합을 못 하게 만드는 장치다 —
    /// "게임이 무조건 유저의 의도에 따라 움직이지 않게" 하는 세 곳 중 둘째.
    ///
    /// 전부 순수 static이다. 서버가 뽑은 목록을 NetworkList로 실어 보내거나 RPC로 알리지 않고
    /// **시드만 복제**해 클라가 같은 함수로 같은 답을 다시 계산한다(<c>Match.MatchRandom</c>).
    /// 목록을 복제하면 판정이 두 군데(서버가 뽑은 것 · 클라가 본 것)가 되어 어긋날 자리가 생긴다.
    /// </summary>
    public static class TechRandomLogic
    {
        /// <summary>
        /// 품절에서 **빼는** 테크 — 요새·대시·창고(기획서 §7-2). 생존과 째기의 바닥이라
        /// 이게 없는 판은 쥐가 할 줄 아는 것이 통째로 사라진다.
        /// </summary>
        public static readonly IReadOnlyList<TechId> NEVER_SOLD_OUT = new[]
            { TechId.Fortress, TechId.Dash, TechId.Warehouse };

        /// <summary>
        /// 그 판에 못 사는 테크. 시드가 같으면 결과도 같다 — 서버가 뽑고 클라가 다시 계산한다.
        ///
        /// 결과는 **카탈로그 순서**로 정렬해 돌려준다: 토스트 문구가 시드에 따라 순서까지 바뀌면
        /// 같은 판을 보는 여덟 명이 서로 다른 줄을 읽게 된다.
        ///
        /// 고양이 카탈로그를 넣어도 동작하지만 **부르지 않는다**(§7은 쥐 테크만) — 고양이는
        /// 상점에 안 가므로 품절이 곧 "못 큰다"가 된다.
        /// </summary>
        public static IReadOnlyList<TechId> SoldOut(IReadOnlyList<TechId> _catalog, int _count, int _seed)
        {
            var result = new List<TechId>();
            if (_catalog == null || _count <= 0) return result;

            // 후보 = 카탈로그 − 면제. 카탈로그 순서를 유지한다(정렬의 기준이 이 순서다).
            var pool = new List<TechId>(_catalog.Count);
            for (int i = 0; i < _catalog.Count; i++)
            {
                TechId id = _catalog[i];
                if (id == TechId.None || IsExempt(id) || pool.Contains(id)) continue;
                pool.Add(id);
            }
            if (pool.Count == 0) return result;

            int take = _count < pool.Count ? _count : pool.Count;

            // Fisher-Yates — `ShopSpotLogic.Shuffled`와 같은 방식(System.Random이라 플랫폼이 달라도 같다).
            var order = new List<int>(pool.Count);
            for (int i = 0; i < pool.Count; i++) order.Add(i);
            var rng = new System.Random(SafeSeed(_seed));
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            List<int> picked = order.GetRange(0, take);
            picked.Sort();   // 인덱스를 정렬하면 결과가 카탈로그 순서가 된다
            for (int i = 0; i < picked.Count; i++) result.Add(pool[picked[i]]);
            return result;
        }

        /// <summary>이 테크가 오늘 품절인가. <paramref name="_soldOut"/>이 null이면 품절 없음(로비·실험실).</summary>
        public static bool IsSoldOut(IReadOnlyList<TechId> _soldOut, TechId _id)
        {
            if (_soldOut == null) return false;
            for (int i = 0; i < _soldOut.Count; i++)
            {
                if (_soldOut[i] == _id) return true;
            }
            return false;
        }

        /// <summary>절대 품절되지 않는 테크인가.</summary>
        public static bool IsExempt(TechId _id)
        {
            for (int i = 0; i < NEVER_SOLD_OUT.Count; i++)
            {
                if (NEVER_SOLD_OUT[i] == _id) return true;
            }
            return false;
        }

        // System.Random은 int.MinValue를 못 받는 구현이 있다(Math.Abs 오버플로) — 시드는 해시라
        // 실제로 나올 수 있는 값이다. 한 값만 비켜 준다: 여기서 죽으면 그 판이 통째로 안 선다.
        private static int SafeSeed(int _seed) => _seed == int.MinValue ? int.MaxValue : _seed;
    }
}
