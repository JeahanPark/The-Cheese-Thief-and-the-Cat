using System.Reflection;
using UnityEditor;

namespace CTC.Editor
{
    /// <summary>콘솔 비우기 — 옛 컴파일 에러가 남아 "아직 깨진 줄" 아는 일을 막는다(MCP 자동화용).</summary>
    public static class ClearConsoleMenu
    {
        [MenuItem("Tools/CTC/Dev/콘솔 비우기")]
        public static void Run()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
            type?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        }
    }
}
