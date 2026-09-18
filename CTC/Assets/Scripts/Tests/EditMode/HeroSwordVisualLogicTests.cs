using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 용사 칼 소품의 보이기·방향(2026-09-19) — 칼은 쓸 때만 보이고, 방향은 코드가 캐릭터 기준으로 준다
    /// (리그에 손목이 없다). 숫자는 블렌더 클립(sword_swing.py·hero_draw.py)과 같아야 한다.
    /// </summary>
    public class HeroSwordVisualLogicTests
    {
        private const float FRAME = 1f / HeroSwordVisualLogic.FPS;

        // ── 휘두르기 ──

        [NUnit.Framework.Property("CaseId", "B-31")]
        [Test]
        public void Swing_SwordShowsOnlyWhileSwinging()
        {
            Assert.AreEqual(0f, HeroSwordVisualLogic.SwingAlpha(0f), 1e-4f, "휘두르기 첫 프레임엔 아직 빈손이다");
            Assert.AreEqual(1f, HeroSwordVisualLogic.SwingAlpha(HeroSwordVisualLogic.SWING_SHOW_AT), 1e-4f, "2프레임에 다 나타난다");
            Assert.AreEqual(1f, HeroSwordVisualLogic.SwingAlpha(6f * FRAME), 1e-4f, "벨 때(7프레임)는 또렷하다");
            Assert.AreEqual(0f, HeroSwordVisualLogic.SwingAlpha(12f * FRAME), 1e-4f, "13프레임엔 사라졌다");
            Assert.AreEqual(0f, HeroSwordVisualLogic.SwingAlpha(HeroSwordVisualLogic.SWING_SECONDS), 1e-4f);
            Assert.Less(HeroSwordVisualLogic.SWING_HIDE_AT + HeroSwordVisualLogic.FADE_SECONDS,
                HeroSwordVisualLogic.SWING_SECONDS + 1e-4f, "휘두르기가 끝나기 전에 칼이 거둬진다 — 걷기로 돌아가면 바닥을 뚫는다");
        }

        [NUnit.Framework.Property("CaseId", "B-31")]
        [Test]
        public void Swing_RaisesUpRightThenCutsForward()
        {
            Vector2 windup = HeroSwordVisualLogic.SwingAim(5f * FRAME);   // 6프레임
            Assert.Greater(windup.y, 90f, "치켜든 칼끝이 하늘을 넘어 뒤로 눕는다");
            Assert.Greater(windup.x, 0f, "오른쪽 위다(칼은 오른 앞발에 있다)");

            Vector2 cut = HeroSwordVisualLogic.SwingAim(7f * FRAME);      // 8프레임
            Assert.Less(Mathf.Abs(cut.y), 15f, "벤 칼은 거의 수평이다 — 바닥을 안 뚫는다");
            Assert.Greater(cut.x, 20f, "오른쪽 앞으로 뻗는다 — 뒤 카메라에서 둥근 몸 너머로 보인다");
        }

        [NUnit.Framework.Property("CaseId", "B-31")]
        [Test]
        public void Aim_SamplesTheBlenderKeysWithSmoothstep()
        {
            // 키 위에서는 그 값 그대로(sword_swing.py 6프레임 = 15, 112)
            Vector2 at6 = HeroSwordVisualLogic.SwingAim(5f * FRAME);
            Assert.AreEqual(15f, at6.x, 1e-3f);
            Assert.AreEqual(112f, at6.y, 1e-3f);
            // 앞·뒤 밖은 끝 키에 붙는다
            Assert.AreEqual(HeroSwordVisualLogic.SwingAim(-1f), HeroSwordVisualLogic.SwingAim(0f));
            Assert.AreEqual(HeroSwordVisualLogic.SwingAim(9f), HeroSwordVisualLogic.SwingAim(HeroSwordVisualLogic.SWING_SECONDS));
        }

        [NUnit.Framework.Property("CaseId", "B-31")]
        [Test]
        public void Direction_FollowsTheCharacterAxes()
        {
            AssertNear(Vector3.forward, HeroSwordVisualLogic.Direction(0f, 0f), "yaw 0 · pitch 0 = 앞");
            AssertNear(Vector3.up, HeroSwordVisualLogic.Direction(0f, 90f), "pitch 90 = 하늘");
            AssertNear(Vector3.right, HeroSwordVisualLogic.Direction(90f, 0f), "yaw + = 오른쪽");
            AssertNear(Vector3.back, HeroSwordVisualLogic.Direction(180f, 0f), "yaw 180 = 뒤");
        }

        [NUnit.Framework.Property("CaseId", "B-31")]
        [Test]
        public void LocalRotation_PointsTheBladeAndKeepsItsWidthSideways()
        {
            foreach (Vector2 aim in new[] { new Vector2(0f, 0f), new Vector2(30f, 45f), new Vector2(15f, 112f), new Vector2(160f, -5f) })
            {
                Vector3 dir = HeroSwordVisualLogic.Direction(aim.x, aim.y);
                Quaternion rot = HeroSwordVisualLogic.LocalRotation(dir);
                AssertNear(dir, rot * Vector3.forward, $"{aim} 날(+Z)이 방향을 본다");
                Assert.Less(Mathf.Abs(Vector3.Dot(rot * Vector3.right, dir)), 1e-4f, $"{aim} 넓이 축은 날에 직교한다");
            }
            // 수평으로 든 칼은 넓은 면이 위를 본다(위 카메라에 보인다) — 넓이(+X)가 오른쪽이다.
            Quaternion flat = HeroSwordVisualLogic.LocalRotation(Vector3.forward);
            AssertNear(Vector3.right, flat * Vector3.right, "수평 칼의 넓이는 오른쪽");
        }

        // ── 변신 ──

        [NUnit.Framework.Property("CaseId", "B-32")]
        [Test]
        public void Transform_DrawsTheSwordAfterReachingTheHip_AndPutsItAwayBeforeUnlocking()
        {
            Assert.AreEqual(0f, HeroTransformLogic.SwordAlpha(0f), 1e-4f, "밟는 순간엔 빈손 — 앞발이 먼저 엉덩이로 간다");
            Assert.AreEqual(1f, HeroTransformLogic.SwordAlpha(HeroTransformLogic.SHOW_AT), 1e-4f, "8프레임에 칼이 손에 있다");
            Assert.AreEqual(1f, HeroTransformLogic.SwordAlpha(17f * FRAME), 1e-4f, "하늘을 찌르는 동안 또렷하다");
            Assert.AreEqual(0f, HeroTransformLogic.SwordAlpha(30f * FRAME), 1e-4f, "31프레임엔 거뒀다");
            Assert.Less(HeroTransformLogic.HIDE_AT + HeroSwordVisualLogic.FADE_SECONDS, HeroTransformLogic.SECONDS,
                "잠금이 풀리기 전에 칼이 거둬진다 — 풀린 뒤 걸으면 칼이 바닥을 뚫는다");
            Assert.AreEqual(1.5f, HeroTransformLogic.SECONDS, 1e-4f, "기획 §5-1 — 1.5초");
        }

        [NUnit.Framework.Property("CaseId", "B-32")]
        [Test]
        public void Transform_RaisesTheSwordToTheSky()
        {
            Vector2 drawn = HeroTransformLogic.SwordAim(HeroTransformLogic.SHOW_AT);
            Assert.Greater(Mathf.Abs(drawn.x), 120f, "막 뽑은 칼은 뒤(엉덩이)를 본다");
            for (int frame = 14; frame <= 26; frame++)
                Assert.Greater(HeroTransformLogic.SwordAim((frame - 1) * FRAME).y, 85f, $"{frame}프레임 — 하늘을 찌른 채 버틴다");
            Assert.IsTrue(HeroTransformLogic.Locked(HeroTransformLogic.SECONDS - 0.01f));
            Assert.IsFalse(HeroTransformLogic.Locked(HeroTransformLogic.SECONDS));
            Assert.IsTrue(HeroTransformLogic.Done(HeroTransformLogic.SECONDS));
        }

        private static void AssertNear(Vector3 _expected, Vector3 _actual, string _message)
        {
            Assert.Less((_expected - _actual).magnitude, 1e-4f, $"{_message} — 기대 {_expected}, 실제 {_actual}");
        }
    }
}
