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
    ///   `GameDebug.Log`가 살아난다(로그는 Player.log로 — 인게임 콘솔 UI는 없다). 테스터 배포용.
    /// - **배포용**: 로그도 콘솔도 전부 빠진다.
    /// 산출 폴더를 나눠 둬서 어느 쪽을 압축하는지 헷갈리지 않는다.
    /// </summary>
    public static class BuildWindowsMenu
    {
        private const string DEV_DIR = "Builds/Windows-Dev";
        private const string RELEASE_DIR = "Builds/Windows";
        private const string EXE_NAME = "CanadianMouse.exe";

        /// <summary>
        /// 3자 고지문 원본. **`Assets/` 밖에 둔다** — 게임이 읽는 데이터가 아니라 exe 옆에
        /// 같이 놓는 파일이라, 에셋으로 들어가면 빌드에 두 번 들어간다.
        /// 내용의 근거는 `docs/design-vault/3자 에셋 라이선스.md`.
        /// </summary>
        private const string NOTICES_TEMPLATE = "Legal/Third-Party Notices.txt";
        private const string NOTICES_OUTPUT = "Third-Party Notices.txt";

        /// <summary>
        /// 굽는 씬을 **여기서 정한다.** 전에는 `Game.unity` 하나뿐이었다 — 화면이 넷으로 갈린 뒤에도
        /// 그대로라 **빌드에 인트로·로비·대기실이 통째로 빠져 있었다**(2026-08-30 확인).
        /// 실행하면 개발자용 IMGUI 접속창이 첫 화면이 된다. 그건 게임이 아니다.
        ///
        /// 빌드 설정(`EditorBuildSettings`)을 그대로 읽지 않는 이유: 거기엔 `SampleScene`처럼
        /// 나가면 안 되는 것이 섞여 있고, 씬 생성기가 새 씬을 넣을 때마다 목록이 조용히 늘어난다.
        /// **0번이 인트로여야 한다** — 실행하면 여기서 시작한다.
        /// </summary>
        private static readonly string[] SHIP_SCENES =
        {
            "Assets/Scenes/Intro.unity",
            "Assets/Scenes/Lobby.unity",
            "Assets/Scenes/Room.unity",
            "Assets/Scenes/Game.unity",
        };

        /// <summary>봇 실험실은 계측기다 — 개발용 빌드에만 넣는다.</summary>
        private const string BOTLAB_SCENE = "Assets/Scenes/BotLab.unity";

        [MenuItem("Tools/CTC/Build Windows (개발용 — 로그·콘솔 켜짐)")]
        public static void BuildDevelopment()
        {
            Run(DEV_DIR, BuildOptions.Development, "개발용", _includeBotLab: true);
        }

        [MenuItem("Tools/CTC/Build Windows (배포용)")]
        public static void BuildRelease()
        {
            Run(RELEASE_DIR, BuildOptions.None, "배포용", _includeBotLab: false);
        }

        private static void Run(string _outDir, BuildOptions _options, string _label, bool _includeBotLab)
        {
            string[] scenes = CollectScenes(_includeBotLab);
            if (scenes.Length == 0) return;

            // **매번 비우고 굽는다.** 이름을 바꾼 옛 exe(CTC.exe)나 지운 에셋의 잔재가 남으면
            // 그게 그대로 뎁포에 올라간다 — 뎁포는 폴더를 통째로 올리기 때문이다.
            if (Directory.Exists(_outDir)) Directory.Delete(_outDir, true);
            Directory.CreateDirectory(_outDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
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

            // 스팀 초기화용 — exe 옆에 있어야 AppID 인식 (정식 입점 전 480).
            // **뎁포에는 올리지 않는다** — 남으면 RestartAppIfNecessary가 항상 false다(app_build.vdf가 제외).
            File.WriteAllText(Path.Combine(_outDir, "steam_appid.txt"),
                Runtime.Networking.SteamBootstrap.APP_ID.ToString());

            CopyNotices(_outDir);

            Debug.Log($"[Build] {_label} 성공: {Path.GetFullPath(Path.Combine(_outDir, EXE_NAME))} " +
                      $"({report.summary.totalSize / (1024 * 1024)}MB · 씬 {scenes.Length}개) — 배포는 이 폴더째 압축");
        }

        /// <summary>
        /// 3자 고지문을 exe 옆에 놓는다. **의무다** — MIT·BSD·Apache·Boost·zlib과
        /// Unity Companion License 5조가 전부 바이너리 배포 시 저작권 고지를 요구한다.
        /// 특히 URP가 품은 NVIDIA FXAA는 *"Redistributions in binary form must reproduce
        /// the above copyright notice"* 라고 바이너리를 콕 집어 말한다.
        ///
        /// 손으로 복사하면 반드시 한 번은 빠뜨리므로 빌드가 매번 넣는다.
        /// </summary>
        private static void CopyNotices(string _outDir)
        {
            if (!File.Exists(NOTICES_TEMPLATE))
            {
                Debug.LogError($"[Build] 3자 고지문이 없다: {NOTICES_TEMPLATE} — 이대로 배포하면 라이선스 위반이다");
                return;
            }

            string company = PlayerSettings.companyName;
            if (string.IsNullOrWhiteSpace(company) || company == "DefaultCompany")
            {
                // 고지문 첫 줄이 저작권자다. 기본값이면 "DefaultCompany 소유"라고 적히는 셈이다.
                Debug.LogWarning("[Build] companyName이 아직 DefaultCompany다 — 고지문의 저작권자가 비어 있는 것과 같다");
            }

            string text = File.ReadAllText(NOTICES_TEMPLATE).Replace("__COMPANY__", company);
            File.WriteAllText(Path.Combine(_outDir, NOTICES_OUTPUT), text);
        }

        /// <summary>
        /// 씬이 하나라도 없으면 **굽지 않는다.** 빠진 채로 구우면 빌드는 성공하고
        /// 실행할 때에야 "씬을 못 찾는다"로 죽는다 — 그때는 원인이 안 보인다.
        /// </summary>
        private static string[] CollectScenes(bool _includeBotLab)
        {
            var list = new System.Collections.Generic.List<string>(SHIP_SCENES);
            if (_includeBotLab) list.Add(BOTLAB_SCENE);

            for (int i = 0; i < list.Count; i++)
            {
                if (File.Exists(list[i])) continue;

                Debug.LogError($"[Build] 씬이 없다: {list[i]} — 씬 생성 메뉴를 먼저 돌려라");
                return System.Array.Empty<string>();
            }
            return list.ToArray();
        }
    }
}
