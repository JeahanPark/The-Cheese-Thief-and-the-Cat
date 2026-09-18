using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// 지형을 격자로 굽는다(2026-08-18) — `MapBuilder`가 맵을 다 지은 **같은 프레임**에 부른다.
    ///
    /// 무엇이 막는가는 맵 생성기가 실제로 세운 콜라이더에서 읽는다. 레이아웃 데이터(방·복도 사각형)를
    /// 다시 해석하지 않는 이유: **문은 데이터에 없다.** 벽 조각이 '안 생긴 자리'가 곧 문이라,
    /// 좌표만으로 알려면 벽 생성 함수를 통째로 재구현해야 한다. 콜라이더를 찍는 편이 정확하고 짧다.
    ///
    /// 걷는 면은 전역 `Floor` 하나가 정사각 전체를 덮는다 — 즉 "정사각에서 막는 박스를 뺀다"가 전부다.
    /// 분류 규칙은 `MapBuilder.CollectSources`와 **같은 이름 접두사**를 쓴다. 갈라지면 폴리곤과 격자가
    /// 다른 맵을 보게 되므로 여기 한 곳에 모아 두고 양쪽이 부른다.
    ///
    /// 회전(yaw)이 걸린 박스가 있다(대각 복도와 그 측벽). 그래서 셀 판정은 월드 AABB가 아니라
    /// **OBB 대 셀 사각형**으로 한다 — AABB로 하면 45° 복도가 실제보다 훨씬 두껍게 막힌다.
    /// </summary>
    public static class NavGridBaker
    {
        /// <summary>이 이름으로 시작하면 못 가는 것(= `CollectSources`의 area 1).</summary>
        public static bool IsBlockingName(string _name) =>
            _name.StartsWith("Wall") || _name.StartsWith("Boundary")
            || _name.StartsWith("Obstacle") || _name.StartsWith("Corridor");

        /// <summary>이 이름으로 시작하면 지형이 아니다(덧바닥 장식).</summary>
        public static bool IsDecorFloorName(string _name) =>
            _name.StartsWith("Floor_") || _name.StartsWith("CorridorFloor");

        /// <summary>
        /// `_root` 밑의 박스 콜라이더로 격자를 굽는다. `_halfExtent`는 맵 반치수(레이아웃 값).
        /// </summary>
        public static NavGrid Bake(Transform _root, float _halfExtent)
        {
            NavGrid grid = StampTerrain(_root, _halfExtent);

            // 경계벽 바깥 여백처럼 못 닿는 곳을 먼저 봉인하고 나서 여유 거리를 잰다
            grid.SealUnreachable();
            grid.BuildClearance();
            return grid;
        }

        /// <summary>
        /// 봉인·여유 거리 **전** 단계 — 막는 박스만 찍은 격자. `Bake`가 부르고,
        /// 테스트(감사 D-12)가 「봉인이 한 존을 통째로 지우는가」를 보려고 따로 부른다.
        /// </summary>
        public static NavGrid StampTerrain(Transform _root, float _halfExtent)
        {
            float cell = NavGrid.CELL_SIZE;
            int side = Mathf.CeilToInt(_halfExtent * 2f / cell) + 2;   // 경계벽까지 담기게 한 칸 여유
            var origin = new Vector2(-_halfExtent - cell, -_halfExtent - cell);
            var grid = new NavGrid(origin, side, side);

            if (_root != null)
            {
                foreach (BoxCollider box in _root.GetComponentsInChildren<BoxCollider>(true))
                {
                    if (box.isTrigger) continue;
                    string name = box.gameObject.name;
                    if (IsDecorFloorName(name)) continue;
                    if (!IsBlockingName(name)) continue;

                    StampBox(grid, box, NavStructureKind.CatBreakable, _terrain: true);
                }
            }
            return grid;
        }

        /// <summary>
        /// 박스 콜라이더가 덮는 셀을 찍는다. `_terrain`이면 지형(영구), 아니면 구조물(되돌릴 수 있음).
        /// 구조물일 때는 찍은 자리를 `_stamp`가 기억한다.
        /// </summary>
        public static void StampBox(NavGrid _grid, BoxCollider _box, NavStructureKind _kind,
            bool _terrain, NavGridStamp _stamp = null)
        {
            if (_grid == null || _box == null) return;

            Transform tr = _box.transform;

            // **크기는 lossyScale에 있다.** 프리미티브 Cube의 콜라이더 size는 늘 (1,1,1)이고
            // 실제 치수는 스케일로 들어간다 — box.size만 믿으면 모든 벽이 1m 큐브가 된다.
            Vector3 scale = tr.lossyScale;
            Vector3 center = tr.TransformPoint(_box.center);
            float halfX = Mathf.Abs(_box.size.x * scale.x) * 0.5f;
            float halfZ = Mathf.Abs(_box.size.z * scale.z) * 0.5f;

            // y축 회전만 쓴다(맵도 구조물도 눕지 않는다)
            float yaw = tr.rotation.eulerAngles.y * Mathf.Deg2Rad;
            float cos = Mathf.Cos(yaw), sin = Mathf.Sin(yaw);

            // 회전한 사각형을 담는 월드 AABB로 후보 셀을 좁힌 뒤, 셀마다 OBB 겹침을 정확히 본다
            float spanX = Mathf.Abs(cos) * halfX + Mathf.Abs(sin) * halfZ;
            float spanZ = Mathf.Abs(sin) * halfX + Mathf.Abs(cos) * halfZ;

            _grid.ToCell(new Vector3(center.x - spanX, 0f, center.z - spanZ), out int minX, out int minZ);
            _grid.ToCell(new Vector3(center.x + spanX, 0f, center.z + spanZ), out int maxX, out int maxZ);

            float half = NavGrid.CELL_SIZE * 0.5f;
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 c = _grid.ToWorld(x, z);
                    if (!BoxOverlapsCell(center, halfX, halfZ, cos, sin, c.x, c.z, half)) continue;

                    if (_terrain) _grid.SetBlocked(x, z, true);
                    else if (_stamp != null) _stamp.Add(x, z, _kind);
                    else _grid.AddStructureCell(x, z, _kind);
                }
            }
        }

        /// <summary>
        /// 회전한 사각형(OBB)과 축 정렬 셀이 겹치나 — 분리축 정리(SAT)를 2D 두 축으로.
        /// 셀을 OBB 로컬로 돌려 놓고 "셀의 반치수를 OBB 축에 투영한 만큼" 부풀린 사각형에 드는지 본다.
        /// </summary>
        private static bool BoxOverlapsCell(Vector3 _center, float _halfX, float _halfZ,
            float _cos, float _sin, float _cellX, float _cellZ, float _cellHalf)
        {
            float dx = _cellX - _center.x;
            float dz = _cellZ - _center.z;

            // OBB 로컬 좌표(월드 → 로컬은 -yaw 회전)
            float lx = dx * _cos - dz * _sin;
            float lz = dx * _sin + dz * _cos;

            // 셀(축 정렬 정사각형)을 OBB 축에 투영한 반치수
            float projX = _cellHalf * (Mathf.Abs(_cos) + Mathf.Abs(_sin));
            float projZ = projX;

            return Mathf.Abs(lx) <= _halfX + projX && Mathf.Abs(lz) <= _halfZ + projZ;
        }
    }
}
