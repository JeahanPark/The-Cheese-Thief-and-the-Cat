using System.Collections.Generic;
using CTC.Editor;
using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 고양이 블렌더 왕복(2026-09-13, <c>art/blender/cat.blend</c> → <c>CTC_Cat_Clips.fbx</c>).
    /// 쥐 쪽 <see cref="ClipParityProbeTests"/>와 같은 세 가지를 고양이에 대해 본다 — 셋 다 **에러 없이** 깨진다:
    /// 왕복한 Walk가 팩 Walk와 다르다(스케일·축 사고) · 클립 이름이 <c>Rig|Walk</c>로 샌다(메타 매핑 유실) ·
    /// 고양이 전용 클립이 FBX에 없다(상태가 빈 채로 굽힌다).
    /// </summary>
    public class CatClipRoundTripTests
    {
        private const string PACK_FBX =
            "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Animations/Cat_Animations.fbx";

        private static List<AnimationClip> RealClips(string _assetPath)
        {
            var clips = new List<AnimationClip>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(_assetPath))
            {
                if (!(o is AnimationClip clip)) continue;
                if (clip.name.StartsWith("__preview__")) continue;
                if ((clip.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
                clips.Add(clip);
            }
            return clips;
        }

        private static void RequireFbx()
        {
            if (AssetDatabase.LoadMainAssetAtPath(RoleClipStates.CAT_CLIPS_FBX) == null)
                Assert.Ignore($"고양이 왕복 FBX가 없다: {RoleClipStates.CAT_CLIPS_FBX}");
        }

        [Test]
        public void RoundTrippedCatWalk_MatchesPackCatWalk()
        {
            RequireFbx();
            AnimationClip pack = ClipParityProbe.FindClip(PACK_FBX, "Walk");
            AnimationClip made = ClipParityProbe.FindClip(RoleClipStates.CAT_CLIPS_FBX, "Walk");
            Assert.IsNotNull(pack, "팩 고양이 Walk가 없다");
            Assert.IsNotNull(made, "왕복 고양이 Walk가 없다 — 메타의 takeName 매핑을 봐라");

            ParityReport report = ClipParityLogic.Compare(
                ClipParityProbe.SampleClip(pack, ClipParityProbe.SAMPLE_COUNT),
                ClipParityProbe.SampleClip(made, ClipParityProbe.SAMPLE_COUNT),
                ClipParityProbe.EPSILON);
            Assert.IsTrue(report.IsIdentical, report.ToText());
            Assert.AreEqual(pack.length, made.length, 1e-3f, "길이가 다르다");
        }

        [Test]
        public void CatClipNames_AreMapped_AndIncludeCatOnly()
        {
            RequireFbx();
            var names = new List<string>();
            foreach (AnimationClip c in RealClips(RoleClipStates.CAT_CLIPS_FBX)) names.Add(c.name);

            Assert.IsFalse(names.Exists(n => n.Contains("|")),
                $"'|'가 든 클립 이름이 있다({string.Join(", ", names)}) — 메타가 새로 생겨 takeName 매핑이 사라졌다");
            foreach (string s in AnimationStateLogic.CatOnly)
                CollectionAssert.Contains(names, s, $"CTC_Cat_Clips.fbx에 '{s}'가 없다. 읽힌 것: {string.Join(", ", names)}");
        }
    }
}
