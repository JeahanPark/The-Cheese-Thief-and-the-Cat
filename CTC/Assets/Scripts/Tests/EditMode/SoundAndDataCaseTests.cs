using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CTC.Runtime.Audio;
using CTC.Runtime.Bots;
using CTC.Runtime.Info;
using CTC.Runtime.Intro;
using CTC.Runtime.Lobby;
using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>QA 대장 C-20·C-21·C-22(소리) · C-28·C-29(CSV 데이터).</summary>
    public class SoundAndDataCaseTests
    {
        private const string CSV_DIR = "Assets/Resources/CSVInfo";
        private const string FORTRESS_PREFAB = "Assets/Prefabs/CheeseFortress.prefab";
        private const string WAREHOUSE_PREFAB = "Assets/Prefabs/CheeseWarehouse.prefab";

        private static SfxId PlaceSfxOf(string _prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            Assert.IsTrue(prefab != null, $"프리팹 없음: {_prefabPath}");
            var place = prefab.GetComponent<PlaceSfxOnSpawn>();
            Assert.IsTrue(place != null, $"{_prefabPath}: PlaceSfxOnSpawn이 없다 — 설치음이 안 난다");
            var so = new SerializedObject(place);
            SerializedProperty prop = so.FindProperty("m_Sfx");
            Assert.IsNotNull(prop, "PlaceSfxOnSpawn.m_Sfx 직렬화 필드가 없다");
            return (SfxId)prop.intValue;
        }

        [NUnit.Framework.Property("CaseId", "C-20")]
        [Test]
        [NUnit.Framework.Category("Asset")]
        public void WarehouseAndFortress_PlaceSounds_Differ()
        {
            SfxId fortress = PlaceSfxOf(FORTRESS_PREFAB);
            SfxId warehouse = PlaceSfxOf(WAREHOUSE_PREFAB);
            Assert.AreNotEqual(fortress, warehouse, $"요새·창고 설치음이 둘 다 {fortress}다");
        }

        [NUnit.Framework.Property("CaseId", "C-21")]
        [Test]
        public void FrenzySound_DiffersFromClassSkillSound()
        {
            Assert.AreNotEqual(CatFrenzy.USE_SFX, CatSkill.USE_SFX,
                $"광분·전직 스킬이 둘 다 {CatSkill.USE_SFX}를 튼다");
        }

        [NUnit.Framework.Property("CaseId", "C-22")]
        [Test]
        public void CatBot_HearsShopBuy_AsPreySound()
        {
            Assert.IsTrue(BotSenseLogic.IsPreySound(SfxId.ShopBuy, RoleId.Mouse),
                "봇 고양이가 쥐의 구매음(ShopBuy)을 먹잇감 소리로 안 듣는다");
            Assert.IsFalse(BotSenseLogic.IsPreySound(SfxId.CatShopBuy, RoleId.Mouse),
                "고양이 자신의 구매음까지 먹잇감으로 들으면 봇 고양이가 제 소리를 쫓는다");
        }

        [NUnit.Framework.Property("CaseId", "C-28")]
        [Test]
        public void IntroGameDataCheck_FailsWhenAnyOfTheFourTablesIsEmpty()
        {
            // 표 목록은 손으로 적지 않고 InfoManager의 *InfoList 필드(생성기 산출물)에서 모은다.
            var tables = new List<FieldInfo>();
            foreach (FieldInfo field in typeof(InfoManager).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.Name.EndsWith("InfoList") && typeof(IList).IsAssignableFrom(field.FieldType)) tables.Add(field);
            }
            int csvCount = Directory.GetFiles(CSV_DIR, "*.csv").Length;
            Assert.AreEqual(4, tables.Count, "InfoManager 표가 네 종이 아니다");
            Assert.AreEqual(csvCount, tables.Count, "CSV 파일 수와 InfoManager 표 수가 다르다");

            var full = new InfoManager();
            full.LoadAllInfo();
            Assert.IsTrue(IntroBootLogic.GameDataLoaded(full), "표가 전부 찼는데 실패로 본다");

            var unnoticed = new List<string>();
            foreach (FieldInfo table in tables)
            {
                var info = new InfoManager();
                info.LoadAllInfo();
                ((IList)table.GetValue(info)).Clear();
                if (IntroBootLogic.GameDataLoaded(info)) unnoticed.Add(table.Name);
            }
            Assert.IsEmpty(unnoticed, "비어도 인트로가 통과시키는 표: " + string.Join(", ", unnoticed));
        }

        [NUnit.Framework.Property("CaseId", "C-29")]
        [Test]
        [NUnit.Framework.Category("Asset")]
        public void EveryCsvRow_HasHeaderColumnCount()
        {
            // InfoManager.LoadInfo와 같은 규칙으로 쪼갠다 — '\n' 줄 · Trim · 빈 줄 건너뜀 · 따옴표 없이 ','.
            // 로더가 따옴표를 모르므로 여기서도 모른다: 값 안의 콤마는 로더에서 열을 민다.
            string[] files = Directory.GetFiles(CSV_DIR, "*.csv");
            Assert.GreaterOrEqual(files.Length, 4, "CSV 표가 네 종보다 적다");

            var problems = new List<string>();
            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                var asset = Resources.Load<TextAsset>("CSVInfo/" + name);
                if (asset == null) { problems.Add($"{name}: Resources에서 못 읽음"); continue; }

                string[] lines = asset.text.Split('\n');
                int headerCols = lines[0].Trim().Split(',').Length;
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0) continue;
                    int cols = line.Split(',').Length;
                    if (cols != headerCols) problems.Add($"{name}.csv {i + 1}행: {cols}열 ≠ 헤더 {headerCols}열");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
