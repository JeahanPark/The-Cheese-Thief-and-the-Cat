using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 맵 천장 자리 계산 — 순수 함수(2026-09-14 사용자 요청 "천장이 비어 있다"). 그리는 쪽은 <see cref="MapCeiling"/>.
    /// 계획: docs/superpowers/plans/2026-09-14-map-ceiling.md
    ///
    /// 좌표는 전부 월드 XZ(미터). 시드·위치만 보고 정하므로 모든 클라이언트가 같은 천장을 본다.
    /// 높이 기준: 방 벽 5m 위, 카메라 최고 높이(거리 3m × sin 75° + 몸 ≈ 4m)보다 충분히 위.
    /// </summary>
    public static class MapCeilingLogic
    {
        public const float DECK_Y = 6.0f;           // 천장 판 아랫면
        public const float DECK_THICK = 0.12f;
        public const float CELL = 4f;               // 판 한 칸 — 채광창 하나의 크기

        public const float GIRDER_SPACING = 8f;     // 큰 들보(X 방향으로 뻗음) 간격
        public const float GIRDER_WIDTH = 0.45f;
        public const float GIRDER_DEPTH = 0.55f;

        /// <summary>
        /// 맵 둘레 판벽 — 경계벽 위와 천장 판 사이 틈으로 **밤 하늘이 띠처럼 새어 보였다**(2026-09-14 첫 촬영).
        /// 이 높이부터 판까지 막는다(경계벽은 이보다 높다).
        /// </summary>
        public const float CURTAIN_BOTTOM = 3.5f;
        public const float CURTAIN_THICK = 0.3f;

        public const float JOIST_SPACING = 1.6f;    // 장선(Z 방향으로 뻗음) 간격
        public const float JOIST_WIDTH = 0.18f;
        public const float JOIST_DEPTH = 0.22f;

        /// <summary>
        /// 달빛 자리에서 이 거리 안에 중심이 든 칸은 **닫힌 채광창**(판 + 테 + 푸른 유리)이다 — 달빛 기둥의 출처가 보이게.
        /// 처음엔 칸을 뚫었는데 사용자가 **덮는 게 더 중요하다**고 했다(2026-09-14) — 천장에 구멍은 없다.
        /// </summary>
        public const float SKYLIGHT_RADIUS = 2.9f;

        public const float HANG_STEP = 3f;          // 들보를 따라 매달 후보 간격
        public const int HANG_ONE_IN = 5;           // 후보 다섯 중 하나에 매단다
        public const float HANG_CLEAR = 1.5f;       // 전등·채광창 둘레는 비운다(줄이 엉켜 보이고 빛줄기를 가린다)
        public const float HANG_MIN = 0.3f;
        public const float HANG_MAX = 0.9f;
        /// <summary>매달린 것 한 덩이의 최대 키 — 프리팹이 이보다 길면 카메라(≈4m)에 닿는다(`MapAtmosphereBuilder`가 이 안으로 굽는다).</summary>
        public const float ITEM_MAX_HEIGHT = 0.5f;

        /// <summary>
        /// 들보 Z 좌표들 — 가장자리부터 <see cref="GIRDER_SPACING"/> 간격, 양 끝 경계 포함.
        /// 가장자리 들보가 벽 위 선을 덮어 판 끝이 허공에 떠 보이지 않게 한다.
        /// </summary>
        public static List<float> GirderZs(float _half) => Lines(_half, GIRDER_SPACING);

        /// <summary>장선 X 좌표들 — 같은 규칙, <see cref="JOIST_SPACING"/> 간격.</summary>
        public static List<float> JoistXs(float _half) => Lines(_half, JOIST_SPACING);

        private static List<float> Lines(float _half, float _spacing)
        {
            var list = new List<float>();
            if (_half <= 0f || _spacing <= 0f) return list;
            int count = Mathf.Max(1, Mathf.RoundToInt(_half * 2f / _spacing));
            float step = _half * 2f / count;
            for (int i = 0; i <= count; i++) list.Add(-_half + step * i);
            return list;
        }

        /// <summary>판 칸 중심들(칸 수는 맵 폭을 <see cref="CELL"/>로 나눠 반올림, 최소 1).</summary>
        public static List<Vector2> CellCenters(float _half, out float _cellSize)
        {
            var list = new List<Vector2>();
            int count = Mathf.Max(1, Mathf.RoundToInt(_half * 2f / CELL));
            _cellSize = _half * 2f / count;
            for (int x = 0; x < count; x++)
            {
                for (int z = 0; z < count; z++)
                {
                    list.Add(new Vector2(-_half + _cellSize * (x + 0.5f), -_half + _cellSize * (z + 0.5f)));
                }
            }
            return list;
        }

        /// <summary>이 칸이 채광창 칸인가 — 달빛 자리 하나라도 <see cref="SKYLIGHT_RADIUS"/> 안.</summary>
        public static bool IsSkylight(Vector2 _cellCenter, IReadOnlyList<Vector2> _moons)
        {
            if (_moons == null) return false;
            foreach (Vector2 m in _moons)
            {
                if ((m - _cellCenter).sqrMagnitude <= SKYLIGHT_RADIUS * SKYLIGHT_RADIUS) return true;
            }
            return false;
        }

        public struct HangSlot
        {
            public Vector2 Pos;
            public int Kind;       // 0..kinds-1
            public float Length;   // 들보 아랫면에서 매단 것 꼭대기까지(m)
        }

        /// <summary>
        /// 들보 아래 매달 자리. 가장자리 들보는 빼고(벽에 붙어 반쯤 묻힌다), 후보 중 <see cref="HANG_ONE_IN"/>에 하나.
        /// 전등·달빛 자리에서 <see cref="HANG_CLEAR"/> + 채광창 반경 안은 비운다.
        /// </summary>
        public static List<HangSlot> HangSlots(float _half, int _seed, int _kinds,
            IReadOnlyList<Vector2> _lamps, IReadOnlyList<Vector2> _moons)
        {
            var slots = new List<HangSlot>();
            if (_kinds <= 0) return slots;

            List<float> girders = GirderZs(_half);
            for (int g = 1; g < girders.Count - 1; g++)
            {
                float z = girders[g];
                int steps = Mathf.FloorToInt((_half * 2f - HANG_STEP) / HANG_STEP);
                for (int s = 0; s <= steps; s++)
                {
                    float x = -_half + HANG_STEP * (s + 0.5f);
                    var pos = new Vector2(x, z);
                    uint h = Hash(Mathf.RoundToInt(x * 10f), Mathf.RoundToInt(z * 10f), _seed);
                    if (h % HANG_ONE_IN != 0) continue;
                    if (Near(pos, _lamps, HANG_CLEAR) || Near(pos, _moons, HANG_CLEAR + SKYLIGHT_RADIUS)) continue;

                    slots.Add(new HangSlot
                    {
                        Pos = pos,
                        Kind = (int)((h >> 8) % (uint)_kinds),
                        Length = Mathf.Lerp(HANG_MIN, HANG_MAX, ((h >> 16) & 0xff) / 255f),
                    });
                }
            }
            return slots;
        }

        private static bool Near(Vector2 _p, IReadOnlyList<Vector2> _points, float _radius)
        {
            if (_points == null) return false;
            foreach (Vector2 q in _points)
            {
                if ((q - _p).sqrMagnitude <= _radius * _radius) return true;
            }
            return false;
        }

        /// <summary>작은 정수 해시 — 위치·시드가 같으면 어느 기계에서나 같은 값.</summary>
        public static uint Hash(int _x, int _z, int _seed)
        {
            unchecked
            {
                uint h = (uint)_x * 73856093u ^ (uint)_z * 19349663u ^ (uint)_seed * 83492791u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return h;
            }
        }
    }
}
