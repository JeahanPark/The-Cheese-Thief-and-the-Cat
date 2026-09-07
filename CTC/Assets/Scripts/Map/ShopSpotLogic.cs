using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 상점 자리 고르기 (테크 v3). 방 중심 후보 중 서로 <c>_minGap</c> 이상 떨어진 N개를 시드로 고른다.
    /// 계획 1은 시드 = 맵 시드라 판마다 같다. 계획 3에서 판 시드로 바꿔 무작위가 된다.
    /// </summary>
    public static class ShopSpotLogic
    {
        public static List<Vector2> Pick(IReadOnlyList<Vector2> _candidates, int _count, float _minGap, int _seed)
        {
            var picked = new List<Vector2>();
            if (_candidates == null || _candidates.Count == 0 || _count <= 0) return picked;

            var order = new List<int>(_candidates.Count);
            for (int i = 0; i < _candidates.Count; i++) order.Add(i);
            var rng = new System.Random(_seed);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            float gapSqr = _minGap * _minGap;
            foreach (int idx in order)
            {
                Vector2 p = _candidates[idx];
                bool ok = true;
                foreach (Vector2 q in picked)
                {
                    if ((p - q).sqrMagnitude < gapSqr) { ok = false; break; }
                }
                if (!ok) continue;
                picked.Add(p);
                if (picked.Count >= _count) break;
            }
            return picked;
        }

        /// <summary>수평 반경 판정 — 높이는 무시(평면 맵).</summary>
        public static bool Contains(Vector3 _center, Vector3 _point, float _radius)
        {
            float dx = _point.x - _center.x, dz = _point.z - _center.z;
            return dx * dx + dz * dz <= _radius * _radius;
        }
    }
}
