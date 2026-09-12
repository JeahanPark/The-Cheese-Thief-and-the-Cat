using CTC.Runtime.Player;
using NUnit.Framework;

namespace CTC.Tests
{
    /// <summary>
    /// 총이 뜨고 사라지는 **시간 계산**만 본다 — 씬도 렌더러도 필요 없는 순수 산수라
    /// 여기서 전부 잡을 수 있다. 반대로 여기서 **못 잡는 것**도 분명하다:
    /// 프로퍼티 블록이 실제로 렌더러에 꽂히는지, <c>SetActive</c>가 알파 0에서 꺼지는지,
    /// URP 머티리얼이 반투명으로 세워져 있는지는 씬(또는 저장된 <c>.mat</c>)이 있어야 안다.
    /// </summary>
    public class GunFadeLogicTests
    {
        private const float EPS = 1e-5f;

        // ===== 나타남 =====

        [Test]
        public void FadeIn_StartsAtZero()
        {
            Assert.AreEqual(0f, GunFadeLogic.Alpha(0f, 0.10f, true), EPS);
        }

        [Test]
        public void FadeIn_HalfwayIsHalf()
        {
            Assert.AreEqual(0.5f, GunFadeLogic.Alpha(0.05f, 0.10f, true), EPS);
        }

        [Test]
        public void FadeIn_EndsAtOne()
        {
            Assert.AreEqual(1f, GunFadeLogic.Alpha(0.10f, 0.10f, true), EPS);
        }

        // ===== 사라짐 =====

        [Test]
        public void FadeOut_StartsAtOne()
        {
            Assert.AreEqual(1f, GunFadeLogic.Alpha(0f, 0.30f, false), EPS);
        }

        [Test]
        public void FadeOut_HalfwayIsHalf()
        {
            Assert.AreEqual(0.5f, GunFadeLogic.Alpha(0.15f, 0.30f, false), EPS);
        }

        [Test]
        public void FadeOut_EndsAtZero()
        {
            Assert.AreEqual(0f, GunFadeLogic.Alpha(0.30f, 0.30f, false), EPS);
        }

        // ===== 자르기 =====
        //
        // 프레임 하나가 길면(스파이크) 경과가 길이를 훌쩍 넘긴다. 거기서 오버슈트하면
        // 알파 1.4 같은 값이 나가고, 반대 방향은 음수가 된다 — 셰이더는 그걸 에러 없이 받는다.

        [Test]
        public void PastTheEnd_DoesNotOvershoot()
        {
            Assert.AreEqual(1f, GunFadeLogic.Alpha(5f, 0.10f, true), EPS);
            Assert.AreEqual(1f, GunFadeLogic.Alpha(0.1001f, 0.10f, true), EPS);
        }

        [Test]
        public void PastTheEnd_DoesNotGoNegative()
        {
            Assert.AreEqual(0f, GunFadeLogic.Alpha(5f, 0.30f, false), EPS);
        }

        [Test]
        public void NegativeElapsed_ClampsToStart()
        {
            Assert.AreEqual(0f, GunFadeLogic.Alpha(-1f, 0.10f, true), EPS);
            Assert.AreEqual(1f, GunFadeLogic.Alpha(-1f, 0.10f, false), EPS);
        }

        /// <summary>
        /// 진행률은 언제나 0~1 안이다 — 위 두 검사가 알파에서 확인한 것을 **원천에서** 한 번 더.
        /// </summary>
        [Test]
        public void Progress_StaysInsideZeroToOne()
        {
            foreach (float t in new[] { -10f, -0.001f, 0f, 0.04f, 0.1f, 0.5f, 100f })
            {
                float p = GunFadeLogic.Progress(t, 0.10f);
                Assert.GreaterOrEqual(p, 0f, $"t={t}");
                Assert.LessOrEqual(p, 1f, $"t={t}");
            }
        }

