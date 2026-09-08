using CTC.Runtime.Bots;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 쥐 봇의 **반격** 규칙 (2026-09-09, 승률 진단 §9-3의 4번).
    ///
    /// 여기서 지키는 것은 하나다 — **봇이 사람보다 잘 쏘면 안 된다.**
    /// 2026-08-12에 봇 쥐의 사격을 통째로 뺀 이유가 그것이었다(여덟이 백발백중으로 쏴
    /// 97초에 고양이 둘이 죽었다). 다시 켜되 사람 수준으로 묶는 규칙을 여기서 잰다.
    /// </summary>
    public class MouseCombatLogicTests
    {
        private const float POPGUN_RANGE = 8f;    // WeaponInfo Tier 0
        private const float CAT_POUNCE = 2.8f;    // PlayerInfo Cat.PounceRange

        private static bool Shoot(
            bool visible = true, bool facing = true, float distance = 4f,
            float range = POPGUN_RANGE, float pounce = CAT_POUNCE, bool ready = true)
            => MouseCombatLogic.ShouldShoot(visible, facing, distance, range, pounce, ready);

        // ── 시야 ──

        [Test]
        public void ShouldShoot_InvisibleCat_Never()
        {
            // 소리로만 아는 고양이는 자리가 흐려져 있다(BotSenseLogic.Blur) — 허공에 쏘는 셈이고,
            // 총소리는 IsPreySound에 걸려 **내 자리만 알려 준다**.
            Assert.IsFalse(Shoot(visible: false));
        }

        // ── 쿨다운 ──

        [Test]
        public void ShouldShoot_OnCooldown_Never()
        {
            // 서버도 같은 판정을 하지만(FireRpc), 봇이 모르면 0.5초마다 죽은 RPC를 보낸다.
            Assert.IsFalse(Shoot(ready: false));
        }

        // ── 사거리 ──

        [Test]
        public void ShouldShoot_BeyondUsableRange_Never()
        {
            // 딱총 8m의 85% = 6.8m. 그 밖에서는 조준 오차가 표적 폭을 넘어 거의 안 맞는데
            // 소리는 그대로 난다.
            Assert.IsFalse(Shoot(distance: 7.5f));
            Assert.IsTrue(Shoot(distance: 6.0f));
        }

        [Test]
        public void UsableRange_ShrinksWithWeapon()
        {
            Assert.AreEqual(POPGUN_RANGE * MouseCombatLogic.RANGE_USABLE_RATIO,
                MouseCombatLogic.UsableRange(POPGUN_RANGE), 0.001f);
            // 총을 사면(Tier 1 = 10m) 쏘는 구간도 같이 늘어난다 — 사거리는 무기가 정한다.
            Assert.Greater(MouseCombatLogic.UsableRange(10f), MouseCombatLogic.UsableRange(POPGUN_RANGE));
        }

        // ── 반격이지 저격이 아니다 ──

        [Test]
        public void ShouldShoot_CatLookingAway_HoldsFire()
        {
            // 지나가는 고양이를 쏘면 **안 들켰을 쥐가 스스로 들킨다.** 사람은 그 짓을 안 한다.
            Assert.IsFalse(Shoot(facing: false, distance: 6f));
        }

        [Test]
        public void ShouldShoot_CatLookingAwayButAdjacent_Shoots()
        {
            // 덮치기 사거리의 1.5배(4.2m) 안이면 어차피 다음 순간 들킨다 — 유일한 예외.
            Assert.IsTrue(Shoot(facing: false, distance: 4f));
        }

        [Test]
        public void LastStandRange_StaysInsideUsableRange()
        {
            // 예외(코앞)가 유효 사거리를 삼키면 「저격 금지」가 죽은 코드가 된다 —
            // 2.5배(7.0m)로 잡았다가 딱총 6.8m를 넘겨 실제로 그랬다.
            Assert.Less(CAT_POUNCE * MouseCombatLogic.LAST_STAND_POUNCE_RATIO,
                MouseCombatLogic.UsableRange(POPGUN_RANGE));
        }

        [Test]
        public void ShouldShoot_SpottedByCat_Shoots()
        {
            Assert.IsTrue(Shoot(facing: true, distance: 6f));
        }

        // ── 도망 중에도 쏜다 ──

        [Test]
        public void ShouldShoot_DoesNotDependOnFleeing()
        {
            // 이 게임의 이동은 카메라 기준이고 총알은 카메라 앞으로 나간다 —
            // 사람은 S를 눌러 **전속력으로 물러나면서** 뒤를 보고 쏜다. 이동 손해가 없다.
            // 그래서 규칙에 「도망 중」이라는 인자가 아예 없다. 도망 가지에서 부르든
            // 파밍 중에 부르든 같은 답이 나와야 한다.
            Assert.IsTrue(Shoot(distance: 3f));
            Assert.IsTrue(Shoot(distance: 6f));
        }

        // ── 도망치며 뒤를 본다 ──

        [Test]
        public void KeepsEyeOn_JustSeen_True()
        {
            // 몸은 도망 방향을 보지만 **카메라는 따로 돈다**(3인칭). 몸 기준으로만 재면
            // 봇 쥐가 영영 뒤를 못 봐 반격이 한 판에 서너 발로 죽는다(2026-09-09 실측).
            Assert.IsTrue(MouseCombatLogic.KeepsEyeOn(0f));
            Assert.IsTrue(MouseCombatLogic.KeepsEyeOn(MouseCombatLogic.LOOK_BACK_SECONDS));
        }

        [Test]
        public void KeepsEyeOn_LongGone_False()
        {
            // 기억력이 아니라 「고개를 돌린 채 쫓기는 동안」이다 — 놓치면 놓친 것이다.
            Assert.IsFalse(MouseCombatLogic.KeepsEyeOn(MouseCombatLogic.LOOK_BACK_SECONDS + 0.1f));
        }

        // ── 조준 오차 ──

        [Test]
        public void AimDirection_ZeroError_PointsAtTarget()
        {
            Vector3 dir = MouseCombatLogic.AimDirection(Vector3.zero, new Vector3(0f, 0f, 10f), 0f, 1f);
            Assert.AreEqual(0f, Vector3.Angle(dir, Vector3.forward), 0.01f);
        }

        [Test]
        public void AimDirection_FullRoll_OffByErrorDegrees()
        {
            Vector3 dir = MouseCombatLogic.AimDirection(Vector3.zero, new Vector3(0f, 0f, 10f), 7f, 1f);
            Assert.AreEqual(7f, Vector3.Angle(dir, Vector3.forward), 0.01f);

            Vector3 other = MouseCombatLogic.AimDirection(Vector3.zero, new Vector3(0f, 0f, 10f), 7f, -1f);
            Assert.AreEqual(7f, Vector3.Angle(other, Vector3.forward), 0.01f);
            // 좌우가 갈려야 한다 — 한쪽으로만 틀리면 그건 오차가 아니라 영점이 나간 것이다.
            Assert.AreEqual(14f, Vector3.Angle(dir, other), 0.01f);
        }

        [Test]
        public void AimDirection_IsFlat()
        {
            // 총알이 수평으로 나가므로(MousePopgun이 y를 눕힌다) 위아래는 안 흔든다.
            Vector3 dir = MouseCombatLogic.AimDirection(
                Vector3.zero, new Vector3(3f, 5f, 4f), 7f, 0.5f);
            Assert.AreEqual(0f, dir.y, 0.0001f);
            Assert.AreEqual(1f, dir.magnitude, 0.001f);
        }

        [Test]
        public void AimDirection_SamePosition_DoesNotReturnZero()
        {
            // 겹쳐 있으면 방향이 영벡터가 되어 FireRpc가 제자리에 쏜다.
            Vector3 dir = MouseCombatLogic.AimDirection(Vector3.one, Vector3.one, 7f, 0.3f);
            Assert.AreEqual(1f, dir.magnitude, 0.001f);
        }

        // ── 손잡이가 실제로 무슨 뜻인지 ──

        [Test]
        public void HitChance_FallsOffWithDistance()
        {
            const float CAT_RADIUS = 0.35f;   // PlayerInfo Cat.CcRadius
            float near = MouseCombatLogic.HitChance(3f, CAT_RADIUS, MouseCombatLogic.AIM_ERROR_DEG);
            float mid = MouseCombatLogic.HitChance(5f, CAT_RADIUS, MouseCombatLogic.AIM_ERROR_DEG);
            float far = MouseCombatLogic.HitChance(8f, CAT_RADIUS, MouseCombatLogic.AIM_ERROR_DEG);

            Assert.Greater(near, mid);
            Assert.Greater(mid, far);
            // 사람이 뛰면서 뒤로 쏘는 정도 — 코앞은 거의 맞고, 사거리 끝에서는 셋에 하나쯤.
            Assert.Greater(near, 0.8f);
            Assert.Less(far, 0.5f);
        }

        [Test]
        public void HitChance_NoError_IsAlwaysOne()
        {
            Assert.AreEqual(1f, MouseCombatLogic.HitChance(20f, 0.35f, 0f), 0.0001f);
        }
    }
}
