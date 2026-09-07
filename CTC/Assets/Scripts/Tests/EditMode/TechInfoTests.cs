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
        public void TechInfo_LoadsTwelveRows()
        {
            // 계획 1: 기존 8항목 = 쥐 6행(총 2레벨) + 고양이 6행(스택 2종 × 3레벨)
            Assert.AreEqual(12, InfoManager.Instance.Tech.Count);
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

                for (int i = 1; i < rows.Count; i++)
                {
                    Assert.Greater(rows[i].Cost, rows[i - 1].Cost,
                        $"{entry.Key}: Lv{rows[i].Level} 비용({rows[i].Cost})이 Lv{rows[i - 1].Level}({rows[i - 1].Cost})보다 커야 한다");
                }
            }
        }
    }
}
