using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            DropUntitledSceneDirtiness();

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultLogger());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        /// <summary>
        /// **이름 없는 씬의 「수정됨」 표시만** 지운다. 저장하지 않는다.
        ///
        /// 왜(2026-09-12 실측): 테스트 러너는 실행 전에 수정된 씬을 저장하라고 요구한다.
        /// 열린 씬이 이름 없는(경로 없는) 씬이면 유니티가 **네이티브 파일 대화상자**를 띄우는데,
        /// 그 대화상자가 에디터 업데이트 루프를 멈춘다 — MCP 호출이 전부 타임아웃하고
        /// 「브리지가 죽었다」와 증상이 똑같아 보인다. 하루에 세 번 물렸다.
        ///
        /// 저장된 씬은 건드리지 않는다(`path`가 있으면 그냥 둔다) — 남의 미저장 작업을
        /// 말없이 버리면 안 되기 때문이다. 경로가 없는 씬은 **아직 아무 데도 없는 임시 씬**이라
        /// 버릴 것이 없다.
        ///
        /// `ClearSceneDirtiness`는 6000.5에서 public이 아니라 리플렉션으로 부른다. 못 찾으면
        /// 조용히 넘어간다 — 이건 편의 장치이고, 대화상자가 떠도 `tools/dismiss-save-scene.ps1`이
        /// 밖에서 닫는다. 테스트 실행 자체를 막을 이유는 없다.
        /// </summary>
        private static void DropUntitledSceneDirtiness()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.isDirty) return;
            if (!string.IsNullOrEmpty(active.path)) return;

            MethodInfo clear = typeof(EditorSceneManager).GetMethod(
                "ClearSceneDirtiness",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(Scene) }, null);

            if (clear == null)
            {
                Debug.LogWarning(
                    "[EditModeTests] ClearSceneDirtiness를 못 찾았다 — 저장 대화상자가 뜰 수 있다. " +
                    "뜨면 tools/dismiss-save-scene.ps1이 닫는다.");
                return;
            }

            clear.Invoke(null, new object[] { active });
            Debug.Log("[EditModeTests] 이름 없는 씬의 수정 표시를 지웠다 — 저장 대화상자가 뜨지 않게.");
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

