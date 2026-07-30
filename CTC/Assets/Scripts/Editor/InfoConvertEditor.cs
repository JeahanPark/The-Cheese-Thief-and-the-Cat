using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// `Assets/Resources/CSVInfo/*.csv` → 스키마 클래스 + 로더 자동 생성 (WES `InfoConvertEditor` 이식).
    ///
    /// 헤더 형식은 **`필드명.타입`** — 예 `Cost.int`, `Role.RoleId`, `CooldownSeconds.float`.
    /// 타입은 그대로 C# 타입명으로 박히므로 enum도 쓸 수 있다(생성 파일이 CTC 네임스페이스를 using한다).
    ///
    /// 컬럼을 추가하려면 CSV 헤더에 `이름.타입`을 넣고 이 메뉴를 다시 돌린다 —
    /// 스키마 클래스를 손으로 고치면 다음 변환에서 덮어써진다.
    /// </summary>
    public static class InfoConvertEditor
    {
        private const string CSV_DIR = "Assets/Resources/CSVInfo";
        private const string SCHEMA_DIR = "Assets/Scripts/Info/Generator/ConvertInfo";
        private const string LOADER_PATH = "Assets/Scripts/Info/Generator/InfoLoader.cs";

        private const string HEADER_COMMENT =
            "// 이 파일은 CSV에서 자동 생성된다 — 직접 고치지 마라.\n";

        [MenuItem("Tools/CTC/Convert CSV → Info")]
        public static void ConvertAll()
        {
            if (!Directory.Exists(CSV_DIR))
            {
                Debug.LogError($"[InfoConvert] CSV 폴더 없음: {CSV_DIR}");
                return;
            }
            Directory.CreateDirectory(SCHEMA_DIR);

            string[] files = Directory.GetFiles(CSV_DIR, "*.csv");
            var tableNames = new List<string>();

            foreach (string csv in files)
            {
                string table = Path.GetFileNameWithoutExtension(csv);
                string[] lines = File.ReadAllLines(csv);
                if (lines.Length == 0)
                {
                    Debug.LogWarning($"[InfoConvert] {table}: 빈 파일 — 건너뜀");
                    continue;
                }

                File.WriteAllText(Path.Combine(SCHEMA_DIR, table + ".cs"),
                    BuildSchema(table, lines[0]), new UTF8Encoding(true));
                tableNames.Add(table);
            }

            tableNames.Sort();
            File.WriteAllText(LOADER_PATH, BuildLoader(tableNames), new UTF8Encoding(true));

            AssetDatabase.Refresh();
            Debug.Log($"[InfoConvert] {tableNames.Count}개 표 생성: {string.Join(", ", tableNames)}");
        }

        // 헤더 한 줄에서 필드 선언을 만든다. 타입 힌트가 없으면 string으로 본다.
        private static string BuildSchema(string _table, string _header)
        {
            var sb = new StringBuilder();
            sb.Append(HEADER_COMMENT);
            sb.AppendLine($"// 원본: {CSV_DIR}/{_table}.csv → 메뉴 Tools/CTC/Convert CSV → Info");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using CTC.Runtime.Economy;");
            sb.AppendLine("using CTC.Runtime.Lobby;");
            sb.AppendLine();
            sb.AppendLine("namespace CTC.Runtime.Info");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {_table}");
            sb.AppendLine("    {");

            foreach (string raw in _header.Split(','))
            {
                string column = raw.Trim();
                if (string.IsNullOrEmpty(column)) continue;

                string[] parts = column.Split('.');
                string name = parts[0].Trim();
                string type = parts.Length > 1 ? parts[1].Trim() : "string";
                sb.AppendLine($"        public {type} {name};");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public partial class InfoManager");
            sb.AppendLine("    {");
            sb.AppendLine($"        public List<{_table}> {_table}List = new List<{_table}>();");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string BuildLoader(List<string> _tables)
        {
            var sb = new StringBuilder();
            sb.Append(HEADER_COMMENT);
            sb.AppendLine($"// 메뉴 Tools/CTC/Convert CSV → Info 가 {CSV_DIR}/*.csv 전부를 훑어 다시 만든다.");
            sb.AppendLine();
            sb.AppendLine("namespace CTC.Runtime.Info");
            sb.AppendLine("{");
            sb.AppendLine("    public partial class InfoManager");
            sb.AppendLine("    {");
            sb.AppendLine("        public void LoadAllInfo()");
            sb.AppendLine("        {");
            foreach (string table in _tables)
            {
                sb.AppendLine($"            {table}List = LoadInfo<{table}>();");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
