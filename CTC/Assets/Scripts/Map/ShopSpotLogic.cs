using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 상점 자리 고르기 (테크 v3). 방 중심 후보 중 서로 <c>_minGap</c> 이상 떨어진 N개를 시드로 고른다.
    /// **방 크기를 본다** — 상점 원반이 들어가는 방을 먼저 쓴다(<see cref="RoomHoldsShop"/>).
    /// 계획 1은 시드 = 맵 시드라 판마다 같다. 계획 3에서 판 시드로 바꿔 무작위가 된다.
    /// </summary>
    public static class ShopSpotLogic
    {
        /// <summary>
        /// 방 벽이 먹는 폭(m) — <c>MapBuilder.ROOM_WALL_THICK</c>(0.5)가 마주 보는 두 벽에 한 번씩.
        /// 상점 원반이 방에 들어가는지 재는 데만 쓴다.
        /// </summary>
        public const float ROOM_WALL_MARGIN = 1.0f;

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
        /// 상점을 앉힐 방 고르기. 시드로 섞은 뒤 **원반이 들어가는 방을 앞으로 당기고**,
        /// 서로 <c>_minGap</c> 이상 떨어진 N개를 집는다.
        ///
        /// 좁은 방을 아예 버리지는 않는다 — 작은 맵에서는 들어가는 방이 하나도 없을 수 있고,
        /// 그때는 좁아도 상점이 서는 편이 상점이 없는 것보다 낫다.
        /// </summary>
        public static List<Vector2> Pick(IReadOnlyList<RoomRect> _rooms, int _count, float _minGap,
            float _radius, int _seed)
        {
            var picked = new List<Vector2>();
            if (_rooms == null || _rooms.Count == 0 || _count <= 0) return picked;

            var order = new List<int>(_rooms.Count);
            for (int i = 0; i < _rooms.Count; i++) order.Add(i);
            var rng = new System.Random(_seed);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            // 섞은 순서는 그대로 두고 넉넉한 방만 앞으로 — 시드가 같으면 결과도 같다.
            var roomy = new List<int>(order.Count);
            var cramped = new List<int>(order.Count);
            foreach (int idx in order)
            {
                if (RoomHoldsShop(_rooms[idx].Size, _radius)) roomy.Add(idx);
                else cramped.Add(idx);
            }
            roomy.AddRange(cramped);

            float gapSqr = _minGap * _minGap;
            foreach (int idx in roomy)
            {
                Vector2 p = _rooms[idx].Center;
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

        /// <summary>
        /// 손 배치 맵(2026-09-07, `Layout_Warehouse01`)은 레벨디자이너가 찍은 자리를 그대로 쓴다 —
        /// 방 중심 규칙은 절차 맵(방 목록이 있는)에만. 손 배치인데 자리가 비어 있으면 방 규칙으로 후퇴한다.
        /// </summary>
        public static List<Vector2> Resolve(bool _handPlaced, IReadOnlyList<Vector2> _placed,
            IReadOnlyList<RoomRect> _rooms, int _count, float _minGap, float _radius, int _seed)
        {
            if (_handPlaced && _placed != null && _placed.Count > 0) return new List<Vector2>(_placed);
            return Pick(_rooms, _count, _minGap, _radius, _seed);
        }

        /// <summary>수평 반경 판정 — 높이는 무시(평면 맵).</summary>
        public static bool Contains(Vector3 _center, Vector3 _point, float _radius)
        {
            float dx = _point.x - _center.x, dz = _point.z - _center.z;
            return dx * dx + dz * dz <= _radius * _radius;
        }
    }
}
