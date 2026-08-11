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
        // 봇 판은 고양이가 더 서므로 넉넉히 잡는다 — 점 하나가 아니라 **선**을 비워야 한다.
        private const float CAT_LINE_LENGTH = 20f;
        private const float CAT_LINE_HALF_WIDTH = 4f;

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

            // 스폰 자리는 따로 — 훨씬 크게 비운다
            var spawns = new List<Vector2>();
            foreach (FortressPlacement f in _layout.Fortresses) spawns.Add(f.Pos);
            foreach (JailPlacement j in _layout.Jails) spawns.Add(j.Pos);

            var placed = new List<ObstaclePlacement>();
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
