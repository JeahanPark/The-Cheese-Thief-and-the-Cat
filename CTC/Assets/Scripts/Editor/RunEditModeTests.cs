using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// EditMode 테스트를 메뉴로 실행하고 결과를 콘솔에 남긴다 (MCP 자동화용).
    /// Tools > CTC > Run EditMode Tests
    /// </summary>
    public static class RunEditModeTests
    {
        [MenuItem("Tools/CTC/Run EditMode Tests")]
        public static void Run()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultLogger());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        private class ResultLogger : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.Test.IsSuite && result.TestStatus != TestStatus.Passed)
                    Debug.LogError($"[EditModeTests] FAIL {result.Test.FullName}: {result.Message}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log($"[EditModeTests] 완료 — Passed: {result.PassCount}, Failed: {result.FailCount}, Skipped: {result.SkipCount}");
            }
        }
    }
}
