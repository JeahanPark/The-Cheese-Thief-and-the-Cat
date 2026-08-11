using System.Collections.Generic;
using CTC.Runtime.Economy;
using TMPro;
using Unity.AI.Navigation;
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
        // 엄폐물 높이 — 눈높이 레이(1.0m)보다 확실히 높고, 방 벽(3m)보다는 낮아 답답하지 않다
        private const float OBSTACLE_HEIGHT = 2.2f;

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
            BuildJails(_layout);
            BuildObstacles(_layout);
            BakeNavMesh();
        }

        /// <summary>
        /// 지형을 다 지은 뒤 **걸을 수 있는 바닥을 폴리곤으로 굽는다** (2026-08-12).
        ///
        /// 그전에는 방 중심·모서리·문에 점을 찍어 이은 웨이포인트 그래프였다. 점만 아니까
        /// 목표가 점이 아닐 때(움직이는 쥐·소리가 난 자리) **마지막 한 구간을 검사 없이 직선**
        /// 으로 때울 수밖에 없었고, 그 직선에 기둥이나 쥐가 지은 창고가 있으면 답이 없었다
        /// (실측: 고양이 둘이 창고 앞에서 8초 동안 1mm도 못 갔다).
        ///
        /// 폴리곤은 바닥 **전체**를 덮으므로 그런 구멍이 없다. 판 중에 생기는 구조물은
        /// `NavMeshObstacle`이 구멍을 파므로 다시 구울 필요도 없다.
        ///
        /// **`NavMeshAgent`는 쓰지 않는다.** 그건 오브젝트를 직접 옮겨 이속·잠행·발소리가
        /// 사람과 다른 코드를 타게 만든다 — 그 순간 봇으로 잰 밸런스가 뜻을 잃는다.
        /// 길 계산만 쓰고 이동은 사람과 같은 입구로 넣는다.
        /// </summary>
        private void BakeNavMesh()
        {
            var surface = GetComponent<NavMeshSurface>();
            if (surface == null) surface = gameObject.AddComponent<NavMeshSurface>();

            // 이 오브젝트 아래(= 방금 지은 GeneratedMap)만 본다. 플레이어·강아지는 안 굽는다.
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
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

        private void BuildRoomWalls(RoomRect _r, IReadOnlyList<CorridorSeg> _corridors)
        {
            // 바닥류는 Ignore Raycast — 카메라가 바닥과 충돌하지 않게 (전역 Floor와 동일)
            AddBox(m_Root, m_FloorMaterial, $"Floor_{_r.Id}",
                new Vector3(_r.Center.x, 0.02f, _r.Center.y), new Vector3(_r.Size.x, 0.04f, _r.Size.y)).layer = 2;

            for (int side = 0; side < 4; side++)
                BuildWallWithGaps(_r, _corridors, side);
        }

        // 방 한 변을 통로 접점(개구부)만 남기고 벽 세그먼트로 깐다.
        // side: 0=N(+z) 1=S(-z) 2=E(+x) 3=W(-x)
        private void BuildWallWithGaps(RoomRect _r, IReadOnlyList<CorridorSeg> _corridors, int _side)
        {
            bool horizontal = _side <= 1; // N/S 변은 x축 방향으로 뻗음
            float fixedCoord = _side == 0 ? _r.Max.y : _side == 1 ? _r.Min.y : _side == 2 ? _r.Max.x : _r.Min.x;
            float min = horizontal ? _r.Min.x : _r.Min.y;
            float max = horizontal ? _r.Max.x : _r.Max.y;

            // 이 변에 닿는 통로 끝점 → 개구부(변 축 좌표, 반폭)
            var gaps = new List<(float center, float half)>();
            foreach (CorridorSeg c in _corridors)
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

            string sideName = _side == 0 ? "N" : _side == 1 ? "S" : _side == 2 ? "E" : "W";
            float cursor = min;
            int seg = 0;
            for (int i = 0; i <= gaps.Count; i++)
            {
                float segEnd = i < gaps.Count ? gaps[i].center - gaps[i].half : max;
                float length = segEnd - cursor;
                if (length > 0.05f)
                {
                    float center = (cursor + segEnd) * 0.5f;
                    AddBox(m_Root, m_WallMaterial, $"Wall_{_r.Id}_{sideName}{seg++}",
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
        private void BuildCorridor(CorridorSeg _c, IReadOnlyList<CorridorSeg> _all)
        {
            Vector3 a = new Vector3(_c.From.x, 0f, _c.From.y);
            Vector3 b = new Vector3(_c.To.x, 0f, _c.To.y);
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.01f) return;

            Vector3 mid = (a + b) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(dir / len);
            AddOrientedBox(m_Root, m_FloorMaterial, "CorridorFloor",
                mid + Vector3.up * 0.02f, rot, new Vector3(_c.Width, 0.04f, len)).layer = 2;

            BuildCorridorSideWall(_c, _all, +1f);
            BuildCorridorSideWall(_c, _all, -1f);
        }

        // 통로 한쪽 측벽: 다른 세그먼트 중심선이 이 벽 라인을 실제로 가로지르는 구간만
        // 개구부로 비우고 나머지를 세그먼트로 깐다. 교차는 2D(x,z) 선분-선분으로 판정 —
        // 분기가 뻗는 쪽 벽만 열리고(u∈[0,1] 조건) 반대쪽·바깥 코너는 닫힌 채 유지된다.
        private void BuildCorridorSideWall(CorridorSeg _c, IReadOnlyList<CorridorSeg> _all, float _sideSign)
        {
            Vector2 a = _c.From;
            Vector2 d = _c.To - _c.From;
            float len = d.magnitude;
            if (len < 0.01f) return;
            Vector2 fwd = d / len;
            Vector2 right = new Vector2(fwd.y, -fwd.x); // (x,z) 평면에서 진행방향의 오른쪽
            Vector2 wallStart = a + right * (_sideSign * (_c.Width * 0.5f + ROOM_WALL_THICK * 0.5f));

            var gaps = new List<(float center, float half)>();
            foreach (CorridorSeg o in _all)
            {
                if (o.From == _c.From && o.To == _c.To) continue; // 자기 자신
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
            string sideName = _sideSign > 0 ? "R" : "L";
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
            CorridorSeg _c, Vector2 _end, IReadOnlyList<RoomRect> _rooms, IReadOnlyList<CorridorSeg> _all)
        {
            const float TOUCH_TOLERANCE = 0.3f;

            foreach (RoomRect r in _rooms)
            {
                if (_end.x >= r.Min.x - TOUCH_TOLERANCE && _end.x <= r.Max.x + TOUCH_TOLERANCE &&
                    _end.y >= r.Min.y - TOUCH_TOLERANCE && _end.y <= r.Max.y + TOUCH_TOLERANCE) return;
            }
            foreach (CorridorSeg o in _all)
            {
                if (o.From == _c.From && o.To == _c.To) continue;
                if (DistancePointToSegment(_end, o.From, o.To) < TOUCH_TOLERANCE) return;
            }

            Vector2 dir = (_c.To - _c.From).normalized;
            Quaternion rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
            AddOrientedBox(m_Root, m_WallMaterial, "CorridorEndCap",
                new Vector3(_end.x, CORRIDOR_WALL_HEIGHT * 0.5f, _end.y), rot,
                new Vector3(_c.Width + ROOM_WALL_THICK * 2f, CORRIDOR_WALL_HEIGHT, ROOM_WALL_THICK));
        }

        private static float DistancePointToSegment(Vector2 _p, Vector2 _a, Vector2 _b)
        {
            Vector2 ab = _b - _a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return Vector2.Distance(_p, _a);
            float t = Mathf.Clamp01(Vector2.Dot(_p - _a, ab) / len2);
            return Vector2.Distance(_p, _a + ab * t);
        }

        /// <summary>감옥 자리(월드 좌표) — `JailDirector`가 판정에 쓴다. 순서 = 레이아웃 순서.</summary>
        public IReadOnlyList<Vector3> JailPositions => m_JailPositions;

        private readonly List<Vector3> m_JailPositions = new List<Vector3>();

        // 감옥은 맞지도 부서지지도 않는 붙박이라 NetworkObject가 아니다 — 전 클라가 각자 짓는다.
        // 복제할 상태는 "누가 갇혔나"뿐이고 그건 JailedTag가 플레이어에 들고 있다.
        private void BuildJails(MapLayout _layout)
        {
            m_JailPositions.Clear();
            if (_layout.Jails.Count == 0) return;

            var root = new GameObject("Jails");
            root.transform.SetParent(m_Root.transform, false);

            foreach (JailPlacement jp in _layout.Jails)
            {
                var pos = new Vector3(jp.Pos.x, 0f, jp.Pos.y);
                m_JailPositions.Add(pos);

                var cage = new GameObject("Jail");
                cage.transform.SetParent(root.transform, false);
                cage.transform.localPosition = pos;

                // 창살 느낌의 기둥 넷 — 벽이 아니라 표식이라 콜라이더를 지운다.
                // 막아 놓으면 구조하러 온 쥐가 못 들어가고, 갇힌 쥐는 어차피 입력이 막혀 있다.
                for (int i = 0; i < 4; i++)
                {
                    float a = Mathf.PI * 0.5f * i + Mathf.PI * 0.25f;
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bar.name = $"Bar{i}";
                    bar.transform.SetParent(cage.transform, false);
                    bar.transform.localPosition = new Vector3(Mathf.Cos(a) * 1.2f, 0.9f, Mathf.Sin(a) * 1.2f);
                    bar.transform.localScale = new Vector3(0.15f, 1.8f, 0.15f);
                    if (m_WallMaterial != null) bar.GetComponent<Renderer>().sharedMaterial = m_WallMaterial;
                    Object.Destroy(bar.GetComponent<Collider>());
                }
            }
        }

        /// <summary>
        /// 방 안 엄폐물 (2026-08-11). 격자 맵의 관통 시야를 끊는다 —
        /// 이게 없으면 한 축에 서는 순간 그 줄 전체가 사냥터라 소리가 아무 일도 하지 않는다.
        ///
        /// 벽과 같은 재질·같은 콜라이더다. 시야 판정(`SightLine`)은 눈높이 1.0m 레이 하나라
        /// 이 높이(2.2m)면 확실히 끊긴다.
        /// </summary>
        private void BuildObstacles(MapLayout _layout)
        {
            var root = new GameObject("Obstacles");
            root.transform.SetParent(m_Root.transform, false);

            foreach (ObstaclePlacement o in _layout.Obstacles)
            {
                AddBox(root, m_WallMaterial, "Obstacle",
                    new Vector3(o.Pos.x, OBSTACLE_HEIGHT * 0.5f, o.Pos.y),
                    new Vector3(o.Size.x, OBSTACLE_HEIGHT, o.Size.y));
            }
        }

        // 치즈 지점 — 레이아웃 순서를 그대로 유지한다. CheeseSpotDirector가 이 순서를
        // 복제 인덱스로 쓰므로 순서가 어긋나면 다른 지점에 상태가 붙는다.
        private void BuildCheeseSpots(MapLayout _layout)
        {
            var root = new GameObject("CheeseSpots");
            root.transform.SetParent(m_Root.transform, false);

            // 벽감이 총량에서 먼저 떼간다 — 더하면 고갈이 늦어져 '성장 기한'이 무너진다(v3.8)
            // **치즈 벽감만** 센다. 강아지 벽감 몫까지 떼면 그 치즈는 아무도 못 받고 총량이 샌다.
            // 상자(v3.9)도 총량에서 뗀다 — 안 떼면 총량이 늘어 고갈이 늦어진다
            int reserved = AlcoveLogic.TotalCheese(_layout.Alcoves)
                           + LootBoxLogic.TotalCheese(_layout.Boxes);
            // 맵을 짓는 시점엔 역할이 아직 바뀔 수 있다 — 권장 인원으로 깔아 두고,
            // 매치가 시작될 때 CheeseSpotDirector.ResetAll()이 실제 쥐 수로 덮어쓴다.
            int reserve = CheeseSpotLogic.ReservePerSpot(
                _layout.Cheese.Count, reserved,
                CheeseSpotLogic.EffectiveMiceCount(_layout.RecommendedMiceMax, _layout.RecommendedMiceMin));
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
        private GameObject AddCheeseVisual(GameObject _spotGo, int _grade)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(_spotGo.transform, false);

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            Destroy(disc.GetComponent<CapsuleCollider>()); // 이동 방해·오버랩 오염 방지
            disc.transform.SetParent(visual.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            disc.transform.localScale = new Vector3(3f, 0.05f, 3f);
            disc.GetComponent<Renderer>().sharedMaterial = GradeDiscMaterial(_grade);

            var labelGo = new GameObject("GradeLabel");
            labelGo.transform.SetParent(visual.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            // 폰트는 TMP 기본(한글 Pretendard로 교체돼 있음) — "점유 중" 표기가 그대로 나온다
            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = _grade.ToString(); // 런타임엔 CheeseSpot이 "등급 · 잔량"으로 덮어쓴다
            tmp.fontSize = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.yellow;
            labelGo.AddComponent<Cameras.FaceCamera>(); // 어느 각도에서도 읽히게

            return visual;
        }

        // 등급별 디스크 색 — 위험도 가독성 (1~3 노랑 / 4~5 주황 / 6~7 빨강)
        private Material GradeDiscMaterial(int _grade)
        {
            return _grade >= 6 ? m_DiscHighMaterial : _grade >= 4 ? m_DiscMidMaterial : m_DiscLowMaterial;
        }

        private static GameObject AddBox(
            GameObject _parent, Material _material, string _name, Vector3 _center, Vector3 _size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = _name;
            box.transform.SetParent(_parent.transform, false);
            box.transform.localPosition = _center;
            box.transform.localScale = _size;
            box.GetComponent<Renderer>().sharedMaterial = _material;
            return box;
        }

        // 대각 통로용 회전 박스 (AddBox + 회전)
        private static GameObject AddOrientedBox(
            GameObject _parent, Material _material, string _name, Vector3 _center, Quaternion _rotation, Vector3 _size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = _name;
            box.transform.SetParent(_parent.transform, false);
            box.transform.localPosition = _center;
            box.transform.localRotation = _rotation;
            box.transform.localScale = _size;
            box.GetComponent<Renderer>().sharedMaterial = _material;
            return box;
        }
    }
}
