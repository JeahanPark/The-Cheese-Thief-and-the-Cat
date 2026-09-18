using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>소스에서 잘라 낸 메서드 한 개. <see cref="Body"/>는 주석·문자열을 지운 본문이다.</summary>
    internal sealed class CsMethod
    {
        public string File;
        public string Name;
        /// <summary>메서드 위의 속성 줄(원문) + 선언 줄 앞부분.</summary>
        public string Attributes;
        /// <summary>문서 주석(`///`) 원문.</summary>
        public string Doc;
        /// <summary>주석·문자열 리터럴을 공백으로 지운 본문(`{…}` 또는 `=> …;`).</summary>
        public string Body;
        /// <summary>같은 자리의 원문 본문.</summary>
        public string RawBody;
        public int Start;
        public int Line;
    }

    /// <summary>
    /// **테스트 도구 자신을 재는 테스트**(케이스 E-06·E-23·E-29·E-30·E-31·E-32)가 쓰는 소스 훑개.
    ///
    /// 컴파일러가 아니다 — 주석·문자열을 먼저 지운 뒤 중괄호를 맞춰 메서드를 자르는 정도다.
    /// 그래서 규칙은 **소스 모양**에 대해서만 말한다. 잘못 자를 수 있는 모양(문자열 속 중괄호 등)은
    /// 마스킹이 먼저 지우므로 흔한 경우는 다 견딘다.
    /// </summary>
    internal static class CsSourceScan
    {
        /// <summary>`Assets/Scripts/Tests` — 테스트 소스 뿌리.</summary>
        public static string TestsRoot => Application.dataPath + "/Scripts/Tests";

        /// <summary>`Assets/Scripts/Testing` — TestManager 훅 뿌리.</summary>
        public static string TestingRoot => Application.dataPath + "/Scripts/Testing";

        private static readonly HashSet<string> KEYWORDS = new HashSet<string>(StringComparer.Ordinal)
        {
            "if", "for", "foreach", "while", "switch", "using", "lock", "catch", "return", "new",
            "nameof", "typeof", "base", "this", "get", "set", "when", "sizeof", "default", "fixed",
            "checked", "unchecked", "else", "do", "try", "finally", "await", "throw",
        };

        private static readonly Regex METHOD = new Regex(
            @"\b(\w+)\s*(?:<[^()]*>)?\s*\(([^;{}()]*(?:\([^;{}()]*\)[^;{}()]*)*)\)\s*(\{|=>)",
            RegexOptions.Compiled);

        private static readonly Regex CLASS = new Regex(@"\b(?:class|struct)\s+(\w+)", RegexOptions.Compiled);

        private static readonly Regex TEST_ATTR = new Regex(
            @"\[\s*(?:NUnit\.Framework\.)?(?:Test|TestCase|TestCaseSource|UnityTest)\b", RegexOptions.Compiled);

        /// <summary>폴더 아래 `.cs` 전부(정렬). 폴더가 없으면 빈 목록.</summary>
        public static List<string> CsFiles(string _dir)
        {
            var list = new List<string>();
            if (!Directory.Exists(_dir)) return list;
            list.AddRange(Directory.GetFiles(_dir, "*.cs", SearchOption.AllDirectories));
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        public static string Read(string _path) => File.ReadAllText(_path);

        public static bool IsTest(CsMethod _m) => TEST_ATTR.IsMatch(_m.Attributes);

        /// <summary>
        /// 주석과 문자열·문자 리터럴을 공백으로 바꾼다. **길이와 줄바꿈은 그대로**라
        /// 마스킹한 글에서 찾은 자리를 원문에 그대로 쓸 수 있다.
        /// </summary>
        public static string Mask(string _text)
        {
            char[] outp = _text.ToCharArray();
            int n = _text.Length;
            int i = 0;
            while (i < n)
            {
                char c = _text[i];
                if (c == '/' && i + 1 < n && _text[i + 1] == '/')
                {
                    int j = _text.IndexOf('\n', i);
                    if (j < 0) j = n;
                    Blank(outp, i, j);
                    i = j;
                }
                else if (c == '/' && i + 1 < n && _text[i + 1] == '*')
                {
                    int j = _text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    j = j < 0 ? n : j + 2;
                    Blank(outp, i, j);
                    i = j;
                }
                else if (c == '\'')
                {
                    int j = i + 1;
                    while (j < n && _text[j] != '\'' && _text[j] != '\n') j += _text[j] == '\\' ? 2 : 1;
                    Blank(outp, i + 1, Math.Min(j, n));
                    i = j + 1;
                }
                else if (c == '"' || ((c == '@' || c == '$') && StartsString(_text, i)))
                {
                    int j = SkipString(_text, i);
                    Blank(outp, i, j);
                    i = j;
                }
                else
                {
                    i++;
                }
            }
            return new string(outp);
        }

        private static bool StartsString(string _t, int _i)
        {
            int k = _i;
            while (k < _t.Length && (_t[k] == '@' || _t[k] == '$') && k - _i < 2) k++;
            return k < _t.Length && _t[k] == '"' && k > _i;
        }

        private static void Blank(char[] _chars, int _from, int _to)
        {
            for (int k = _from; k < _to && k < _chars.Length; k++)
            {
                if (_chars[k] != '\n' && _chars[k] != '\r') _chars[k] = ' ';
            }
        }

        private static int SkipString(string _t, int _i)
        {
            int n = _t.Length;
            bool verbatim = false, interp = false;
            while (_i < n && (_t[_i] == '@' || _t[_i] == '$'))
            {
                if (_t[_i] == '@') verbatim = true; else interp = true;
                _i++;
            }
            _i++; // 여는 따옴표
            while (_i < n)
            {
                char c = _t[_i];
                if (verbatim && c == '"' && _i + 1 < n && _t[_i + 1] == '"') { _i += 2; continue; }
                if (!verbatim && c == '\\') { _i += 2; continue; }
                if (c == '"') return _i + 1;
                if (!verbatim && c == '\n') return _i;
                if (interp && c == '{')
                {
                    if (_i + 1 < n && _t[_i + 1] == '{') { _i += 2; continue; }
                    int depth = 1;
                    _i++;
                    while (_i < n && depth > 0)
                    {
                        char d = _t[_i];
                        if (d == '"' || ((d == '@' || d == '$') && StartsString(_t, _i)))
                        {
                            _i = SkipString(_t, _i);
                            continue;
                        }
                        if (d == '\'')
                        {
                            _i++;
                            while (_i < n && _t[_i] != '\'') _i += _t[_i] == '\\' ? 2 : 1;
                            _i++;
                            continue;
                        }
                        if (d == '{') depth++;
                        else if (d == '}') depth--;
                        _i++;
                    }
                    continue;
                }
                _i++;
            }
            return n;
        }

        /// <summary>`_open`의 `{`와 짝이 맞는 `}` 다음 자리.</summary>
        public static int MatchBrace(string _masked, int _open)
        {
            int depth = 0;
            for (int p = _open; p < _masked.Length; p++)
            {
                if (_masked[p] == '{') depth++;
                else if (_masked[p] == '}')
                {
                    depth--;
                    if (depth == 0) return p + 1;
                }
            }
            return _masked.Length;
        }

        /// <summary>메서드(지역 함수 포함)를 잘라 낸다. 원문과 마스킹본을 같이 받는다.</summary>
        public static List<CsMethod> Methods(string _file, string _raw, string _masked)
        {
            var result = new List<CsMethod>();
            foreach (Match mm in METHOD.Matches(_masked))
            {
                string name = mm.Groups[1].Value;
                if (KEYWORDS.Contains(name)) continue;

                int lineStart = _masked.LastIndexOf('\n', Math.Max(0, mm.Index - 1)) + 1;
                if (mm.Index == 0) lineStart = 0;
                string prefix = _masked.Substring(lineStart, mm.Index - lineStart).Trim();
                if (prefix.Length == 0) continue;
                char last = prefix[prefix.Length - 1];
                if (!(char.IsLetterOrDigit(last) || last == '_' || last == '>' || last == ']' || last == '?')) continue;
                if (EndsWithWord(prefix, "new") || EndsWithWord(prefix, "return") || EndsWithWord(prefix, "await")
                    || EndsWithWord(prefix, "else") || EndsWithWord(prefix, "throw")) continue;

                int bodyStart = mm.Index + mm.Length - (mm.Groups[3].Value == "{" ? 1 : 2);
                int end;
                if (mm.Groups[3].Value == "{")
                {
                    end = MatchBrace(_masked, bodyStart);
                }
                else
                {
                    int semi = _masked.IndexOf(';', bodyStart);
                    end = semi < 0 ? _masked.Length : semi + 1;
                }

                string rawPrefix = _raw.Substring(lineStart, mm.Index - lineStart);
                result.Add(new CsMethod
                {
                    File = _file,
                    Name = name,
                    Attributes = AttributesAbove(_raw, lineStart) + rawPrefix,
                    Doc = DocAbove(_raw, lineStart),
                    Body = _masked.Substring(bodyStart, end - bodyStart),
                    RawBody = _raw.Substring(bodyStart, end - bodyStart),
                    Start = mm.Index,
                    Line = LineOf(_raw, mm.Index),
                });
            }
            return result;
        }

        private static bool EndsWithWord(string _s, string _word)
        {
            if (!_s.EndsWith(_word, StringComparison.Ordinal)) return false;
            int k = _s.Length - _word.Length - 1;
            return k < 0 || !(char.IsLetterOrDigit(_s[k]) || _s[k] == '_' || _s[k] == '.');
        }

        public static int LineOf(string _text, int _index)
        {
            int line = 1;
            for (int k = 0; k < _index && k < _text.Length; k++) if (_text[k] == '\n') line++;
            return line;
        }

        private static string[] LinesBefore(string _raw, int _lineStart)
        {
            if (_lineStart <= 0) return new string[0];
            return _raw.Substring(0, _lineStart).Replace("\r", "").Split('\n');
        }

        /// <summary>선언 바로 위의 `[…]` 줄들(문서 주석·빈 주석 줄은 건너뛴다).</summary>
        public static string AttributesAbove(string _raw, int _lineStart)
        {
            string[] lines = LinesBefore(_raw, _lineStart);
            var sb = new StringBuilder();
            for (int k = lines.Length - 2; k >= 0; k--)
            {
                string s = lines[k].Trim();
                if (s.StartsWith("[", StringComparison.Ordinal)) sb.Insert(0, s + "\n");
                else if (s.StartsWith("//", StringComparison.Ordinal)) continue;
                else break;
            }
            return sb.ToString();
        }

        /// <summary>선언 위의 `///` 줄들(속성 줄은 건너뛴다).</summary>
        public static string DocAbove(string _raw, int _lineStart)
        {
            string[] lines = LinesBefore(_raw, _lineStart);
            var sb = new StringBuilder();
            for (int k = lines.Length - 2; k >= 0; k--)
            {
                string s = lines[k].Trim();
                if (s.StartsWith("///", StringComparison.Ordinal)) sb.Insert(0, s + "\n");
                else if (s.StartsWith("[", StringComparison.Ordinal)) continue;
                else break;
            }
            return sb.ToString();
        }

        /// <summary>클래스·구조체 이름 → (본문 시작, 끝, 선언 위 속성 원문).</summary>
        public static List<(string name, int start, int end, string attrs)> Classes(string _raw, string _masked)
        {
            var list = new List<(string, int, int, string)>();
            foreach (Match cm in CLASS.Matches(_masked))
            {
                int open = _masked.IndexOf('{', cm.Index);
                if (open < 0) continue;
                // 선언과 `{` 사이에 `;`가 있으면 선언이 아니다(예: `where T : class;`)
                if (_masked.IndexOf(';', cm.Index, open - cm.Index) >= 0) continue;
                int lineStart = cm.Index == 0 ? 0 : _masked.LastIndexOf('\n', cm.Index - 1) + 1;
                string attrs = AttributesAbove(_raw, lineStart) + _raw.Substring(lineStart, cm.Index - lineStart);
                list.Add((cm.Groups[1].Value, open, MatchBrace(_masked, open), attrs));
            }
            return list;
        }

        /// <summary>본문에서 `이름(` / `이름<…>(` 꼴로 불린 식별자들.</summary>
        public static HashSet<string> CalledNames(string _maskedBody)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in CALL.Matches(_maskedBody)) set.Add(m.Groups[1].Value);
            return set;
        }

        private static readonly Regex CALL = new Regex(@"\b([A-Za-z_]\w*)\s*(?:<[^<>()]*>)?\s*\(", RegexOptions.Compiled);

        /// <summary>테스트 소스 전체를 한 번에 — 파일 경로(`Assets/…` 상대) · 원문 · 마스킹본 · 메서드.</summary>
        public static List<(string rel, string raw, string masked, List<CsMethod> methods)> LoadTree(string _root)
        {
            var list = new List<(string, string, string, List<CsMethod>)>();
            string dataParent = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
            foreach (string path in CsFiles(_root))
            {
                string norm = path.Replace('\\', '/');
                string rel = norm.StartsWith(dataParent, StringComparison.Ordinal)
                    ? norm.Substring(dataParent.Length + 1)
                    : norm;
                string raw = Read(path);
                string masked = Mask(raw);
                list.Add((rel, raw, masked, Methods(rel, raw, masked)));
            }
            return list;
        }
    }
}
