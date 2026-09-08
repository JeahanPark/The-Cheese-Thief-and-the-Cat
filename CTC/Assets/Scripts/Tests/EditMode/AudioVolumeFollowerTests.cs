using CTC.Runtime.Audio;
using CTC.Runtime.Core;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// **배경음이 배경음 슬라이더를 따라가는가**. 실제 `AudioSource.volume`을 본다 —
    /// Model(UserSettings) 값만 보면 배선이 빠져도 초록이다(2026-08-09 전례).
    ///
    /// 왜 있나: `SceneBgm`이 `AddComponent&lt;AudioVolumeFollower&gt;()` 하고 **그다음 줄에서**
    /// `SetCategory(Bgm)`을 불렀다. `AddComponent`는 그 자리에서 `Awake`를 돌리므로 이미
    /// `Sfx`를 구독한 뒤였고, 배경음 슬라이더가 아무 일도 안 했다(wesqa qa_v37 실측 0.245 = 0.35 × sfx 0.7).
    /// 에러가 없어서 안 보였다 — 그래서 순서를 뒤집어 놓고도 초록이 되지 않게 여기서 막는다.
    ///
    /// **EditMode는 `Awake`를 안 돌린다.** 그게 오히려 이 테스트의 요점이다 — 구독이 `Awake`
    /// 타이밍에 매달려 있으면 여기서 볼륨이 아예 안 따라와 전부 빨개진다. 플레이 중 실측은
    /// wesqa `qa_v37`이 실제 `AudioSource.volume`으로 따로 본다.
    /// </summary>
    public class AudioVolumeFollowerTests
    {
        private const float DESIGN = 0.35f;   // BgmDirector가 굽는 설계 볼륨

        private float m_SavedBgm;
        private float m_SavedSfx;
        private GameObject m_Go;

        [SetUp]
        public void SetUp()
        {
            // 스트림만 건드린다 — UserSettings.Save()를 안 부르므로 디스크(PlayerPrefs)는 그대로다
            m_SavedBgm = UserSettings.BgmVolume.Value;
            m_SavedSfx = UserSettings.SfxVolume.Value;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Go != null) Object.DestroyImmediate(m_Go);
            m_Go = null;
            UserSettings.BgmVolume.Value = m_SavedBgm;
            UserSettings.SfxVolume.Value = m_SavedSfx;
        }

        private AudioSource NewSource()
        {
            m_Go = new GameObject("VolumeFollowerTestSource", typeof(AudioSource));
            AudioSource src = m_Go.GetComponent<AudioSource>();
            src.volume = DESIGN;
            return src;
        }

        [Test]
        public void Attach_BgmFollowsBgmSliderNotSfx()
        {
            AudioSource src = NewSource();
            AudioVolumeFollower.Attach(m_Go, AudioVolumeFollower.Category.Bgm);

            // **설계값(0.35)과 다른 수를 기대한다.** 0.35를 기대하면 구독이 아예 안 걸려
            // 볼륨이 그대로 남아 있어도 초록이 된다 — 뮤테이션이 안 잡히는 테스트가 된다.
            UserSettings.SfxVolume.Value = 1f;
            UserSettings.BgmVolume.Value = 0.5f;
            Assert.AreEqual(DESIGN * 0.5f, src.volume, 1e-4f, "배경음 슬라이더를 따라가야 한다");

            UserSettings.BgmVolume.Value = 0f;
            Assert.AreEqual(0f, src.volume, 1e-4f, "배경음 0%면 소리가 꺼져야 한다");
        }

        /// <summary>
        /// `AddComponent` **뒤에** 카테고리를 줘도 늦지 않아야 한다 — 이게 결함의 본체였다.
        /// 되돌리면(= SetCategory가 구독을 안 갈아 끼우면) 여기서 0.35 × 0.7 = 0.245가 나와 빨개진다.
        /// </summary>
        [Test]
        public void SetCategoryAfterAwake_ReboundsToBgm()
        {
            AudioSource src = NewSource();
            var follower = m_Go.AddComponent<AudioVolumeFollower>();   // 여기서 Awake가 이미 Sfx를 구독한다
            follower.SetCategory(AudioVolumeFollower.Category.Bgm);

            UserSettings.SfxVolume.Value = 1f;
            UserSettings.BgmVolume.Value = 0.5f;

            Assert.AreEqual(AudioVolumeFollower.Category.Bgm, follower.Kind);
            Assert.AreEqual(DESIGN * 0.5f, src.volume, 1e-4f,
                "붙인 뒤에 준 카테고리가 반영돼야 한다 — 0.35면 아직 효과음을 따라가고 있다");
        }

        [Test]
        public void SetCategoryAfterAwake_StopsFollowingOldCategory()
        {
            AudioSource src = NewSource();
            var follower = m_Go.AddComponent<AudioVolumeFollower>();
            follower.SetCategory(AudioVolumeFollower.Category.Bgm);

            UserSettings.BgmVolume.Value = 0.5f;
            UserSettings.SfxVolume.Value = 0f;   // 효과음을 죽여도 배경음은 살아 있어야 한다

            Assert.AreEqual(DESIGN * 0.5f, src.volume, 1e-4f,
                "옛 구독(Sfx)이 남아 있으면 0, 아무 구독도 없으면 0.35가 나온다");
        }

        [Test]
        public void Sfx_IsStillTheDefaultCategory()
        {
            AudioSource src = NewSource();
            var follower = m_Go.AddComponent<AudioVolumeFollower>();
            Assert.AreEqual(AudioVolumeFollower.Category.Sfx, follower.Kind, "기본값은 그대로 효과음이다");

            // 발소리·미끼처럼 씬에 구워 두는 소스는 이 카테고리로 남는다.
            follower.SetCategory(AudioVolumeFollower.Category.Sfx);
            UserSettings.BgmVolume.Value = 1f;
            UserSettings.SfxVolume.Value = 0.5f;

            Assert.AreEqual(DESIGN * 0.5f, src.volume, 1e-4f, "효과음 슬라이더를 따라가야 한다");
        }

        [Test]
        public void DesignVolumeRatioSurvivesSliderRoundTrip()
        {
            AudioSource src = NewSource();
            AudioVolumeFollower.Attach(m_Go, AudioVolumeFollower.Category.Bgm);

            UserSettings.BgmVolume.Value = 0.25f;
            Assert.AreEqual(DESIGN * 0.25f, src.volume, 1e-4f);
            UserSettings.BgmVolume.Value = 1f;
            Assert.AreEqual(DESIGN, src.volume, 1e-4f, "설계 비율(0.35)이 유지돼야 한다");
        }
    }
}
