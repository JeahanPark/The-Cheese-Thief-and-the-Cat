using CTC.Runtime.Player;
using NUnit.Framework;

namespace CTC.Tests
{
    /// <summary>
    /// 전직 규칙 (2026-09-09, 계획 3 Task 5). 못 박는 것은 둘이다.
    /// ① 문턱은 **누적 획득 장난기 하나뿐**이다 — 무엇을 샀는지 묻지 않는다.
    ///    옛 규칙("강화 둘 다 만렙")은 실효 780짜리 강제라 테크 v3의 칸 5 선택을 없앴다.
    /// ② 광분 × 불은 **곱이 아니라 큰 쪽**이다 — 곱이면 ×5.0이 되어 요새 Lv3이 한 방에 무너진다.
    /// </summary>
    public class CatClassLogicTests
    {
        [Test]
        public void CanAdvance_BelowThreshold_IsFalse()
        {
            Assert.IsFalse(CatClassLogic.CanAdvance(299, 300, CatClassId.None));
            Assert.IsFalse(CatClassLogic.CanAdvance(0, 300, CatClassId.None));
        }

        [Test]
        public void CanAdvance_ExactThreshold_IsTrue()
        {
            // 경계는 "넘어야"가 아니라 "닿으면"이다 — 문턱 값이 곧 안내 문구의 숫자라서.
            Assert.IsTrue(CatClassLogic.CanAdvance(300, 300, CatClassId.None));
            Assert.IsTrue(CatClassLogic.CanAdvance(1000, 300, CatClassId.None));
        }

        [Test]
        public void CanAdvance_AlreadyAdvanced_IsFalse()
        {
            // 되돌릴 수 없는 선택이라 두 번은 없다 — 아무리 벌어도 안 열린다.
            Assert.IsFalse(CatClassLogic.CanAdvance(99999, 300, CatClassId.Fire));
            Assert.IsFalse(CatClassLogic.CanAdvance(99999, 300, CatClassId.Dark));
        }

        [Test]
        public void CanAdvance_ZeroThreshold_OpensImmediately()
        {
            // 문턱 0은 "판 시작하자마자 전직"이다. 설정 실수를 감추지 않고 그대로 드러낸다.
            Assert.IsTrue(CatClassLogic.CanAdvance(0, 0, CatClassId.None));
        }

        [Test]
        public void CanAdvance_RealThreshold_OpensWithoutBuyingAnything()
        {
            // 계획 3 Task 5의 핵심: **아무것도 안 산** 고양이도 문턱만큼 벌면 열린다.
            // 스택이 서명에서 사라졌다는 것 자체가 "정해진 두 테크를 사라"는 강제가 없어졌다는 뜻이다.
            int threshold = CatClassLogic.MischiefThreshold;
            Assert.IsFalse(CatClassLogic.CanAdvance(threshold - 1, threshold, CatClassId.None));
            Assert.IsTrue(CatClassLogic.CanAdvance(threshold, threshold, CatClassId.None));
        }

        [Test]
        public void Threshold_IsFromCsv_AndPositive()
        {
            // 코드에 박힌 값이 아니라 CSV(BalanceInfo.CatAdvanceMischief)에서 온다.
            Assert.AreEqual(
                CTC.Runtime.Info.InfoManager.Instance.Balance.CatAdvanceMischief,
                CatClassLogic.MischiefThreshold);
            Assert.Greater(CatClassLogic.MischiefThreshold, 0,
                "문턱 0이면 스폰 즉시 전직이다 — CSV를 고쳐야 한다(테스트가 아니라)");
        }

        [Test]
        public void PounceMultiplier_BothOff_IsOne()
        {
            Assert.AreEqual(1f, CatClassLogic.PounceMultiplier(1f, 1f), 1e-4f);
        }

        [Test]
        public void PounceMultiplier_OnlyOneOn_IsThatOne()
        {
            Assert.AreEqual(2.5f, CatClassLogic.PounceMultiplier(2.5f, 1f), 1e-4f);
            Assert.AreEqual(2f, CatClassLogic.PounceMultiplier(1f, 2f), 1e-4f);
        }

        [Test]
        public void PounceMultiplier_BothOn_TakesMaxNotProduct()
        {
            // 곱이면 5.0이다. 흡수(기획서 §5)라서 큰 쪽만 남는다.
            Assert.AreEqual(2.5f, CatClassLogic.PounceMultiplier(2.5f, 2f), 1e-4f);
            Assert.AreEqual(3f, CatClassLogic.PounceMultiplier(2f, 3f), 1e-4f);
        }
    }
}
