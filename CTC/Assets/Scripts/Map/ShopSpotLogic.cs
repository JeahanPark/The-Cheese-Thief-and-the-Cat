using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 상점 자리 고르기 (테크 v3). 방 중심(또는 손 배치 후보) 중 서로 <c>_minGap</c> 이상 떨어진 N개를
    /// 시드로 고른다. **방 크기를 본다** — 상점 원반이 들어가는 방을 먼저 쓴다(<see cref="RoomHoldsShop"/>).
    ///
    /// 시드의 주인은 계획 3부터 <c>MatchRandom</c>이다(판마다 다르다). 맵 시드는 시드가 없을 때의 후퇴다.
    /// </summary>
    public static class ShopSpotLogic
    {
        /// <summary>
        /// 방 벽이 먹는 폭(m) — <c>MapBuilder.ROOM_WALL_THICK</c>(0.5)가 마주 보는 두 벽에 한 번씩.
        /// 상점 원반이 방에 들어가는지 재는 데만 쓴다.
        /// </summary>
        public const float ROOM_WALL_MARGIN = 1.0f;

        /// <summary>간격을 몇 번까지 반으로 줄여 볼까 — 25 → 12.5 → 6.25 → 3.125.</summary>
        public const int GAP_RELAX_STEPS = 3;

        /// <summary>
        /// 상점 원반이 이 방 안에 들어가는가.
        ///
        /// 안 들어가면 원반의 대부분이 **벽 속**이다. 상점은 걸어 들어가는 구역인데
        /// 정작 그 안에서 겨눌 수 있는 자리가 전부 벽이 되어 「상점에서 설치권을 사고
        /// 그 자리에 세운다」가 통째로 막힌다 — 2026-09-08 실측: 기본 맵의 상점이
        /// 4×6 m 방(R2) 한가운데 앉는 바람에 반경 2.7 m 둘레 여덟 자리 중 일곱이 벽이었다.
        /// </summary>
        public static bool RoomHoldsShop(Vector2 _roomSize, float _radius)
        {
            float need = _radius * 2f + ROOM_WALL_MARGIN;
            return _roomSize.x >= need && _roomSize.y >= need;
        }

        /// <summary>
        /// 이 맵에 상점 몇 곳을 세울까 (기획서 §6 — 대형 4 · 기본·확장 2).
        ///
        /// **전역 상수 하나로는 안 된다**: 30×30에 넷을 세우면 서로 붙어 서서
        /// "한 곳을 지키는 동안 나머지가 열린다"가 성립하지 않는다.
        /// 간격 완화(<see cref="GAP_RELAX_STEPS"/>)만 넣고 개수를 그대로 두면
        /// 완화가 지키려던 것을 개수가 도로 깬다.
        /// </summary>
        public static int CountFor(float _halfExtent, int _largeCount, int _smallCount, float _largeHalfExtent)
        {
            int count = _halfExtent >= _largeHalfExtent ? _largeCount : _smallCount;
            return Mathf.Max(1, count);
        }

        /// <summary>
        /// 상점을 앉힐 방 고르기. 시드로 섞은 뒤 **원반이 들어가는 방을 앞으로 당기고**,
        /// 서로 <c>_minGap</c> 이상 떨어진 N개를 집는다.
        ///
        /// 좁은 방을 아예 버리지는 않는다 — 작은 맵에서는 들어가는 방이 하나도 없을 수 있고,
        /// 그때는 좁아도 상점이 서는 편이 상점이 없는 것보다 낫다.
        /// </summary>
        public static List<Vector2> Pick(IReadOnlyList<RoomRect> _rooms, int _count, float _minGap,
            float _radius, int _seed)
        {
            if (_rooms == null || _rooms.Count == 0 || _count <= 0) return new List<Vector2>();

            List<int> order = Shuffled(_rooms.Count, _seed);

            // 섞은 순서는 그대로 두고 넉넉한 방만 앞으로 — 시드가 같으면 결과도 같다.
            var roomy = new List<Vector2>(order.Count);
            var cramped = new List<Vector2>(order.Count);
            foreach (int idx in order)
            {
                if (RoomHoldsShop(_rooms[idx].Size, _radius)) roomy.Add(_rooms[idx].Center);
                else cramped.Add(_rooms[idx].Center);
            }
            roomy.AddRange(cramped);

            return SelectRelaxing(roomy, _count, _minGap, _radius);
        }

        /// <summary>
        /// 손 배치 후보에서 오늘 설 자리를 고른다 — 자리는 레벨디자이너 것이고,
        /// **오늘 어느 넷이 서는지**만 판이 정한다(기획서 §7-1).
        ///
        /// 후보가 목표 이하면 지금처럼 전부 쓴다(고를 것이 없다). 후보가 더 많으면 시드로 섞어
        /// 간격 규칙까지 태운다 — 레벨디자이너가 붙여 찍었어도 오늘 선 넷이 뭉치지는 않게.
        /// </summary>
        public static List<Vector2> PickPlaced(IReadOnlyList<Vector2> _placed, int _count, float _minGap,
            float _radius, int _seed)
        {
            if (_placed == null || _placed.Count == 0 || _count <= 0) return new List<Vector2>();
            if (_placed.Count <= _count) return new List<Vector2>(_placed);

            List<int> order = Shuffled(_placed.Count, _seed);
            var ordered = new List<Vector2>(order.Count);
            foreach (int idx in order) ordered.Add(_placed[idx]);

            return SelectRelaxing(ordered, _count, _minGap, _radius);
        }

        /// <summary>
        /// 손 배치 맵(2026-09-07, `Layout_Warehouse01`)은 레벨디자이너가 찍은 자리 중에서 고른다 —
        /// 방 중심 규칙은 절차 맵(방 목록이 있는)에만. 손 배치인데 자리가 비어 있으면 방 규칙으로 후퇴한다.
        /// </summary>
        public static List<Vector2> Resolve(bool _handPlaced, IReadOnlyList<Vector2> _placed,
            IReadOnlyList<RoomRect> _rooms, int _count, float _minGap, float _radius, int _seed)
        {
            if (_handPlaced && _placed != null && _placed.Count > 0)
                return PickPlaced(_placed, _count, _minGap, _radius, _seed);
            return Pick(_rooms, _count, _minGap, _radius, _seed);
        }

        /// <summary>수평 반경 판정 — 높이는 무시(평면 맵).</summary>
        public static bool Contains(Vector3 _center, Vector3 _point, float _radius)
        {
            float dx = _point.x - _center.x, dz = _point.z - _center.z;
            return dx * dx + dz * dz <= _radius * _radius;
        }

        /// <summary>0..n−1을 시드로 섞은 순서 — Fisher-Yates, `System.Random`이라 플랫폼이 달라도 같다.</summary>
        private static List<int> Shuffled(int _count, int _seed)
        {
            var order = new List<int>(_count);
            for (int i = 0; i < _count; i++) order.Add(i);
            var rng = new System.Random(_seed);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        /// <summary>
        /// 목표 개수를 채울 때까지 **간격을 반으로 줄여 가며** 다시 고른다.
        ///
        /// 맵 크기에 비례하는 간격은 기각했다 — 25 m는 "고양이 하나가 두 상점을 동시에 못 지키는 거리"라
        /// **절대값에 뜻이 있다**. 맵 크기로 곱하면 30×30에서 6 m가 되어 상점 둘이 나란히 서고,
        /// 그러면 상점을 여럿 만든 이유(기획서 §3 시간 결핍)가 조용히 사라진다.
        /// 재시도는 25를 **목표**로 두고 이 맵이 감당하는 만큼만 양보한다 — 대형 맵에선 한 번도 안 걸린다.
        ///
        /// 순서(<paramref name="_ordered"/>)는 **한 번만** 만들어 시도마다 재사용한다.
        /// 시도마다 다시 섞으면 같은 시드가 다른 답을 낸다.
        /// </summary>
        private static List<Vector2> SelectRelaxing(List<Vector2> _ordered, int _count, float _minGap, float _radius)
        {
            // 바닥: 원반 지름. 이보다 가까우면 원반끼리 겹쳐 "다른 곳"이 아니다.
            float floorGap = Mathf.Max(0f, _radius * 2f);
            float gap = _minGap;
            List<Vector2> best = null;

            for (int attempt = 0; attempt <= GAP_RELAX_STEPS; attempt++)
            {
                List<Vector2> got = SelectWithGap(_ordered, _count, gap);
                if (best == null || got.Count > best.Count) best = got;
                if (got.Count >= _count)
                {
                    if (attempt > 0)
                    {
                        Core.GameDebug.Log($"[ShopSpotLogic] 간격 완화 {attempt}회 — {_minGap:0.#} → {gap:0.#} m로 {got.Count}곳");
                    }
                    return got;
                }
                if (gap <= floorGap) break;
                gap = Mathf.Max(gap * 0.5f, floorGap);
            }

            Core.GameDebug.Log($"[ShopSpotLogic] 목표 {_count}곳을 못 채웠다 — 간격 {_minGap:0.#} m를 {gap:0.#} m까지 줄여 {best.Count}곳");
            return best;
        }

        /// <summary>순서대로 훑으며 이미 고른 자리에서 <paramref name="_gap"/> 이상 떨어진 것만 집는다.</summary>
        private static List<Vector2> SelectWithGap(List<Vector2> _ordered, int _count, float _gap)
        {
            var picked = new List<Vector2>(_count);
            float gapSqr = _gap * _gap;
            foreach (Vector2 p in _ordered)
            {
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
    }
}
