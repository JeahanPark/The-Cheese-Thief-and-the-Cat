using NUnit.Framework;
using CTC.Runtime.Audio;
using CTC.Runtime.Info;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>고양이가 다가오면 쥐에게 추격곡(2026-09-20 사용자 요청) — 켜기·버티기·섞기.</summary>
    public class ChaseBgmLogicTests
    {
        [Test]
        [NUnit.Framework.Property("CaseId", "B-43")]
        public void SneakingCat_MustComeCloser()
        {
            Assert.AreEqual(20f, ChaseBgmLogic.TriggerMeters(false, 20f, 10f));
            Assert.AreEqual(10f, ChaseBgmLogic.TriggerMeters(true, 20f, 10f), "살금살금 고양이는 더 가까이 와야 걸린다");
            Assert.AreEqual(0f, ChaseBgmLogic.TriggerMeters(true, 20f, -3f), "음수 반경은 0");
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "B-43")]
        public void Balance_SneakTriggerIsHalf()
        {
            // 사용자 결정: 살금살금 고양이는 절반 거리에서만 — CSV가 원본이다
            BalanceInfo b = InfoManager.Instance.Balance;
            Assert.Greater(b.ChaseBgmMeters, 0f);
            Assert.AreEqual(b.ChaseBgmMeters * 0.5f, b.ChaseBgmSneakMeters, 0.01f);
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "B-43")]
        public void FlatDistance_IgnoresHeight()
        {
            Assert.AreEqual(5f, ChaseBgmLogic.FlatDistance(new Vector3(0f, 0f, 0f), new Vector3(3f, 9f, 4f)), 1e-4f);
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "B-43")]
        public void Chasing_StartsAtOnce_AndHoldsBeforeEnding()
        {
            Assert.IsTrue(ChaseBgmLogic.NextChasing(false, true, 0f), "반경 안이면 바로 켠다");
            Assert.IsTrue(ChaseBgmLogic.NextChasing(true, false, ChaseBgmLogic.HOLD_SECONDS - 0.1f), "벗어나도 잠깐은 버틴다");
            Assert.IsFalse(ChaseBgmLogic.NextChasing(true, false, ChaseBgmLogic.HOLD_SECONDS + 0.1f), "다 버티면 끝낸다");
            Assert.IsFalse(ChaseBgmLogic.NextChasing(false, false, 0f), "켜진 적 없으면 반경 밖에선 안 켠다");
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "B-43")]
        public void Weight_FadesInFasterThanOut()
        {
            float up = ChaseBgmLogic.StepWeight(0f, true, 0.1f);
            float down = 1f - ChaseBgmLogic.StepWeight(1f, false, 0.1f);
            Assert.Greater(up, down, "켜기는 빠르고 끄기는 천천히");
            Assert.AreEqual(1f, ChaseBgmLogic.StepWeight(0.99f, true, 10f), "목표를 넘지 않는다");
            Assert.AreEqual(0f, ChaseBgmLogic.StepWeight(0.01f, false, 10f));
        }
    }
}

