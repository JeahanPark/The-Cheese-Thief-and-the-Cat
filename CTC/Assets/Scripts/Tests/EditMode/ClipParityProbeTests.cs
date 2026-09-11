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

        [Test]
        public void ClipSettings_AreComparedAgainstPack()
        {
            AnimationClip walk = Pack("Walk");
            var report = new ParityReport();

            ClipParityProbe.CompareClipSettings(walk, walk, report);

            CollectionAssert.IsEmpty(report.SettingMismatches, report.ToText());
        }
    }
}
