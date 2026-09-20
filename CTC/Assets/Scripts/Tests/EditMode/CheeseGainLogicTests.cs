using NUnit.Framework;
using CTC.Runtime.UI;

namespace CTC.Tests
{
    /// <summary>재화를 벌면 뜨는 「+N」(2026-09-20 사용자 선택 A안) — 합치기·사라지기.</summary>
    public class CheeseGainLogicTests
    {
        [Test]
        [NUnit.Framework.Property("CaseId", "C-45")]
        public void Merge_AddsWhileTheWindowIsOpen_RestartsAfter()
        {
            // 채굴은 잘게 여러 번 들어온다 — 창 안이면 더해서 한 숫자로 키운다
            Assert.AreEqual(5, CheeseGainLogic.Merge(0, 5, 0f));
            Assert.AreEqual(8, CheeseGainLogic.Merge(5, 3, CheeseGainLogic.MERGE_SECONDS - 0.1f));
            Assert.AreEqual(3, CheeseGainLogic.Merge(5, 3, CheeseGainLogic.MERGE_SECONDS + 0.1f), "창이 지나면 새로 센다");
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "C-45")]
        public void Merge_IgnoresSpending()
        {
            // 쓴 돈은 안 띄운다 — 값은 상점이 이미 보여 준다
            Assert.AreEqual(5, CheeseGainLogic.Merge(5, -80, 0f));
            Assert.AreEqual(5, CheeseGainLogic.Merge(5, 0, 0f));
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "C-45")]
        public void Alpha_HoldsThenFades()
        {
            Assert.AreEqual(1f, CheeseGainLogic.Alpha(0f));
            Assert.AreEqual(1f, CheeseGainLogic.Alpha(CheeseGainLogic.HOLD_SECONDS));
            Assert.AreEqual(0.5f, CheeseGainLogic.Alpha(CheeseGainLogic.HOLD_SECONDS + CheeseGainLogic.FADE_SECONDS * 0.5f), 1e-3f);
            Assert.AreEqual(0f, CheeseGainLogic.Alpha(CheeseGainLogic.HOLD_SECONDS + CheeseGainLogic.FADE_SECONDS + 1f));
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "C-45")]
        public void Scale_PopsThenSettles()
        {
            Assert.AreEqual(CheeseGainLogic.POP_SCALE, CheeseGainLogic.Scale(0f));
            Assert.AreEqual(1f, CheeseGainLogic.Scale(CheeseGainLogic.POP_SECONDS), 1e-4f);
            Assert.AreEqual(1f, CheeseGainLogic.Scale(5f), 1e-4f, "튀어오른 뒤엔 커진 채로 남지 않는다");
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "C-45")]
        public void Text_IsEmptyWhenNothingGained()
        {
            Assert.AreEqual("+12", CheeseGainLogic.Text(12));
            Assert.AreEqual("", CheeseGainLogic.Text(0));
        }
    }
}
