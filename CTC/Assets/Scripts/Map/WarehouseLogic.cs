using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 창고 평면도(2026-09-07)의 순수 규칙 — 선반 열을 틈으로 자르고, 경계벽 겹침을 검사한다.
    /// 도면(`docs/design-vault/맵 시안 v2 창고.html`)의 좌표를 그대로 받는다. x 동 · z 북 · 미터.
    /// </summary>
    public static class WarehouseLogic
    {
        /// <summary>이보다 짧은 조각은 안 짓는다 — 틈 옆에 남는 부스러기.</summary>
        public const float MIN_SEGMENT = 0.5f;

        public const float SHELF_HEIGHT = 5f;
        public const float SHELF_THICK = 1f;
        public const float CRATE_WALL_HEIGHT = 2.4f;
        public const float BARREL_HEIGHT = 3f;
        public const float BARREL_SIZE = 2.4f;

        /// <summary>[lo, hi] 구간에서 틈(x=시작, y=끝)들을 뺀 나머지 구간 목록. 틈 순서는 상관없다.</summary>
        public static List<Vector2> SplitByGaps(float _lo, float _hi, IReadOnlyList<Vector2> _gaps)
        {
            var gaps = new List<Vector2>(_gaps ?? new List<Vector2>());
            gaps.Sort((a, b) => a.x.CompareTo(b.x));

            var segments = new List<Vector2>();
            float cursor = _lo;
            for (int i = 0; i <= gaps.Count; i++)
            {
                float end = i < gaps.Count ? gaps[i].x : _hi;
                if (end - cursor >= MIN_SEGMENT) segments.Add(new Vector2(cursor, end));
                if (i < gaps.Count) cursor = Mathf.Max(cursor, gaps[i].y);
            }
            return segments;
        }

        /// <summary>
        /// 열(from→to, 축 정렬) 위의 한 구간을 박스로. 가로 열(z 고정)이면 폭이 x, 세로 열이면 폭이 z.
        /// </summary>
        public static PropPlacement ShelfBox(string _name, Vector2 _from, Vector2 _to, Vector2 _segment, float _thickness)
        {
            bool horizontal = Mathf.Approximately(_from.y, _to.y);
            float mid = (_segment.x + _segment.y) * 0.5f;
            float length = _segment.y - _segment.x;
            return new PropPlacement
            {
                Name = _name,
                Kind = PropKind.Shelf,
                Pos = horizontal ? new Vector2(mid, _from.y) : new Vector2(_from.x, mid),
                Size = horizontal ? new Vector2(length, _thickness) : new Vector2(_thickness, length),
                YawDeg = 0f,
                Height = SHELF_HEIGHT,
            };
        }

        /// <summary>박스가 경계벽 안쪽(여유 inset)에 온전히 드는가 — 아니면 벽에 파고든다.</summary>
        public static bool InsideBounds(Vector2 _pos, Vector2 _size, float _halfExtent, float _inset)
        {
            float limit = _halfExtent - _inset;
            return _pos.x - _size.x * 0.5f >= -limit && _pos.x + _size.x * 0.5f <= limit
                && _pos.y - _size.y * 0.5f >= -limit && _pos.y + _size.y * 0.5f <= limit;
        }
    }
}
