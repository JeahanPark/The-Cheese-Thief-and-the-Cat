using CTC.Runtime.Audio;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>앞단 화면 클릭음(2026-09-27) — 메인·서버 목록·대기실 버튼이 무음이던 것.</summary>
    public class UiClickSoundsTests
    {
        [Test]
        public void ShouldAttach_OnlyWhereNoBuilderChoseClicks()
        {
            Assert.IsTrue(UiClickSounds.ShouldAttach(false, false), "앞단 화면 버튼 — 붙인다");
            Assert.IsFalse(UiClickSounds.ShouldAttach(false, true), "이미 있으면 두 번 안 붙인다(소리 두 번)");
            Assert.IsFalse(UiClickSounds.ShouldAttach(true, false),
                "게임 캔버스는 생성기가 골랐다 — 상점 줄처럼 구매음이 따로 나는 버튼에 클릭음을 겹치지 않는다");
        }

        [Test]
        [NUnit.Framework.Category("Asset")]
        public void ClickClip_LoadsFromResources_WithoutTheSfxHub()
        {
            // 효과음 허브는 게임 씬에만 있다 — 앞단 화면은 이 경로 하나로 소리를 낸다
            Assert.IsNotNull(Resources.Load<AudioClip>(SfxPlayer.UI_CLICK_RESOURCE),
                $"Resources/{SfxPlayer.UI_CLICK_RESOURCE}.ogg가 없다 — 앞단 화면 클릭이 무음이 된다");
        }
    }
}
