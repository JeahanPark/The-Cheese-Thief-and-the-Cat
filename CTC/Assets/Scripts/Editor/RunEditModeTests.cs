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
            SceneDirtGuard.DropUntitledDirtiness();

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultLogger());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        private class ResultLogger : ICallbacks
        {
            public void RunStarted(ITestAdaptor _testsToRun) { }
            public void TestStarted(ITestAdaptor _test) { }

            public void TestFinished(ITestResultAdaptor _result)
            {
                if (!_result.Test.IsSuite && _result.TestStatus != TestStatus.Passed)
                    Debug.LogError($"[EditModeTests] FAIL {_result.Test.FullName}: {_result.Message}");
            }

            public void RunFinished(ITestResultAdaptor _result)
            {
                Debug.Log($"[EditModeTests] 완료 — Passed: {_result.PassCount}, Failed: {_result.FailCount}, Skipped: {_result.SkipCount}");
            }
        }
    }
}

