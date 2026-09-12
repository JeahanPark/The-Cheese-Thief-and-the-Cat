using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace CTC.Editor
{
    /// <summary>
    /// <see cref="ClipPreviewRender"/>가 구운 프레임 PNG + 클립 원본 .py를 **검수 페이지 한 장**으로
    /// 묶는다. Tools > CTC > Dev > 클립 검수 페이지 만들기
    ///
    /// **GIF와 뭐가 다른가**(<see cref="ClipGifExport"/>): GIF는 움직임만 보여 주고 세우지를
    /// 못한다. 「5프레임 몸통이 -32도인 게 이 그림이 맞나」를 판정하려면 그 프레임에서
    /// 멈추고 숫자를 봐야 한다. 이 페이지는 재생·한 프레임씩·스크러버·속도에 더해
    /// **보고 있는 프레임의 열이 같이 하이라이트되는 포즈 각도 표**를 붙인다.
    ///
    /// **표는 클립 원본에서 파싱한다 — 손으로 옮겨 적지 않는다.** 각도를 굴리는 도중에
    /// 페이지만 옛 숫자를 들고 있으면 검수 도구가 조용히 거짓말을 한다. 그래서 이 메뉴는
    /// 프레임을 굽고 나서 <c>tools/clip_review_page.py</c>에 **클립 원본 .py 경로를 같이
    /// 넘긴다**. 파이썬 쪽이 그 파일을 읽어 표·박자·주석을 만든다.
    ///
    /// 두 반쪽을 나눈 이유는 GIF 쪽과 같다: 조립 로직을 게임 없이 pytest로 검증할 수 있다.
    /// </summary>
    public static class ClipReviewPageExport
    {
        /// <summary>
        /// 한 페이지에 같이 올릴 클립들. **Shoot과 ShootDraw는 반드시 같이 본다** —
        /// 둘이 이어 붙어 도는 사이라 발사 자세가 서로 어긋나면 연사가 튄다.
        /// </summary>
        private static readonly string[] CLIP_NAMES = { "ShootDraw", "Shoot" };

        private const int FPS = 24;       // 클립 프레임레이트와 맞춘다.
        private const int FRAME_WIDTH = 512; // 768 렌더를 줄인다 — 두 클립이면 페이지가 1.1MB 안쪽.

        private const string PAGE_FILE_NAME = "ctc-clip-review.html";
        private const string PYTHON_SCRIPT = "clip_review_page.py";

        /// <summary>클립 원본 .py가 사는 곳 — 레포 루트 기준이다(유니티 프로젝트 밖).</summary>
        private const string CLIP_DEF_DIR = "tools/blender/clips";

        [MenuItem("Tools/CTC/Dev/클립 검수 페이지 만들기")]
        public static void Export()
        {
            string pythonExe = FindPython();
            if (pythonExe == null)
            {
                Debug.LogError(
                    "[ClipReviewPageExport] Python을 못 찾았다. https://www.python.org/downloads/ 에서 " +
                    "설치하고 PATH에 'python' 또는 'py'가 잡히는지 확인해라.");
                return;
            }

            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string repoRoot = Directory.GetParent(projectRoot).FullName;

            string scriptPath = Path.Combine(projectRoot, "tools", PYTHON_SCRIPT);
            if (!File.Exists(scriptPath))
            {
                Debug.LogError($"[ClipReviewPageExport] 조립 스크립트를 못 찾았다: {scriptPath}");
                return;
            }

            var argumentTokens = new List<string> { Quote(scriptPath) };
            string frameDir = null;

            foreach (string clipName in CLIP_NAMES)
            {
                // 클립 원본이 없으면 표를 만들 수 없다. **여기서 멈춘다** — 그림만 있고
                // 숫자가 없는 페이지는 GIF보다 나을 게 없는데 이름만 「검수」가 된다.
                string clipDef = Path.Combine(repoRoot, CLIP_DEF_DIR, clipName.ToLowerInvariant() + ".py");
                if (!File.Exists(clipDef))
                {
                    Debug.LogError(
                        $"[ClipReviewPageExport] {clipName} 클립 원본을 못 찾았다: {clipDef} — " +
                        "이 파일이 포즈 각도 표의 원본이라 없으면 페이지를 만들지 않는다.");
                    return;
                }

                // **먼저 지운다**: 같은 클립·같은 태그로 예전에 구운 프레임이 남아 있으면
                // (길이가 달랐던 시절 등) 이번 세트와 섞여 장수가 어긋난다(ClipGifExport가
                // 실제로 그렇게 당했다 — 11장이어야 할 GIF가 26장이 됐다).
                string tempDir = Path.GetTempPath();
                string framePrefix = $"ctc-clip-preview-gif-{clipName}-";
                foreach (string stale in Directory.GetFiles(tempDir, framePrefix + "*.png"))
                {
                    File.Delete(stale);
                }

                // GIF와 같은 렌더 경로를 그대로 쓴다 — 블렌더 프레임 하나하나가 한 장씩
                // 나와야 스크러버의 한 칸이 클립의 한 프레임과 맞아떨어진다.
                List<string> framePaths = ClipPreviewRender.RenderClipFramesForGif(clipName);
                if (framePaths == null || framePaths.Count == 0)
                {
                    Debug.LogError($"[ClipReviewPageExport] {clipName} 프레임 렌더에 실패했다");
                    return;
                }

                frameDir = Path.GetDirectoryName(framePaths[0]);

                // **--dir/클립명이 아니라 태그까지 박은 글롭**을 넘긴다: 클립명만으로 매칭하면
                // 다른 메뉴(미리보기·페이드)가 남긴 같은 클립명의 프레임까지 같이 걸린다.
                argumentTokens.Add("--clip-def");
                argumentTokens.Add(Quote(clipDef));
                argumentTokens.Add("--frames");
                argumentTokens.Add(Quote(Path.Combine(frameDir, framePrefix + "*.png")));
            }

            string outPath = Path.Combine(frameDir, PAGE_FILE_NAME);
            argumentTokens.Add("--out");
            argumentTokens.Add(Quote(outPath));
            argumentTokens.Add("--fps");
            argumentTokens.Add(FPS.ToString());
            argumentTokens.Add("--size");
            argumentTokens.Add(FRAME_WIDTH.ToString());
            argumentTokens.Add("--title");
            argumentTokens.Add(Quote("쥐 사격 클립 검수"));

            if (!RunBuilder(pythonExe, string.Join(" ", argumentTokens))) return;

            Debug.Log($"[ClipReviewPageExport] 검수 페이지 완료: {outPath}");
            EditorUtility.OpenWithDefaultApp(outPath);
        }

        /// <summary>
        /// "python"과 "py"(윈도우 런처) 순서로 실제 실행되는지 --version으로 떠본다.
        /// <see cref="ClipGifExport"/>와 같은 방식이다 — 하드코딩된 경로를 가정하지 않는다.
        /// </summary>
        private static string FindPython()
        {
            foreach (string candidate in new[] { "python", "py" })
            {
                if (CanRun(candidate)) return candidate;
            }
            return null;
        }

        private static bool CanRun(string _exe)
        {
            try
            {
                var psi = new ProcessStartInfo(_exe, "--version")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using (var process = Process.Start(psi))
                {
                    process.WaitForExit(5000);
                    return process.ExitCode == 0;
                }
            }
            catch (Win32Exception)
            {
                // 실행 파일 자체가 없을 때 여기로 온다 — 조용히 다음 후보로 넘어간다.
                return false;
            }
        }

        private static bool RunBuilder(string _pythonExe, string _arguments)
        {
            var psi = new ProcessStartInfo(_pythonExe, _arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(psi))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(120000);

                if (!string.IsNullOrEmpty(stdout)) Debug.Log($"[ClipReviewPageExport] {stdout.Trim()}");

                if (process.ExitCode != 0)
                {
                    Debug.LogError(
                        $"[ClipReviewPageExport] {PYTHON_SCRIPT} 실패(exit {process.ExitCode}): {stderr}");
                    return false;
                }
                return true;
            }
        }

        /// <summary>경로에 공백이 낄 수 있어(임시 폴더 등) 항상 따옴표로 감싼다.</summary>
        private static string Quote(string _s) => $"\"{_s}\"";
    }
}
