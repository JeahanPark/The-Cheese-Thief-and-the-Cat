using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 벽감이 앉을 벽 자리를 고르는 규칙(순수). 방 하나에 한 자리씩, 벽면에 붙여서.
    ///
    /// 두 단계로 본다 — 이상 거리(문 4m·치즈 3.5m)로 먼저 훑고, 그걸로 하한
    /// (강아지 1 + 직업권 2 + 치즈 벽 1 = 4)을 못 채우는 맵에서만 탈락한 방을
    /// 물리 최소 거리로 다시 본다. 기본 맵(30×30·방 6)이 실제로 이랬다(2026-08-12) —
    /// 이상 거리만 쓰면 자리가 3개뿐이라 직업권 두 장이 상자 벽을 다 차지해
    /// **치즈 벽이 0개**가 됐고, 부술 벽이 없어 즉시 획득 루프가 통째로 죽었다.
    /// </summary>
    public static class AlcoveSpotLogic
    {
        public struct Spot
        {
            public Vector2 Pos;
            public float YawDeg;
            public Vector2 Anchor;   // 방 중심 — 흩기(SpreadPick)의 기준점
        }

        // 벽에서 안쪽으로 이만큼 들어와 앉는다 — 딱 붙이면 콜라이더가 벽을 파고든다
        public const float WALL_INSET = 0.7f;

        // 이상 거리 — 자리가 넉넉한 맵이 쓰는 기본값
        public const float MIN_CHEESE_DIST = 3.5f;   // 붙어 있으면 한 자리에서 다 해결돼 이동이 사라진다
        public const float MIN_DOOR_DIST = 4f;       // 벽 한가운데 = 문 자리 — 막다른 방이 잠긴다(2026-08-11)

        // 물리 최소 거리 — 하한을 못 채울 때만 쓴다. 이 아래로는 실제로 겹친다.
        public const float FLOOR_CHEESE_DIST = 2.6f; // 지점 원반 최대 반지름 2.0 + 벽감 반폭 0.6
        public const float FLOOR_DOOR_DIST = 2.5f;   // 복도 반폭 1.5 + 벽감 반폭 0.6 + 여유

        // 문을 피해 벽을 따라 비껴 앉을 자리들(벽 길이 대비 비율). 0이 먼저 = 원래의 한가운데.
        private static readonly float[] WALL_SLIDES = { 0f, 0.3f, -0.3f };

        /// <summary>
        /// 방마다 한 자리씩 모은다. 이상 거리로 <paramref name="_floorCount"/>개가 안 나오면
        /// 탈락한 방만 물리 최소 거리로 다시 봐서 하한까지 채운다. 그래도 모자라면 있는 만큼만 —
        /// 자리를 지어내지는 않는다.
        /// </summary>
        public static List<Spot> CollectSpots(
            IReadOnlyList<RoomRect> _rooms,
            IReadOnlyList<CorridorSeg> _corridors,
            IReadOnlyList<CheesePlacement> _cheese,
            int _floorCount)
        {
            var spots = new List<Spot>();
            var failedRooms = new List<RoomRect>();
            foreach (RoomRect room in _rooms)
            {
                if (TryWallSpot(room, _corridors, _cheese, MIN_DOOR_DIST, MIN_CHEESE_DIST, out Spot spot))
                {
                    spots.Add(spot);
                }
                else
                {
                    failedRooms.Add(room);
                }
            }

            foreach (RoomRect room in failedRooms)
            {
                if (spots.Count >= _floorCount) break;
                if (TryWallSpot(room, _corridors, _cheese, FLOOR_DOOR_DIST, FLOOR_CHEESE_DIST, out Spot spot))
                {
                    spots.Add(spot);
                }
            }
            return spots;
        }

        /// <summary>
        /// 방의 네 벽을 순서대로 시도한다. 무작위를 쓰지 않는 건 같은 레이아웃이면
        /// 항상 같은 자리가 나와야 QA가 재현 가능하기 때문이다.
        /// </summary>
        public static bool TryWallSpot(
            RoomRect _room,
            IReadOnlyList<CorridorSeg> _corridors,
            IReadOnlyList<CheesePlacement> _cheese,
            float _minDoorDist,
            float _minCheeseDist,
            out Spot _spot)
        {
            Vector2 c = _room.Center;
            Vector2 half = _room.Size * 0.5f;

            // 벽마다 한가운데를 먼저 보고, 걸리면 벽을 따라 좌우로 비껴 앉는다.
            foreach (float slide in WALL_SLIDES)
            {
                float dx = _room.Size.x * slide;
                float dy = _room.Size.y * slide;

                // (자리, 방 안쪽을 보는 yaw) — 북·남·동·서
                var candidates = new (Vector2 pos, float yaw)[]
                {
                    (new Vector2(c.x + dx, c.y + half.y - WALL_INSET), 180f),
                    (new Vector2(c.x + dx, c.y - half.y + WALL_INSET), 0f),
                    (new Vector2(c.x + half.x - WALL_INSET, c.y + dy), 270f),
                    (new Vector2(c.x - half.x + WALL_INSET, c.y + dy), 90f),
                };

                foreach ((Vector2 pos, float yaw) in candidates)
                {
                    if (!IsClearOfCheese(pos, _cheese, _minCheeseDist)) continue;
                    if (!IsClearOfDoors(pos, _corridors, _minDoorDist)) continue;
                    _spot = new Spot { Pos = pos, YawDeg = yaw, Anchor = _room.Center };
                    return true;
                }
            }

            _spot = default;
            return false;
        }

        // 문은 복도 끝점이다 — 거기에 벽감을 앉히면 그 방이 잠긴다.
        private static bool IsClearOfDoors(Vector2 _pos, IReadOnlyList<CorridorSeg> _corridors, float _minDist)
        {
            for (int i = 0; i < _corridors.Count; i++)
            {
                if (Vector2.Distance(_corridors[i].From, _pos) < _minDist) return false;
                if (Vector2.Distance(_corridors[i].To, _pos) < _minDist) return false;
            }
            return true;
        }

        private static bool IsClearOfCheese(Vector2 _pos, IReadOnlyList<CheesePlacement> _cheese, float _minDist)
        {
            for (int i = 0; i < _cheese.Count; i++)
            {
                if (Vector2.Distance(_pos, _cheese[i].Pos) < _minDist) return false;
            }
            return true;
        }
    }
}