        // ===== 길이가 0이면 =====
        //
        // 0으로 나누면 알파가 NaN이 되고, NaN은 예외도 경고도 없이 셰이더까지 간다 —
        // 총이 조용히 사라지거나 새까매진다. 「이미 끝났다」로 본다.

        [Test]
        public void ZeroDuration_LandsOnTargetInstead_OfDividingByZero()
        {
            Assert.AreEqual(1f, GunFadeLogic.Alpha(0f, 0f, true), EPS);
            Assert.AreEqual(0f, GunFadeLogic.Alpha(0f, 0f, false), EPS);
            Assert.IsFalse(float.IsNaN(GunFadeLogic.Alpha(0f, 0f, true)));
        }

        [Test]
        public void NegativeDuration_IsTreatedAsFinished()
        {
            Assert.AreEqual(1f, GunFadeLogic.Progress(0f, -0.5f), EPS);
            Assert.AreEqual(1f, GunFadeLogic.Alpha(0f, -0.5f, true), EPS);
            Assert.IsTrue(GunFadeLogic.IsComplete(0f, -0.5f));
        }

        // ===== 지금 알파에서 목표로 =====

        [Test]
        public void AlphaBetween_WalksFromCurrentValue()
        {
            // 수납이 뽑는 도중에 끼어든 경우 — 0.4에서 0으로 내려간다. 1로 튀었다 내려가면 깜빡인다.
            Assert.AreEqual(0.4f, GunFadeLogic.AlphaBetween(0f, 0.30f, 0.4f, 0f), EPS);
            Assert.AreEqual(0.2f, GunFadeLogic.AlphaBetween(0.15f, 0.30f, 0.4f, 0f), EPS);
            Assert.AreEqual(0f, GunFadeLogic.AlphaBetween(0.30f, 0.30f, 0.4f, 0f), EPS);
        }

        [Test]
        public void AlphaBetween_ZeroDuration_SnapsToTarget()
        {
            Assert.AreEqual(0f, GunFadeLogic.AlphaBetween(0f, 0f, 0.4f, 0f), EPS);
        }

        // ===== 바꿔치기 한 쌍 =====

        /// <summary>
        /// **합이 항상 1이다.** 이게 깨지면 둘 다 진해져 총이 둘로 보이거나, 둘 다 옅어져
        /// 바꿔치기 순간에 총이 잠깐 없어진다 — 페이드를 넣은 이유가 통째로 사라진다.
        /// </summary>
        [Test]
        public void Crossfade_AlwaysSumsToOne()
        {
            float d = GunFadeLogic.SWAP_CROSSFADE_SECONDS;
            foreach (float t in new[] { -1f, 0f, d * 0.25f, d * 0.5f, d * 0.75f, d, d * 3f })
            {
                GunFadeLogic.CrossfadeAlphas(t, d, out float outgoing, out float incoming);
                Assert.AreEqual(1f, outgoing + incoming, EPS, $"t={t}에서 합이 1이 아니다");
            }
        }

        [Test]
        public void Crossfade_StartsOnHolstered_EndsOnHeld()
        {
            float d = GunFadeLogic.SWAP_CROSSFADE_SECONDS;

            GunFadeLogic.CrossfadeAlphas(0f, d, out float outStart, out float inStart);
            Assert.AreEqual(1f, outStart, EPS, "바꿔치기 시작에는 허리춤 총만 보여야 한다");
            Assert.AreEqual(0f, inStart, EPS);

            GunFadeLogic.CrossfadeAlphas(d, d, out float outEnd, out float inEnd);
            Assert.AreEqual(0f, outEnd, EPS);
            Assert.AreEqual(1f, inEnd, EPS, "바꿔치기 끝에는 든 총만 보여야 한다");
        }

        [Test]
        public void Crossfade_MidpointIsHalfAndHalf()
        {
            float d = GunFadeLogic.SWAP_CROSSFADE_SECONDS;
            GunFadeLogic.CrossfadeAlphas(d * 0.5f, d, out float outgoing, out float incoming);
            Assert.AreEqual(0.5f, outgoing, EPS);
            Assert.AreEqual(0.5f, incoming, EPS);
        }

