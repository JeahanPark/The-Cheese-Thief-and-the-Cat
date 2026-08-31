using NUnit.Framework;
using CTC.Runtime.Combat;

namespace CTC.Tests
{
    public class PepperDotLogicTests
    {
        [Test]
        public void AddStack_Caps_At_Max()
        {
            Assert.AreEqual(1, PepperDotLogic.AddStack(0, 3));
            Assert.AreEqual(3, PepperDotLogic.AddStack(2, 3));
            Assert.AreEqual(3, PepperDotLogic.AddStack(3, 3));
            Assert.AreEqual(1, PepperDotLogic.AddStack(-5, 3));
        }

        [Test]
        public void Accumulate_Emits_Exactly_Dps_Per_Second()
        {
            // 0.1초 틱 × 10회 = 1초 → 스택 2 × 초당 3 = 6 데미지가 정확히 나와야 한다(부동소수 누락 금지)
            float carry = 0f;
            int total = 0;
            for (int i = 0; i < 10; i++)
                total += PepperDotLogic.Accumulate(0.1f, 2 * 3, ref carry);
            Assert.AreEqual(6, total + (carry > 0.999f ? 1 : 0) * 0);
            Assert.That(total, Is.InRange(5, 6), "1초에 5~6 (반올림 경계 1 허용)");
        }

        [Test]
        public void Accumulate_Long_Run_Matches_Total()
        {
            // 4초간 초당 9(3스택) → 총 36. 프레임이 들쭉날쭉해도 총량은 같아야 한다.
            float carry = 0f;
            int total = 0;
            float[] dts = { 0.016f, 0.033f, 0.2f, 0.05f };
            float elapsed = 0f;
            int i = 0;
            while (elapsed < 4f - 1e-4f)
            {
                float dt = System.Math.Min(dts[i++ % dts.Length], 4f - elapsed);
                elapsed += dt;
                total += PepperDotLogic.Accumulate(dt, 9f, ref carry);
            }
            Assert.That(total, Is.InRange(35, 36));
        }

        [Test]
        public void Expired_Means_No_Damage()
        {
            Assert.IsTrue(PepperDotLogic.IsExpired(10.0, 10.01));
            Assert.IsFalse(PepperDotLogic.IsExpired(10.0, 9.5));
        }
    }
}
