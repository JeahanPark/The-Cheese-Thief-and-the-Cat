using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 임시 QA 유틸: MPPM 가상 플레이어(Player 2)를 코드로 켜기 위한 내부 API 탐색/호출.
    /// MPPM은 공개 활성화 API가 없어 리플렉션 사용 — 패키지 업데이트 시 깨질 수 있음(QA 전용).
    /// </summary>
    public static class DevMppmMenu
    {
        [MenuItem("Tools/CTC/Dev/MPPM Dump API")]
        public static void DumpApi()
        {
            var multiplayerAsms = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.IndexOf("ultiplayer", StringComparison.Ordinal) >= 0)
                .Select(a => a.GetName().Name).ToArray();
            Debug.Log("[DevMppm] multiplayer 어셈블리:\n  " + string.Join("\n  ", multiplayerAsms));

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = asm.GetName().Name;
                if (name.IndexOf("ultiplayer", StringComparison.Ordinal) < 0) continue;

                foreach (Type t in SafeTypes(asm))
                {
                    if (t == null || !(t.Name.Contains("Player") || t.Name.Contains("VirtualProject"))) continue;

                    MethodInfo[] methods = t.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                    var interesting = methods.Where(m =>
                        m.Name.Contains("Activate") || m.Name.Contains("Launch") ||
                        m.Name.Contains("Open") || m.Name.Contains("Start")).ToArray();
                    if (interesting.Length == 0) continue;

                    Debug.Log($"[DevMppm] {name} :: {t.FullName}\n  " +
                        string.Join("\n  ", interesting.Select(m =>
                            $"{(m.IsStatic ? "static " : "")}{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")));
                }
            }
            Debug.Log("[DevMppm] Dump 완료");
        }

        private static Type[] SafeTypes(Assembly _asm)
        {
            try { return _asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types; }
        }

        private const BindingFlags ALL =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        /// <summary>MPPM 가상 플레이어를 이름으로 찾아 Activate — 창 토글과 동일 효과(클론 에디터 기동).</summary>
        [MenuItem("Tools/CTC/Dev/MPPM Activate Player 2")]
        public static void ActivatePlayer2() => TogglePlayer("Player 2", true);

        [MenuItem("Tools/CTC/Dev/MPPM Deactivate Player 2")]
        public static void DeactivatePlayer2() => TogglePlayer("Player 2", false);

        private static void TogglePlayer(string _playerName, bool _activate)
        {
            // MultiplayerPlaymode.PlayerTwo 등 정적 프로퍼티가 창과 동일한 인스턴스를 노출한다
            Type api = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Unity.Multiplayer.PlayMode.Editor.MultiplayerPlaymode"))
                .FirstOrDefault(t => t != null);
            if (api == null) { Debug.LogError("[DevMppm] MultiplayerPlaymode 타입 없음"); return; }

            string propName = _playerName == "Player 2" ? "PlayerTwo"
                : _playerName == "Player 3" ? "PlayerThree"
                : _playerName == "Player 4" ? "PlayerFour" : "PlayerOne";
            object player = api.GetProperty(propName, ALL)?.GetValue(null);
            if (player == null) { Debug.LogError($"[DevMppm] {propName} null (MPPM 미초기화?)"); return; }

            MethodInfo method = player.GetType().GetMethod(_activate ? "Activate" : "Deactivate", ALL);
            ParameterInfo[] ps = method.GetParameters();
            object[] args = new object[ps.Length];
            for (int i = 1; i < ps.Length; i++)
            {
                Type pt = ps[i].ParameterType;
                args[i] = pt.IsByRef ? null : Activator.CreateInstance(pt);
            }
            bool ok = (bool)method.Invoke(player, args);
            Debug.Log($"[DevMppm] {_playerName} {(_activate ? "Activate" : "Deactivate")} → {ok} (err={args[0]})");
        }

        // systems 객체의 프로퍼티/필드를 1단계 순회하며 UnityPlayer 컬렉션에서 이름 매칭.
        private static object FindUnityPlayer(object _systems, string _playerName)
        {
            var candidates = new System.Collections.Generic.List<object>();
            Type t = _systems.GetType();
            foreach (PropertyInfo p in t.GetProperties(ALL))
            {
                try { candidates.Add(p.GetValue(_systems)); } catch { }
            }
            foreach (FieldInfo f in t.GetFields(ALL))
            {
                try { candidates.Add(f.GetValue(_systems)); } catch { }
            }

            foreach (object c in candidates)
            {
                if (c == null) continue;
                if (IsPlayerWithName(c, _playerName)) return c;
                if (c is System.Collections.IDictionary dict)
                {
                    foreach (object v in dict.Values)
                        if (IsPlayerWithName(v, _playerName)) return v;
                }
                else if (c is System.Collections.IEnumerable en && !(c is string))
                {
                    foreach (object v in en)
                        if (IsPlayerWithName(v, _playerName)) return v;
                }
            }
            return null;
        }

        private static bool IsPlayerWithName(object _obj, string _name)
        {
            if (_obj == null || _obj.GetType().Name != "UnityPlayer") return false;
            return (string)_obj.GetType().GetProperty("Name", ALL)?.GetValue(_obj) == _name;
        }

        // UnityPlayer를 반환/보유하는 지점을 전 타입에서 역추적
        [MenuItem("Tools/CTC/Dev/MPPM Find UnityPlayer Sources")]
        public static void FindUnityPlayerSources()
        {
            var sb = new System.Text.StringBuilder();
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name.IndexOf("ultiplayer", StringComparison.Ordinal) < 0) continue;
                foreach (Type t in SafeTypes(asm))
                {
                    if (t == null || t.FullName == null) continue;
                    foreach (MethodInfo m in t.GetMethods(F).Where(m => !m.IsSpecialName))
                    {
                        string rt = m.ReturnType.ToString();
                        if (rt.Contains("UnityPlayer"))
                            sb.AppendLine($"{t.FullName} :: {(m.IsStatic ? "static " : "")}{rt} {m.Name}({string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name))})");
                    }
                    foreach (PropertyInfo p in t.GetProperties(F))
                        if (p.PropertyType.ToString().Contains("UnityPlayer"))
                            sb.AppendLine($"{t.FullName} :: prop {(p.GetMethod?.IsStatic == true ? "static " : "")}{p.PropertyType} {p.Name}");
                    foreach (FieldInfo f2 in t.GetFields(F))
                        if (f2.FieldType.ToString().Contains("UnityPlayer"))
                            sb.AppendLine($"{t.FullName} :: field {(f2.IsStatic ? "static " : "")}{f2.FieldType} {f2.Name}");
                }
            }
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mppm_player_sources.txt");
            System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log($"[DevMppm] UnityPlayer 소스 덤프 → {path}");
        }

        [MenuItem("Tools/CTC/Dev/MPPM Dump UnityPlayer Members")]
        public static void DumpUnityPlayerMembers()
        {
            var sb = new System.Text.StringBuilder();
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type t in SafeTypes(asm))
                {
                    if (t == null || t.FullName == null) continue;
                    if (!t.FullName.StartsWith("Unity.Multiplayer.PlayMode.Editor")) continue;
                    if (!(t.Name == "UnityPlayer" || t.Name.Contains("Repository") || t.Name.Contains("SystemData")
                          || t.Name.Contains("PlayersApi") || t.Name.Contains("Workflow")
                          || t.Name.Contains("PlayerSystems") || t.Name.Contains("Orchestr"))) continue;

                    sb.AppendLine($"==== {t.FullName} ====");
                    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                    foreach (PropertyInfo p in t.GetProperties(F))
                        sb.AppendLine($"  prop {(p.GetMethod?.IsStatic == true ? "static " : "")}{p.PropertyType.Name} {p.Name}");
                    foreach (MethodInfo m in t.GetMethods(F).Where(m => !m.IsSpecialName))
                        sb.AppendLine($"  method {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name))})");
                }
            }
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mppm_api_dump.txt");
            System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log($"[DevMppm] 멤버 덤프 → {path}");
        }
    }
}
