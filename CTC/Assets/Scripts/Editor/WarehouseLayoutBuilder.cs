using System.Collections.Generic;
using CTC.Runtime.Map;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 창고 평면도 v2(2026-09-07)를 `Layout_Warehouse01.asset`으로 굽는다.
    /// **원본은 도면**(`docs/design-vault/맵 시안 v2 창고.html`의 `MAP` 블록)이다 — 여기 숫자는 그걸 옮긴 것이라
    /// 도면을 고치면 여기도 같이 고친다. 격자 방·복도가 없다: 존을 가르는 것은 선반 열과 궤짝 벽이다.
    /// x 동 · z 북 · 미터.
    /// </summary>
    public static class WarehouseLayoutBuilder
    {
        public const string LayoutPath = "Assets/Prefabs/Maps/Layout_Warehouse01.asset";

        private const float HALF_EXTENT = 60f;
        private const float WALL_INSET = 0.25f;       // 경계벽 안쪽 여유 — 프롭이 벽에 파고들면 실패
        private const float CHEESE_RADIUS = 2f;

        // 선반 열: from → to (축 정렬), 틈은 열 축 좌표 구간
        private static readonly (string id, Vector2 from, Vector2 to, Vector2[] gaps)[] SHELVES =
        {
            ("hallN", new Vector2(-18, 15), new Vector2(18, 15), new[] { new Vector2(-12, -9), new Vector2(6, 9) }),
            ("hallS", new Vector2(-18, -15), new Vector2(18, -15), new[] { new Vector2(-6, -3), new Vector2(10, 13) }),
            ("hallW", new Vector2(-18, -15), new Vector2(-18, 15), new[] { new Vector2(-10, -7), new Vector2(5, 8) }),
            ("hallE", new Vector2(18, -15), new Vector2(18, 15), new[] { new Vector2(-8, -5), new Vector2(8, 11) }),
            // 북쪽 끝을 z 56 → 50으로 물렸다: 요새자리 #0(-54,54) 앞 스폰과 이 열의 틈이 0.227 m밖에 없었다
            // (쥐 몸 반지름 0.22) — 아래 지점 검산이 잡았다.
            ("p1", new Vector2(-52, 20), new Vector2(-52, 50), new[] { new Vector2(34, 37) }),
            ("p2", new Vector2(-44, 20), new Vector2(-44, 56), new[] { new Vector2(24, 27), new Vector2(46, 49) }),
            ("p3", new Vector2(-36, 20), new Vector2(-36, 56), new[] { new Vector2(38, 41) }),
            ("p4", new Vector2(-28, 20), new Vector2(-28, 56), new[] { new Vector2(22, 25), new Vector2(50, 53) }),
            ("nW", new Vector2(-18, 15), new Vector2(-18, 48), new[] { new Vector2(30, 33) }),
            ("nE", new Vector2(18, 15), new Vector2(18, 48), new[] { new Vector2(30, 33) }),
            ("sW", new Vector2(-18, -48), new Vector2(-18, -15), new[] { new Vector2(-33, -30) }),
            ("sE", new Vector2(18, -48), new Vector2(18, -15), new[] { new Vector2(-33, -30) }),
            ("cellarN", new Vector2(-56, -15), new Vector2(-22, -15), new[] { new Vector2(-48, -45), new Vector2(-30, -27) }),
            ("loadN", new Vector2(22, -15), new Vector2(56, -15), new[] { new Vector2(28, 31), new Vector2(46, 49) }),
        };

        // 궤짝 벽 [x, z, 폭, 깊이] — 2.4 m, 넘겨다보인다
        private static readonly (string id, Rect r)[] CRATE_WALLS =
        {
            ("y1", new Rect(24, 20, 10, 4)), ("y2", new Rect(40, 18, 12, 4)),
            ("y3", new Rect(22, 30, 6, 14)), ("y4", new Rect(36, 28, 8, 8)),
            // y5는 폭 8(x 48~56)이었다 — 강아지 벽감 #2(58,36) 정면 1.6 m가 이 벽에서 0.4 m였다(감사 M7).
            ("y5", new Rect(48, 30, 6, 10)), ("y6", new Rect(28, 48, 12, 4)),
            // y7은 (46,46,10,8)이었다 — 요새자리 #2(54,54)와 그 앞 쥐 스폰(52.73,52.73)을 통째로 삼켰다.
            // 마당 북동쪽을 가리는 역할은 그대로 두고 3 m 서·2 m 남으로 물렸다(감사 M1).
            ("y7", new Rect(44, 44, 8, 8)),
            ("n1", new Rect(-12, 24, 12, 2)), ("n2", new Rect(3, 24, 9, 2)),
            ("s1", new Rect(-12, -32, 9, 2)), ("s2", new Rect(0, -32, 12, 2)),
            ("l1", new Rect(24, -38, 8, 2)), ("l2", new Rect(35, -38, 9, 2)),
        };

        private static readonly Vector2[] BARRELS =
        {
            new(-56, -20), new(-52, -24), new(-48, -28), new(-44, -32), new(-40, -36), new(-36, -40), new(-32, -44), new(-28, -48), new(-24, -52),
            new(-56, -36), new(-52, -40), new(-48, -44), new(-44, -48), new(-40, -52), new(-36, -56),
            new(-26, -22), new(-22, -26),
            new(-14, -10), new(14, 10),
            new(54, -30), new(56, -34),
        };

        // 작업대·자루·계단 [x, z, 폭, 깊이], 높이
        private static readonly (string id, Rect r, float h)[] BLOCKS =
        {
            ("bench", new Rect(-7, 3, 14, 2), 3f),
            ("sack1", new Rect(27, -33, 6, 4), 3f),
            ("sack2", new Rect(44, -49, 5, 5), 3f),
            ("sack3", new Rect(50, -26, 4, 4), 3f),
            ("stairs", new Rect(-4, 50, 8, 8), 5f),
            ("sackN", new Rect(-14, 40, 4, 4), 3f),
            ("crateN", new Rect(10, 38, 4, 5), 3f),
        };

        // 파밍 원반(반경 2 m) 안에 차단물이 들면 고양이 몸이 지점 중심에 못 서서 파밍 대치가 깨진다.
        // #3 (-20,-20)→(-21,-20) · #10 (-30,-48)→(-32,-48) · #12 (-48,-30)→(-48,-32)로 물렸다(감사 M6).
        private static readonly (int grade, Vector2 at)[] CHEESE =
        {
            (7, new Vector2(-8, -6)), (7, new Vector2(0, 44)),
            (6, new Vector2(10, 8)), (6, new Vector2(-21, -20)),
            (5, new Vector2(-32, 46)), (5, new Vector2(34, 40)),
            (4, new Vector2(-48, 30)), (4, new Vector2(50, 26)),
            (3, new Vector2(-40, 56)), (3, new Vector2(26, -22)),
            (2, new Vector2(-32, -48)), (2, new Vector2(48, -36)),
            (1, new Vector2(-48, -32)), (1, new Vector2(56, -6)),
        };

        private static readonly (Vector2 at, float yaw)[] FORTS =
        {
            (new Vector2(-54, 54), 135f), (new Vector2(18, 57), 180f),
            (new Vector2(54, 54), 225f), (new Vector2(57, -18), 270f),
            (new Vector2(54, -54), 315f), (new Vector2(-18, -57), 0f),
            (new Vector2(-54, -54), 45f), (new Vector2(-57, 18), 90f),
        };

        // 상점 **후보** 8곳 — 그날 서는 것은 이 중 넷이다(계획 3 Task 2, `ShopSpotLogic.PickPlaced`).
        // 넷만 두면 아무리 섞어도 매 판 같은 자리라 「오늘 상점이 어디 섰나」가 성립하지 않는다.
        // 앞 넷은 네 모서리 존(창고·하역장·식료품고·마당), 뒤 넷은 **존 사이 통로** —
        // 중앙 홀·북쪽 계단길·서/동 연결로다. 전부 반경 3 m 원반이 들어가는 빈 바닥이고,
        // 여덟 중 **어느 두 곳을 잡아도 25 m 이상** 떨어져 있다(간격 완화가 걸리지 않게).
        private static readonly Vector2[] SHOPS =
        {
            // 마당 후보는 (36,32) → (32,32)로 4 m 옮겼다: 원반 절반이 궤짝 벽 y4(x 36~44) 속이라
            // 상점에는 들어가지는데 그 안에서 설치할 자리가 벽이었다(아래 검산이 잡았다).
            new(-36, -28), new(40, -24), new(-40, 36), new(32, 32),
            new(0, -6), new(0, 32), new(-40, 4), new(40, 4),
        };

        /// <summary>상점 원반 반경(m) — `BalanceInfo.ShopSpotRadius`와 같은 값. 후보 검산에만 쓴다.</summary>
        private const float SHOP_RADIUS = 3f;

        /// <summary>후보끼리 지켜야 하는 거리(m) — `BalanceInfo.ShopSpotMinGap`과 같은 값.</summary>
        private const float SHOP_MIN_GAP = 25f;

        private static readonly (Vector2 at, float yaw, bool dog)[] ALCOVES =
        {
            (new Vector2(-58, 40), 90f, false), (new Vector2(-40, 58), 180f, false),
            (new Vector2(58, 36), 270f, true), (new Vector2(30, -58), 0f, false),
            (new Vector2(-58, -40), 90f, false), (new Vector2(58, -46), 270f, false),
        };

        private static readonly (Vector2 at, float r, bool moon)[] LIGHTS =
        {
            (new Vector2(0, 0), 12f, false), (new Vector2(-44, 36), 8f, false), (new Vector2(38, 30), 8f, false),
            (new Vector2(-40, -40), 5f, false), (new Vector2(38, -38), 12f, false), (new Vector2(0, 56), 9f, true),
            (new Vector2(0, -44), 6f, false),
        };

        /// <summary>원반이 축 정렬 박스에 닿는가 — 박스 위 가장 가까운 점까지의 거리로 잰다.</summary>
        private static bool DiscHitsBox(Vector2 _center, float _radius, Vector2 _boxPos, Vector2 _boxSize)
        {
            float hx = _boxSize.x * 0.5f, hz = _boxSize.y * 0.5f;
            float cx = Mathf.Clamp(_center.x, _boxPos.x - hx, _boxPos.x + hx);
            float cz = Mathf.Clamp(_center.y, _boxPos.y - hz, _boxPos.y + hz);
            return (new Vector2(cx, cz) - _center).sqrMagnitude < _radius * _radius;
        }

        [MenuItem("Tools/CTC/Build Warehouse Layout (창고 평면도 v2)")]
        public static void Build()
        {
            var props = new List<PropPlacement>();
            foreach ((string id, Vector2 from, Vector2 to, Vector2[] gaps) in SHELVES)
            {
                bool horizontal = Mathf.Approximately(from.y, to.y);
                float lo = horizontal ? Mathf.Min(from.x, to.x) : Mathf.Min(from.y, to.y);
                float hi = horizontal ? Mathf.Max(from.x, to.x) : Mathf.Max(from.y, to.y);
                int n = 0;
                foreach (Vector2 seg in WarehouseLogic.SplitByGaps(lo, hi, gaps))
                    props.Add(WarehouseLogic.ShelfBox($"S-{id}-{n++}", from, to, seg, WarehouseLogic.SHELF_THICK));
            }
            foreach ((string id, Rect r) in CRATE_WALLS)
                props.Add(new PropPlacement { Name = "C-" + id, Kind = PropKind.CrateWall, Pos = r.center, Size = r.size, Height = WarehouseLogic.CRATE_WALL_HEIGHT });
            foreach (Vector2 b in BARRELS)
                props.Add(new PropPlacement { Name = "B", Kind = PropKind.Barrel, Pos = b, Size = Vector2.one * WarehouseLogic.BARREL_SIZE, Height = WarehouseLogic.BARREL_HEIGHT });
            foreach ((string id, Rect r, float h) in BLOCKS)
                props.Add(new PropPlacement { Name = "K-" + id, Kind = PropKind.Block, Pos = r.center, Size = r.size, Height = h });

            // 벽 겹침 검사 — 하나라도 파고들면 굽지 않는다(조용히 깨지는 것보다 낫다)
            int bad = 0;
            foreach (PropPlacement p in props)
            {
                if (WarehouseLogic.InsideBounds(p.Pos, p.Size, HALF_EXTENT, WALL_INSET)) continue;
                bad++;
                Debug.LogError($"[Warehouse] 경계벽 겹침: {p.Name} @({p.Pos.x},{p.Pos.y}) size({p.Size.x}×{p.Size.y})");
            }
            if (bad > 0) { Debug.LogError($"[Warehouse] 위반 {bad}건 — 도면을 고쳐라"); return; }

            // 상점 후보 검산 — 원반이 차단물에 물리거나 후보끼리 붙어 있으면 굽지 않는다.
            // 눈대중으로 좌표를 옮기다 원반 절반이 선반 속에 들어가면 **에러가 안 난다**:
            // 상점에는 들어가지는데 그 안에서 설치할 자리가 벽이 된다(2026-09-08 실측과 같은 증상).
            int shopBad = 0;
            for (int i = 0; i < SHOPS.Length; i++)
            {
                foreach (PropPlacement p in props)
                {
                    if (!DiscHitsBox(SHOPS[i], SHOP_RADIUS, p.Pos, p.Size)) continue;
                    shopBad++;
                    Debug.LogError($"[Warehouse] 상점 후보 {i} @({SHOPS[i].x},{SHOPS[i].y})가 차단물 {p.Name}에 물린다");
                }
                for (int j = i + 1; j < SHOPS.Length; j++)
                {
                    float d = Vector2.Distance(SHOPS[i], SHOPS[j]);
                    if (d >= SHOP_MIN_GAP) continue;
                    shopBad++;
                    Debug.LogError($"[Warehouse] 상점 후보 {i}·{j}가 {d:0.#} m — 최소 간격 {SHOP_MIN_GAP} m 미만");
                }
            }
            if (shopBad > 0) { Debug.LogError($"[Warehouse] 상점 후보 위반 {shopBad}건 — 좌표를 고쳐라"); return; }

            var cheese = new List<CheesePlacement>();
            foreach ((int grade, Vector2 at) in CHEESE) cheese.Add(new CheesePlacement { Pos = at, Grade = grade, Radius = CHEESE_RADIUS });

            var forts = new List<FortressPlacement>();
            foreach ((Vector2 at, float yaw) in FORTS) forts.Add(new FortressPlacement { Pos = at, YawDeg = yaw });

            var alcoves = new List<AlcovePlacement>();
            foreach ((Vector2 at, float yaw, bool dog) in ALCOVES)
                alcoves.Add(new AlcovePlacement { Pos = at, YawDeg = yaw, Payload = dog ? AlcovePayload.Dog : AlcovePayload.Box, BoxContents = new List<LootEntry>() });
            SetupNetworkingScene.FillBoxContents(alcoves);   // 직업권 2 + 치즈 벽 — 자동 계획과 같은 규칙

            var shops = new List<ShopPlacement>();
            foreach (Vector2 s in SHOPS) shops.Add(new ShopPlacement { Pos = s });

            var lights = new List<LightPlacement>();
            foreach ((Vector2 at, float r, bool moon) in LIGHTS) lights.Add(new LightPlacement { Pos = at, Radius = r, Moon = moon });

            // 지점 검산 — 쥐 스폰·치즈 원반·강아지 벽감 정면이 차단물에 물리면 굽지 않는다 (감사 D-01·D-02·D-05·D-06).
            // 상점만 검사가 있던 시절에 쥐 스폰 #2가 궤짝 벽 속으로 들어갔다(M1). 규칙은 `MapPointAudit` 한 벌이고
            // `MapLayoutIntegrityTests`가 구운 에셋에 대고 같은 것을 다시 본다.
            List<MapPointAudit.Finding> findings = MapPointAudit.Audit(props, forts, cheese, alcoves);
            if (findings.Count > 0)
            {
                Debug.LogError($"[Warehouse] 지점 검산 위반 {findings.Count}건 — 좌표를 고쳐라\n{MapPointAudit.Describe(findings)}");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Maps")) AssetDatabase.CreateFolder("Assets/Prefabs", "Maps");
            var layout = AssetDatabase.LoadAssetAtPath<MapLayout>(LayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<MapLayout>();
                AssetDatabase.CreateAsset(layout, LayoutPath);
            }
            layout.SetHandPlaced("창고 (120×120)", HALF_EXTENT, 6, 8, props, cheese, forts, alcoves, shops, lights);
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Warehouse] 저장 — 차단물 {props.Count}(선반 열 {SHELVES.Length}·궤짝 벽 {CRATE_WALLS.Length}·배럴 {BARRELS.Length}·가구 {BLOCKS.Length})" +
                      $" · 치즈 {cheese.Count} · 요새 {forts.Count} · 벽감 {alcoves.Count} · 상점 {shops.Count} · 빛 {lights.Count} → {LayoutPath}");
        }
    }
}
