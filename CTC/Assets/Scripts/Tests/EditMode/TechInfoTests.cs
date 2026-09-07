using System.Collections.Generic;
using CTC.Runtime.Economy;
using CTC.Runtime.Info;
using CTC.Runtime.Lobby;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class TechInfoTests
    {
        [Test]
        public void TechInfo_LoadsFortyEightRows()
        {
            // 계획 2a: 계획 1의 12행 + 쥐 테크 24행(상위 레벨 9 + 신규 5종 15)
            // 계획 2b: 고양이 신규 4종 × 3레벨 = 12행
            Assert.AreEqual(48, InfoManager.Instance.Tech.Count);
        }

        [Test]
        public void TechInfo_Params_ReadableByLevel()
        {
            Assert.AreEqual(4f, TechLogic.ParamOf(TechId.Pepper, 2, 1), 1e-4f);   // 중첩
            Assert.AreEqual(6f, TechLogic.ParamOf(TechId.Pepper, 3, 2), 1e-4f);   // 초
            Assert.AreEqual(1200f, TechLogic.ParamOf(TechId.Fortress, 3, 1), 1e-4f);
            Assert.AreEqual(25f, TechLogic.ParamOf(TechId.Fortress, 3, 2), 1e-4f);
            Assert.AreEqual(0f, TechLogic.ParamOf(TechId.Fortress, 0, 1), 1e-4f);  // 미보유 = 0
            Assert.AreEqual(0f, TechLogic.ParamOf(TechId.CheeseShield, 9, 1), 1e-4f); // 없는 레벨 = 0
        }

        [Test]
        public void TechInfo_Fortress_HpAndReflectLadder()
        {
            // 요새 HP(P1)·반사(P2) 사다리 — ShopController가 스폰 전에 이 값을 요새에 넣는다.
            // BalanceInfo.FortressHp(480)는 Lv1과 같은 값이지만, 이제 프리팹 굽기·대체값 전용이다.
            Assert.AreEqual(480f, TechLogic.ParamOf(TechId.Fortress, 1, 1), 1e-4f);
            Assert.AreEqual(900f, TechLogic.ParamOf(TechId.Fortress, 2, 1), 1e-4f);
            Assert.AreEqual(1200f, TechLogic.ParamOf(TechId.Fortress, 3, 1), 1e-4f);

            // 가시 벽은 Lv3부터 — 그 아래는 0이라 ThornWall이 붙어 있어도 아무 일도 없다.
            Assert.AreEqual(0f, TechLogic.ParamOf(TechId.Fortress, 1, 2), 1e-4f);
            Assert.AreEqual(0f, TechLogic.ParamOf(TechId.Fortress, 2, 2), 1e-4f);
            Assert.AreEqual(25f, TechLogic.ParamOf(TechId.Fortress, 3, 2), 1e-4f);
        }

        [Test]
        public void TechInfo_MouseCatalog_IsTen()
        {
            Assert.AreEqual(10, TechLogic.Catalog(RoleId.Mouse).Count);
        }

        [Test]
        public void TechInfo_CatCatalog_IsSix()
        {
            // 계획 2b: 발톱·발 구르기에 두꺼운 가죽·배고픔·광분·예민한 귀가 붙었다.
            Assert.AreEqual(6, TechLogic.Catalog(RoleId.Cat).Count);
        }

        [Test]
        public void TechInfo_CatTech_LaddersMatchSpec()
        {
            // 기획서 §5 값 — CatHide P1은 최대 HP, CatFrenzy는 P1 배율·P2 지속·P3 쿨.
            Assert.AreEqual(1450f, TechLogic.ParamOf(TechId.CatHide, 3, 1), 1e-4f);
            Assert.AreEqual(1.6f, TechLogic.ParamOf(TechId.CatHunger, 3, 1), 1e-4f);
            Assert.AreEqual(2.5f, TechLogic.ParamOf(TechId.CatFrenzy, 3, 1), 1e-4f);
            Assert.AreEqual(10f, TechLogic.ParamOf(TechId.CatFrenzy, 3, 2), 1e-4f);
            Assert.AreEqual(20f, TechLogic.ParamOf(TechId.CatFrenzy, 3, 3), 1e-4f);
            Assert.AreEqual(2f, TechLogic.ParamOf(TechId.CatEar, 3, 1), 1e-4f);
        }

        [Test]
        public void TechInfo_GunLevel2_IsMachineGun600()
        {
            TechInfo found = null;
            foreach (TechInfo row in InfoManager.Instance.Tech)
                if (row.TechId == TechId.Gun && row.Level == 2) found = row;
            Assert.IsNotNull(found);
            Assert.AreEqual(600, found.Cost);
            Assert.AreEqual(RoleId.Mouse, found.Role);
            Assert.AreEqual(TechAxis.Attack, found.Axis);
        }

        [Test]
        public void Balance_HasTechColumns()
        {
            BalanceInfo b = InfoManager.Instance.Balance;
            // 기획 규칙 1 — 테크 슬롯은 5칸
            Assert.AreEqual(5, b.TechSlotCap);
            // 즉발 방패 값 유지(2026-08-15) — 요새 설치 1회 비용
            Assert.AreEqual(80, b.FortressPlaceCost);
            // 창고 설치 1회 비용 — 설치권 50과 합쳐 첫 창고가 옛 값(150) 그대로가 되게 잡은 수다.
            // 재설치가 공짜면 고양이가 창고를 째도 응징이 안 된다(2026-09-07).
            Assert.AreEqual(100, b.WarehousePlaceCost);
            // 한 곳이면 고양이가 지킨다 — 상점 스팟은 최소 4곳
            Assert.AreEqual(4, b.ShopSpotCount);
            // 요새 풋프린트(2.4)보다 살짝 큼 — 겹쳐 놓지 못하게
            Assert.AreEqual(3f, b.ShopSpotRadius, 1e-4f);
            // 대형 맵 시야 차폐 20.5m보다 멀어야 두 상점이 한 눈에 안 보인다
            Assert.AreEqual(25f, b.ShopSpotMinGap, 1e-4f);
            // 기존 BoxBreakSoundMeters(상자 파괴음)와 같은 거리 — "찾은 사람만 조용히" 급의 소리.
            // 상점 근처 고양이만 듣는다(정보=소리)
            Assert.AreEqual(12f, b.ShopBuySoundMeters, 1e-4f);
        }

        [Test]
        public void TechInfo_TableIsWellFormed()
        {
            Assert.IsNotEmpty(InfoManager.Instance.Tech);

            var levelCostById = new Dictionary<TechId, List<(int Level, int Cost)>>();
            var roleById = new Dictionary<TechId, RoleId>();
            var axisById = new Dictionary<TechId, TechAxis>();
            var seenPairs = new HashSet<(TechId, int)>();

            foreach (TechInfo row in InfoManager.Instance.Tech)
            {
                Assert.AreNotEqual(TechId.None, row.TechId, $"Lv{row.Level} 행에 TechId.None이 있으면 안 된다");
                Assert.AreNotEqual(RoleId.None, row.Role, $"{row.TechId}: Role.None이면 안 된다");

                var pair = (row.TechId, row.Level);
                Assert.IsTrue(seenPairs.Add(pair), $"{row.TechId} Lv{row.Level} 중복 행");

                if (!levelCostById.TryGetValue(row.TechId, out List<(int Level, int Cost)> rows))
                {
                    rows = new List<(int Level, int Cost)>();
                    levelCostById[row.TechId] = rows;
                    roleById[row.TechId] = row.Role;
                    axisById[row.TechId] = row.Axis;
                }
                rows.Add((row.Level, row.Cost));

                Assert.AreEqual(roleById[row.TechId], row.Role, $"{row.TechId}: 같은 테크인데 Role이 다르다");
                Assert.AreEqual(axisById[row.TechId], row.Axis, $"{row.TechId}: 같은 테크인데 Axis가 다르다");

                Assert.IsFalse(string.IsNullOrEmpty(row.Label), $"{row.TechId} Lv{row.Level}: Label 비어있음");
                Assert.IsFalse(string.IsNullOrEmpty(row.Hint), $"{row.TechId} Lv{row.Level}: Hint 비어있음");
            }

            // Label/Hint에 콤마가 있으면 로더가 줄을 쪼갤 때 깨진다 — 이미 파싱된 값이 아니라
            // 원본 CSV 텍스트를 직접 읽어 각 줄의 콤마 개수(헤더와 동일해야 함)로 검사한다.
            var csv = Resources.Load<TextAsset>("CSVInfo/TechInfo");
            Assert.IsNotNull(csv, "Resources/CSVInfo/TechInfo.csv 를 못 찾음");
            string[] lines = csv.text.Split('\n');
            string[] headerCols = lines[0].Trim().Split(',');
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                string[] cols = line.Split(',');
                Assert.AreEqual(headerCols.Length, cols.Length,
                    $"TechInfo.csv {i + 1}번째 줄 컬럼 수가 헤더와 다르다 (Label/Hint에 콤마?): \"{line}\"");
            }

            foreach (KeyValuePair<TechId, List<(int Level, int Cost)>> entry in levelCostById)
            {
                List<(int Level, int Cost)> rows = entry.Value;
                rows.Sort((_a, _b) => _a.Level.CompareTo(_b.Level));

                for (int i = 0; i < rows.Count; i++)
                {
                    Assert.AreEqual(i + 1, rows[i].Level,
                        $"{entry.Key}: 레벨이 1..N 연속이 아니다 ({string.Join(",", rows.ConvertAll(_r => _r.Level))})");
                }

                // 총만 값이 내려간다(기획서 §4) — 기관총(Lv2 600)이 값의 봉우리고
                // 긴 총열(Lv3 450)은 그 위에 얹는 사거리·피해라 일부러 더 싸다.
                // 예외는 여기 한 곳에만 적는다 — 다른 테크가 내려가면 이 테스트가 잡는다.
                if (entry.Key == TechId.Gun)
                {
                    // 오르내림 검사에서 빠지는 대신, 정확한 비용 사다리를 여기서 핀으로 고정한다.
                    CollectionAssert.AreEqual(new[] { 150, 600, 450 }, rows.ConvertAll(_r => _r.Cost),
                        "Gun 비용 사다리가 150/600/450에서 벗어났다");
                    continue;
                }

                for (int i = 1; i < rows.Count; i++)
                {
                    Assert.Greater(rows[i].Cost, rows[i - 1].Cost,
                        $"{entry.Key}: Lv{rows[i].Level} 비용({rows[i].Cost})이 Lv{rows[i - 1].Level}({rows[i - 1].Cost})보다 커야 한다");
                }
            }
        }
    }
}
