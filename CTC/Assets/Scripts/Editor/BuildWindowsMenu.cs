using System.IO;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>Windows 스탠드얼론 빌드 + steam_appid.txt 동봉 (배포는 Builds/Windows 폴더째 압축).</summary>
    public static class BuildWindowsMenu
    {
        private const string OUT_DIR = "Builds/Windows";
        private const string EXE_NAME = "CTC.exe";
        private const string SCENE_PATH = "Assets/Scenes/Game.unity";

        [MenuItem("Tools/CTC/Build Windows")]
        public static void Build()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SCENE_PATH },
                locationPathName = Path.Combine(OUT_DIR, EXE_NAME),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError($"[Build] 실패: {report.summary.result} (에러 {report.summary.totalErrors}건)");
                return;
            }

            // 스팀 초기화용 — exe 옆에 있어야 AppID 인식 (정식 입점 전 480)
            File.WriteAllText(Path.Combine(OUT_DIR, "steam_appid.txt"),
                Runtime.Networking.SteamBootstrap.APP_ID.ToString());

            Debug.Log($"[Build] 성공: {Path.GetFullPath(Path.Combine(OUT_DIR, EXE_NAME))} " +
                      $"({report.summary.totalSize / (1024 * 1024)}MB)");
        }
    }
}