        [Test]
        public void Crossfade_ZeroDuration_LandsOnHeldWithoutNaN()
        {
            GunFadeLogic.CrossfadeAlphas(0f, 0f, out float outgoing, out float incoming);
            Assert.AreEqual(0f, outgoing, EPS);
            Assert.AreEqual(1f, incoming, EPS);
            Assert.AreEqual(1f, outgoing + incoming, EPS);
        }

        // ===== 끝났나 / 그릴 가치가 있나 =====

        [Test]
        public void IsComplete_OnlyAtOrPastTheDuration()
        {
            Assert.IsFalse(GunFadeLogic.IsComplete(0f, 0.10f));
            Assert.IsFalse(GunFadeLogic.IsComplete(0.099f, 0.10f));
            Assert.IsTrue(GunFadeLogic.IsComplete(0.10f, 0.10f));
            Assert.IsTrue(GunFadeLogic.IsComplete(0.5f, 0.10f));
        }

        /// <summary>
        /// 알파 0짜리 총은 끈다 — 안 보이는 물건이 드로우콜과 그림자를 먹지 않게.
        /// 0보다 아주 조금만 커도 켜야 한다: 페이드 첫 프레임이 거기 있다.
        /// </summary>
        [Test]
        public void ShouldRender_IsFalseOnlyAtZero()
        {
            Assert.IsFalse(GunFadeLogic.ShouldRender(0f));
            Assert.IsTrue(GunFadeLogic.ShouldRender(0.001f));
            Assert.IsTrue(GunFadeLogic.ShouldRender(1f));
        }

        // ===== 시간표 자체 =====

        /// <summary>
        /// 페이드인이 바꿔치기보다 **짧아야** 한다. 길면 허리춤 총이 다 나타나기 전에 손으로
        /// 넘어가 「허리에 총이 있었다」가 한 번도 안 읽힌다 — 총을 둘로 나눈 이유가 사라진다.
        /// </summary>
        [Test]
        public void DrawFadeIn_FinishesBeforeTheSwap()
        {
            Assert.Less(GunFadeLogic.DRAW_FADE_IN_SECONDS, MouseGunVisualLogic.DRAW_SWAP_SECONDS);
        }

        /// <summary>
        /// 넣기는 뽑기보다 느긋하다 — 뽑기는 "지금 쏜다"는 신호라 빨라야 하고, 넣기는
        /// 아무 사건도 아니다. 이 순서가 뒤집히면 총이 사라지는 게 등장보다 눈에 띈다.
        /// </summary>
        [Test]
        public void HolsterFadeOut_IsSlowerThanTheDraw()
        {
            Assert.Greater(GunFadeLogic.HOLSTER_FADE_OUT_SECONDS, GunFadeLogic.DRAW_FADE_IN_SECONDS);
        }

        [Test]
        public void AllDurations_ArePositive()
        {
            Assert.Greater(GunFadeLogic.DRAW_FADE_IN_SECONDS, 0f);
            Assert.Greater(GunFadeLogic.SWAP_CROSSFADE_SECONDS, 0f);
            Assert.Greater(GunFadeLogic.HOLSTER_FADE_OUT_SECONDS, 0f);
        }

        /// <summary>
        /// 뽑기 전체(페이드인 + 대기 + 크로스페이드)가 발사 클립(10프레임 @24fps ≈ 0.417초)
        /// 안에 끝나야 한다. 넘치면 총이 아직 반쯤 투명한 채로 발사 포즈가 지나간다.
        /// </summary>
        [Test]
        public void WholeDraw_FitsInsideTheShootClip()
        {
            const float CLIP_SECONDS = 10f / 24f;
            float total = MouseGunVisualLogic.DRAW_SWAP_SECONDS + GunFadeLogic.SWAP_CROSSFADE_SECONDS;
            Assert.Less(total, CLIP_SECONDS);
        }
    }
}
