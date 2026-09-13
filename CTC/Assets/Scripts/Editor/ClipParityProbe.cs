using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 클립 두 개가 **같은 동작인지** 잰다. 블렌더 왕복이 뼈를 틀어뜨렸는지 보는 자다.
    /// Tools > CTC > Dev > Probe Clip Parity
    ///
    /// 씬에 세워서 재지 않는다. `AnimationUtility.GetEditorCurve`로 **클립 안의 커브를 직접**
    /// 평가한다 — 본을 코드로 돌려도 에디터에서 스킨드 메시가 다시 스키닝되지 않기 때문이다
    /// (CLAUDE.md 애니메이션 함정). 커브를 직접 읽으면 그 문제를 통째로 비껴간다.
    /// </summary>
    public static class ClipParityProbe
    {
        /// <summary>팩에 없고 일부러 더한 뼈 — 무릎(2026-09-13, <c>tools/blender/add_knees.py</c>).</summary>
        public static readonly string[] INTENTIONAL_BONES = { "shin.B.L", "shin.B.R" };

        private const string PACK_MOUSE_FBX =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Animations/Mouse_Animations.fbx";

        private const string ROUNDTRIP_FBX = "Assets/Animations/Pets/CTC_Mouse_Clips.fbx";
        private const string ROUNDTRIP_CLIP = "Walk";

        /// <summary>클립당 균등 샘플 수. 테스트도 이 값을 그대로 쓴다 — 따로 들고 있으면 튜닝 때 어긋난다.</summary>
        public const int SAMPLE_COUNT = 21;

        /// <summary>값 비교 허용 오차. 테스트도 이 값을 그대로 쓴다.</summary>
        public const float EPSILON = 1e-3f;

        /// <summary>클립 길이·프레임레이트 비교 허용 오차. 부동소수 저장 오차 수준이라 값 비교보다 훨씬 빡빡할 필요는 없다.</summary>
        public const float SETTING_EPSILON = 1e-3f;

        private static string ReportPath => Path.Combine(Path.GetTempPath(), "ctc-clip-parity.txt");

        /// <summary>
        /// 클립의 모든 커브를 <paramref name="_sampleCount"/>번 균등하게 잰다.
        /// 키는 `경로:프로퍼티` — **이 문자열이 곧 유니티가 뼈를 찾는 방법이다.**
        /// </summary>
        public static CurveSample[] SampleClip(AnimationClip _clip, int _sampleCount)
        {
            if (_sampleCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(_sampleCount), _sampleCount,
                    "샘플은 최소 2개(양 끝) 있어야 한다 — 이보다 적으면 구간을 하나도 못 재고도 '같다'가 나올 수 있다");
            }

            if (_clip == null) return new CurveSample[0];

            var result = new List<CurveSample>();
            float length = _clip.length;

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(_clip))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(_clip, binding);
                if (curve == null) continue;

                var values = new float[_sampleCount];
                for (int i = 0; i < _sampleCount; i++)
                {
                    float t = length * i / (_sampleCount - 1);
                    values[i] = curve.Evaluate(t);
                }

                result.Add(new CurveSample($"{binding.path}:{binding.propertyName}", values));
            }

            return result.ToArray();
        }

        /// <summary>
        /// 커브 밖의 클립 설정을 견준다. 커브가 값까지 같아도 길이·프레임레이트·반복은
        /// 커브 비교가 못 본다 — 길이가 다르면 같은 모양이 시간축으로 늘어난 것이라
        /// (정규화된 지점마다 재므로) **매 지점에서 똑같이 찍힌다.**
        /// </summary>
        public static void CompareClipSettings(AnimationClip _a, AnimationClip _b, ParityReport _report)
        {
            if (Mathf.Abs(_a.length - _b.length) > SETTING_EPSILON)
            {
                _report.SettingMismatches.Add($"길이: {_a.length:F5} vs {_b.length:F5}");
            }

            if (Mathf.Abs(_a.frameRate - _b.frameRate) > SETTING_EPSILON)
            {
                _report.SettingMismatches.Add($"프레임레이트: {_a.frameRate:F5} vs {_b.frameRate:F5}");
            }

            if (_a.isLooping != _b.isLooping)
            {
                _report.SettingMismatches.Add($"반복: {_a.isLooping} vs {_b.isLooping}");
            }
        }

        [MenuItem("Tools/CTC/Dev/Probe Clip Parity")]
        public static void Probe()
        {
            AnimationClip packWalk = FindClip(PACK_MOUSE_FBX, ROUNDTRIP_CLIP);
            AnimationClip madeWalk = FindClip(ROUNDTRIP_FBX, ROUNDTRIP_CLIP);

            if (packWalk == null)
            {
                Debug.LogError($"[ClipParityProbe] 팩 '{ROUNDTRIP_CLIP}'을 못 찾았다: {PACK_MOUSE_FBX}");
                return;
            }

            if (madeWalk == null)
            {
                Debug.LogError(
                    $"[ClipParityProbe] 왕복 '{ROUNDTRIP_CLIP}'을 못 찾았다: {ROUNDTRIP_FBX}\n" +
                    "아직 내보내지 않았거나, 유니티가 클립을 하나도 못 읽었다(= 리그 인식 실패).\n" +
                    DescribeClipsIn(ROUNDTRIP_FBX));
                return;
            }

            CurveSample[] packSamples = SampleClip(packWalk, SAMPLE_COUNT);
            CurveSample[] madeSamples = SampleClip(madeWalk, SAMPLE_COUNT);

            // 무릎(shin.B.*)은 팩에 없는 뼈를 일부러 더한 것이다(2026-09-13) — Walk에서 가만히 있으면 같다고 본다.
            ParityReport report = ClipParityLogic.Compare(packSamples, madeSamples, EPSILON, INTENTIONAL_BONES);
            CompareClipSettings(packWalk, madeWalk, report);

            string text =
                $"=== 클립 동일성 ===\nA(팩)   {PACK_MOUSE_FBX} :: {ROUNDTRIP_CLIP}\n" +
                $"B(왕복) {ROUNDTRIP_FBX} :: {ROUNDTRIP_CLIP}\n" +
                $"길이 {packWalk.length:F4}s vs {madeWalk.length:F4}s · " +
                $"fps {packWalk.frameRate:F1} vs {madeWalk.frameRate:F1} · " +
                $"반복 {packWalk.isLooping} vs {madeWalk.isLooping}\n" +
                $"원커브 수 {packSamples.Length} vs {madeSamples.Length}\n\n" +
                report.ToText();

            File.WriteAllText(ReportPath, text);

            if (report.IsIdentical) Debug.Log($"[ClipParityProbe] 같다 → {ReportPath}\n{text}");
            else Debug.LogError($"[ClipParityProbe] 다르다 → {ReportPath}\n{text}");
        }

        public static AnimationClip FindClip(string _assetPath, string _clipName)
        {
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(_assetPath))
            {
                var clip = o as AnimationClip;
                if (clip != null && clip.name == _clipName) return clip;
            }
            return null;
        }

        /// <summary>
        /// 원하는 클립이 없을 때 그 파일에 **실제로 뭐가 있는지** 알려준다. 블렌더가 테이크를
        /// 하나로 뭉쳐 `Take 001`만 남기는 실패가 예측돼 있다 — 이름만 보고 헤매지 않게 한다.
        /// </summary>
        private static string DescribeClipsIn(string _assetPath)
        {
            var names = new List<string>();
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(_assetPath))
            {
                var clip = o as AnimationClip;
                if (clip != null) names.Add(clip.name);
            }

            if (names.Count == 0) return $"{_assetPath}: 클립이 하나도 없다";

            var sb = new StringBuilder();
            sb.Append($"{_assetPath}에 실제로 있는 클립({names.Count}개): ");
            sb.Append(string.Join(", ", names));
            return sb.ToString();
        }
    }
}
