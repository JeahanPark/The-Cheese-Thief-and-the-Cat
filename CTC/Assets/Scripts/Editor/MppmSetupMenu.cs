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

        [MenuItem("Tools/CTC/Dev/MPPM 3인 세팅 (클론 2개 + wes 태그)")]
        public static void SetupThreePlayers()
        {
            if (!TryGetPlayers(out object[] players, out string error))
            {
                Debug.LogError($"[MppmSetup] {error}");
                return;
            }

            var sb = new StringBuilder("[MppmSetup] 3인 세팅\n");
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
                if (cloneOrder > 2)
                {
                    sb.AppendLine($"  {name}: 비활성 유지(3인 초과)");
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

        private static bool TryGetPlayers(out object[] players, out string error)
        {
            players = null;
            error = null;

            // UnityPlayer.GetPlayers()는 직렬화 DTO(PlayerStateJson)를 주고 Activate/AddTag가 없다.
            // 실제 조작 대상은 MultiplayerPlaymode.Players (UnityPlayer[]) — MPPM 창이 쓰는 것과 동일.
            Type mppm = FindType("MultiplayerPlaymode");
            if (mppm == null)
            {
                error = "MPPM 내부 타입(MultiplayerPlaymode)을 못 찾음 — 패키지 버전 변경 가능성";
                return false;
            }

            object result = mppm.GetProperty("Players",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            if (result is IEnumerable e)
            {
                players = e.Cast<object>().Where(o => o != null).ToArray();
                return true;
            }

            error = "MultiplayerPlaymode.Players 접근 실패";
            return false;
        }

        // Activate(out ActivationError, List<T>) — 두 번째 인자는 빈 리스트로 호출.
        private static string Activate(object player)
        {
            if (Get(player, "PlayerState")?.ToString() == "Launched") return "이미 실행 중";

            MethodInfo activate = player.GetType()
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

            activate.Invoke(player, args);
            for (int i = 0; i < ps.Length; i++)
            {
                if (!ps[i].ParameterType.IsByRef) continue;
                object err = args[i];
                return err == null || err.ToString() == "None" ? "활성화" : $"활성화 실패({err})";
            }
            return "활성화";
        }

        private static string Deactivate(object player)
        {
            MethodInfo deactivate = player.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "Deactivate");
            if (deactivate == null) return "Deactivate 없음";

            var args = new object[deactivate.GetParameters().Length];
            deactivate.Invoke(player, args);
            return "종료";
        }

        private static string EnsureTag(object player, string tag)
        {
            IEnumerable tags = Get(player, "Tags") as IEnumerable;
            if (tags != null && tags.Cast<object>().Any(o => o?.ToString() == tag)) return $"{tag}(기존)";

            MethodInfo add = player.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "AddTag");
            if (add == null) return "AddTag 없음";

            var args = new object[] { tag, null };
            add.Invoke(player, args);
            return args[1] == null || args[1].ToString() == "None" ? tag : $"{tag} 실패({args[1]})";
        }

        private static object Get(object target, string propertyName)
        {
            PropertyInfo p = target.GetType().GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return p?.GetValue(target);
        }

        private static Type FindType(string shortName)
        {
            Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == ASM);
            return asm?.GetType(NS + shortName);
        }
    }
}
