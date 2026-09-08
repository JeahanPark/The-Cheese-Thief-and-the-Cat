using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 케이스 ID가 **어느 구현에 붙어 있는가**를 소스에서 훑고, 사람이 읽는 대장 표를 **생성**한다.
    ///
    /// 부착 문법은 둘뿐이다(둘 다 기존 문법 안이라 새 러너·새 포맷이 없다):
    ///   · C#      <c>[NUnit.Framework.Property("CaseId", "E-11")]</c>  — NUnit 기본 속성. 결과 XML에 그대로 실린다
    ///   · 파이썬  <c>check(..., case="E-18")</c> (스위트) · <c>@qa_case("E-04")</c> (pytest)
    ///
    /// 파일 경로만 모으고 **줄 번호는 안 적는다** — 줄이 밀릴 때마다 대장이 썩기 때문이다.
    /// </summary>
    public static class QaCaseLedger
    {
        /// <summary>대장 문서의 자리. 이 프로젝트의 자동 문서 금고 안이다.</summary>
        public const string LEDGER_PATH = "docs/design-vault/QA 케이스 대장.md";

        private const string CS_ROOT = "Assets/Scripts/Tests";
        private const string PY_ROOT = "tools/wesqa";

        private static readonly Regex CS_ATTACH = new Regex(
            @"\[\s*(?:NUnit\.Framework\.)?Property\s*\(\s*""CaseId""\s*,\s*""([A-Z]-[0-9]{2})""\s*\)\s*\]",
            RegexOptions.Compiled);

        // 한 항목이 케이스 둘을 덮으면 `case="B-04 C-23"`처럼 띄어 쓴다.
        private static readonly Regex PY_KWARG = new Regex(
            @"\bcase\s*=\s*([""'])([^""']*)\1", RegexOptions.Compiled);

        private static readonly Regex BARE_ID = new Regex(@"\b([A-Z]-[0-9]{2})\b", RegexOptions.Compiled);

        private static readonly Regex PY_DECORATOR = new Regex(
            @"qa_case\s*\(([^)]*)\)", RegexOptions.Compiled);

        private static readonly Regex ID_LITERAL = new Regex(
            @"[""']([A-Z]-[0-9]{2})[""']", RegexOptions.Compiled);

        /// <summary>`CTC/` — `Assets`의 부모.</summary>
        public static string RepoRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName.Replace('\\', '/') : "";
        }

        /// <summary>ID → 그 ID가 박힌 파일들(리포지토리 상대 경로, 정렬됨).</summary>
        public static SortedDictionary<string, List<string>> ScanAttachments()
        {
            var found = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            string root = RepoRoot();
            if (root.Length == 0) return found;

            Collect(found, root, CS_ROOT, "*.cs", ScanCsharp);
            Collect(found, root, PY_ROOT, "*.py", ScanPython);

            foreach (List<string> files in found.Values)
            {
                files.Sort(StringComparer.Ordinal);
            }
            return found;
        }

        private static void Collect(SortedDictionary<string, List<string>> _into, string _root,
                                    string _relDir, string _pattern, Func<string, IEnumerable<string>> _scan)
        {
            string dir = _root + "/" + _relDir;
            if (!Directory.Exists(dir)) return;

            string[] files = Directory.GetFiles(dir, _pattern, SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                string rel = file.Replace('\\', '/').Substring(_root.Length + 1);
                if (rel.Contains("__pycache__")) continue;
                // 부착 수단·대장 자신은 세지 않는다 — 설명 안의 예시 ID가 부착으로 잡힌다
                if (rel.EndsWith("/QaCases.cs") || rel.EndsWith("/QaCaseLedger.cs")) continue;
                if (rel.EndsWith("/test_case_registry.py") || rel.EndsWith("/qacase.py")) continue;

                string text;
                try { text = File.ReadAllText(file); }
                catch (IOException) { continue; }

                foreach (string id in _scan(text))
                {
                    List<string> list;
                    if (!_into.TryGetValue(id, out list))
                    {
                        list = new List<string>();
                        _into[id] = list;
                    }
                    if (!list.Contains(rel)) list.Add(rel);
                }
            }
        }

        private static IEnumerable<string> ScanCsharp(string _text)
        {
            foreach (Match m in CS_ATTACH.Matches(_text)) yield return m.Groups[1].Value;
        }

        private static IEnumerable<string> ScanPython(string _text)
        {
            foreach (Match m in PY_KWARG.Matches(_text))
                foreach (Match id in BARE_ID.Matches(m.Groups[2].Value))
                    yield return id.Groups[1].Value;
            foreach (Match m in PY_DECORATOR.Matches(_text))
                foreach (Match id in ID_LITERAL.Matches(m.Groups[1].Value))
                    yield return id.Groups[1].Value;
        }

        // ───────────────────────────── 대장 문서 생성 ─────────────────────────────

        /// <summary>대장 마크다운 전문. 줄바꿈은 항상 `\n`이다.</summary>
        public static string Render(SortedDictionary<string, List<string>> _attach)
        {
            var sb = new StringBuilder();
            sb.Append("# QA 테스트 케이스 대장\n\n");
            sb.Append("> **이 파일은 생성물이다. 손으로 고치지 마라.**\n");
            sb.Append("> 원본은 `Assets/Scripts/Tests/EditMode/QaCases.cs`이고, 「붙어 있는 구현」 칸은\n");
            sb.Append("> 소스에 박힌 케이스 ID를 훑어 만든다. 다시 만들려면 메뉴\n");
            sb.Append("> `Tools/CTC/Generate QA Case Ledger`를 누르거나 EditMode 테스트를 돌린다 —\n");
            sb.Append("> 이 파일이 선언과 어긋나면 `QaCaseRegistryTests`가 **빨개진다**.\n\n");
            sb.Append("케이스에 구현을 붙이는 문법은 둘뿐이다.\n\n");
            sb.Append("| 층 | 부착 |\n|---|---|\n");
            sb.Append("| C# (EditMode) | `[NUnit.Framework.Property(\"CaseId\", \"E-11\")]` — 테스트 메서드 위."
                      + " `UnityEngine`에 같은 이름이 있어 **정규화된 이름**을 쓴다 |\n");
            sb.Append("| 파이썬 (wesqa 스위트) | `check(\"…\", ok, detail, case=\"E-18\")`"
                      + " — 한 항목이 둘을 덮으면 `case=\"B-04 C-23\"`처럼 띄어 쓴다 |\n");
            sb.Append("| 파이썬 (pytest) | `@qa_case(\"E-04\")` |\n\n");
            sb.Append("**상태**는 부착으로 판정된다 — 자동 케이스는 붙은 구현이 하나라도 있으면 `구현됨`,\n");
            sb.Append("없으면 `미구현`이다. 수동 케이스는 절차·기대가 적혀 있으면 `구현됨`(사람이 그대로 돌릴 수 있다는 뜻이고,\n");
            sb.Append("누가 언제 돌렸는지는 **여기 안 적는다** — 그게 손 동기화의 출발점이다).\n");
            sb.Append("**폐기 번호는 비워 둔 채 재사용하지 않는다** — 폐기 행이 그 번호를 붙잡는다.\n\n");

            sb.Append(RenderSummary());
            sb.Append('\n');

            foreach (string domain in QaCases.DOMAINS)
            {
                sb.Append("## ").Append(domain).Append(" — ").Append(QaCases.TitleOf(domain)).Append("\n\n");
                sb.Append("| ID | 케이스 (동작) | 층 | 붙어 있는 구현 | 상태 | 잡는 회귀 |\n");
                sb.Append("|:--:|---|:--:|---|:--:|---|\n");
                foreach (QaCase c in QaCases.ALL)
                {
                    if (c.Domain != domain) continue;
                    sb.Append("| **").Append(c.Id).Append("** | ").Append(Cell(c.What))
                      .Append(" | ").Append(LayerText(c))
                      .Append(" | ").Append(Cell(Attached(c, _attach)))
                      .Append(" | ").Append(StatusText(c.Status))
                      .Append(" | ").Append(Cell(Reason(c))).Append(" |\n");
                }
                sb.Append('\n');
            }

            sb.Append("## 수동 케이스의 절차\n\n");
            foreach (QaCase c in QaCases.ALL)
            {
                if (c.IsAuto || c.Status == QaStatus.Retired) continue;
                sb.Append("### ").Append(c.Id).Append(" — ").Append(c.What).Append("\n\n");
                sb.Append("- **절차** ").Append(c.Procedure).Append('\n');
                sb.Append("- **기대** ").Append(c.Expected).Append('\n');
                sb.Append("- **잡는 회귀** ").Append(c.Catches).Append('\n');
                if (c.Note.Length > 0) sb.Append("- **비고** ").Append(c.Note).Append('\n');
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static string RenderSummary()
        {
            var sb = new StringBuilder();
            sb.Append("## 덮개 요약\n\n");
            sb.Append("| 도메인 | 전체 | 구현됨 | 미구현 | 폐기 |\n|---|:--:|:--:|:--:|:--:|\n");
            int tAll = 0, tOk = 0, tNo = 0, tRet = 0;
            foreach (string domain in QaCases.DOMAINS)
            {
                int all = 0, ok = 0, no = 0, ret = 0;
                foreach (QaCase c in QaCases.ALL)
                {
                    if (c.Domain != domain) continue;
                    all++;
                    if (c.Status == QaStatus.Implemented) ok++;
                    else if (c.Status == QaStatus.Missing) no++;
                    else ret++;
                }
                sb.Append("| ").Append(domain).Append(" ").Append(QaCases.TitleOf(domain))
                  .Append(" | ").Append(all).Append(" | ").Append(ok).Append(" | ")
                  .Append(no).Append(" | ").Append(ret).Append(" |\n");
                tAll += all; tOk += ok; tNo += no; tRet += ret;
            }
            sb.Append("| **합계** | **").Append(tAll).Append("** | **").Append(tOk)
              .Append("** | **").Append(tNo).Append("** | **").Append(tRet).Append("** |\n");
            return sb.ToString();
        }

        private static string LayerText(QaCase _c)
        {
            switch (_c.Layer)
            {
                case QaLayer.EditMode: return "EditMode";
                case QaLayer.Wesqa: return "wesqa";
                case QaLayer.Pytest: return "pytest";
                default: return "수동";
            }
        }

        private static string StatusText(QaStatus _s)
        {
            switch (_s)
            {
                case QaStatus.Implemented: return "구현됨";
                case QaStatus.Missing: return "**미구현**";
                default: return "폐기";
            }
        }

        private static string Attached(QaCase _c, SortedDictionary<string, List<string>> _attach)
        {
            List<string> files;
            if (_attach.TryGetValue(_c.Id, out files) && files.Count > 0)
                return "`" + string.Join("` · `", files.ToArray()) + "`";
            if (!_c.IsAuto && _c.Status == QaStatus.Implemented) return "(절차 — 이 문서 아래)";
            return "—";
        }

        private static string Reason(QaCase _c)
        {
            if (_c.Status == QaStatus.Retired) return "폐기 — " + _c.Note;
            if (_c.Note.Length == 0) return _c.Catches;
            return _c.Catches + "<br>※ " + _c.Note;
        }

        /// <summary>표 칸 안에서 마크다운을 안 깨뜨리게 다듬는다.</summary>
        private static string Cell(string _text)
        {
            return _text.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
        }

        /// <summary>디스크의 대장 전문(없으면 null). CRLF는 LF로 접는다.</summary>
        public static string ReadLedger()
        {
            string path = RepoRoot() + "/" + LEDGER_PATH;
            if (!File.Exists(path)) return null;
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        /// <summary>대장을 그 자리에 쓴다. 생성 메뉴와 드리프트 테스트가 같이 쓴다.</summary>
        public static void WriteLedger(string _text)
        {
            string path = RepoRoot() + "/" + LEDGER_PATH;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, _text, new UTF8Encoding(false));
        }
    }
}
