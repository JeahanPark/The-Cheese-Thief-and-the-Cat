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
            ("p1", new Vector2(-52, 20), new Vector2(-52, 56), new[] { new Vector2(34, 37) }),
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
            ("y5", new Rect(48, 30, 8, 10)), ("y6", new Rect(28, 48, 12, 4)),
            ("y7", new Rect(46, 46, 10, 8)),
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

        private static readonly (int grade, Vector2 at)[] CHEESE =
        {
            (7, new Vector2(-8, -6)), (7, new Vector2(0, 44)),
            (6, new Vector2(10, 8)), (6, new Vector2(-20, -20)),
            (5, new Vector2(-32, 46)), (5, new Vector2(34, 40)),
            (4, new Vector2(-48, 30)), (4, new Vector2(50, 26)),
            (3, new Vector2(-40, 56)), (3, new Vector2(26, -22)),
            (2, new Vector2(-30, -48)), (2, new Vector2(48, -36)),
            (1, new Vector2(-48, -30)), (1, new Vector2(56, -6)),
        };

        private static readonly (Vector2 at, float yaw)[] FORTS =
        {
            (new Vector2(-54, 54), 135f), (new Vector2(18, 57), 180f),
            (new Vector2(54, 54), 225f), (new Vector2(57, -18), 270f),
            (new Vector2(54, -54), 315f), (new Vector2(-18, -57), 0f),
            (new Vector2(-54, -54), 45f), (new Vector2(-57, 18), 90f),
        };

        private static readonly Vector2[] SHOPS = { new(-36, -28), new(40, -24), new(-40, 36), new(36, 32) };

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
