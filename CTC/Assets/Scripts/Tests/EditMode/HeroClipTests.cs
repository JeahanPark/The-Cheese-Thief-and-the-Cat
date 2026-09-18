using CTC.Editor;
using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 용사 클립 셋(2026-09-19) — 휘두르기·변신은 쥐 전용, 스턴은 **쥐·고양이 둘 다**다(칼의 주 표적이 고양이다).
    /// 셋 다 **에러 없이** 깨진다: 목록에서 빠지면 굽는 메뉴가 상태를 안 만들어 <c>CrossFadeInFixedTime</c>이 아무 일도 안 하고,
    /// 클립 길이가 칼 시간표와 어긋나면 칼이 허공에서 휘둘린다(칼 방향은 코드가 준다 — 리그에 손목이 없다).
    /// </summary>
    public class HeroClipTests
    {
        private const float HALF_FRAME = 0.5f / HeroSwordVisualLogic.FPS;

        [NUnit.Framework.Property("CaseId", "B-33")]
        [Test]
        public void Stunned_IsSharedByBothControllers_SwordClipsAreMouseOnly()
        {
            CollectionAssert.Contains(AnimationStateLogic.CustomShared, AnimationStateLogic.Stunned);
            CollectionAssert.Contains(AnimationStateLogic.Shared, AnimationStateLogic.Stunned,
                "스턴은 고양이도 맞는다 — 두 컨트롤러가 다 가져야 한다");
            foreach (string s in AnimationStateLogic.CustomShared)
            {
                CollectionAssert.DoesNotContain(AnimationStateLogic.MouseOnly, s);
                CollectionAssert.DoesNotContain(AnimationStateLogic.CatOnly, s);
            }
            CollectionAssert.IsSubsetOf(new[] { AnimationStateLogic.SwordSwing, AnimationStateLogic.HeroDraw },
                AnimationStateLogic.MouseOnly, "칼 클립이 MouseOnly에서 빠지면 Bake Mouse Gun이 AC_Mouse에 상태를 안 만든다");
        }

        [NUnit.Framework.Property("CaseId", "B-33")]
        [Test]
        [NUnit.Framework.Category("Asset")]
        public void HeroClipLengths_MatchTheSwordTimelines()
        {
            AnimationClip swing = ClipParityProbe.FindClip(RoleClipStates.MOUSE_CLIPS_FBX, AnimationStateLogic.SwordSwing);
            AnimationClip draw = ClipParityProbe.FindClip(RoleClipStates.MOUSE_CLIPS_FBX, AnimationStateLogic.HeroDraw);
            Assert.IsNotNull(swing, "CTC_Mouse_Clips.fbx에 SwordSwing이 없다 — 메타의 takeName 매핑을 봐라");
            Assert.IsNotNull(draw, "CTC_Mouse_Clips.fbx에 HeroDraw가 없다");
            Assert.AreEqual(HeroSwordVisualLogic.SWING_SECONDS, swing.length, HALF_FRAME,
                "휘두르기 클립 길이 ≠ 칼 시간표 — sword_swing.py FRAME_END와 HeroSwordVisualLogic을 같이 고쳐라");
            Assert.AreEqual(HeroTransformLogic.SECONDS, draw.length, HALF_FRAME,
                "변신 클립 길이 ≠ 잠금 시간 — hero_draw.py FRAME_END와 HeroTransformLogic을 같이 고쳐라");
            Assert.IsFalse(swing.isLooping, "휘두르기는 원샷이다");
            Assert.IsFalse(draw.isLooping, "변신은 원샷이다");
        }

        /// <summary>스턴은 0.8초(CSV)지만 바뀔 수 있어 **반복 클립**이다 — 루프가 꺼지면 짧은 클립 끝에서 굳어 선다.</summary>
        [NUnit.Framework.Property("CaseId", "B-33")]
        [Test]
        [NUnit.Framework.Category("Asset")]
        public void StunnedClips_ExistForMouseAndCat_AndLoop()
        {
            foreach (string fbx in new[] { RoleClipStates.MOUSE_CLIPS_FBX, RoleClipStates.CAT_CLIPS_FBX })
            {
                AnimationClip stunned = ClipParityProbe.FindClip(fbx, AnimationStateLogic.Stunned);
                Assert.IsNotNull(stunned, $"{fbx}에 Stunned가 없다 — 메타의 takeName 매핑을 봐라");
                Assert.IsTrue(stunned.isLooping, $"{fbx} Stunned가 반복이 아니다 — 메타 loopTime: 1");
            }
        }
    }
}
