using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Testing;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// **QA 훅(<see cref="TestManager"/>) 자신의 계약**(케이스 E-06·E-16·E-23).
    ///
    /// 훅은 wesqa가 이름으로 부르는 리플렉션 입구라 컴파일러가 거의 아무것도 안 잡아 준다.
    /// 「못 찾음」이 정상값으로 읽히거나(E-06), 훅이 게임과 다른 분모를 쓰거나(E-16),
    /// 리플렉션으로 잡는 필드 이름이 바뀌어도(E-23) **전부 에러 없이** 틀린 값이 된다.
    /// </summary>
    public class TestHookContractTests
    {
        // ───────────────────────────── E-06 「못 찾음」 센티널 ─────────────────────────────

        private static readonly Regex NUMERIC_GETTER_DECL = new Regex(
            @"\bpublic\s+(?:int|float|long|double)\s*$", RegexOptions.Compiled);

        private static readonly Regex MENTIONS_MINUS_ONE = new Regex(@"(?<![\w.])-1(?!\d|\.\d)", RegexOptions.Compiled);

        private static readonly Regex RETURNS_SENTINEL = new Regex(
            @"-\s*1(?:[fFlLdD])?\b|\bNaN\b", RegexOptions.Compiled);

        // `x != null ? x.Value : 0;` · `if (x == null) return 0;` — 대상을 못 찾았을 때 0으로 답하는 자리
        private static readonly Regex ZERO_ON_MISSING = new Regex(
            @"\b[\w.]+\s*!=\s*null\s*\?[^;:]*:\s*0(?:[fFlLdD])?\s*;" +
            @"|\bif\s*\(\s*[\w.]+\s*==\s*null\s*\)\s*(?:\{[^{}]*?)?return\s+0(?:[fFlLdD])?\s*;",
            RegexOptions.Compiled);

        /// <summary>
        /// 못 찾음을 0으로 답해도 되는 훅 — 이름 → 이유. **0이 정상값으로 나올 수 없는** 경우만 넣는다.
        /// </summary>
        private static readonly Dictionary<string, string> ZERO_SENTINEL_ALLOWED = new Dictionary<string, string>
        {
            { "TestGetMatchSeed", "시드 0은 「아직 안 굴렸다」로 문서화돼 있고 MatchRandom.NextSeed는 0을 안 낸다" },
            { "TestGetMatchSubSeed", "MatchRandom.SubSeed는 0 대신 SEED_FALLBACK을 낸다 — 0은 정상값이 아니다" },
            { "TestGetNavMeshPolygons", "구운 폴리곤이 없는 것과 삼각분할이 비어 있는 것은 같은 사실(0 = 길찾기 사망)이다" },
        };

        /// <summary>숫자 훅의 센티널 위반(파일:줄 이름 — 이유).</summary>
        internal static List<string> SentinelViolations(IEnumerable<CsMethod> _methods)
        {
            var bad = new List<string>();
            foreach (CsMethod m in _methods)
            {
                if (!m.Name.StartsWith("TestGet", StringComparison.Ordinal)) continue;
                string declLine = m.Attributes.Replace("\r", "").Split('\n').Last();
                if (!NUMERIC_GETTER_DECL.IsMatch(declLine)) continue;

                // ① 문서가 -1을 약속하면 본문에 실제로 -1(또는 NaN)이 있다 — 문서와 코드가 어긋나면 파이썬이 틀린 걸 믿는다
                if (MENTIONS_MINUS_ONE.IsMatch(m.Doc) && !RETURNS_SENTINEL.IsMatch(m.Body))
                    bad.Add($"{m.File}:{m.Line} {m.Name} — 문서는 -1을 말하는데 본문에 -1이 없다");

                // ② 대상을 못 찾았을 때 0으로 답하지 않는다 — 0은 대개 「세어 봤더니 없다」라는 정상값이다
                if (ZERO_ON_MISSING.IsMatch(m.Body) && !ZERO_SENTINEL_ALLOWED.ContainsKey(m.Name))
                    bad.Add($"{m.File}:{m.Line} {m.Name} — 못 찾으면 0을 준다(정상값과 겹친다): " +
                            ZERO_ON_MISSING.Match(m.Body).Value.Trim());
            }
            return bad;
        }

        [NUnit.Framework.Property("CaseId", "E-06")]
        [Test]
        public void NumericHooks_NotFoundSentinel_DoesNotOverlapNormalValues()
        {
            var hooks = new List<CsMethod>();
            foreach (var file in CsSourceScan.LoadTree(CsSourceScan.TestingRoot))
            {
                if (!file.rel.Contains("TestManager")) continue;
                hooks.AddRange(file.methods);
            }
            Assert.Greater(hooks.Count(h => h.Name.StartsWith("TestGet", StringComparison.Ordinal)), 30,
                "훅을 거의 못 찾았다 — 훑개가 죽었다");

            List<string> bad = SentinelViolations(hooks);
            Assert.IsEmpty(bad,
                "「못 찾음」이 정상값으로 읽히는 숫자 훅(계측기 없음은 -1 — 감사 T9 규약):\n" + string.Join("\n", bad));
        }

        [NUnit.Framework.Property("CaseId", "E-06")]
        [Test]
        public void SentinelScan_CatchesZeroFallback_AndDocCodeMismatch()
        {
            const string SRC =
                "partial class TestManager {\n" +
                "    /// <summary>계측기가 없으면 -1.</summary>\n" +
                "    public int TestGetGood() { var m = Find(); if (m == null) return -1; return m.Count; }\n" +
                "    /// <summary>계측기가 없으면 -1.</summary>\n" +
                "    public int TestGetDocLies() { var m = Find(); return m != null ? m.Count : 99; }\n" +
                "    /// <summary>지휘 명령 수.</summary>\n" +
                "    public int TestGetZero() { var m = Find(); return m != null ? m.Count : 0; }\n" +
                "    public float TestGetZeroIf() { var m = Find(); if (m == null) return 0f; return m.Value; }\n" +
                "    public int TestGetLiteralZeroIsFine() => 0;\n" +
                "    public bool TestGetBoolIsOutOfScope() { var m = Find(); return m != null && m.On; }\n" +
                "}\n";
            string masked = CsSourceScan.Mask(SRC);
            List<string> bad = SentinelViolations(CsSourceScan.Methods("T.cs", SRC, masked));

            Assert.AreEqual(3, bad.Count, string.Join("\n", bad));
            Assert.IsTrue(bad.Any(b => b.Contains("TestGetDocLies")));
            Assert.IsTrue(bad.Any(b => b.Contains("TestGetZero ")));
            Assert.IsTrue(bad.Any(b => b.Contains("TestGetZeroIf")));
        }

        // ───────────────────────────── E-16 치즈 총량의 분모는 하나 ─────────────────────────────

        private static readonly MethodInfo COUNT_SPAWNED =
            typeof(CheeseSpotDirector).GetMethod(nameof(CheeseSpotDirector.CountSpawnedMice), BindingFlags.Public | BindingFlags.Static);

        [NUnit.Framework.Property("CaseId", "E-16")]
        [Test]
        public void Game_WritesDesignedMiceCount_OnlyFromCountSpawnedMice()
        {
            Assert.IsNotNull(COUNT_SPAWNED);
            MethodInfo setter = typeof(CheeseSpotDirector)
                .GetProperty(nameof(CheeseSpotDirector.DesignedMiceCount))?.GetSetMethod(true);
            Assert.IsNotNull(setter, "DesignedMiceCount 세터가 없다");

            int writers = 0;
            foreach (MethodInfo m in DeclaredMethods(typeof(CheeseSpotDirector)))
            {
                if (SameMethod.Instance.Equals(m, setter)) continue;
                HashSet<MethodBase> calls = IlCalls(m);
                if (!calls.Contains(setter)) continue;

                writers++;
                Assert.IsTrue(calls.Contains(COUNT_SPAWNED),
                    $"{m.Name}이 DesignedMiceCount를 CountSpawnedMice 없이 쓴다 — 게임 분모가 둘이 된다");
                Assert.IsTrue(calls.Any(c => c.DeclaringType == typeof(CheeseSpotLogic) && c.Name == nameof(CheeseSpotLogic.EffectiveMiceCount)),
                    $"{m.Name}이 권장 인원 하한(EffectiveMiceCount)을 안 탄다 — 훅과 식이 갈린다");
            }
            Assert.Greater(writers, 0, "매장량을 까는 자리를 못 찾았다 — IL 판독기가 죽었거나 구조가 바뀌었다");
        }

        [NUnit.Framework.Property("CaseId", "E-16")]
        [TestCase(nameof(TestManager.TestGetMapTotalCheese))]
        [TestCase(nameof(TestManager.TestGetDesignedSpotReserve))]
        [TestCase(nameof(TestManager.TestGetDesignedMouseCount))]
        [TestCase(nameof(TestManager.TestGetSpawnedMouseCount))]
        public void Hook_ReachesTheGameDenominator_NeverTheLobbyHeadcount(string _hook)
        {
            MethodInfo hook = typeof(TestManager).GetMethod(_hook, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(hook, _hook + " 훅이 없다");

            HashSet<MethodBase> reached = Reach(hook, t => t == typeof(TestManager));
            MethodInfo designedGetter = typeof(CheeseSpotDirector)
                .GetProperty(nameof(CheeseSpotDirector.DesignedMiceCount))?.GetGetMethod();

            Assert.IsTrue(reached.Contains(COUNT_SPAWNED) || reached.Contains(designedGetter),
                $"{_hook}이 게임의 분모(CountSpawnedMice / DesignedMiceCount)에 닿지 않는다");

            MethodInfo lobbyHeadcount = typeof(LobbyController).GetProperty(nameof(LobbyController.MouseCount))?.GetGetMethod();
            Assert.IsNotNull(lobbyHeadcount);
            Assert.IsFalse(reached.Contains(lobbyHeadcount),
                $"{_hook}이 로비 사람 수(LobbyController.MouseCount)를 분모로 쓴다 — 봇 판에서 최대 여덟 배 어긋난다(감사 T7)");
        }

        [NUnit.Framework.Property("CaseId", "E-16")]
        [Test]
        public void HookAndGame_ShareTheReserveFormula()
        {
            MethodInfo hook = typeof(TestManager).GetMethod(nameof(TestManager.TestGetDesignedSpotReserve));
            HashSet<MethodBase> hookCalls = Reach(hook, t => t == typeof(TestManager));

            var gameCalls = new HashSet<MethodBase>();
            foreach (MethodInfo m in DeclaredMethods(typeof(CheeseSpotDirector)))
            {
                HashSet<MethodBase> calls = IlCalls(m);
                if (calls.Contains(COUNT_SPAWNED)) gameCalls.UnionWith(calls);
            }

            foreach (string formula in new[] { nameof(CheeseSpotLogic.ReservePerSpot), nameof(CheeseSpotLogic.EffectiveMiceCount) })
            {
                Assert.IsTrue(gameCalls.Any(c => c.DeclaringType == typeof(CheeseSpotLogic) && c.Name == formula),
                    $"게임이 CheeseSpotLogic.{formula}를 안 부른다");
                Assert.IsTrue(hookCalls.Any(c => c.DeclaringType == typeof(CheeseSpotLogic) && c.Name == formula),
                    $"훅이 CheeseSpotLogic.{formula}를 안 부른다 — 식을 따로 계산하고 있다");
            }
        }

        [NUnit.Framework.Property("CaseId", "E-16")]
        [Test]
        public void IlReader_SeesADirectCall()
        {
            // 판독기 자체가 죽으면 위 테스트의 「안 부른다」가 전부 거짓 양성이 되고 「닿지 않는다」는 영원히 통과한다.
            MethodInfo hook = typeof(TestManager).GetMethod(nameof(TestManager.TestGetSpawnedMouseCount));
            HashSet<MethodBase> calls = IlCalls(hook);
            Assert.IsTrue(calls.Contains(COUNT_SPAWNED), "IL 판독기가 직접 호출을 못 본다");
            Assert.IsFalse(calls.Contains(
                typeof(LobbyController).GetProperty(nameof(LobbyController.MouseCount)).GetGetMethod()));
        }

        private static IEnumerable<MethodInfo> DeclaredMethods(Type _type)
        {
            return _type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                    | BindingFlags.Static | BindingFlags.DeclaredOnly);
        }

        /// <summary>시작 메서드에서 IL 호출을 따라간다 — `_follow`가 허락한 타입의 메서드 안으로만 들어간다.</summary>
        private static HashSet<MethodBase> Reach(MethodBase _start, Func<Type, bool> _follow)
        {
            var seen = new HashSet<MethodBase>(SameMethod.Instance);
            var stack = new Stack<MethodBase>();
            stack.Push(_start);
            while (stack.Count > 0)
            {
                MethodBase m = stack.Pop();
                foreach (MethodBase callee in IlCalls(m))
                {
                    if (!seen.Add(callee)) continue;
                    Type owner = callee.DeclaringType;
                    while (owner != null && owner.IsNested && owner.Name.Contains("<")) owner = owner.DeclaringType;
                    if (owner != null && _follow(owner)) stack.Push(callee);
                }
            }
            return seen;
        }

        /// <summary>
        /// 같은 메서드 = 같은 모듈의 같은 메타데이터 토큰. `GetProperty(...).GetGetMethod()`와 IL에서 푼 메서드는
        /// ReflectedType이 달라 기본 동등성으로는 다른 것으로 나올 수 있다.
        /// </summary>
        private sealed class SameMethod : IEqualityComparer<MethodBase>
        {
            public static readonly SameMethod Instance = new SameMethod();

            public bool Equals(MethodBase _a, MethodBase _b)
            {
                if (ReferenceEquals(_a, _b)) return true;
                if (_a == null || _b == null) return false;
                return _a.Module == _b.Module && _a.MetadataToken == _b.MetadataToken;
            }

            public int GetHashCode(MethodBase _m) => _m == null ? 0 : _m.MetadataToken;
        }

        private static Dictionary<short, OpCode> s_OpCodes;

        /// <summary>메서드 본문의 IL에서 `call`·`callvirt`·`newobj`·`ldftn` 대상을 뽑는다.</summary>
        internal static HashSet<MethodBase> IlCalls(MethodBase _method)
        {
            var result = new HashSet<MethodBase>(SameMethod.Instance);
            MethodBody body = _method.GetMethodBody();
            if (body == null) return result;
            byte[] il = body.GetILAsByteArray();
            if (il == null) return result;

            if (s_OpCodes == null)
            {
                s_OpCodes = new Dictionary<short, OpCode>();
                foreach (FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (f.GetValue(null) is OpCode op) s_OpCodes[op.Value] = op;
                }
            }

            Type[] typeArgs = _method.DeclaringType != null && _method.DeclaringType.IsGenericType
                ? _method.DeclaringType.GetGenericArguments() : null;
            Type[] methodArgs = _method.IsGenericMethod ? _method.GetGenericArguments() : null;

            int i = 0;
            while (i < il.Length)
            {
                short value = il[i++];
                if (value == 0xFE && i < il.Length) value = unchecked((short)(0xFE00 | il[i++]));
                if (!s_OpCodes.TryGetValue(value, out OpCode op)) break;   // 모르는 opcode — 더 읽으면 어긋난다

                switch (op.OperandType)
                {
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        i += 1;
                        break;
                    case OperandType.InlineVar:
                        i += 2;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        i += 8;
                        break;
                    case OperandType.InlineSwitch:
                        int count = BitConverter.ToInt32(il, i);
                        i += 4 + 4 * count;
                        break;
                    case OperandType.InlineMethod:
                        int token = BitConverter.ToInt32(il, i);
                        i += 4;
                        try
                        {
                            MethodBase target = _method.Module.ResolveMethod(token, typeArgs, methodArgs);
                            if (target != null) result.Add(target);
                        }
                        catch (ArgumentException) { }
                        break;
                    default:
                        i += 4;
                        break;
                }
            }
            return result;
        }

        // ───────────────────────────── E-23 리플렉션 이름 실재 ─────────────────────────────

        private enum MemberKind { Field, Property, Method }

        /// <summary>
        /// `typeof(X).GetField("…")`처럼 **소스에서 타입을 못 읽는** 자리 — (파일 이름, 멤버 이름) → 타입 전체 이름.
        /// 새 자리가 생기면 여기에 넣으라고 검사가 실패한다(분류 안 된 자리는 통과가 아니다).
        /// </summary>
        private static readonly Dictionary<(string file, string member), string> DYNAMIC_RECEIVERS =
            new Dictionary<(string, string), string>
            {
                // 인게임 콘솔(yasirkula) — 타입 이름으로 찾아 들어간다(TestGetConsoleState · TestOpenConsole)
                { ("TestManager.View.cs", "toggleWithKey"), "IngameDebugConsole.DebugLogManager" },
                { ("TestManager.View.cs", "toggleBinding"), "IngameDebugConsole.DebugLogManager" },
                { ("TestManager.View.cs", "IsLogWindowVisible"), "IngameDebugConsole.DebugLogManager" },
                { ("TestManager.View.cs", "ShowLogWindow"), "IngameDebugConsole.DebugLogManager" },
            };

        private static readonly Regex REFLECT_SITE = new Regex(@"\.\s*Get(Field|Property|Method)\s*\(", RegexOptions.Compiled);
        private static readonly Regex HUD_TEXT_SITE = new Regex(@"\bHudText\s*\(", RegexOptions.Compiled);
        private static readonly Regex TYPEOF_TAIL = new Regex(@"typeof\s*\(\s*([\w.]+)\s*\)\s*$", RegexOptions.Compiled);
        private static readonly Regex STRING_LITERAL = new Regex(@"^\s*""([^""\\]*)""\s*$", RegexOptions.Compiled);
        private static readonly Regex INDEXED_IDENT = new Regex(@"^\s*(\w+)\s*\[[^\]]*\]\s*$", RegexOptions.Compiled);
        private static readonly Regex FIND_TYPE = new Regex(@"Find(?:First|Any)ObjectByType\s*<\s*([\w.]+)\s*>", RegexOptions.Compiled);

        /// <summary>리플렉션으로 잡는 이름 중 실재하지 않거나 분류 안 된 것.</summary>
        internal static List<string> ReflectionNameViolations(string _file, string _raw, string _masked,
                                                             Func<string, IEnumerable<Type>> _resolve)
        {
            var bad = new List<string>();
            string fileName = System.IO.Path.GetFileName(_file);

            foreach (Match site in REFLECT_SITE.Matches(_masked))
            {
                var kind = (MemberKind)Enum.Parse(typeof(MemberKind), site.Groups[1].Value);
                int open = site.Index + site.Length - 1;
                List<string> args = SplitArgs(_raw, _masked, open);
                if (args.Count == 0) continue;
                List<string> names = LiteralNames(args[0], _raw, _masked, site.Index);
                if (names.Count == 0) continue;   // 인자로 받은 이름(_key 등) — 호출하는 쪽이 책임진다

                int line = CsSourceScan.LineOf(_raw, site.Index);
                string receiver = _masked.Substring(0, site.Index);
                Match typeOf = TYPEOF_TAIL.Match(receiver);
                foreach (string name in names)
                {
                    string typeName = typeOf.Success ? typeOf.Groups[1].Value
                        : DYNAMIC_RECEIVERS.TryGetValue((fileName, name), out string known) ? known : null;
                    if (typeName == null)
                    {
                        bad.Add($"{_file}:{line} Get{kind}(\"{name}\") — 받는 타입을 소스에서 못 읽는다. DYNAMIC_RECEIVERS에 등록한다");
                        continue;
                    }
                    string problem = CheckMember(typeName, name, kind, _resolve);
                    if (problem != null) bad.Add($"{_file}:{line} {problem}");
                }
            }

            foreach (Match site in HUD_TEXT_SITE.Matches(_masked))
            {
                int open = site.Index + site.Length - 1;
                List<string> args = SplitArgs(_raw, _masked, open);
                if (args.Count < 2) continue;
                Match lit = STRING_LITERAL.Match(args[1]);
                if (!lit.Success) continue;   // 정의부(`string _field`)

                int line = CsSourceScan.LineOf(_raw, site.Index);
                string typeName = null;
                Match inline = FIND_TYPE.Match(args[0]);
                if (inline.Success) typeName = inline.Groups[1].Value;
                else
                {
                    string ident = args[0].Trim();
                    MatchCollection decls = Regex.Matches(_raw.Substring(0, site.Index),
                        @"\b" + Regex.Escape(ident) + @"\s*=\s*Find(?:First|Any)ObjectByType\s*<\s*([\w.]+)\s*>");
                    if (decls.Count > 0) typeName = decls[decls.Count - 1].Groups[1].Value;
                }
                if (typeName == null)
                {
                    bad.Add($"{_file}:{line} HudText({args[0].Trim()}, \"{lit.Groups[1].Value}\") — 대상 타입을 못 읽는다");
                    continue;
                }
                string problem = CheckMember(typeName, lit.Groups[1].Value, MemberKind.Field, _resolve);
                if (problem != null) bad.Add($"{_file}:{line} {problem}");
            }
            return bad;
        }

        private static List<string> LiteralNames(string _arg, string _raw, string _masked, int _siteIndex)
        {
            var names = new List<string>();
            Match lit = STRING_LITERAL.Match(_arg);
            if (lit.Success)
            {
                names.Add(lit.Groups[1].Value);
                return names;
            }
            // `fields[f]` — 같은 파일 앞쪽의 `fields = { "a", "b" }`
            Match indexed = INDEXED_IDENT.Match(_arg);
            if (!indexed.Success) return names;
            MatchCollection decls = Regex.Matches(_raw.Substring(0, _siteIndex),
                @"\b" + Regex.Escape(indexed.Groups[1].Value) + @"\s*=\s*(?:new\s*(?:string)?\s*\[\s*\]\s*)?\{(?<items>[^{}]*)\}");
            if (decls.Count == 0) return names;
            foreach (Match s in Regex.Matches(decls[decls.Count - 1].Groups["items"].Value, @"""([^""\\]*)"""))
                names.Add(s.Groups[1].Value);
            return names;
        }

        /// <summary>`(` 자리부터 최상위 쉼표로 인자를 자른다(원문 조각).</summary>
        private static List<string> SplitArgs(string _raw, string _masked, int _open)
        {
            var args = new List<string>();
            int depth = 0, start = _open + 1;
            for (int p = _open; p < _masked.Length; p++)
            {
                char c = _masked[p];
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        args.Add(_raw.Substring(start, p - start));
                        return args;
                    }
                }
                else if (c == ',' && depth == 1)
                {
                    args.Add(_raw.Substring(start, p - start));
                    start = p + 1;
                }
                else if (c == '<' && depth >= 1)
                {
                    // 제네릭 인자 속 쉼표(`Foo<A, B>`)는 드물다 — 넘어간다
                }
            }
            return args;
        }

        private static string CheckMember(string _typeName, string _member, MemberKind _kind, Func<string, IEnumerable<Type>> _resolve)
        {
            List<Type> types = _resolve(_typeName).ToList();
            if (types.Count == 0) return $"{_typeName} — 타입을 못 찾는다({_kind} \"{_member}\")";

            const BindingFlags ALL = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                     | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (Type type in types)
            {
                for (Type t = type; t != null; t = t.BaseType)
                {
                    switch (_kind)
                    {
                        case MemberKind.Field:
                            if (t.GetField(_member, ALL) != null) return null;
                            break;
                        case MemberKind.Property:
                            if (t.GetProperty(_member, ALL) != null) return null;
                            break;
                        default:
                            if (t.GetMethods(ALL).Any(m => m.Name == _member)) return null;
                            break;
                    }
                }
            }
            return $"{_typeName}.{_member} — {_kind}가 실재하지 않는다(이름이 바뀌면 훅이 조용히 무동작이 된다)";
        }

        private static Dictionary<string, List<Type>> s_AllTypes;

        /// <summary>`UI.CatClassPopup`·`IngameDebugConsole.DebugLogManager` 같은 소스 표기 → 로드된 타입.</summary>
        private static IEnumerable<Type> ResolveAnyType(string _dotted)
        {
            if (s_AllTypes == null)
            {
                s_AllTypes = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.IsDynamic) continue;
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); }
                    catch (Exception) { continue; }
                    foreach (Type t in types)
                    {
                        if (!s_AllTypes.TryGetValue(t.Name, out List<Type> list)) s_AllTypes[t.Name] = list = new List<Type>();
                        list.Add(t);
                    }
                }
            }

            string shortName = _dotted.Substring(_dotted.LastIndexOf('.') + 1);
            if (!s_AllTypes.TryGetValue(shortName, out List<Type> candidates)) return Enumerable.Empty<Type>();
            if (_dotted.IndexOf('.') < 0) return candidates;
            return candidates.Where(t => t.FullName != null
                                         && (t.FullName == _dotted || t.FullName.EndsWith("." + _dotted, StringComparison.Ordinal)));
        }

        [NUnit.Framework.Property("CaseId", "E-23")]
        [Test]
        public void ReflectedPrivateNames_InTestingSources_Exist()
        {
            var bad = new List<string>();
            int files = 0;
            foreach (var file in CsSourceScan.LoadTree(CsSourceScan.TestingRoot))
            {
                files++;
                bad.AddRange(ReflectionNameViolations(file.rel, file.raw, file.masked, ResolveAnyType));
            }
            Assert.Greater(files, 5, "Testing 소스를 못 찾았다");
            Assert.IsEmpty(bad, "리플렉션으로 잡는 이름이 실재하지 않는다:\n" + string.Join("\n", bad));
        }

        [NUnit.Framework.Property("CaseId", "E-23")]
        [Test]
        public void ReflectionScan_CatchesRenamedField_UnknownType_AndUnclassifiedReceiver()
        {
            const string SRC =
                "class TestManager {\n" +
                "  void A() { var f = typeof(Fake).GetField(\"m_Real\", Flags); }\n" +
                "  void B() { var f = typeof(Fake).GetField(\"m_Renamed\", Flags); }\n" +
                "  void C() { var f = typeof(Missing).GetField(\"m_Real\"); }\n" +
                "  void D(object o) { var f = o.GetType().GetField(\"m_Real\"); }\n" +
                "  void E(string k) { var f = typeof(Fake).GetField(k); }\n" +
                "  string G() => HudText(FindFirstObjectByType<Fake>(), \"m_Gone\");\n" +
                "  string H() { var hud = FindFirstObjectByType<Fake>(); return HudText(hud, \"m_Real\"); }\n" +
                "  void I() { string[] fields = { \"m_Real\", \"m_Nope\" }; for (int i = 0; i < 2; i++) typeof(Fake).GetField(fields[i], Flags); }\n" +
                "  static string HudText(object _hud, string _field) => _hud.GetType().GetField(_field, Flags).Name;\n" +
                "}\n";
            string masked = CsSourceScan.Mask(SRC);
            List<string> bad = ReflectionNameViolations("T.cs", SRC, masked,
                n => n == "Fake" ? new[] { typeof(FakeReflected) } : Enumerable.Empty<Type>());

            Assert.AreEqual(5, bad.Count, string.Join("\n", bad));
            Assert.IsTrue(bad.Any(b => b.Contains("m_Renamed")));
            Assert.IsTrue(bad.Any(b => b.Contains("Missing")));
            Assert.IsTrue(bad.Any(b => b.Contains("DYNAMIC_RECEIVERS")));
            Assert.IsTrue(bad.Any(b => b.Contains("m_Gone")));
            Assert.IsTrue(bad.Any(b => b.Contains("m_Nope")));
        }

        private sealed class FakeReflected
        {
#pragma warning disable 0169
            private int m_Real;
#pragma warning restore 0169
        }
    }
}
