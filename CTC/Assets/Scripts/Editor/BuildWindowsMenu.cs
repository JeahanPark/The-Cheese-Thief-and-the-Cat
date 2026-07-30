using System.IO;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// Windows 스탠드얼론 빌드 + steam_appid.txt 동봉 (배포는 산출 폴더째 압축).
    ///
    /// 두 갈래다(2026-07-31):
    /// - **개발용**: `BuildOptions.Development` → `DEVELOPMENT_BUILD` 정의 →
    ///   `GameDebug.Log`가 살아나고 인게임 로그 콘솔(`DebugConsoleGate`)도 켜진다. 테스터 배포용.
    /// - **배포용**: 로그도 콘솔도 전부 빠진다.
    /// 산출 폴더를 나눠 둬서 어느 쪽을 압축하는지 헷갈리지 않는다.
    /// </summary>
    public static class BuildWindowsMenu
    {
        private const string DEV_DIR = "Builds/Windows-Dev";
        private const string RELEASE_DIR = "Builds/Windows";
        private const string EXE_NAME = "CTC.exe";
        private const string SCENE_PATH = "Assets/Scenes/Game.unity";

        [MenuItem("Tools/CTC/Build Windows (개발용 — 로그·콘솔 켜짐)")]
        public static void BuildDevelopment()
        {
            Run(DEV_DIR, BuildOptions.Development, "개발용");
        }

        [MenuItem("Tools/CTC/Build Windows (배포용)")]
        public static void BuildRelease()
        {
            Run(RELEASE_DIR, BuildOptions.None, "배포용");
        }

        private static void Run(string _outDir, BuildOptions _options, string _label)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SCENE_PATH },
                locationPathName = Path.Combine(_outDir, EXE_NAME),
                target = BuildTarget.StandaloneWindows64,
                options = _options,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError($"[Build] {_label} 실패: {report.summary.result} (에러 {report.summary.totalErrors}건)");
                return;
            }

            // 스팀 초기화용 — exe 옆에 있어야 AppID 인식 (정식 입점 전 480)
            File.WriteAllText(Path.Combine(_outDir, "steam_appid.txt"),
                Runtime.Networking.SteamBootstrap.APP_ID.ToString());

            Debug.Log($"[Build] {_label} 성공: {Path.GetFullPath(Path.Combine(_outDir, EXE_NAME))} " +
                      $"({report.summary.totalSize / (1024 * 1024)}MB) — 배포는 이 폴더째 압축");
        }
    }
}
