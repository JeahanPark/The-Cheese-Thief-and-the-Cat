using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// **테스트 묶음 자신의 위생**(케이스 E-29·E-30·E-31·E-32). 스냅숏 목록이 아니라 **규칙**으로 잰다 —
    /// 새 테스트 파일이 들어와도 같은 규칙을 탄다.
    ///
    /// 각 규칙은 소스 모양만 본다(<see cref="CsSourceScan"/>). 그래서 규칙마다 **합성 소스로 규칙 자체가
    /// 살아 있는지**를 먼저 잰다 — 검사기가 죽어 있으면 이 파일은 영원히 초록이다.
    /// </summary>
    public class TestSuiteHygieneTests
    {
        // ───────────────────────────── E-29 어서트 없는 테스트 ─────────────────────────────

        private static readonly Regex ASSERT = new Regex(
            @"\b(?:Assert|CollectionAssert|StringAssert|FileAssert|DirectoryAssert|Assume|LogAssert)\s*\.",
            RegexOptions.Compiled);

        /// <summary>
        /// 어서트가 없어도 되는 테스트 — 이름 → 이유. **비워 두는 것이 기본이다.**
        /// 넣을 때는 「무엇이 실패를 만드는가」를 이유로 적는다(예: 예외가 곧 실패인 연기 테스트).
        /// </summary>
        private static readonly Dictionary<string, string> NO_ASSERT_ALLOWED = new Dictionary<string, string>();

        /// <summary>어서트에 닿지 않는 테스트 목록(파일:줄 이름). 도우미를 따라가 닿으면 통과다.</summary>
        internal static List<string> TestsWithoutAssert(IEnumerable<CsMethod> _all)
        {
            List<CsMethod> methods = _all.ToList();
            var asserting = new HashSet<string>(StringComparer.Ordinal);
            var calls = new Dictionary<CsMethod, HashSet<string>>();
            foreach (CsMethod m in methods)
            {
                calls[m] = CsSourceScan.CalledNames(m.Body);
                if (ASSERT.IsMatch(m.Body)) asserting.Add(m.Name);
            }

            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (CsMethod m in methods)
                {
                    if (asserting.Contains(m.Name)) continue;
                    if (!calls[m].Overlaps(asserting)) continue;
                    asserting.Add(m.Name);
                    changed = true;
                }
            }

            var bad = new List<string>();
            foreach (CsMethod m in methods)
            {
                if (!CsSourceScan.IsTest(m)) continue;
                if (ASSERT.IsMatch(m.Body) || calls[m].Overlaps(asserting)) continue;
                if (NO_ASSERT_ALLOWED.ContainsKey(m.Name)) continue;
                bad.Add($"{m.File}:{m.Line} {m.Name}");
            }
            return bad;
        }

        [NUnit.Framework.Property("CaseId", "E-29")]
        [Test]
        public void EveryTest_ReachesAnAssert()
        {
            var all = new List<CsMethod>();
            foreach (var file in CsSourceScan.LoadTree(CsSourceScan.TestsRoot)) all.AddRange(file.methods);
            Assert.Greater(all.Count(CsSourceScan.IsTest), 100, "테스트를 거의 못 찾았다 — 훑개가 죽었다");

            List<string> bad = TestsWithoutAssert(all);
            Assert.IsEmpty(bad, "어서트에 한 번도 닿지 않는 테스트(이름만 그럴듯하다):\n" + string.Join("\n", bad));
        }

        [NUnit.Framework.Property("CaseId", "E-29")]
        [Test]
        public void AssertScan_CatchesAnEmptyTest_AndFollowsHelpers()
        {
            const string SRC =
                "class T {\n" +
                "    [Test]\n    public void Empty() { int x = 1; x++; }\n" +
                "    [Test]\n    public void ViaHelper() { Check(3); }\n" +
                "    [NUnit.Framework.Test]\n    public void Direct() { Assert.AreEqual(1, 1); }\n" +
                "    private static void Check(int _v) { Inner(_v); }\n" +
                "    private static void Inner(int _v) { Assert.Greater(_v, 0); }\n" +
                "    [Test]\n    public void OnlyInComment() { // Assert.IsTrue(true);\n    }\n" +
                "}\n";
            List<string> bad = TestsWithoutAssert(Parse(SRC));

            Assert.AreEqual(2, bad.Count, string.Join("\n", bad));
            Assert.IsTrue(bad.Any(b => b.EndsWith(" Empty", StringComparison.Ordinal)));
            Assert.IsTrue(bad.Any(b => b.EndsWith(" OnlyInComment", StringComparison.Ordinal)), "주석 속 어서트는 어서트가 아니다");
        }

        // ───────────────────────────── E-30 에셋 없음 = 건너뛰기 금지 ─────────────────────────────

        private static readonly Regex SKIP_CALL = new Regex(
            @"\bAssert\s*\.\s*(?:Ignore|Inconclusive)\s*\(|\bAssume\s*\.", RegexOptions.Compiled);

        // `if (x == null) return;` · `if (!File.Exists(p)) { return; }` — 테스트 본문에서 조용히 빠져나가는 자리
        private static readonly Regex SILENT_RETURN = new Regex(
            @"\bif\s*\((?<c>[^;{}]*?(?:==\s*null|Exists\s*\(|Length\s*==\s*0|Count\s*==\s*0)[^;{}]*)\)\s*(?:\{\s*)?return\s*;",
            RegexOptions.Compiled);

        internal static List<string> SilentSkips(string _file, string _raw, string _masked, IEnumerable<CsMethod> _methods)
        {
            var bad = new List<string>();
            foreach (Match m in SKIP_CALL.Matches(_masked))
                bad.Add($"{_file}:{CsSourceScan.LineOf(_raw, m.Index)} {m.Value.Trim()}");
            foreach (CsMethod method in _methods)
            {
                if (!CsSourceScan.IsTest(method)) continue;
                foreach (Match m in SILENT_RETURN.Matches(method.Body))
                    bad.Add($"{_file}:{method.Line} {method.Name} — if ({m.Groups["c"].Value.Trim()}) return;");
            }
            return bad;
        }

        [NUnit.Framework.Property("CaseId", "E-30")]
        [Test]
        public void MissingAsset_FailsInsteadOfSkipping()
        {
            var bad = new List<string>();
            foreach (var file in CsSourceScan.LoadTree(CsSourceScan.TestsRoot))
                bad.AddRange(SilentSkips(file.rel, file.raw, file.masked, file.methods));

            Assert.IsEmpty(bad,
                "에셋·파일을 못 찾으면 건너뛰거나 조용히 return 한다 — 경로가 틀어지면 영영 초록이 된다. " +
                "Assert.IsNotNull / Assert.Fail로 바꾼다:\n" + string.Join("\n", bad));
        }

        [NUnit.Framework.Property("CaseId", "E-30")]
        [Test]
        public void SkipScan_CatchesIgnoreAssumeAndSilentReturn_NotLoopContinue()
        {
            const string SRC =
                "class T {\n" +
                "    [Test]\n    public void A() { var p = Load(); if (p == null) Assert.Ignore(\"없다\"); Assert.IsNotNull(p); }\n" +
                "    [Test]\n    public void B() { Assume.That(Exists()); Assert.Pass(); }\n" +
                "    [Test]\n    public void C() { var p = Load(); if (p == null) return; Assert.IsNotNull(p); }\n" +
                "    [Test]\n    public void D() { if (!System.IO.File.Exists(Path())) { return; } Assert.Pass(); }\n" +
                "    [Test]\n    public void E() { foreach (var x in Xs()) { if (x == null) continue; Assert.IsNotNull(x); } }\n" +
                "    [Test]\n    public void F() { string s = \"Assert.Ignore(\"; Assert.IsNotNull(s); }\n" +
                "}\n";
            string masked = CsSourceScan.Mask(SRC);
            List<string> bad = SilentSkips("T.cs", SRC, masked, CsSourceScan.Methods("T.cs", SRC, masked));

            Assert.AreEqual(4, bad.Count, string.Join("\n", bad));
            Assert.IsFalse(bad.Any(b => b.Contains(" E ")), "루프의 continue는 건너뛰기가 아니다");
        }

        // ───────────────────────────── E-31 결정성 = 같은 씨앗 + 다른 씨앗 ─────────────────────────────

        private static readonly Regex DETERMINISTIC_NAME = new Regex(@"Determin|SameSeed", RegexOptions.Compiled);
        private static readonly Regex DIFFER_NAME = new Regex(@"Differ|Vary|Varies", RegexOptions.Compiled);

        private static readonly Regex QUALIFIED_CALL = new Regex(
            @"\b([A-Z]\w*)\s*\.\s*([A-Z]\w*)\s*(?:<[^<>()]*>)?\s*\(", RegexOptions.Compiled);

        private static readonly Regex CTOR_CALL = new Regex(
            @"\bnew\s+([A-Z]\w*)\s*(?:<[^<>()]*>)?\s*\(", RegexOptions.Compiled);

        /// <summary>
        /// 본문이 부르는 **씨앗을 받는** 게임 코드 — `Type.Method` / `new Type` 꼴의 토큰.
        /// 씨앗을 받는다 = 매개변수 이름에 `seed`가 들었거나 `System.Random`을 받는다.
        /// 씨앗이 없는 순수 함수(방향 양자화 등)의 「같은 입력 → 같은 출력」은 이 규칙 대상이 아니다.
        /// </summary>
        internal static HashSet<string> SeededCalls(string _maskedBody, Func<string, IEnumerable<Type>> _resolve)
        {
            var tokens = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in QUALIFIED_CALL.Matches(_maskedBody))
            {
                string type = m.Groups[1].Value, method = m.Groups[2].Value;
                foreach (Type t in _resolve(type))
                {
                    const BindingFlags ALL = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                    if (t.GetMethods(ALL).Any(mi => mi.Name == method && TakesSeed(mi)))
                    {
                        tokens.Add(type + "." + method);
                        break;
                    }
                }
            }
            foreach (Match m in CTOR_CALL.Matches(_maskedBody))
            {
                string type = m.Groups[1].Value;
                foreach (Type t in _resolve(type))
                {
                    if (t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(TakesSeed))
                    {
                        tokens.Add("new " + type);
                        break;
                    }
                }
            }
            return tokens;
        }

        private static bool TakesSeed(MethodBase _m)
        {
            foreach (ParameterInfo p in _m.GetParameters())
            {
                if (p.Name != null && p.Name.IndexOf("seed", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (p.ParameterType == typeof(System.Random)) return true;
            }
            return false;
        }

        /// <summary>결정성 테스트 중 「다른 씨앗」 짝이 없는 것.</summary>
        internal static List<string> DeterminismWithoutVariation(IEnumerable<CsMethod> _all, Func<string, IEnumerable<Type>> _resolve)
        {
            List<CsMethod> tests = _all.Where(CsSourceScan.IsTest).ToList();
            var differTokens = new HashSet<string>(StringComparer.Ordinal);
            foreach (CsMethod t in tests)
            {
                if (DIFFER_NAME.IsMatch(t.Name)) differTokens.UnionWith(SeededCalls(t.Body, _resolve));
            }

            var bad = new List<string>();
            foreach (CsMethod t in tests)
            {
                if (!DETERMINISTIC_NAME.IsMatch(t.Name)) continue;
                HashSet<string> seeded = SeededCalls(t.Body, _resolve);
                if (seeded.Count == 0) continue;
                if (seeded.Overlaps(differTokens)) continue;
                bad.Add($"{t.File}:{t.Line} {t.Name} — 「다른 씨앗 → 다른 결과」 짝이 없다: {string.Join(", ", seeded)}");
            }
            return bad;
        }

        private static Dictionary<string, List<Type>> s_GameTypes;

        /// <summary>게임 어셈블리(`CTC.Runtime`·`CTC.Editor`)의 짧은 이름 → 타입.</summary>
        private static IEnumerable<Type> ResolveGameType(string _shortName)
        {
            if (s_GameTypes == null)
            {
                s_GameTypes = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string name = asm.GetName().Name;
                    if (name != "CTC.Runtime" && name != "CTC.Editor") continue;
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); }
                    foreach (Type t in types)
                    {
                        if (!s_GameTypes.TryGetValue(t.Name, out List<Type> list)) s_GameTypes[t.Name] = list = new List<Type>();
                        list.Add(t);
                    }
                }
            }
            return s_GameTypes.TryGetValue(_shortName, out List<Type> found) ? found : Enumerable.Empty<Type>();
        }

        [NUnit.Framework.Property("CaseId", "E-31")]
        [Test]
        public void EverySeededDeterminismTest_HasADifferentSeedPartner()
        {
            var all = new List<CsMethod>();
            foreach (var file in CsSourceScan.LoadTree(CsSourceScan.TestsRoot)) all.AddRange(file.methods);
            Assert.IsTrue(ResolveGameType("MatchRandom").Any(), "게임 어셈블리를 못 찾았다 — 규칙이 전부 면제로 빠진다");

            List<string> bad = DeterminismWithoutVariation(all, ResolveGameType);
            Assert.IsEmpty(bad,
                "같은 씨앗 두 번만 부르면 씨앗이 결과에 안 닿아도 통과한다. 이름에 Differ가 든 짝 테스트가 " +
                "같은 함수를 **다른 씨앗**으로 불러야 한다:\n" + string.Join("\n", bad));
        }

        [NUnit.Framework.Property("CaseId", "E-31")]
        [Test]
        public void DeterminismScan_NeedsPartnerOnlyForSeededCalls()
        {
            const string SRC =
                "class T {\n" +
                "    [Test]\n    public void Pick_SameSeed_SameResult() { Assert.AreEqual(Fake.Pick(1, 7), Fake.Pick(1, 7)); }\n" +
                "    [Test]\n    public void Roll_IsDeterministic() { Assert.AreEqual(Fake.Roll(7), Fake.Roll(7)); }\n" +
                "    [Test]\n    public void Roll_DifferentSeeds_Differ() { Assert.AreNotEqual(Fake.Roll(1), Fake.Roll(2)); }\n" +
                "    [Test]\n    public void Pack_IsDeterministic() { Assert.AreEqual(Fake.Pack(3f), Fake.Pack(3f)); }\n" +
                "}\n";
            string masked = CsSourceScan.Mask(SRC);
            List<string> bad = DeterminismWithoutVariation(
                CsSourceScan.Methods("T.cs", SRC, masked),
                n => n == "Fake" ? new[] { typeof(FakeSeeded) } : Enumerable.Empty<Type>());

            Assert.AreEqual(1, bad.Count, string.Join("\n", bad));
            StringAssert.Contains("Pick_SameSeed_SameResult", bad[0]);
        }

        private static class FakeSeeded
        {
            public static int Pick(int _slot, int _seed) => _slot ^ _seed;
            public static int Roll(int _seed) => _seed * 31;
            public static int Pack(float _value) => (int)_value;
        }

        // ───────────────────────────── E-32 에셋 테스트 범주 ─────────────────────────────

        /// <summary>테스트가 에셋·셰이더·씬을 만진다고 보는 코드 모양(주석·문자열 제외).</summary>
        private static readonly Regex ASSET_CODE = new Regex(
            @"\bAssetDatabase\s*\.|\bShader\s*\.\s*Find\s*\(|\bEditorSceneManager\s*\.|\bPrefabUtility\s*\." +
            @"|\bResources\s*\.\s*Load|\bAssetImporter\b|\bModelImporter\b",
            RegexOptions.Compiled);

        /// <summary>에셋 경로 문자열(확장자로 판정 — 원문에서 본다).</summary>
        private static readonly Regex ASSET_LITERAL = new Regex(
            "\"[^\"\\n]*\\.(?:prefab|unity|asset|mat|fbx|FBX|controller|shader|anim)\"", RegexOptions.Compiled);

        private static readonly Regex ASSET_CATEGORY = new Regex(
            @"\bCategory\s*\(\s*""Asset""\s*\)", RegexOptions.Compiled);

        private static readonly Regex SETUP_ATTR = new Regex(
            @"\[\s*(?:NUnit\.Framework\.)?(?:SetUp|OneTimeSetUp|UnitySetUp)\b", RegexOptions.Compiled);

        private static readonly Regex FIELD_INIT = new Regex(
            @"\b(?:const|static\s+readonly)\s+[\w\[\]<>.,\s]+?\s+(\w+)\s*=\s*(?<init>[^;]*);", RegexOptions.Compiled);

        /// <summary>`[Category("Asset")]` 없이 에셋을 만지는 테스트(같은 파일의 도우미·상수를 따라간다).</summary>
        internal static List<string> UncategorizedAssetTests(string _file, string _raw, string _masked, List<CsMethod> _methods)
        {
            var bad = new List<string>();
            if (!_methods.Any(CsSourceScan.IsTest)) return bad;

            var touching = new HashSet<string>(StringComparer.Ordinal);
            foreach (CsMethod m in _methods)
            {
                if (ASSET_CODE.IsMatch(m.Body) || ASSET_LITERAL.IsMatch(m.RawBody)) touching.Add(m.Name);
            }
            foreach (Match f in FIELD_INIT.Matches(_raw))
            {
                // 초기화 식의 원문 — 경로 상수는 문자열로 들어 있다
                string init = f.Groups["init"].Value;
                string maskedInit = _masked.Substring(f.Groups["init"].Index, f.Groups["init"].Length);
                if (_masked.Substring(f.Index, 5).Trim().Length == 0) continue; // 주석·문자열 속
                if (ASSET_LITERAL.IsMatch(init) || ASSET_CODE.IsMatch(maskedInit)) touching.Add(f.Groups[1].Value);
            }

            var refs = new Dictionary<CsMethod, HashSet<string>>();
            foreach (CsMethod m in _methods) refs[m] = Identifiers(m.Body);

            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (CsMethod m in _methods)
                {
                    if (touching.Contains(m.Name) || !refs[m].Overlaps(touching)) continue;
                    touching.Add(m.Name);
                    changed = true;
                }
            }

            var classes = CsSourceScan.Classes(_raw, _masked);

            // [SetUp]·[OneTimeSetUp]이 에셋을 읽으면 그 클래스의 테스트는 전부 에셋 위에서 돈다
            var setupTouched = classes
                .Where(c => _methods.Any(m => SETUP_ATTR.IsMatch(m.Attributes) && touching.Contains(m.Name)
                                              && c.start <= m.Start && m.Start < c.end))
                .ToList();

            foreach (CsMethod m in _methods)
            {
                if (!CsSourceScan.IsTest(m)) continue;
                bool touches = ASSET_CODE.IsMatch(m.Body) || ASSET_LITERAL.IsMatch(m.RawBody)
                               || refs[m].Any(id => id != m.Name && touching.Contains(id))
                               || setupTouched.Any(c => c.start <= m.Start && m.Start < c.end);
                if (!touches) continue;
                if (ASSET_CATEGORY.IsMatch(m.Attributes)) continue;
                if (classes.Any(c => c.start <= m.Start && m.Start < c.end && ASSET_CATEGORY.IsMatch(c.attrs))) continue;
                bad.Add($"{_file}:{m.Line} {m.Name}");
            }
            return bad;
        }

        private static readonly Regex IDENT = new Regex(@"\b[A-Za-z_]\w*\b", RegexOptions.Compiled);

        private static HashSet<string> Identifiers(string _maskedBody)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in IDENT.Matches(_maskedBody)) set.Add(m.Value);
            return set;
        }

        [NUnit.Framework.Property("CaseId", "E-32")]
        [Test]
        public void AssetTouchingTests_CarryAssetCategory()
        {
            var bad = new List<string>();
            foreach (var file in CsSourceScan.LoadTree(CsSourceScan.TestsRoot))
                bad.AddRange(UncategorizedAssetTests(file.rel, file.raw, file.masked, file.methods));

            Assert.IsEmpty(bad,
                "에셋·셰이더·씬·Resources를 만지는 테스트는 [NUnit.Framework.Category(\"Asset\")]를 단다 " +
                "(클래스 전체면 클래스에). 에셋이 움직였을 때 논리 회귀와 갈라 보려고:\n" + string.Join("\n", bad));
        }

        [NUnit.Framework.Property("CaseId", "E-32")]
        [Test]
        public void AssetCategoryScan_FollowsHelpersAndConstants()
        {
            // 확장자는 붙여 만든다 — 이 파일의 원문에 경로 문자열이 그대로 있으면 이 테스트 자신이 「에셋 테스트」가 된다.
            string ext = "pre" + "fab";
            string src =
                "class T {\n" +
                "    private const string PATH = \"Assets/X." + ext + "\";\n" +
                "    [Test]\n    public void Logic() { Assert.AreEqual(2, 1 + 1); }\n" +
                "    [Test]\n    public void ViaConst() { Assert.IsTrue(PATH.Length > 0); }\n" +
                "    [Test]\n    public void ViaHelper() { Assert.IsNotNull(Load()); }\n" +
                "    [NUnit.Framework.Category(\"Asset\")]\n    [Test]\n    public void Tagged() { Assert.IsNotNull(Load()); }\n" +
                "    private static object Load() => AssetDatabase.LoadMainAssetAtPath(\"x\");\n" +
                "}\n" +
                "[NUnit.Framework.Category(\"Asset\")]\n" +
                "class U {\n" +
                "    [Test]\n    public void InTaggedClass() { Assert.IsNotNull(Shader.Find(\"s\")); }\n" +
                "}\n";
            string masked = CsSourceScan.Mask(src);
            List<string> bad = UncategorizedAssetTests("T.cs", src, masked, CsSourceScan.Methods("T.cs", src, masked));

            var names = bad.Select(b => b.Substring(b.LastIndexOf(' ') + 1)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(new[] { "ViaConst", "ViaHelper" }, names, string.Join("\n", bad));
        }

        // ───────────────────────────── 공용 ─────────────────────────────

        private static List<CsMethod> Parse(string _src)
        {
            string masked = CsSourceScan.Mask(_src);
            return CsSourceScan.Methods("T.cs", _src, masked);
        }
    }
}
