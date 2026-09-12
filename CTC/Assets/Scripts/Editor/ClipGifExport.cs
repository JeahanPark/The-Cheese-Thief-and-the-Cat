using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace CTC.Editor
{
    /// <summary>
    /// <see cref="ClipPreviewRender"/>가 구운 프레임 PNG를 애니메이션 GIF로 묶어 준다.
    /// Tools > CTC > Dev > 클립 GIF 내보내기
    ///
    /// **두 반쪽을 일부러 나눈다**: 유니티 쪽(C#)은 프레임을 굽는 것까지만 하고, 그걸
    /// 움직이는 그림으로 조립하는 건 <c>tools/gif_from_frames.py</c>(Pillow)가 맡는다 —
    /// GIF 조립(팔레트 양자화·타이밍)은 이미지 처리 전용 도구가 훨씬 간단하고,
    /// 그 조립 로직을 게임 없이 pytest로 검증할 수 있다는 이점도 그대로 살아난다.
    /// 이 파일은 그 둘을 한 번의 메뉴 클릭으로 잇는 접착제일 뿐이다.
    /// </summary>
    public static class ClipGifExport
    {
        /// <summary>GIF로 내보낼 클립들 — 지금은 손으로 갓 만든 사격 클립 둘.</summary>
        private static readonly string[] CLIP_NAMES = { "ShootDraw", "Shoot" };

        private const int FPS = 24; // 클립 프레임레이트와 맞춘다.
        private const int UPSCALE = 2; // 768px 렌더를 그대로 두면 GIF가 작게 보인다 — 최근접 확대.

        [MenuItem("Tools/CTC/Dev/클립 GIF 내보내기")]
        public static void Export()
        {
            string pythonExe = FindPython();
            if (pythonExe == null)
            {
                Debug.LogError(
                    "[ClipGifExport] Python을 못 찾았다. https://www.python.org/downloads/ 에서 설치하고 " +
                    "PATH에 'python' 또는 'py'가 잡히는지 확인해라. " +
                    "설치가 어려우면 프레임은 이미 구워져 있으니 손으로 대신 돌려도 된다: " +
                    "python tools/gif_from_frames.py --dir <임시경로> --clip <클립명> --out <출력.gif>");
                return;
            }

            string scriptPath = Path.Combine(
                Directory.GetParent(UnityEngine.Application.dataPath).FullName,
                "tools", "gif_from_frames.py");
            if (!File.Exists(scriptPath))
            {
                Debug.LogError($"[ClipGifExport] 조립 스크립트를 못 찾았다: {scriptPath}");
                return;
            }

            foreach (string clipName in CLIP_NAMES)
            {
                // **먼저 지운다**: 같은 클립·같은 태그로 예전에 구운 프레임이 임시 폴더에
                // 남아 있으면(다른 길이의 클립이었을 때 등) 이번 세트와 섞여 인덱스가
                // 겹치거나 장수가 어긋난다. 매번 이 클립·이 태그 몫만 비우고 새로 굽는다.
                string tempDir = Path.GetTempPath();
                string frameGlobPrefix = $"ctc-clip-preview-gif-{clipName}-";
                foreach (string stale in Directory.GetFiles(tempDir, frameGlobPrefix + "*.png"))
                {
                    File.Delete(stale);
                }

                List<string> framePaths = ClipPreviewRender.RenderClipFramesForGif(clipName);
                if (framePaths == null || framePaths.Count == 0)
                {
                    Debug.LogError($"[ClipGifExport] {clipName} 프레임 렌더에 실패해 GIF를 생략한다");
                    continue;
                }

                string dir = Path.GetDirectoryName(framePaths[0]);
                string outPath = Path.Combine(dir, $"ctc-clip-preview-gif-{clipName}.gif");

                // **--dir/--clip이 아니라 --glob으로 태그까지 못 박는다**: 클립명만으로
                // 매칭하면(`*ShootDraw*.png`) 다른 메뉴(클립 미리보기 렌더·총 페이드 미리보기)가
                // 남긴 같은 클립명의 프레임(태그 pack/roundtrip/fade)까지 같이 걸려
                // 장수가 부풀고 순서가 뒤섞인다 — 실측(2026-09-12): ShootDraw 11장이어야
                // 할 GIF가 26장으로 나왔다. 이 메뉴가 방금 구운 "gif" 태그 프레임만 정확히 짚는다.
                string glob = Path.Combine(dir, frameGlobPrefix + "*.png");

                if (RunAssembler(pythonExe, scriptPath, glob, outPath))
                {
                    Debug.Log($"[ClipGifExport] {clipName} GIF 완료: {outPath}");
                }
            }
        }

        /// <summary>
        /// "python"과 "py"(윈도우 런처) 순서로 실제 실행되는지 --version으로 떠본다.
        /// 이 저장소 C# 쪽에는 파이썬을 부르는 기존 코드가 없어서(확인함) 하드코딩된 경로를
        /// 가정하지 않고 PATH를 그대로 신뢰한다 — 있으면 조용히 쓰고, 없으면 분명하게 알린다.
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

        private static bool RunAssembler(
            string _pythonExe, string _scriptPath, string _frameGlob, string _outPath)
        {
            string arguments = string.Join(" ", new[]
            {
                Quote(_scriptPath),
                "--glob", Quote(_frameGlob),
                "--out", Quote(_outPath),
                "--fps", FPS.ToString(),
                "--scale", UPSCALE.ToString(),
            });

            var psi = new ProcessStartInfo(_pythonExe, arguments)
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
                process.WaitForExit(60000);

                if (!string.IsNullOrEmpty(stdout)) Debug.Log($"[ClipGifExport] {stdout.Trim()}");

                if (process.ExitCode != 0)
                {
                    Debug.LogError(
                        $"[ClipGifExport] gif_from_frames.py 실패(exit {process.ExitCode}): {stderr}");
                    return false;
                }
                return true;
            }
        }

        /// <summary>경로에 공백이 낄 수 있어(임시 폴더 등) 항상 따옴표로 감싼다.</summary>
        private static string Quote(string _s) => $"\"{_s}\"";
    }
}
