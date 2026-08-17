using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 맵에 물건을 **흩어 놓는** 규칙(순수). 벽감처럼 "몇 개를 서로 멀리" 두어야 하는
    /// 배치가 쓴다.
    ///
    /// 왜 필요한가: 방 목록 앞에서부터 N개를 집으면 생성기가 방을 만든 순서를 그대로 따라간다.
    /// 격자 맵은 그 순서가 한 줄씩이라 **벽감 6개가 맨 아래 한 줄에 전부 몰렸다**
    /// (2026-08-10 실측, 120×120 맵). 랜드마크가 한쪽에 몰리면 반대편은 아무 일도 안 일어난다.
    /// </summary>
    public static class PlacementLogic
    {
        /// <summary>기준점에 가장 가까운 후보의 인덱스. 후보가 없으면 -1.</summary>
        public static int NearestTo(IReadOnlyList<Vector2> _points, Vector2 _target)
        {
            if (_points == null || _points.Count == 0) return -1;

            int best = 0;
            float bestSqr = (_points[0] - _target).sqrMagnitude;
            for (int i = 1; i < _points.Count; i++)
            {
                float sqr = (_points[i] - _target).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }
            return best;
        }

        /// <summary>
        /// 서로 멀리 떨어진 후보 <paramref name="_count"/>개를 고른다(farthest-point sampling).
        /// `_firstIndex`에서 시작해 "이미 고른 것들에서 가장 먼" 후보를 계속 더한다.
        ///
        /// 무작위를 안 쓴다 — 같은 레이아웃이면 항상 같은 자리가 나와야 QA가 재현할 수 있다.
        /// </summary>
        public static List<int> SpreadPick(IReadOnlyList<Vector2> _points, int _count, int _firstIndex)
        {
            var chosen = new List<int>();
            if (_points == null || _points.Count == 0 || _count <= 0) return chosen;

            int first = _firstIndex >= 0 && _firstIndex < _points.Count ? _firstIndex : 0;
            chosen.Add(first);

            while (chosen.Count < _count && chosen.Count < _points.Count)
            {
                int farthest = -1;
                float bestMin = -1f;
                for (int i = 0; i < _points.Count; i++)
                {
                    if (chosen.Contains(i)) continue;

                    float nearest = float.MaxValue;
                    foreach (int c in chosen)
                    {
                        nearest = Mathf.Min(nearest, Vector2.Distance(_points[i], _points[c]));
                    }
                    if (nearest <= bestMin) continue;
                    bestMin = nearest;
                    farthest = i;
                }
                if (farthest < 0) break;
                chosen.Add(farthest);
            }
            return chosen;
        }

        /// <summary>이 방 안에(여유 포함) 점이 하나라도 들어 있는가 — 요새 방을 피할 때 쓴다.</summary>
        public static bool RoomHoldsAny(RoomRect _room, IReadOnlyList<Vector2> _points, float _margin)
        {
            if (_points == null) return false;

            Vector2 min = _room.Min - Vector2.one * _margin;
            Vector2 max = _room.Max + Vector2.one * _margin;
            for (int i = 0; i < _points.Count; i++)
            {
                Vector2 p = _points[i];
                if (p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y) return true;
            }
            return false;
        }
    }
}
