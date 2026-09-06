using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 방 모듈 조립의 순수 규칙(2026-09-07, EditMode TDD). 문 변 판정은
    /// <c>MapBuilder.BuildWallWithGaps</c>·<c>ObstacleFactory.PlanDoorScreens</c>와 **같은 기준**이다
    /// (끝점이 변에 닿는 복도) — 어긋나면 문이 없는 변에 문틀이 선다.
    /// </summary>
    public static class RoomModuleLogic
    {
        public const string FOOTPRINT_BIG = "Big";      // 12×11
        public const string FOOTPRINT_SMALL = "Small";  // 10×9

        public static readonly Vector2 BIG_SIZE = new Vector2(12f, 11f);
        public static readonly Vector2 SMALL_SIZE = new Vector2(10f, 9f);

        /// <summary>변 순서 0=N(+z) 1=S(-z) 2=E(+x) 3=W(-x) — MapBuilder와 같다.</summary>
        public static bool[] DoorSides(RoomRect _room, IReadOnlyList<CorridorSeg> _corridors)
        {
            var sides = new bool[4];
            if (_corridors == null) return sides;

            for (int side = 0; side < 4; side++)
            {
                bool horizontal = side <= 1;
                float fixedCoord = side == 0 ? _room.Max.y : side == 1 ? _room.Min.y : side == 2 ? _room.Max.x : _room.Min.x;
                float min = horizontal ? _room.Min.x : _room.Min.y;
                float max = horizontal ? _room.Max.x : _room.Max.y;

                foreach (CorridorSeg c in _corridors)
                {
                    foreach (Vector2 p in new[] { c.From, c.To })
                    {
                        float perp = horizontal ? p.y : p.x;
                        float along = horizontal ? p.x : p.y;
                        if (Mathf.Abs(perp - fixedCoord) > c.Width * 0.5f + 0.6f) continue;
                        if (along < min - 0.5f || along > max + 0.5f) continue;
                        sides[side] = true;
                    }
                }
            }
            return sides;
        }

        /// <summary>크기 → 발자국 키. 모르는 크기는 빈 문자열(큐브 경로).</summary>
        public static string FootprintKey(Vector2 _size)
        {
            if (Approximately(_size, BIG_SIZE)) return FOOTPRINT_BIG;
            if (Approximately(_size, SMALL_SIZE)) return FOOTPRINT_SMALL;
            return "";
        }

        /// <summary>같은 발자국 후보 중 하나 — 시드·방 번호로 결정적.</summary>
        public static string PickModule(IReadOnlyList<string> _candidates, int _roomIndex, int _seed)
        {
            if (_candidates == null || _candidates.Count == 0) return "";
            int pick = (int)(PantryTextureLogic.Hash(_roomIndex, 23, _seed) * _candidates.Count) % _candidates.Count;
            return _candidates[pick];
        }

        /// <summary>변 번호 → 바깥을 보는 yaw(도). 문틀·막벽을 그 방향으로 세운다.</summary>
        public static float SideYaw(int _side) => _side == 0 ? 0f : _side == 1 ? 180f : _side == 2 ? 90f : 270f;

        private static bool Approximately(Vector2 _a, Vector2 _b) =>
            Mathf.Abs(_a.x - _b.x) < 0.01f && Mathf.Abs(_a.y - _b.y) < 0.01f;
    }
}
