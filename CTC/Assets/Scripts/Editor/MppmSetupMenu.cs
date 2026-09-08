using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// MPPM 가상 플레이어를 코드로 구성한다(3인 로컬 테스트: 메인=고양이, 클론2=쥐).
    /// MPPM 2.0.1은 패키지에 소스가 없고(에디터 내장 UnityEditor.MultiplayerModule) 공개 API도 없어
    /// UnityPlayer.GetPlayers/Activate/AddTag를 리플렉션으로 호출한다.
    /// 태그 wes1/wes2는 WesQABootstrap의 포트 인덱스(5001+N)와 짝이다.
    /// </summary>
    public static class MppmSetupMenu
    {
        private const string ASM = "UnityEditor.MultiplayerModule";
        private const string NS = "Unity.Multiplayer.PlayMode.Editor.";

        // Preferences > General > Interaction Mode. 0=Default(유휴 시 절전) / 1=No Throttling.
        // 기본값이면 포커스를 잃은 클론 에디터가 초당 몇 프레임으로 떨어져 "클릭해야 갱신"이 된다.
        private const string INTERACTION_MODE_PREF = "InteractionMode";
        private const int INTERACTION_MODE_NO_THROTTLING = 1;

        /// <summary>
        /// 클론이 포커스 없이도 계속 도는 조건 두 가지를 한 번에 켠다(2026-07-29).
        /// - Player Settings > Run In Background: 플레이모드가 백그라운드에서 계속 갱신
        ///   (런타임에도 AppBootstrap이 켜지만, 에디터는 이 체크박스를 먼저 본다)
        /// - Interaction Mode = No Throttling: 에디터 자체의 유휴 절전 해제
        ///   → 이게 꺼져 있으면 클론 창을 클릭해야만 화면이 갱신된다
        /// </summary>
        [MenuItem("Tools/CTC/Dev/MPPM 백그라운드 갱신 켜기")]
        public static void EnableBackgroundUpdates()
        {
            var sb = new StringBuilder("[MppmSetup] 백그라운드 갱신\n");

            if (!PlayerSettings.runInBackground)
            {
                PlayerSettings.runInBackground = true;
                AssetDatabase.SaveAssets();
                sb.AppendLine("  Run In Background: 꺼짐 → 켬 (ProjectSettings 저장)");
            }
            else sb.AppendLine("  Run In Background: 이미 켜짐");

            int mode = EditorPrefs.GetInt(INTERACTION_MODE_PREF, 0);
            if (mode != INTERACTION_MODE_NO_THROTTLING)
            {
                EditorPrefs.SetInt(INTERACTION_MODE_PREF, INTERACTION_MODE_NO_THROTTLING);
                sb.AppendLine($"  Interaction Mode: {mode} → 1(No Throttling) — 클론은 재시작해야 적용된다");
            }
            else sb.AppendLine("  Interaction Mode: 이미 No Throttling");

            Report(sb.ToString());
        }

        /// <summary>
        /// 클론을 **전부 끈다** — 봇 승률 계측(`run_matches.py`)의 전제다 (2026-09-09).
        ///
        /// 클론은 조작하지 않아도 `MppmAutoConnect`가 쥐로 앉히고, 그 쥐가 스폰 자리에 서서
        /// **판이 끝날 때까지 안 죽는다.** 봇 쥐 여덟이 전멸해도 판이 안 끝나 전멸시각이
        /// 166초에서 400초로 부풀었다(2026-09-09 실측). 프로세스를 손으로 죽여도
        /// **MPPM이 다시 띄우므로** 여기서 비활성으로 내려야 한다.
        /// </summary>
        [MenuItem("Tools/CTC/Dev/MPPM 1인 (클론 전부 끄기 — 봇 계측용)")]
        public static void SetupSoloPlayer() => SetupPlayers(0);

        [MenuItem("Tools/CTC/Dev/MPPM 3인 세팅 (클론 2개 + wes 태그)")]
        public static void SetupThreePlayers() => SetupPlayers(2);

        // 4인 = 고양이 1 + 쥐 3 — 기본 맵 권장 쥐 상한(3)까지 채우는 구성(2026-08-23 건의).
        // wes3 → 포트 5004. qa_v36~45 스위트는 3인 기준이라 4인은 수동·전용 시나리오에 쓴다.
        [MenuItem("Tools/CTC/Dev/MPPM 4인 세팅 (클론 3개 + wes 태그)")]
        public static void SetupFourPlayers() => SetupPlayers(3);

        private static void SetupPlayers(int _clones)
        {
            if (!TryGetPlayers(out object[] players, out string error))
            {
                Debug.LogError($"[MppmSetup] {error}");
                return;
            }

            EnableBackgroundUpdates(); // 세팅할 때마다 절전이 켜져 있는지 확인

            var sb = new StringBuilder($"[MppmSetup] {_clones + 1}인 세팅" + "\n");
            int cloneOrder = 0;
            foreach (object player in players)
            {
                string name = (string)Get(player, "Name");
                bool isMain = Get(player, "Type").ToString() == "Main";
                if (isMain)
                {
                    sb.AppendLine($"  {name}: 메인 에디터 — 고양이로 사용");
                    continue;
                }

                cloneOrder++;
                if (cloneOrder > _clones)
                {
                    // "유지"가 아니라 **실제로 끈다**(2026-08-23) — 손으로 켜 둔 여분 클론이
                    // 태그 없이 살아 있으면 WesQA 폴백 인덱스 1로 wes1과 포트 5002를 두고
                    // 싸운다(SocketException). 세팅 인원만 남긴다.
                    bool running = Get(player, "PlayerState")?.ToString() == "Launched";
                    sb.AppendLine($"  {name}: {(running ? Deactivate(player) : "비활성 유지")}({_clones + 1}인 초과)");
                    continue;
                }

                sb.AppendLine($"  {name}: {Activate(player)} / 태그 {EnsureTag(player, $"wes{cloneOrder}")}");
            }

            Report(sb.ToString());
        }

        /// <summary>
        /// 클론을 껐다 켠다. `Setup Networking Scene`으로 씬을 재생성하면 클론이 들고 있던
        /// in-scene NetworkObject 해시가 어긋나 NGO 동기화가 NullReferenceException으로 죽는다
        /// (클론 로그: SceneEventData.SynchronizeSceneNetworkObjects) — 그때 이걸 돌린다.
        /// </summary>
        [MenuItem("Tools/CTC/Dev/MPPM 클론 재시작")]
        public static void RestartClones()
        {
            if (!TryGetPlayers(out object[] players, out string error))
            {
                Debug.LogError($"[MppmSetup] {error}");
                return;
            }

            var sb = new StringBuilder("[MppmSetup] 클론 재시작\n");
            foreach (object player in players)
            {
                if (Get(player, "Type")?.ToString() == "Main") continue;
                if (Get(player, "PlayerState")?.ToString() != "Launched") continue;

                sb.AppendLine($"  {Get(player, "Name")}: {Deactivate(player)} → 재활성화 예약");
            }

            sb.AppendLine("  ※ 종료 후 'MPPM 3인 세팅'을 다시 실행해 켠다(활성화는 비동기라 분리)");
            Report(sb.ToString());
        }

        [MenuItem("Tools/CTC/Dev/MPPM 상태 보기")]
        public static void DumpState()
        {
            if (!TryGetPlayers(out object[] players, out string error))
            {
                Debug.LogError($"[MppmSetup] {error}");
                return;
            }

            var sb = new StringBuilder("[MppmSetup] MPPM 플레이어 상태\n");
            foreach (object player in players)
            {
                object state = Get(player, "PlayerState");
                IEnumerable tags = Get(player, "Tags") as IEnumerable;
                string tagList = tags == null ? "" : string.Join(",", tags.Cast<object>().Select(o => o?.ToString()));
                sb.AppendLine($"  {Get(player, "Name")} type={Get(player, "Type")} state={state} tags=[{tagList}]");
            }
            Report(sb.ToString());
        }

        // u_console은 첫 줄만 보여줘서 다중행 결과는 파일로도 남긴다.
        private static void Report(string _text)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ctc-mppm-state.txt");
            try { System.IO.File.WriteAllText(path, _text); } catch (System.IO.IOException) { }
            Debug.Log(_text);
        }

        // ---- 리플렉션 헬퍼 ----

        private static bool TryGetPlayers(out object[] _players, out string _error)
        {
            _players = null;
            _error = null;

            // UnityPlayer.GetPlayers()는 직렬화 DTO(PlayerStateJson)를 주고 Activate/AddTag가 없다.
            // 실제 조작 대상은 MultiplayerPlaymode.Players (UnityPlayer[]) — MPPM 창이 쓰는 것과 동일.
            Type mppm = FindType("MultiplayerPlaymode");
            if (mppm == null)
            {
                _error = "MPPM 내부 타입(MultiplayerPlaymode)을 못 찾음 — 패키지 버전 변경 가능성";
                return false;
            }

            object result = mppm.GetProperty("Players",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            if (result is IEnumerable e)
            {
                _players = e.Cast<object>().Where(o => o != null).ToArray();
                return true;
            }

            _error = "MultiplayerPlaymode.Players 접근 실패";
            return false;
        }

        // Activate(out ActivationError, List<T>) — 두 번째 인자는 빈 리스트로 호출.
        private static string Activate(object _player)
        {
            if (Get(_player, "PlayerState")?.ToString() == "Launched") return "이미 실행 중";

            MethodInfo activate = _player.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "Activate");
            if (activate == null) return "Activate 없음";

            ParameterInfo[] ps = activate.GetParameters();
            var args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                Type pt = ps[i].ParameterType;
                if (pt.IsByRef) { args[i] = null; continue; }
                args[i] = pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(List<>)
                    ? Activator.CreateInstance(pt)
                    : (pt.IsValueType ? Activator.CreateInstance(pt) : null);
            }

            activate.Invoke(_player, args);
            for (int i = 0; i < ps.Length; i++)
            {
                if (!ps[i].ParameterType.IsByRef) continue;
                object err = args[i];
                return err == null || err.ToString() == "None" ? "활성화" : $"활성화 실패({err})";
            }
            return "활성화";
        }

        private static string Deactivate(object _player)
        {
            MethodInfo deactivate = _player.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "Deactivate");
            if (deactivate == null) return "Deactivate 없음";

            var args = new object[deactivate.GetParameters().Length];
            deactivate.Invoke(_player, args);
            return "종료";
        }

        private static string EnsureTag(object _player, string _tag)
        {
            IEnumerable tags = Get(_player, "Tags") as IEnumerable;
            if (tags != null && tags.Cast<object>().Any(o => o?.ToString() == _tag)) return $"{_tag}(기존)";

            MethodInfo add = _player.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "AddTag");
            if (add == null) return "AddTag 없음";

            var args = new object[] { _tag, null };
            add.Invoke(_player, args);
            return args[1] == null || args[1].ToString() == "None" ? _tag : $"{_tag} 실패({args[1]})";
        }

        private static object Get(object _target, string _propertyName)
        {
            PropertyInfo p = _target.GetType().GetProperty(_propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return p?.GetValue(_target);
        }

        private static Type FindType(string _shortName)
        {
            Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == ASM);
            return asm?.GetType(NS + _shortName);
        }
    }
}
