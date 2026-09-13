using System;
using System.Collections.Generic;
using CTC.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// **자가 잴 수 있는지를 잰다.** 블렌더는 아직 안 쓴다 — 팩 클립끼리만 견준다.
    ///
    /// 같은 클립을 자기 자신과 견줘 "같다"가 나오는 것만으로는 부족하다. 그건 항진명제라
    /// 비교가 통째로 죽어 있어도 통과한다. **다른 클립을 "다르다"고 말해야** 자가 살아 있다.
    /// </summary>
    public class ClipParityProbeTests
    {
        private const string PACK_FBX =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Animations/Mouse_Animations.fbx";

        private const string ROUNDTRIP_FBX = "Assets/Animations/Pets/CTC_Mouse_Clips.fbx";

        private static AnimationClip Pack(string _name)
        {
            AnimationClip clip = ClipParityProbe.FindClip(PACK_FBX, _name);
            if (clip == null) Assert.Ignore($"팩 원본에 '{_name}'이 없다: {PACK_FBX}");
            return clip;
        }

        /// <summary>
        /// 왕복 FBX를 찾는다. 파일 자체가 없으면(레포를 받았지만 아직 블렌더 왕복을 안 돌린
        /// 경우) 실패가 아니라 스킵이어야 한다 — 이 테스트들은 산출물이 있을 때만 의미가 있다.
        /// </summary>
        private static AnimationClip RoundTrip(string _name)
        {
            if (AssetDatabase.LoadMainAssetAtPath(ROUNDTRIP_FBX) == null)
            {
                Assert.Ignore($"왕복 FBX가 없다(아직 안 만들었거나 지워짐): {ROUNDTRIP_FBX}");
            }

            AnimationClip clip = ClipParityProbe.FindClip(ROUNDTRIP_FBX, _name);
            return clip;
        }

        /// <summary>
        /// 에셋에 든 **사람이 쓰는 클립**만. `LoadAllAssetsAtPath`는 유니티 내부의 `__preview__`
        /// 클립까지 돌려주는데, 그쪽은 FBX의 **원본 테이크 이름**(`Rig|Walk`)을 그대로 달고 있어
        /// 이름을 검사하면 실제와 다른 것을 본다(2026-09-12 실측).
        /// </summary>
        private static List<AnimationClip> RealClips(string _assetPath)
        {
            var clips = new List<AnimationClip>();
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(_assetPath))
            {
                var clip = o as AnimationClip;
                if (clip == null) continue;
                if (clip.name.StartsWith("__preview__")) continue;
                if ((clip.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
                clips.Add(clip);
            }
            return clips;
        }

        [Test]
        public void SampleClip_FindsBoneCurves()
        {
            CurveSample[] samples = ClipParityProbe.SampleClip(Pack("Walk"), ClipParityProbe.SAMPLE_COUNT);

            Assert.Greater(samples.Length, 0, "커브를 하나도 못 뽑았다 — 채취가 죽어 있다");

            bool sawRig = false;
            foreach (CurveSample s in samples)
            {
                if (s.Key.StartsWith("Rig/root")) { sawRig = true; break; }
            }
            Assert.IsTrue(sawRig, "경로가 'Rig/root...'로 안 나온다 — 유니티가 뼈를 못 찾는다는 뜻");
        }

        [Test]
        public void Walk_ComparedToItself_IsIdentical()
        {
            AnimationClip walk = Pack("Walk");
            ParityReport r = ClipParityLogic.Compare(
                ClipParityProbe.SampleClip(walk, ClipParityProbe.SAMPLE_COUNT),
                ClipParityProbe.SampleClip(walk, ClipParityProbe.SAMPLE_COUNT), ClipParityProbe.EPSILON);

            Assert.IsTrue(r.IsIdentical, r.ToText());
        }

        [Test]
        public void Walk_ComparedToRun_IsDifferent()
        {
            ParityReport r = ClipParityLogic.Compare(
                ClipParityProbe.SampleClip(Pack("Walk"), ClipParityProbe.SAMPLE_COUNT),
                ClipParityProbe.SampleClip(Pack("Run"), ClipParityProbe.SAMPLE_COUNT), ClipParityProbe.EPSILON);

            Assert.IsFalse(r.IsIdentical,
                "Walk와 Run을 '같다'고 한다 — 자가 아무것도 안 재고 있다");
        }

        [Test]
        public void PackWalk_IsWithinTenFrames()
        {
            AnimationClip walk = Pack("Walk");

            Assert.LessOrEqual(walk.length * walk.frameRate, 10.5f,
                $"팩 예산이 0~10프레임인데 {walk.length * walk.frameRate:F1}프레임이다 — " +
                "새 클립의 기준이 흔들린다");
        }

        [Test]
        public void SampleClip_RejectsFewerThanTwoSamples()
        {
            AnimationClip walk = Pack("Walk");

            Assert.Throws<ArgumentOutOfRangeException>(() => ClipParityProbe.SampleClip(walk, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClipParityProbe.SampleClip(walk, 1));
        }

        /// <summary>
        /// 인메모리 클립을 만든다. 팩 에셋에 기대지 않아 조건을 하나씩 정확히 통제할 수 있다.
        /// `length`는 커브의 마지막 키프레임 시각에서 따라온다 — 직접 세팅할 수 없다.
        /// </summary>
        private static AnimationClip MakeClip(float _curveEndTime, float _frameRate, bool _looping)
        {
            var clip = new AnimationClip { frameRate = _frameRate };
            clip.SetCurve("Rig/root", typeof(Transform), "m_LocalPosition.x",
                AnimationCurve.Linear(0f, 0f, _curveEndTime, 1f));

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = _looping;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            return clip;
        }

        [Test]
        public void ClipSettings_Identical_ReportsNoMismatch()
        {
            AnimationClip a = MakeClip(0.5f, 24f, true);
            AnimationClip b = MakeClip(0.5f, 24f, true);
            try
            {
                var report = new ParityReport();
                ClipParityProbe.CompareClipSettings(a, b, report);

                CollectionAssert.IsEmpty(report.SettingMismatches, report.ToText());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void ClipSettings_DifferentLength_IsReported()
        {
            AnimationClip a = MakeClip(0.5f, 24f, true);
            AnimationClip b = MakeClip(1.0f, 24f, true);
            try
            {
                var report = new ParityReport();
                ClipParityProbe.CompareClipSettings(a, b, report);

                Assert.AreEqual(1, report.SettingMismatches.Count, report.ToText());
                string line = report.SettingMismatches[0];
                StringAssert.Contains("길이", line);
                StringAssert.Contains(a.length.ToString("F5"), line);
                StringAssert.Contains(b.length.ToString("F5"), line);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void ClipSettings_DifferentFrameRate_IsReported()
        {
            AnimationClip a = MakeClip(0.5f, 24f, true);
            AnimationClip b = MakeClip(0.5f, 30f, true);
            try
            {
                var report = new ParityReport();
                ClipParityProbe.CompareClipSettings(a, b, report);

                Assert.AreEqual(1, report.SettingMismatches.Count, report.ToText());
                string line = report.SettingMismatches[0];
                StringAssert.Contains("프레임레이트", line);
                StringAssert.Contains(a.frameRate.ToString("F5"), line);
                StringAssert.Contains(b.frameRate.ToString("F5"), line);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void ClipSettings_DifferentLooping_IsReported()
        {
            AnimationClip a = MakeClip(0.5f, 24f, true);
            AnimationClip b = MakeClip(0.5f, 24f, false);
            try
            {
                var report = new ParityReport();
                ClipParityProbe.CompareClipSettings(a, b, report);

                Assert.AreEqual(1, report.SettingMismatches.Count, report.ToText());
                string line = report.SettingMismatches[0];
                StringAssert.Contains("반복", line);
                StringAssert.Contains(a.isLooping.ToString(), line);
                StringAssert.Contains(b.isLooping.ToString(), line);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }

        /// <summary>
        /// **진짜 왕복 검사.** 팩 원본 Walk와 블렌더를 거쳐 나온 Walk가 커브까지 같은지 잰다.
        /// 산출물이 없으면(레포만 받은 상태) 스킵 — 있는데 다르면 반드시 실패해야 한다.
        /// </summary>
        [Test]
        public void RoundTrippedWalk_MatchesPackWalk()
        {
            AnimationClip packWalk = Pack("Walk");
            AnimationClip roundTripWalk = RoundTrip("Walk");
            if (roundTripWalk == null) Assert.Ignore($"왕복 FBX에 'Walk'가 없다: {ROUNDTRIP_FBX}");

            ParityReport report = ClipParityLogic.Compare(
                ClipParityProbe.SampleClip(packWalk, ClipParityProbe.SAMPLE_COUNT),
                ClipParityProbe.SampleClip(roundTripWalk, ClipParityProbe.SAMPLE_COUNT),
                ClipParityProbe.EPSILON, ClipParityProbe.INTENTIONAL_BONES);

            Assert.IsTrue(report.IsIdentical, report.ToText());
        }

        /// <summary>
        /// 커브 비교가 못 보는 부분(길이·프레임레이트·반복)을 따로 잰다 — 정규화된 지점마다
        /// 재는 커브 비교는 길이가 늘어나도 매 지점에서 같은 값을 찍기 때문이다.
        /// </summary>
        [Test]
        public void RoundTrippedWalk_HasSameClipSettings()
        {
            AnimationClip packWalk = Pack("Walk");
            AnimationClip roundTripWalk = RoundTrip("Walk");
            if (roundTripWalk == null) Assert.Ignore($"왕복 FBX에 'Walk'가 없다: {ROUNDTRIP_FBX}");

            Assert.AreEqual(packWalk.length, roundTripWalk.length, ClipParityProbe.SETTING_EPSILON,
                $"길이가 다르다: {packWalk.length:F5} vs {roundTripWalk.length:F5}");
            Assert.AreEqual(packWalk.frameRate, roundTripWalk.frameRate, ClipParityProbe.SETTING_EPSILON,
                $"프레임레이트가 다르다: {packWalk.frameRate:F5} vs {roundTripWalk.frameRate:F5}");
            Assert.AreEqual(packWalk.isLooping, roundTripWalk.isLooping,
                $"반복 여부가 다르다: {packWalk.isLooping} vs {roundTripWalk.isLooping}");
        }

        /// <summary>
        /// `CTC_Mouse_Clips.fbx.meta`의 `clipAnimations`(손으로 쓴 `takeName: Rig|Walk` →
        /// `name: Walk` 매핑)를 지키는 회귀 테스트다. 메타가 새로 생성되면(리임포트,
        /// 임포터 Reset) 이 매핑이 사라지고 클립이 조용히 `Rig|Walk`로 되돌아간다 —
        /// 아무 데도 에러가 안 나서 이 테스트가 없으면 아무도 못 본다.
        /// </summary>
        [Test]
        public void RoundTrippedClip_IsNamedWalk_NotRigPipeWalk()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ROUNDTRIP_FBX) == null)
            {
                Assert.Ignore($"왕복 FBX가 없다(아직 안 만들었거나 지워짐): {ROUNDTRIP_FBX}");
            }

            const string RECOVERY_HINT =
                "메타가 새로 생성되면서 clipAnimations의 takeName 매핑이 사라졌다 — " +
                "임포터의 Animation 탭에서 클립 이름을 다시 'Walk'로 잡아야 한다.";

            var names = new List<string>();
            foreach (AnimationClip clip in RealClips(ROUNDTRIP_FBX)) names.Add(clip.name);

            bool hasWalk = names.Contains("Walk");
            bool hasPipe = names.Exists(n => n.Contains("|"));

            Assert.IsTrue(hasWalk,
                $"'Walk'라는 이름의 클립이 없다(실제: {string.Join(", ", names)}). " + RECOVERY_HINT);
            Assert.IsFalse(hasPipe,
                $"'|'가 든 클립 이름이 있다(실제: {string.Join(", ", names)}) — 블렌더가 하드코딩한 " +
                "'Rig|Walk' 같은 테이크 이름이 그대로 새어나온 것이다. " + RECOVERY_HINT);
        }

        /// <summary>
        /// 팩에 있는 클립 이름이 왕복 FBX에도 전부 있는지 본다. 테이크가 하나로 뭉개지는
        /// 실패(전부 `Take 001`)는 이름 하나만 보면 못 잡고, 목록 전체를 대조해야 잡힌다.
        /// </summary>
        [Test]
        public void RoundTrippedFbx_HasAllPackClips()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ROUNDTRIP_FBX) == null)
            {
                Assert.Ignore($"왕복 FBX가 없다(아직 안 만들었거나 지워짐): {ROUNDTRIP_FBX}");
            }

            var packNames = new List<string>();
            foreach (AnimationClip clip in RealClips(PACK_FBX)) packNames.Add(clip.name);

            var roundTripNames = new List<string>();
            foreach (AnimationClip clip in RealClips(ROUNDTRIP_FBX)) roundTripNames.Add(clip.name);

            var missing = new List<string>();
            foreach (string name in packNames)
            {
                if (!roundTripNames.Contains(name)) missing.Add(name);
            }

            Assert.IsEmpty(missing,
                $"팩에는 있는데 왕복에는 없는 클립: {string.Join(", ", missing)} " +
                $"(왕복 FBX에 실제로 있는 클립: {string.Join(", ", roundTripNames)})");
        }
    }
}
