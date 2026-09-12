using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 쏠 때 눈 감기의 **시간 계산**만 본다 — 씬도 눈꺼풀도 필요 없는 순수 산수다.
    ///
    /// 여기서 **못 잡는 것**도 분명하다: 눈꺼풀 구가 실제로 커지는지, <c>LateUpdate</c>가
    /// 이 값을 스케일로 옮기는지, 발사 RPC가 정말 이걸 부르는지는 씬(플레이모드)이 있어야 안다.
    /// 그건 <c>TestStartGunTrace</c>/<c>TestGetGunTraceSummary</c>가 프레임마다 적어 잰다.
    /// </summary>
    public class ShotBlinkLogicTests
    {
        private const float EPS = 1e-4f;

        /// <summary>가장 빠른 무기의 쿨다운(WeaponInfo 티어 2·3) — 연사 시나리오의 근거.</summary>
        private const float MACHINEGUN_COOLDOWN = 0.15f;

        // ===== 발사 프레임에서 나온 지연 =====

        [Test]
        public void Delay_ComesFromFireFrame_Shoot()
        {
            // 5프레임째 = (5-1)/24 ≈ 0.1667초
            Assert.AreEqual((ShotBlinkLogic.SHOOT_FIRE_FRAME - 1) / ShotBlinkLogic.CLIP_FPS,
                ShotBlinkLogic.DelayFor(true), EPS);
            Assert.AreEqual(0.16667f, ShotBlinkLogic.DelayFor(true), 1e-3f);
        }

        [Test]
        public void Delay_ComesFromFireFrame_ShootDraw()
        {
            // 7프레임째 = (7-1)/24 = 0.25초
            Assert.AreEqual((ShotBlinkLogic.SHOOT_DRAW_FIRE_FRAME - 1) / ShotBlinkLogic.CLIP_FPS,
                ShotBlinkLogic.DelayFor(false), EPS);
            Assert.AreEqual(0.25f, ShotBlinkLogic.DelayFor(false), EPS);
        }

        /// <summary>뽑아서 쏘는 클립이 **더 늦게** 터진다 — 하나로 합치면 첫 발에서 눈이 먼저 감긴다.</summary>
        [Test]
        public void Delay_DrawIsLaterThanRecoil()
        {
            Assert.Greater(ShotBlinkLogic.DelayFor(false), ShotBlinkLogic.DelayFor(true));
        }

        /// <summary>두 지연 모두 클립 길이(10프레임 = 0.4167초) 안에 있어야 한다.</summary>
        [Test]
        public void Delay_FallsInsideTheClip()
        {
            float clip = 10f / ShotBlinkLogic.CLIP_FPS;
            Assert.Less(ShotBlinkLogic.DelayFor(true), clip);
            Assert.Less(ShotBlinkLogic.DelayFor(false), clip);
        }

        // ===== 겨눌 때는 뜨고 있다(이 연출의 요점) =====

        [Test]
        public void Cover_IsZeroWhileStillAiming()
        {
            Assert.AreEqual(0f, ShotBlinkLogic.Cover(-ShotBlinkLogic.DelayFor(true)), EPS);
            Assert.AreEqual(0f, ShotBlinkLogic.Cover(-0.01f), EPS);
            Assert.AreEqual(0f, ShotBlinkLogic.Cover(0f), EPS);
        }

        // ===== 감기 → 붙들기 → 뜨기 =====

        [Test]
        public void Cover_ClosesLinearly()
        {
            Assert.AreEqual(0.5f, ShotBlinkLogic.Cover(ShotBlinkLogic.CLOSE_SECONDS * 0.5f), EPS);
            Assert.AreEqual(1f, ShotBlinkLogic.Cover(ShotBlinkLogic.CLOSE_SECONDS), EPS);
        }

        [Test]
        public void Cover_StaysShutThroughTheHold()
        {
            float start = ShotBlinkLogic.CLOSE_SECONDS;
            Assert.AreEqual(1f, ShotBlinkLogic.Cover(start + ShotBlinkLogic.SHUT_SECONDS * 0.5f), EPS);
            Assert.AreEqual(1f, ShotBlinkLogic.Cover(start + ShotBlinkLogic.SHUT_SECONDS), EPS);
        }

        [Test]
        public void Cover_ReopensAndEndsAtZero()
        {
            Assert.AreEqual(0f, ShotBlinkLogic.Cover(ShotBlinkLogic.TOTAL_SECONDS), EPS);
            Assert.AreEqual(0f, ShotBlinkLogic.Cover(ShotBlinkLogic.TOTAL_SECONDS + 5f), EPS);

            float mid = ShotBlinkLogic.CLOSE_SECONDS + ShotBlinkLogic.SHUT_SECONDS
                        + ShotBlinkLogic.OPEN_SECONDS * 0.5f;
            Assert.AreEqual(0.5f, ShotBlinkLogic.Cover(mid), EPS);
        }

        /// <summary>감기는 빨라야 하고(놀람) 뜨기는 느긋해야 한다(사건이 아니다).</summary>
        [Test]
        public void Cover_ClosesFasterThanItOpens()
        {
            Assert.Less(ShotBlinkLogic.CLOSE_SECONDS, ShotBlinkLogic.OPEN_SECONDS);
        }

        [Test]
        public void Cover_NeverLeavesZeroToOne()
        {
            for (float p = -0.5f; p < 1f; p += 0.005f)
            {
                float c = ShotBlinkLogic.Cover(p);
                Assert.GreaterOrEqual(c, 0f, "phase " + p);
                Assert.LessOrEqual(c, 1f, "phase " + p);
            }
        }

        [Test]
        public void IsComplete_OnlyAtTheEnd()
        {
            Assert.IsFalse(ShotBlinkLogic.IsComplete(-ShotBlinkLogic.DelayFor(false)));
            Assert.IsFalse(ShotBlinkLogic.IsComplete(ShotBlinkLogic.TOTAL_SECONDS - 0.001f));
            Assert.IsTrue(ShotBlinkLogic.IsComplete(ShotBlinkLogic.TOTAL_SECONDS));
        }

        // ===== 다음 발이 왔을 때 =====

        [Test]
        public void Retrigger_FromRest_WaitsTheFullDelay()
        {
            Assert.AreEqual(-ShotBlinkLogic.DelayFor(false),
                ShotBlinkLogic.RetriggerPhase(float.PositiveInfinity, ShotBlinkLogic.DelayFor(false)), EPS);
        }

        /// <summary>
        /// **예약된 감기를 미루지 않는다.** 이게 빠지면 기관총(쿨다운 0.15 &lt; 지연 0.167)에서
        /// 매 발이 지연을 다시 걸어 **연사 내내 눈을 뜨고 있다**.
        /// </summary>
        [Test]
        public void Retrigger_DuringDelay_DoesNotPushTheCloseBack()
        {
            float running = -0.02f;   // 0.02초 뒤면 감기 시작
            Assert.AreEqual(running,
                ShotBlinkLogic.RetriggerPhase(running, ShotBlinkLogic.DelayFor(false)), EPS);
        }

        /// <summary>새 클립의 지연이 더 짧으면 그쪽으로 **당긴다** — 늦추지만 않으면 된다.</summary>
        [Test]
        public void Retrigger_DuringDelay_PullsEarlierWhenNewDelayIsShorter()
        {
            // ShootDraw 지연(0.25)을 갓 시작했는데 다음 발은 Shoot(0.1667)이다 —
            // 0.1667초 뒤가 0.24초 뒤보다 이르므로 그쪽으로 당긴다.
            Assert.AreEqual(-ShotBlinkLogic.DelayFor(true),
                ShotBlinkLogic.RetriggerPhase(-0.24f, ShotBlinkLogic.DelayFor(true)), EPS);

            // 반대로 이미 더 이른 경우는 그대로 둔다(늦추지 않는다).
            Assert.AreEqual(-0.02f,
                ShotBlinkLogic.RetriggerPhase(-0.02f, ShotBlinkLogic.DelayFor(true)), EPS);
        }

        /// <summary>이미 완전히 감겨 있으면 붙드는 구간의 **시작**으로 돌아간다 — 다시 버틴다.</summary>
        [Test]
        public void Retrigger_WhileShut_RestartsTheHold()
        {
            float shut = ShotBlinkLogic.CLOSE_SECONDS + ShotBlinkLogic.SHUT_SECONDS * 0.5f;
            float got = ShotBlinkLogic.RetriggerPhase(shut, ShotBlinkLogic.DelayFor(true));
            Assert.AreEqual(ShotBlinkLogic.CLOSE_SECONDS, got, EPS);
            Assert.AreEqual(1f, ShotBlinkLogic.Cover(got), EPS);
        }

        /// <summary>이어 감기는 **끊기지 않는다** — 되감은 위상의 감김 정도가 지금과 같다.</summary>
        [Test]
        public void Retrigger_WhileReopening_IsContinuous()
        {
            float reopening = ShotBlinkLogic.CLOSE_SECONDS + ShotBlinkLogic.SHUT_SECONDS
                              + ShotBlinkLogic.OPEN_SECONDS * 0.3f;
            float before = ShotBlinkLogic.Cover(reopening);
            Assert.Greater(before, ShotBlinkLogic.OPEN_EPSILON);

            float got = ShotBlinkLogic.RetriggerPhase(reopening, ShotBlinkLogic.DelayFor(true));
            Assert.AreEqual(before, ShotBlinkLogic.Cover(got), EPS);
        }

        /// <summary>**절대 다시 뜨지 않는다** — 어떤 순간에 다음 발이 와도 감김이 줄지 않는다.</summary>
        [Test]
        public void Retrigger_NeverOpensTheEyes()
        {
            for (float p = 0f; p < ShotBlinkLogic.TOTAL_SECONDS; p += 0.002f)
            {
                float now = ShotBlinkLogic.Cover(p);
                if (now <= ShotBlinkLogic.OPEN_EPSILON) continue;   // 다 뜬 눈은 처음부터 다시다
                float next = ShotBlinkLogic.Cover(ShotBlinkLogic.RetriggerPhase(p, ShotBlinkLogic.DelayFor(true)));
                Assert.GreaterOrEqual(next, now - EPS, "phase " + p);
            }
        }

        /// <summary>다 뜬 뒤에 온 발은 **지연부터** 다시 밟는다 — 겨누기도 전에 감으면 안 된다.</summary>
        [Test]
        public void Retrigger_AfterFullyOpen_WaitsAgain()
        {
            float tail = ShotBlinkLogic.TOTAL_SECONDS - 0.001f;   // 사실상 다 뜬 꼬리
            Assert.LessOrEqual(ShotBlinkLogic.Cover(tail), ShotBlinkLogic.OPEN_EPSILON);
            Assert.AreEqual(-ShotBlinkLogic.DelayFor(true),
                ShotBlinkLogic.RetriggerPhase(tail, ShotBlinkLogic.DelayFor(true)), EPS);
        }

        // ===== 연사 =====

        /// <summary>
        /// **붙들기 하나가 가장 빠른 쿨다운보다 길어야 한다.** 다음 발은 붙들기의 «시작»으로
        /// 되돌아가므로 감기 시간은 여기 안 더해진다 — 그게 「연사 중엔 감은 채로 있다」의 근거다.
        /// 0.15초 정확히로 잡으면 다음 발과 구간 끝이 같은 순간에 걸려 눈이 떨린다.
        /// </summary>
        [Test]
        public void Burst_HoldOutlastsTheFastestCooldown()
        {
            Assert.Greater(ShotBlinkLogic.SHUT_SECONDS, MACHINEGUN_COOLDOWN);
        }

        /// <summary>
        /// 기관총 연사(0.15초 간격 8발)를 60fps로 굴려 본다 —
        /// 한 번 감긴 뒤로는 **마지막 발까지 1.00에서 내려오지 않는다**.
        /// </summary>
        [Test]
        public void Burst_StaysShutOnceClosed()
        {
            const float DT = 1f / 60f;
            float phase = ShotBlinkLogic.RetriggerPhase(float.PositiveInfinity, ShotBlinkLogic.DelayFor(false));
            float sinceShot = 0f;
            int shotsLeft = 7;
            bool everShut = false;

            for (int i = 0; i < 600 && shotsLeft > 0; i++)
            {
                phase += DT;
                sinceShot += DT;
                if (sinceShot >= MACHINEGUN_COOLDOWN)
                {
                    sinceShot -= MACHINEGUN_COOLDOWN;
                    shotsLeft--;
                    phase = ShotBlinkLogic.RetriggerPhase(phase, ShotBlinkLogic.DelayFor(true));
                }

                float cover = ShotBlinkLogic.Cover(phase);
                if (cover >= 1f) everShut = true;
                if (everShut) Assert.AreEqual(1f, cover, EPS, "연사 중에 눈이 떴다(frame " + i + ")");
            }

            Assert.IsTrue(everShut, "연사 내내 한 번도 안 감겼다");
        }

        /// <summary>연사가 끝나면 **반드시 다시 뜬다** — 감긴 채로 굳지 않는다.</summary>
        [Test]
        public void Burst_ReopensAfterTheLastShot()
        {
            float phase = ShotBlinkLogic.CLOSE_SECONDS;   // 마지막 발 직후 = 붙들기 시작
            const float DT = 1f / 60f;

            for (int i = 0; i < 200 && !ShotBlinkLogic.IsComplete(phase); i++) phase += DT;

            Assert.IsTrue(ShotBlinkLogic.IsComplete(phase));
            Assert.AreEqual(0f, ShotBlinkLogic.Cover(phase), EPS);
            // 마지막 발로부터 다시 뜨기까지 — 발사 모션(0.42초)과 같은 자릿수다.
            Assert.Less(phase, 0.5f);
        }

        /// <summary>
        /// 느린 무기(쿨다운 0.8초)는 발마다 **온전한 한 번**이다 —
        /// 다음 발이 오기 전에 이미 다 떠 있다(0.1667 + 0.31 = 0.477 &lt; 0.8).
        /// </summary>
        [Test]
        public void SlowWeapon_BlinksOncePerShot()
        {
            float whole = ShotBlinkLogic.DelayFor(true) + ShotBlinkLogic.TOTAL_SECONDS;
            Assert.Less(whole, 0.8f);
        }

        // ===== 망가진 입력 =====

        [Test]
        public void Retrigger_NegativeDelayIsTreatedAsNone()
        {
            Assert.AreEqual(0f, ShotBlinkLogic.RetriggerPhase(float.PositiveInfinity, -1f), EPS);
        }

        [Test]
        public void Retrigger_NanRunningPhaseFallsBackToTheDelay()
        {
            Assert.AreEqual(-ShotBlinkLogic.DelayFor(true),
                ShotBlinkLogic.RetriggerPhase(float.NaN, ShotBlinkLogic.DelayFor(true)), EPS);
        }

        [Test]
        public void Cover_HandlesInfinity()
        {
            Assert.AreEqual(0f, ShotBlinkLogic.Cover(float.PositiveInfinity), EPS);
            Assert.AreEqual(0f, ShotBlinkLogic.Cover(float.NegativeInfinity), EPS);
        }

        /// <summary>상수들이 서로 맞물려 있다 — 하나를 고치면 합도 같이 움직여야 한다.</summary>
        [Test]
        public void Total_IsTheSumOfThePhases()
        {
            Assert.AreEqual(
                ShotBlinkLogic.CLOSE_SECONDS + ShotBlinkLogic.SHUT_SECONDS + ShotBlinkLogic.OPEN_SECONDS,
                ShotBlinkLogic.TOTAL_SECONDS, EPS);
            Assert.Greater(ShotBlinkLogic.TOTAL_SECONDS, 0f);
            Assert.IsFalse(Mathf.Approximately(ShotBlinkLogic.OPEN_EPSILON, 0f));
        }
    }
}
