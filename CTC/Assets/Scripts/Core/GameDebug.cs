// WES에서 이식(2026-07-12)
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace CTC.Runtime.Core
{
    public static class GameDebug
    {
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Log(object _message)
        {
            Debug.Log(_message);
        }

        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogWarning(object _message)
        {
            Debug.LogWarning(_message);
        }

        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogError(object _message)
        {
            Debug.LogError(_message);
        }
    }
}
