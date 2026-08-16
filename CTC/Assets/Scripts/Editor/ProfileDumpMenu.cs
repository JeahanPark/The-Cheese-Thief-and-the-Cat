using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Profiling.Editor;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 플레이모드에서 프로파일러를 3초 켰다가 **메인 스레드 상위 마커**를 콘솔·파일로 덤프한다.
    /// "프레임이 떨어진다"를 추측이 아니라 마커 이름으로 답하기 위한 개발 메뉴(2026-08-16).
    /// 결과: `%TEMP%/ctc-profile.txt` + 콘솔 한 줄 요약.
    /// </summary>
    public static class ProfileDumpMenu
    {
        private const int SAMPLE_FRAMES = 120;
        private const int TOP = 20;

        [MenuItem("Tools/CTC/Dev/Profile 3s → 상위 마커 덤프")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ProfileDump] 플레이모드에서만");
                return;
            }

            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.enabled = true;
            ProfilerDriver.profileEditor = false;
            double start = EditorApplication.timeSinceStartup;

            void Tick()
            {
                if (EditorApplication.timeSinceStartup - start < 3.0) return;
                EditorApplication.update -= Tick;
                ProfilerDriver.enabled = false;
                Dump();
            }
            EditorApplication.update += Tick;
        }

        private static void Dump()
        {
            int last = ProfilerDriver.lastFrameIndex;
            int first = Mathf.Max(ProfilerDriver.firstFrameIndex, last - SAMPLE_FRAMES + 1);
            if (last < 0 || first > last)
            {
                Debug.LogWarning("[ProfileDump] 수집된 프레임이 없다");
                return;
            }

            // 마커별 자기 시간(self) 합계 — 부모 시간에 자식이 겹쳐 세어지지 않게
            var self = new Dictionary<string, double>();
            var total = new Dictionary<string, double>();
            var count = new Dictionary<string, int>();
            int frames = 0;
            double frameSum = 0;

            for (int f = first; f <= last; f++)
            {
                using var view = ProfilerDriver.GetHierarchyFrameDataView(
                    f, 0, HierarchyFrameDataView.ViewModes.Default,
                    HierarchyFrameDataView.columnSelfTime, false);
                if (view == null || !view.valid) continue;
                frames++;
                frameSum += view.frameTimeMs;

                var children = new List<int>();
                var stack = new Stack<int>();
                stack.Push(view.GetRootItemID());
                while (stack.Count > 0)
                {
                    int id = stack.Pop();
                    string name = view.GetItemName(id);
                    double s = view.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnSelfTime);
                    double t = view.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnTotalTime);
                    self[name] = self.GetValueOrDefault(name) + s;
                    total[name] = total.GetValueOrDefault(name) + t;
                    count[name] = count.GetValueOrDefault(name) + 1;

                    children.Clear();
                    view.GetItemChildren(id, children);
                    foreach (int c in children) stack.Push(c);
                }
            }
            if (frames == 0) { Debug.LogWarning("[ProfileDump] 유효 프레임 0"); return; }

            var sb = new StringBuilder();
            sb.AppendLine($"[ProfileDump] {frames}프레임 · 평균 프레임 {frameSum / frames:F1}ms — 메인 스레드 self 상위 {TOP}");
            foreach (var kv in self.OrderByDescending(k => k.Value).Take(TOP))
            {
                sb.AppendLine($"  {kv.Value / frames,7:F2}ms self  {total[kv.Key] / frames,7:F2}ms total  {kv.Key}");
            }
            sb.AppendLine("  -- total 상위 --");
            foreach (var kv in total.OrderByDescending(k => k.Value).Take(TOP))
            {
                sb.AppendLine($"  {kv.Value / frames,7:F2}ms total  {kv.Key}");
            }

            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ctc-profile.txt");
            System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log(sb.ToString());
        }
    }
}
