using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Builder = global::CTC.Editor.WarehouseLayoutBuilder;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 감사 D-04 — 굽기 메뉴(`Tools/CTC/Build Warehouse Layout`)가 **지금** 뱉는 값이 디스크의 에셋과 같다.
    ///
    /// 막는 것: 도면 상수(`WarehouseLayoutBuilder`)만 고치고 굽기를 안 돌린 채 커밋한다.
    /// 그러면 `MapLayoutIntegrityTests`는 옛 에셋을 보고 초록이고, 코드 리뷰는 새 좌표를 보고 안심한다 —
    /// 실제 판은 둘 중 **에셋**을 쓴다. 벽감 내용물은 CSV(`CheesePerAlcove`)도 타므로 CSV만 바꿔도 여기서 빨개진다.
    /// </summary>
    [NUnit.Framework.Category("Asset")]
    public class WarehouseLayoutBakeParityTests
    {
        private const float EPS = 1e-4f;

        [NUnit.Framework.Property("CaseId", "D-04")]
        [Test]
        public void WarehouseBakeMenu_OutputMatchesLayoutAssetOnDisk()
        {
            var layout = AssetDatabase.LoadAssetAtPath<MapLayout>(Builder.LayoutPath);
            Assert.IsNotNull(layout, $"레이아웃 에셋이 없다: {Builder.LayoutPath} — 굽기 메뉴를 돌려라");

            Builder.Generated fresh = Builder.Generate();
            Assert.IsNotNull(fresh, "굽기 검산이 실패해 메뉴가 아무것도 안 쓴다 — 콘솔의 [Warehouse] 에러를 봐라");

            const string STALE = " — 도면 상수와 에셋이 어긋났다. `Tools/CTC/Build Warehouse Layout`을 돌려 다시 구워라";

            Assert.IsTrue(layout.HandPlaced, "HandPlaced" + STALE);
            Assert.AreEqual(Builder.HalfExtent, layout.HalfExtent, EPS, "HalfExtent" + STALE);
            Assert.AreEqual(Builder.DISPLAY_NAME, layout.DisplayName, "DisplayName" + STALE);
            Assert.AreEqual(Builder.RECOMMENDED_MICE_MIN, layout.RecommendedMiceMin, "RecommendedMiceMin" + STALE);
            Assert.AreEqual(Builder.RECOMMENDED_MICE_MAX, layout.RecommendedMiceMax, "RecommendedMiceMax" + STALE);

            var diffs = new List<string>();

            CompareCount("Props", fresh.Props.Count, layout.Props.Count, diffs);
            for (int i = 0; i < Mathf.Min(fresh.Props.Count, layout.Props.Count); i++)
            {
                PropPlacement a = fresh.Props[i], b = layout.Props[i];
                if (a.Name != b.Name || a.Kind != b.Kind || !Near(a.Pos, b.Pos) || !Near(a.Size, b.Size)
                    || !Near(a.YawDeg, b.YawDeg) || !Near(a.Height, b.Height))
                {
                    diffs.Add($"Props[{i}] 메뉴 {a.Name}/{a.Kind} @{a.Pos} {a.Size} h{a.Height} yaw{a.YawDeg}" +
                              $" ≠ 에셋 {b.Name}/{b.Kind} @{b.Pos} {b.Size} h{b.Height} yaw{b.YawDeg}");
                }
            }

            CompareCount("Cheese", fresh.Cheese.Count, layout.Cheese.Count, diffs);
            for (int i = 0; i < Mathf.Min(fresh.Cheese.Count, layout.Cheese.Count); i++)
            {
                CheesePlacement a = fresh.Cheese[i], b = layout.Cheese[i];
                if (a.Grade != b.Grade || !Near(a.Pos, b.Pos) || !Near(a.Radius, b.Radius))
                    diffs.Add($"Cheese[{i}] 메뉴 G{a.Grade} @{a.Pos} r{a.Radius} ≠ 에셋 G{b.Grade} @{b.Pos} r{b.Radius}");
            }

            CompareCount("Fortresses", fresh.Fortresses.Count, layout.Fortresses.Count, diffs);
            for (int i = 0; i < Mathf.Min(fresh.Fortresses.Count, layout.Fortresses.Count); i++)
            {
                FortressPlacement a = fresh.Fortresses[i], b = layout.Fortresses[i];
                if (!Near(a.Pos, b.Pos) || !Near(a.YawDeg, b.YawDeg))
                    diffs.Add($"Fortresses[{i}] 메뉴 @{a.Pos} yaw{a.YawDeg} ≠ 에셋 @{b.Pos} yaw{b.YawDeg}");
            }

            CompareCount("Alcoves", fresh.Alcoves.Count, layout.Alcoves.Count, diffs);
            for (int i = 0; i < Mathf.Min(fresh.Alcoves.Count, layout.Alcoves.Count); i++)
            {
                AlcovePlacement a = fresh.Alcoves[i], b = layout.Alcoves[i];
                if (a.Payload != b.Payload || !Near(a.Pos, b.Pos) || !Near(a.YawDeg, b.YawDeg))
                    diffs.Add($"Alcoves[{i}] 메뉴 {a.Payload} @{a.Pos} yaw{a.YawDeg} ≠ 에셋 {b.Payload} @{b.Pos} yaw{b.YawDeg}");

                string contentsA = Describe(a.BoxContents), contentsB = Describe(b.BoxContents);
                if (contentsA != contentsB)
                    diffs.Add($"Alcoves[{i}].BoxContents 메뉴 [{contentsA}] ≠ 에셋 [{contentsB}]");
            }

            CompareCount("Shops", fresh.Shops.Count, layout.Shops.Count, diffs);
            for (int i = 0; i < Mathf.Min(fresh.Shops.Count, layout.Shops.Count); i++)
            {
                if (!Near(fresh.Shops[i].Pos, layout.Shops[i].Pos))
                    diffs.Add($"Shops[{i}] 메뉴 @{fresh.Shops[i].Pos} ≠ 에셋 @{layout.Shops[i].Pos}");
            }

            CompareCount("Lights", fresh.Lights.Count, layout.Lights.Count, diffs);
            for (int i = 0; i < Mathf.Min(fresh.Lights.Count, layout.Lights.Count); i++)
            {
                LightPlacement a = fresh.Lights[i], b = layout.Lights[i];
                if (a.Moon != b.Moon || !Near(a.Pos, b.Pos) || !Near(a.Radius, b.Radius))
                    diffs.Add($"Lights[{i}] 메뉴 @{a.Pos} r{a.Radius} moon{a.Moon} ≠ 에셋 @{b.Pos} r{b.Radius} moon{b.Moon}");
            }

            Assert.IsEmpty(diffs, $"어긋남 {diffs.Count}건{STALE}:\n" + string.Join("\n", diffs));
        }

        private static void CompareCount(string _label, int _fresh, int _disk, List<string> _diffs)
        {
            if (_fresh != _disk) _diffs.Add($"{_label} 개수 메뉴 {_fresh} ≠ 에셋 {_disk}");
        }

        private static bool Near(float _a, float _b) => Mathf.Abs(_a - _b) <= EPS;

        private static bool Near(Vector2 _a, Vector2 _b) => Near(_a.x, _b.x) && Near(_a.y, _b.y);

        // null과 빈 목록은 같은 것으로 본다 — 직렬화가 null을 빈 목록으로 되돌린다
        private static string Describe(List<LootEntry> _contents)
        {
            if (_contents == null || _contents.Count == 0) return "";
            var parts = new List<string>(_contents.Count);
            foreach (LootEntry e in _contents) parts.Add($"{e.Id}×{e.Amount}");
            return string.Join(",", parts);
        }
    }
}
