// McpBridgeConsole.cs
// MCP Bridge 핸들러: read_console
// 모든 어셈블리를 탐색해 UnityEditorInternal.LogEntries 또는 UnityEditor.LogEntries를 찾는다.
// Unity 6에서 GetEntryInternal 시그니처가 (int, LogEntry) → (int, out LogEntry)로 변경됨을 처리한다.
//
// 요청 파라미터:
//   logType  : "error" | "warning" | "log" | "all" (기본값: "all")
//   maxCount : 반환할 최대 항목 수 (기본값: 50, 최신 항목부터)

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

public static partial class McpBridge
{
    private static string ReadConsole(BridgeRequest _req)
    {
        string filter   = string.IsNullOrEmpty(_req.logType) ? "all" : _req.logType.ToLowerInvariant();
        int    maxCount = _req.maxCount > 0 ? _req.maxCount : 50;

        try
        {
            // ── 1. 모든 어셈블리에서 LogEntries / LogEntry 타입 탐색 ──
            Type logEntriesType = null;
            Type logEntryType   = null;

            string[] entriesNames = { "UnityEditorInternal.LogEntries", "UnityEditor.LogEntries" };
            string[] entryNames   = { "UnityEditorInternal.LogEntry",   "UnityEditor.LogEntry"   };

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (logEntriesType == null)
                    foreach (var n in entriesNames)
                    {
                        logEntriesType = asm.GetType(n);
                        if (logEntriesType != null) break;
                    }

                if (logEntryType == null)
                    foreach (var n in entryNames)
                    {
                        logEntryType = asm.GetType(n);
                        if (logEntryType != null) break;
                    }

                if (logEntriesType != null && logEntryType != null) break;
            }

            if (logEntriesType == null || logEntryType == null)
                return BuildError($"LogEntries API를 찾을 수 없습니다. " +
                                  $"(logEntries={logEntriesType != null}, logEntry={logEntryType != null})");

            // ── 2. 메서드 탐색 ──
            const BindingFlags SF = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            var mStart    = logEntriesType.GetMethod("StartGettingEntries", SF);
            var mEnd      = logEntriesType.GetMethod("EndGettingEntries",   SF);
            var mGetCount = logEntriesType.GetMethod("GetCount",            SF);
            var mGetEntry = logEntriesType.GetMethod("GetEntryInternal",    SF);

            if (mStart == null || mEnd == null || mGetCount == null || mGetEntry == null)
                return BuildError($"LogEntries 메서드 누락. " +
                                  $"Start={mStart!=null} End={mEnd!=null} Count={mGetCount!=null} Get={mGetEntry!=null}");

            // ── 3. GetEntryInternal 호출 방식 결정 (by-value vs out) ──
            //  Unity 5~2021 : GetEntryInternal(int row, LogEntry entry)
            //  Unity 2022+  : GetEntryInternal(int row, out LogEntry entry)
            var getEntryParams   = mGetEntry.GetParameters();
            bool usesOutParam    = getEntryParams.Length == 2 && getEntryParams[1].IsOut;

            // ── 4. 항목 읽기 ──
            int count = (int)mStart.Invoke(null, null);
            var entries    = new List<string>(Math.Min(maxCount, count));
            var entryInst  = Activator.CreateInstance(logEntryType);

            var fMessage = logEntryType.GetField("message",  BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var fMode    = logEntryType.GetField("mode",     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var fFile    = logEntryType.GetField("file",     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var fLine    = logEntryType.GetField("line",     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // 최신 항목부터 역순으로 읽어 maxCount개를 수집한다.
            try
            {
                for (int i = count - 1; i >= 0 && entries.Count < maxCount; i--)
                {
                    if (usesOutParam)
                    {
                        var args = new object[] { i, entryInst };
                        mGetEntry.Invoke(null, args);
                        entryInst = args[1];
                    }
                    else
                    {
                        mGetEntry.Invoke(null, new object[] { i, entryInst });
                    }

                    string message = fMessage?.GetValue(entryInst) as string ?? "";
                    int    mode    = fMode != null ? (int)fMode.GetValue(entryInst) : 0;
                    string file    = fFile?.GetValue(entryInst) as string ?? "";
                    int    line    = fLine != null ? (int)fLine.GetValue(entryInst) : 0;

                    string type = ClassifyLogMode(mode);
                    if (filter != "all" && type != filter)
                        continue;

                    int newlineIdx = message.IndexOf('\n');
                    if (newlineIdx > 0) message = message[..newlineIdx];
                    if (message.Length > 300) message = message[..300] + "...";

                    string filePart = string.IsNullOrEmpty(file) ? "" : $",\"file\":\"{Escape(file)}\",\"line\":{line}";
                    entries.Add($"{{\"type\":\"{type}\",\"message\":\"{Escape(message)}\"{filePart}}}");
                }

                // 역순으로 수집했으므로 시간순으로 되돌린다
                entries.Reverse();
            }
            finally
            {
                mEnd.Invoke(null, null);
            }

            var sb = new StringBuilder();
            sb.Append("{\"success\":true,\"message\":\"OK\",\"total\":");
            sb.Append(count);
            sb.Append(",\"count\":");
            sb.Append(entries.Count);
            sb.Append(",\"entries\":[");
            sb.Append(string.Join(",", entries));
            sb.Append("]}");
            return sb.ToString();
        }
        catch (Exception e)
        {
            return BuildError($"read_console 실패: {e.Message}");
        }
    }

    // Unity LogMessageFlags → "error" | "warning" | "log"
    private static string ClassifyLogMode(int mode)
    {
        const int ERROR_MASK   = 1 | 2 | 16 | 256 | 2048 | 524288;
        const int WARNING_MASK = 4 | 512 | 4096;

        if ((mode & ERROR_MASK)   != 0) return "error";
        if ((mode & WARNING_MASK) != 0) return "warning";
        return "log";
    }
}
