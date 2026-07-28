using System.Collections.Generic;
using CTC.Runtime.Economy;
using TMPro;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// MapLayout 하나를 읽어 바닥·벽·통로·치즈 지점을 만든다(v3.5에서 에디터 굽기에서 이관).
    ///
    /// 네트워크와 무관하다 — 모든 클라이언트가 같은 에셋으로 같은 결과를 만들기 때문에
    /// 지오메트리를 복제할 이유가 없다. 로비에서 맵을 바꾸면 각자 다시 짓는다.
    /// 치즈 지점의 잔량·점유만 CheeseSpotDirector가 인덱스로 복제한다.
    /// </summary>
    public class MapBuilder : MonoBehaviour
    {
        private const float ROOM_WALL_HEIGHT = 3f;
        private const float ROOM_WALL_THICK = 0.5f;
        private const float CORRIDOR_WALL_HEIGHT = 1.6f;
        private const float BOUNDARY_THICK = 0.5f;
        private const float FLOOR_THICK = 0.5f;

        [SerializeField] private Material m_WallMaterial;
        [SerializeField] private Material m_FloorMaterial;
        [SerializeField] private Material m_GroundMaterial;
        [SerializeField] private Material m_DiscHighMaterial;   // 등급 6~7
        [SerializeField] private Material m_DiscMidMaterial;    // 등급 4~5
        [SerializeField] private Material m_DiscLowMaterial;    // 등급 1~3

        private GameObject m_Root;
        private readonly List<CheeseSpot> m_Spots = new List<CheeseSpot>();

        /// <summary>마지막으로 지은 맵의 치즈 지점 — 레이아웃 순서와 같다(복제 인덱스의 근거).</summary>
        public IReadOnlyList<CheeseSpot> Spots => m_Spots;

        public MapLayout CurrentLayout { get; private set; }

        /// <summary>맵을 새로 짓는다. 기존 맵은 파괴 — 같은 레이아웃으로 여러 번 불러도 안전.</summary>
        public void Build(MapLayout _layout)
        {
            Clear();
            if (_layout == null) return;

            CurrentLayout = _layout;
            m_Root = new GameObject("GeneratedMap");
            m_Root.transform.SetParent(transform, false);

            BuildGround(_layout.HalfExtent);
            BuildFromLayout(_layout);
            BuildCheeseSpots(_layout);
        }

        public void Clear()
        {
            m_Spots.Clear();
            CurrentLayout = null;
            if (m_Root == null) return;

            if (Application.isPlaying) Destroy(m_Root);
            else DestroyImmediate(m_Root);
            m_Root = null;
        }

        // 전역 바닥 + 경계 외벽. 바닥은 두께 있는 박스 — 단면 Plane은 밑으로 한 번 빠지면 영구 낙하다.
        // 카메라가 바닥과 충돌하지 않게 Ignore Raycast(2) 레이어로 둔다.
        private void BuildGround(float _halfExtent)
        {
            float span = _halfExtent * 2f;

            GameObject floor = AddBox(m_Root, m_GroundMaterial, "Floor",
                new Vector3(0f, -FLOOR_THICK * 0.5f, 0f), new Vector3(span, FLOOR_THICK, span));
            floor.layer = 2;

            // 지오메트리 틈으로 새어나가도 바닥 밖으로 못 나가게
            float h = ROOM_WALL_HEIGHT;
            float outer = span + BOUNDARY_THICK;
            AddBox(m_Root, m_WallMaterial, "Boundary_N", new Vector3(0f, h * 0.5f, _halfExtent),
                new Vector3(outer, h, BOUNDARY_THICK));
            AddBox(m_Root, m_WallMaterial, "Boundary_S", new Vector3(0f, h * 0.5f, -_halfExtent),
                new Vector3(outer, h, BOUNDARY_THICK));
            AddBox(m_Root, m_WallMaterial, "Boundary_E", new Vector3(_halfExtent, h * 0.5f, 0f),
                new Vector3(BOUNDARY_THICK, h, outer));
            AddBox(m_Root, m_WallMaterial, "Boundary_W", new Vector3(-_halfExtent, h * 0.5f, 0f),
                new Vector3(BOUNDARY_THICK, h, outer));
        }

        private void BuildFromLayout(MapLayout _layout)
        {
            // 방: 바닥 + 통로 접점만 개방한 4면 벽
            foreach (RoomRect r in _layout.Rooms)
                BuildRoomWalls(r, _layout.Corridors);

            // 통로(미로 포함): 중심선 세그먼트마다 폭만큼 바닥 + 양측 낮은 벽(접합부 개방)
            foreach (CorridorSeg c in _layout.Corridors)
                BuildCorridor(c, _layout.Corridors);

            // 막다른 통로 끝(방·타 세그먼트 미접촉)은 끝막이 벽 — 맵 외곽 공터로 새는 틈 봉쇄
            foreach (CorridorSeg c in _layout.Corridors)
            {
                AddEndCapIfDeadEnd(c, c.From, _layout.Rooms, _layout.Corridors);
                AddEndCapIfDeadEnd(c, c.To, _layout.Rooms, _layout.Corridors);
            }
        }

        private void BuildRoomWalls(RoomRect r, IReadOnlyList<CorridorSeg> corridors)
        {
            // 바닥류는 Ignore Raycast — 카메라가 바닥과 충돌하지 않게 (전역 Floor와 동일)
            AddBox(m_Root, m_FloorMaterial, $"Floor_{r.Id}",
                new Vector3(r.Center.x, 0.02f, r.Center.y), new Vector3(r.Size.x, 0.04f, r.Size.y)).layer = 2;

            for (int side = 0; side < 4; side++)
                BuildWallWithGaps(r, corridors, side);
        }

        // 방 한 변을 통로 접점(개구부)만 남기고 벽 세그먼트로 깐다.
        // side: 0=N(+z) 1=S(-z) 2=E(+x) 3=W(-x)
        private void BuildWallWithGaps(RoomRect r, IReadOnlyList<CorridorSeg> corridors, int side)
        {
            bool horizontal = side <= 1; // N/S 변은 x축 방향으로 뻗음
            float fixedCoord = side == 0 ? r.Max.y : side == 1 ? r.Min.y : side == 2 ? r.Max.x : r.Min.x;
            float min = horizontal ? r.Min.x : r.Min.y;
            float max = horizontal ? r.Max.x : r.Max.y;

            // 이 변에 닿는 통로 끝점 → 개구부(변 축 좌표, 반폭)
            var gaps = new List<(float center, float half)>();
            foreach (CorridorSeg c in corridors)
            {
                foreach (Vector2 p in new[] { c.From, c.To })
                {
                    float perp = horizontal ? p.y : p.x;
                    float along = horizontal ? p.x : p.y;
                    if (Mathf.Abs(perp - fixedCoord) > c.Width * 0.5f + 0.6f) continue;
                    if (along < min - 0.5f || along > max + 0.5f) continue;
                    gaps.Add((along, c.Width * 0.5f + 0.25f));
                }
            }
            gaps.Sort((a, b) => a.center.CompareTo(b.center));

            string sideName = side == 0 ? "N" : side == 1 ? "S" : side == 2 ? "E" : "W";
            float cursor = min;
            int seg = 0;
            for (int i = 0; i <= gaps.Count; i++)
            {
                float segEnd = i < gaps.Count ? gaps[i].center - gaps[i].half : max;
                float length = segEnd - cursor;
                if (length > 0.05f)
                {
                    float center = (cursor + segEnd) * 0.5f;
                    AddBox(m_Root, m_WallMaterial, $"Wall_{r.Id}_{sideName}{seg++}",
                        horizontal ? new Vector3(center, ROOM_WALL_HEIGHT * 0.5f, fixedCoord)
                                   : new Vector3(fixedCoord, ROOM_WALL_HEIGHT * 0.5f, center),
                        horizontal ? new Vector3(length, ROOM_WALL_HEIGHT, ROOM_WALL_THICK)
                                   : new Vector3(ROOM_WALL_THICK, ROOM_WALL_HEIGHT, length));
                }
                if (i < gaps.Count) cursor = Mathf.Max(cursor, gaps[i].center + gaps[i].half);
            }
        }

        // 통로 세그먼트: 바닥 + 양측 낮은 벽(고양이도 통과 — 폭이 이동을 제약, 벽은 이탈 차단).
        // 대각 세그먼트(미로·사선 통로) 지원을 위해 회전 박스로 깐다.
        private void BuildCorridor(CorridorSeg c, IReadOnlyList<CorridorSeg> all)
        {
            Vector3 a = new Vector3(c.From.x, 0f, c.From.y);
            Vector3 b = new Vector3(c.To.x, 0f, c.To.y);
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.01f) return;

            Vector3 mid = (a + b) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(dir / len);
            AddOrientedBox(m_Root, m_FloorMaterial, "CorridorFloor",
                mid + Vector3.up * 0.02f, rot, new Vector3(c.Width, 0.04f, len)).layer = 2;

            BuildCorridorSideWall(c, all, +1f);
            BuildCorridorSideWall(c, all, -1f);
        }

        // 통로 한쪽 측벽: 다른 세그먼트 중심선이 이 벽 라인을 실제로 가로지르는 구간만
        // 개구부로 비우고 나머지를 세그먼트로 깐다. 교차는 2D(x,z) 선분-선분으로 판정 —
        // 분기가 뻗는 쪽 벽만 열리고(u∈[0,1] 조건) 반대쪽·바깥 코너는 닫힌 채 유지된다.
        private void BuildCorridorSideWall(CorridorSeg c, IReadOnlyList<CorridorSeg> all, float sideSign)
        {
            Vector2 a = c.From;
            Vector2 d = c.To - c.From;
            float len = d.magnitude;
            if (len < 0.01f) return;
            Vector2 fwd = d / len;
            Vector2 right = new Vector2(fwd.y, -fwd.x); // (x,z) 평면에서 진행방향의 오른쪽
            Vector2 wallStart = a + right * (sideSign * (c.Width * 0.5f + ROOM_WALL_THICK * 0.5f));

            var gaps = new List<(float center, float half)>();
            foreach (CorridorSeg o in all)
            {
                if (o.From == c.From && o.To == c.To) continue; // 자기 자신
                Vector2 od = o.To - o.From;
                float denom = od.x * fwd.y - od.y * fwd.x;     // cross(od, fwd)
                if (Mathf.Abs(denom) < 0.15f) continue;        // 평행/준평행 — 접합 아님

                Vector2 rel = wallStart - o.From;
                float u = (rel.x * fwd.y - rel.y * fwd.x) / denom;
                if (u < -0.05f || u > 1.05f) continue;         // 상대 선분 범위 밖

                Vector2 hit = o.From + od * u;
                float s = Vector2.Dot(hit - wallStart, fwd);
                if (s < -0.4f || s > len + 0.4f) continue;     // 이 벽 범위 밖

                float sin = Mathf.Abs(denom) / od.magnitude;   // 교차각 보정(사선 분기는 개구부 확대)
                gaps.Add((s, o.Width * 0.5f / Mathf.Max(sin, 0.4f) + 0.25f));
            }
            gaps.Sort((x, y) => x.center.CompareTo(y.center));

            Quaternion rot = Quaternion.LookRotation(new Vector3(fwd.x, 0f, fwd.y));
            string sideName = sideSign > 0 ? "R" : "L";
            float cursor = -ROOM_WALL_THICK * 0.5f;
            float end = len + ROOM_WALL_THICK * 0.5f;
            int seg = 0;
            for (int i = 0; i <= gaps.Count; i++)
            {
                float segEnd = i < gaps.Count ? gaps[i].center - gaps[i].half : end;
                float segLen = segEnd - cursor;
                if (segLen > 0.05f)
                {
                    Vector2 center2 = wallStart + fwd * ((cursor + segEnd) * 0.5f);
                    AddOrientedBox(m_Root, m_WallMaterial, $"CorridorWall_{sideName}{seg++}",
                        new Vector3(center2.x, CORRIDOR_WALL_HEIGHT * 0.5f, center2.y), rot,
                        new Vector3(ROOM_WALL_THICK, CORRIDOR_WALL_HEIGHT, segLen));
                }
                if (i < gaps.Count) cursor = Mathf.Max(cursor, gaps[i].center + gaps[i].half);
            }
        }

        // 통로 끝점이 방 경계·다른 세그먼트에 닿지 않으면(진짜 막다른 끝) 폭+여유만큼 캡 벽을 세운다.
        private void AddEndCapIfDeadEnd(
            CorridorSeg c, Vector2 end, IReadOnlyList<RoomRect> rooms, IReadOnlyList<CorridorSeg> all)
        {
            const float TOUCH_TOLERANCE = 0.3f;

            foreach (RoomRect r in rooms)
            {
                if (end.x >= r.Min.x - TOUCH_TOLERANCE && end.x <= r.Max.x + TOUCH_TOLERANCE &&
                    end.y >= r.Min.y - TOUCH_TOLERANCE && end.y <= r.Max.y + TOUCH_TOLERANCE) return;
            }
            foreach (CorridorSeg o in all)
            {
                if (o.From == c.From && o.To == c.To) continue;
                if (DistancePointToSegment(end, o.From, o.To) < TOUCH_TOLERANCE) return;
            }

            Vector2 dir = (c.To - c.From).normalized;
            Quaternion rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
            AddOrientedBox(m_Root, m_WallMaterial, "CorridorEndCap",
                new Vector3(end.x, CORRIDOR_WALL_HEIGHT * 0.5f, end.y), rot,
                new Vector3(c.Width + ROOM_WALL_THICK * 2f, CORRIDOR_WALL_HEIGHT, ROOM_WALL_THICK));
        }

        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }

        // 치즈 지점 — 레이아웃 순서를 그대로 유지한다. CheeseSpotDirector가 이 순서를
        // 복제 인덱스로 쓰므로 순서가 어긋나면 다른 지점에 상태가 붙는다.
        private void BuildCheeseSpots(MapLayout _layout)
        {
            var root = new GameObject("CheeseSpots");
            root.transform.SetParent(m_Root.transform, false);

            int reserve = CheeseSpotLogic.ReservePerSpot(_layout.Cheese.Count);
            foreach (CheesePlacement cp in _layout.Cheese)
            {
                var spotGo = new GameObject($"CheeseSpot_G{cp.Grade}");
                spotGo.transform.SetParent(root.transform, false);
                spotGo.transform.localPosition = new Vector3(cp.Pos.x, 0f, cp.Pos.y);

                var spot = spotGo.AddComponent<CheeseSpot>();
                GameObject visual = AddCheeseVisual(spotGo, cp.Grade);
                spot.Configure(cp.Grade, cp.Radius, reserve, visual,
                    visual.GetComponentInChildren<TextMeshPro>(),
                    visual.transform.Find("Disc").GetComponent<Renderer>());
                m_Spots.Add(spot);
            }
        }

        // 디스크·라벨을 한 루트 밑에 묶는다 — 고갈 시 이 루트만 끄면 지점이 사라진 것으로 보인다.
        private GameObject AddCheeseVisual(GameObject spotGo, int grade)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(spotGo.transform, false);

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            Destroy(disc.GetComponent<CapsuleCollider>()); // 이동 방해·오버랩 오염 방지
            disc.transform.SetParent(visual.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            disc.transform.localScale = new Vector3(3f, 0.05f, 3f);
            disc.GetComponent<Renderer>().sharedMaterial = GradeDiscMaterial(grade);

            var labelGo = new GameObject("GradeLabel");
            labelGo.transform.SetParent(visual.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            // 폰트는 TMP 기본(한글 Pretendard로 교체돼 있음) — "점유 중" 표기가 그대로 나온다
            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = grade.ToString(); // 런타임엔 CheeseSpot이 "등급 · 잔량"으로 덮어쓴다
            tmp.fontSize = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.yellow;
            labelGo.AddComponent<Cameras.FaceCamera>(); // 어느 각도에서도 읽히게

            return visual;
        }

        // 등급별 디스크 색 — 위험도 가독성 (1~3 노랑 / 4~5 주황 / 6~7 빨강)
        private Material GradeDiscMaterial(int grade)
        {
            return grade >= 6 ? m_DiscHighMaterial : grade >= 4 ? m_DiscMidMaterial : m_DiscLowMaterial;
        }

        private static GameObject AddBox(
            GameObject parent, Material material, string name, Vector3 center, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent.transform, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        // 대각 통로용 회전 박스 (AddBox + 회전)
        private static GameObject AddOrientedBox(
            GameObject parent, Material material, string name, Vector3 center, Quaternion rotation, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent.transform, false);
            box.transform.localPosition = center;
            box.transform.localRotation = rotation;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }
    }
}
