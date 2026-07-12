using System;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace WesQA
{
    /// <summary>Invoke(listener, data) → QA 타깃 싱글턴(TARGET_TYPE_NAME)의 listener 이름 public 메서드를
    /// 리플렉션으로 찾아 data(kwargs)를 파라미터에 매핑해 호출. 기존 메서드 호출만(신규 로직 없음).
    /// WesQA(Editor asmdef)는 게임 어셈블리(타깃 타입 소속)를 컴파일타임 참조할 수 없으므로
    /// 타입·Instance까지 전부 리플렉션으로 해석한다.</summary>
    // [CTC 이식 수정] 리플렉션 타깃 타입명을 상수 TARGET_TYPE_NAME으로 분리(기본값 "TestManager" 유지).
    // CTC에는 TestManager가 아직 없으므로(후속 이식 예정) 타입 미존재 시 원인·조치가 담긴
    // 명확한 에러 메시지를 RPC 에러 응답으로 돌려주도록 방어 코드를 추가했다.
    public static class InvokeBridge
    {
        /// <summary>리플렉션으로 찾을 QA 진입점 싱글턴의 타입명(네임스페이스 포함 가능).
        /// CTC TestManager 이식 시 이 이름의 타입이 로드되면 즉시 연결된다.</summary>
        public const string TARGET_TYPE_NAME = "TestManager";

        private static Type s_TargetType;

        private static Type ResolveTargetType()
        {
            if (s_TargetType != null) return s_TargetType;

            // 1차: 풀네임 매칭 (TARGET_TYPE_NAME에 네임스페이스가 포함된 경우)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType(TARGET_TYPE_NAME, false); }
                catch { t = null; }
                if (t != null) { s_TargetType = t; return t; }
            }

            // [CTC 이식 수정] 2차: 네임스페이스 무관 타입명 매칭 —
            // CTC TestManager는 CTC.Runtime.Testing 네임스페이스 소속이라 GetType("TestManager")로는 못 찾는다.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t != null && t.Name == TARGET_TYPE_NAME) { s_TargetType = t; return t; }
                }
            }

            // [CTC 이식 수정] 타입 미존재 시 명확한 안내 — CTC는 TestManager 미이식 상태가 정상 경로일 수 있다.
            throw new Exception(
                $"'{TARGET_TYPE_NAME}' type not found in any loaded assembly. " +
                "CTC 프로젝트에 TestManager가 아직 이식되지 않았습니다. " +
                $"Invoke/SendMessage RPC를 쓰려면 '{TARGET_TYPE_NAME}' 싱글턴(static Instance " +
                "프로퍼티/필드 보유)을 게임 어셈블리에 추가하세요.");
        }

        private static object ResolveInstance(Type targetType)
        {
            // MonoSingleton<T>.Instance (public static property) 우선
            var prop = targetType.GetProperty(
                "Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (prop != null) return prop.GetValue(null);

            var field = targetType.GetField(
                "Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (field != null) return field.GetValue(null);

            throw new Exception($"{TARGET_TYPE_NAME}.Instance accessor not found " +
                "(static 'Instance' 프로퍼티 또는 필드가 필요합니다)");
        }

        public static object Invoke(string listener, JObject data)
        {
            if (string.IsNullOrEmpty(listener)) throw new Exception("listener is null/empty");

            var targetType = ResolveTargetType();
            var target = ResolveInstance(targetType);
            if (target == null)
                throw new Exception($"{TARGET_TYPE_NAME}.Instance is null " +
                    "(씬에 인스턴스가 아직 생성되지 않았을 수 있습니다)");

            var method = targetType.GetMethod(
                listener, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (method == null)
                throw new Exception($"no such {TARGET_TYPE_NAME} method: " + listener);

            var ps = method.GetParameters();
            var args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                JToken tok = data != null ? data[p.Name] : null;
                if (tok == null && data != null && data.Count == ps.Length)
                {
                    int idx = 0;
                    foreach (var prop in data.Properties())
                    {
                        if (idx == i) { tok = prop.Value; break; }
                        idx++;
                    }
                }
                if (tok != null) args[i] = tok.ToObject(p.ParameterType);
                else if (p.HasDefaultValue) args[i] = p.DefaultValue;
                else args[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
            }

            return method.Invoke(target, args); // void면 null
        }
    }
}
