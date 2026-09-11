using System;
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

        private static AnimationClip Pack(string _name)
        {
            AnimationClip clip = ClipParityProbe.FindClip(PACK_FBX, _name);
            if (clip == null) Assert.Ignore($"팩 원본에 '{_name}'이 없다: {PACK_FBX}");
            return clip;
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
    }
}
