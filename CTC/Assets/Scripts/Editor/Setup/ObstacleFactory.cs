using System.Collections.Generic;
using CTC.Runtime.Map;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    // SetupNetworkingScene의 일부 — 방 안 엄폐물 배치 (2026-08-11)
    public static partial class SetupNetworkingScene
    {
        // 눈높이 레이(1.0m)보다 확실히 높게. 쥐·고양이보다 커서 "가려진다"가 눈에 보여야 한다.
        private const float OBSTACLE_HEIGHT = 2.2f;
        // 다른 물건과 이만큼은 떨어뜨린다 — 치즈 지점을 막으면 파밍이 불가능해진다
        private const float OBSTACLE_CLEARANCE = 2.2f;
        // 방 벽에서 이만큼 안쪽에만 둔다 — 벽에 붙이면 문을 막아 방이 잠긴다
        private const float OBSTACLE_WALL_MARGIN = 2.0f;

        /// <summary>
        /// **사람이 판을 시작하는 자리는 더 크게 비운다** (2026-08-11 실측으로 얻은 값).
        ///
        /// 쥐는 요새 중심이 아니라 **입구 1.8m 앞**에 선다. 그냥 2.2m만 띄우면 기둥(1.4폭)의
        /// 면이 1.7m에 오고, 스폰과 **0.1m** 간격이 된다 — 실제로 쥐 하나가 스폰 지점에서
        /// 30초 동안 한 발도 못 뗐다. 감옥도 같다: 갇힌 쥐가 서고 구하러 온 쥐가 붙어야 한다.
        /// </summary>
        private const float OBSTACLE_SPAWN_CLEARANCE = 6f;

        // 고양이는 원점에서 +x로 2m씩 늘어선다(<see cref="Lobby.PlayerSpawner.SpawnPosition"/>).
        // 봇 판은 고양이가 더 서므로 선을 비운다 — 단 20m×±4는 중앙 행을 통째로 뚫어
        // 52.5m 관통 시야를 만들었다(2026-08-12 sightscan). 봇은 빈자리를 찾아 서므로
        // (TestBotManager, 38b225f) 실제 스폰 대열 크기만 비우면 된다.
        private const float CAT_LINE_LENGTH = 8f;
        private const float CAT_LINE_HALF_WIDTH = 2.5f;

        // 차폐벽이 스폰과 지켜야 할 거리 — 기둥(6m)보다 좁다. 벽에 붙은 얇은 판이라
        // 양옆 1.2m 통행이 보장되고, 끼임은 봇 건강 검사(⓪①⑦)가 실측으로 잡는다.
        private const float SCREEN_SPAWN_CLEARANCE = 1.2f;

        /// <summary>
        /// 문 차폐벽(스크린) — **정렬된 문 관통 채널**을 방 안쪽에서 끊는다 (2026-08-12).
        ///
        /// 격자 맵은 문이 벽 한가운데 일직선으로 뚫려, 방 중심선의 기둥(1.4m)으로는
        /// 문 폭(3m)의 절반이 남는다 — sightscan 실측: 그 틈으로 **94.5m** 직선이 살아 있었다.
        /// 문 폭보다 넓은 벽을 문 앞에 세우면 그 문을 지나는 축 방향 시선이 전부 끊긴다.
        /// 이동은 양옆으로 돌아가면 된다(끝과 벽 사이 1.2m 이상을 보장한다).
        /// </summary>
        private const float SCREEN_DEPTH = 0.8f;
        private const float SCREEN_EXTRA_WIDTH = 1.2f;      // 문 폭 + 이만큼 — 비껴 보는 선까지 덮는다
        private const float SCREEN_SIDE_GAP = 1.2f;         // 끝과 벽 사이 통행 폭 (몸 반지름 0.35 + 여유)
        private static readonly float[] SCREEN_OFFSETS = { 2.5f, 3.5f, 4.5f, 5.5f };   // 문에서 안쪽으로

        /// <summary>
        /// 방마다 시야를 끊는 물건을 놓는다 (2026-08-11).
        ///
        /// **왜 필요한가**: 격자 맵은 방과 복도가 축에 정렬돼 있어 문 구멍이 일직선으로 뚫린다.
        /// 봇 실측에서 고양이가 33m 떨어진 쥐를 첫 판단 틱에 봤다 — 그러면 소리로 찾을 이유가
        /// 없어져 발소리 18m·파밍 32m 같은 값이 아무 일도 하지 않는다.
        ///
        /// **어디에 놓는가**: 문은 방 변의 한가운데에 뚫린다. 그래서 방을 가로지르는 시선은
        /// 중심선을 지난다 — 그 선 위, 중심에서 비껴난 자리에 두면 관통 시야가 끊긴다.
        /// 가로·세로 각각 하나씩 두 개가 최소한이다.
        ///
        /// **막으면 안 되는 것**: 치즈 지점·벽감·상자·요새·감옥. 그리고 방 중심 —
        /// 고양이 스폰과 요새·감옥이 거기 있다.
        /// </summary>
        private static void PlanObstaclesInto(MapLayout _layout)
        {
            if (_layout == null) return;

            var blocked = new List<Vector2>();
            foreach (CheesePlacement c in _layout.Cheese) blocked.Add(c.Pos);
            foreach (AlcovePlacement a in _layout.Alcoves) blocked.Add(a.Pos);
            foreach (BoxPlacement b in _layout.Boxes) blocked.Add(b.Pos);

            // 차폐벽은 치즈(파밍 원반)와 구조물(다가가서 부수는 것)의 여유를 따로 본다 —
            // 원반은 2.0m을 지켜야 하지만, 벽감·상자는 옆으로 돌아가면 되니 1.2m이면 된다.
            var cheese = new List<Vector2>();
            foreach (CheesePlacement c in _layout.Cheese) cheese.Add(c.Pos);
            var structures = new List<Vector2>();
            foreach (AlcovePlacement a in _layout.Alcoves) structures.Add(a.Pos);
            foreach (BoxPlacement b in _layout.Boxes) structures.Add(b.Pos);

            // 스폰 자리는 따로 — 훨씬 크게 비운다
            var spawns = new List<Vector2>();
            foreach (FortressPlacement f in _layout.Fortresses) spawns.Add(f.Pos);
            foreach (JailPlacement j in _layout.Jails) spawns.Add(j.Pos);

            var placed = new List<ObstaclePlacement>();

            // **차폐벽이 먼저다.** 기둥을 먼저 놓으면 문 앞자리를 선점해 차폐벽이 무산되는데,
            // 기둥(1.4m)은 문 폭(3m)의 절반만 가려 관통 채널이 살아남는다(2026-08-12 실측:
            // 기둥 먼저 → 최장 41m, 차폐벽 먼저 → 20.5m).
            foreach (RoomRect room in _layout.Rooms)
            {
                PlanDoorScreens(placed, cheese, structures, spawns, room, _layout.Corridors);
            }

            foreach (RoomRect room in _layout.Rooms)
            {
                Vector2 half = room.Size * 0.5f - Vector2.one * OBSTACLE_WALL_MARGIN;
                if (half.x <= 0f || half.y <= 0f) continue;

                // 가로 관통을 끊는 기둥(중심선 위, x로 비껴서) · 세로를 끊는 기둥(y로 비껴서)
                TryPlace(placed, blocked, spawns, new Vector2(room.Center.x + half.x * 0.6f, room.Center.y),
                    new Vector2(1.4f, 1.4f));
                TryPlace(placed, blocked, spawns, new Vector2(room.Center.x, room.Center.y + half.y * 0.6f),
                    new Vector2(1.4f, 1.4f));

                // 큰 방에는 하나 더 — 대각 시야까지 끊는다
                if (room.Size.x >= 11f && room.Size.y >= 10f)
                {
                    TryPlace(placed, blocked, spawns,
                        new Vector2(room.Center.x - half.x * 0.6f, room.Center.y - half.y * 0.5f),
                        new Vector2(2.2f, 1.2f));
                }
            }

            _layout.SetObstacles(placed);
            EditorUtility.SetDirty(_layout);
        }

        /// <summary>
        /// 방의 문마다 안쪽에 차폐벽을 세운다. 문 판정은 `MapBuilder.BuildWallWithGaps`와
        /// 같은 기준(끝점이 변에 닿는 통로)이다 — 어긋나면 벽이 없는 곳을 막는다.
        /// </summary>
        private static void PlanDoorScreens(
            List<ObstaclePlacement> _placed, List<Vector2> _cheese, List<Vector2> _structures,
            List<Vector2> _spawns, RoomRect _room,
            System.Collections.Generic.IReadOnlyList<CorridorSeg> _corridors)
        {
            for (int side = 0; side < 4; side++)   // 0=N(+y) 1=S(-y) 2=E(+x) 3=W(-x)
            {
                bool horizontal = side <= 1;
                float wallCoord = side == 0 ? _room.Max.y : side == 1 ? _room.Min.y
                    : side == 2 ? _room.Max.x : _room.Min.x;
                float inwardSign = (side == 0 || side == 2) ? -1f : 1f;
                float min = horizontal ? _room.Min.x : _room.Min.y;
                float max = horizontal ? _room.Max.x : _room.Max.y;
                float depth = horizontal ? _room.Size.y : _room.Size.x;

                foreach (CorridorSeg c in _corridors)
                {
                    foreach (Vector2 p in new[] { c.From, c.To })
                    {
                        float perp = horizontal ? p.y : p.x;
                        float along = horizontal ? p.x : p.y;
                        if (Mathf.Abs(perp - wallCoord) > c.Width * 0.5f + 0.6f) continue;
                        if (along < min - 0.5f || along > max + 0.5f) continue;

                        TryPlaceScreen(_placed, _cheese, _structures, _spawns,
                            horizontal, wallCoord, inwardSign, along, depth, c.Width, min, max);
                    }
                }
            }
        }

        private static void TryPlaceScreen(
            List<ObstaclePlacement> _placed, List<Vector2> _cheese, List<Vector2> _structures,
            List<Vector2> _spawns, bool _horizontalWall, float _wallCoord, float _inwardSign,
            float _doorAlong, float _roomDepth, float _doorWidth, float _wallMin, float _wallMax)
        {
            float width = _doorWidth + SCREEN_EXTRA_WIDTH;

            // 옆으로 살짝 밀어도 문 폭은 계속 덮인다(여유 1.2의 절반). 치즈가 문 정면에
            // 있을 때 이 슬라이드가 자리를 살린다 — 없으면 그 문의 채널이 통째로 남는다.
            float[] laterals = { 0f, SCREEN_EXTRA_WIDTH * 0.5f, -SCREEN_EXTRA_WIDTH * 0.5f };

            foreach (float offset in SCREEN_OFFSETS)
            {
                if (offset + SCREEN_DEPTH > _roomDepth - 1.0f) break;   // 작은 방 — 맞은편 벽에 붙는다

                // 1차: 문 중앙(±슬라이드) — 양옆 통행 폭(SCREEN_SIDE_GAP)을 지킨다(벽감의 교훈)
                foreach (float lateral in laterals)
                {
                    float along = _doorAlong + lateral;
                    if (Mathf.Min(along - _wallMin, _wallMax - along) - width * 0.5f < SCREEN_SIDE_GAP)
                    {
                        continue;
                    }
                    if (PlaceScreenAt(_placed, _cheese, _structures, _spawns,
                            _horizontalWall, _wallCoord, _inwardSign, offset, along, width))
                    {
                        return;
                    }
                }

                // 2차: **모서리 문** — 문이 방 구석에 붙어 중앙 배치가 안 되면, 가까운 옆벽에
                // 붙는 확장 차폐벽을 세운다. 통행은 열린 쪽 한 방향뿐이지만 문은 안 막는다
                // (확장 맵 실측: 문 18개가 endgap으로 전멸해 39m 관통이 남았다).
                bool nearMin = _doorAlong - _wallMin < _wallMax - _doorAlong;
                float coverEnd = nearMin
                    ? _doorAlong + _doorWidth * 0.5f + SCREEN_EXTRA_WIDTH * 0.5f
                    : _doorAlong - _doorWidth * 0.5f - SCREEN_EXTRA_WIDTH * 0.5f;
                float spanMin = nearMin ? _wallMin : coverEnd;
                float spanMax = nearMin ? coverEnd : _wallMax;
                float farGap = nearMin ? _wallMax - spanMax : spanMin - _wallMin;
                if (farGap >= SCREEN_SIDE_GAP)
                {
                    if (PlaceScreenAt(_placed, _cheese, _structures, _spawns,
                            _horizontalWall, _wallCoord, _inwardSign, offset,
                            (spanMin + spanMax) * 0.5f, spanMax - spanMin))
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>차폐벽 하나를 검사하고 놓는다 — 성공하면 true.</summary>
        private static bool PlaceScreenAt(
            List<ObstaclePlacement> _placed, List<Vector2> _cheese, List<Vector2> _structures,
            List<Vector2> _spawns, bool _horizontalWall, float _wallCoord, float _inwardSign,
            float _offset, float _alongCenter, float _width)
        {
            Vector2 halfSize = _horizontalWall
                ? new Vector2(_width * 0.5f, SCREEN_DEPTH * 0.5f)
                : new Vector2(SCREEN_DEPTH * 0.5f, _width * 0.5f);
            Vector2 pos = _horizontalWall
                ? new Vector2(_alongCenter, _wallCoord + _inwardSign * _offset)
                : new Vector2(_wallCoord + _inwardSign * _offset, _alongCenter);

            // 고양이가 늘어서는 줄은 통째로 비운다 (TryPlace와 같은 규칙)
            if (Mathf.Abs(pos.y) < CAT_LINE_HALF_WIDTH
                && pos.x > -CAT_LINE_HALF_WIDTH && pos.x < CAT_LINE_LENGTH)
            {
                return false;
            }
            if (!RectClear(pos, halfSize, _cheese, 2.0f)) return false;
            if (!RectClear(pos, halfSize, _structures, SCREEN_SPAWN_CLEARANCE)) return false;
            if (!RectClear(pos, halfSize, _spawns, SCREEN_SPAWN_CLEARANCE)) return false;

            // 같은 자리 중복만 거른다. 수직·수평 차폐벽이 L자로 만나는 건 무해하다 —
            // 크기 기준 0.8m 간격을 강제했더니 한 방의 문 넷이 서로를 죽였다(near 24건).
            foreach (ObstaclePlacement o in _placed)
            {
                if (Mathf.Abs(o.Pos.x - pos.x) < 1.0f && Mathf.Abs(o.Pos.y - pos.y) < 1.0f)
                {
                    return false;
                }
            }

            _placed.Add(new ObstaclePlacement
            {
                Pos = pos,
                Size = new Vector2(halfSize.x * 2f, halfSize.y * 2f),
            });
            return true;
        }

        // 점들이 사각형(여유 포함) 밖에 있는가 — 폭이 있는 차폐벽은 중심 거리로 재면 끝이 겹친다
        private static bool RectClear(
            Vector2 _center, Vector2 _halfSize, List<Vector2> _points, float _margin)
        {
            foreach (Vector2 p in _points)
            {
                if (Mathf.Abs(p.x - _center.x) < _halfSize.x + _margin
                    && Mathf.Abs(p.y - _center.y) < _halfSize.y + _margin)
                {
                    return false;
                }
            }
            return true;
        }

        // 다른 물건 위에 얹지 않는다. 자리를 못 찾으면 그냥 넘어간다 —
        // 억지로 밀어 넣으면 파밍 자리나 문을 막는다.
        private static void TryPlace(
            List<ObstaclePlacement> _placed, List<Vector2> _blocked, List<Vector2> _spawns,
            Vector2 _pos, Vector2 _size)
        {
            foreach (Vector2 b in _blocked)
            {
                if (Vector2.Distance(b, _pos) < OBSTACLE_CLEARANCE) return;
            }
            foreach (Vector2 s in _spawns)
            {
                if (Vector2.Distance(s, _pos) < OBSTACLE_SPAWN_CLEARANCE) return;
            }
            foreach (ObstaclePlacement o in _placed)
            {
                if (Vector2.Distance(o.Pos, _pos) < OBSTACLE_CLEARANCE) return;
            }
            // 고양이가 늘어서는 줄은 통째로 비운다 — 시작하자마자 기둥에 끼면 안 된다
            if (Mathf.Abs(_pos.y) < CAT_LINE_HALF_WIDTH
                && _pos.x > -CAT_LINE_HALF_WIDTH && _pos.x < CAT_LINE_LENGTH)
            {
                return;
            }

            _placed.Add(new ObstaclePlacement { Pos = _pos, Size = _size });
        }
    }
}
