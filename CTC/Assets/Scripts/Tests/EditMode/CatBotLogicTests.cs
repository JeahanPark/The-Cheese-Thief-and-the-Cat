using CTC.Runtime.Bots;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 고양이 봇의 판단 규칙. 쥐 봇과 같은 원칙 — **영리해지면 안 된다**, 사람이 하는
    /// 판단(들리는 것·보이는 것·화면에 뜨는 것)만 쓴다.
    /// </summary>
    public class CatBotLogicTests
    {
        // 현재 CSV 값 — 판정이 이 속도차(1.8)를 전제로 동작하는지 그대로 본다
        private const float CAT = 7.8f;
        private const float MOUSE = 6.0f;

        [Test]
        public void Chase_LostRace_WhenPreyCanReenterFirst()
        {
            // 실관찰 재현(2026-08-24): 벽을 부수는 중 쥐가 반대편에 보임 — 고양이 10m,
            // 쥐는 요새 문에서 8m. 잡는 데 5.6초, 되돌아가는 데 1.3초 → 진 경주다.
            Assert.IsFalse(CatBotLogic.IsChaseWinnable(10f, 8f, CAT, MOUSE), "쫓으면 왕복 카이팅에 말린다");
        }

        [Test]
        public void Chase_Winnable_WhenPreyStrayedFar()
        {
            // 쥐가 진짜 멀리 도망갔다 — 코앞(2m)이고 요새는 20m 뒤 → 잡는 게 빠르다
            Assert.IsTrue(CatBotLogic.IsChaseWinnable(2f, 20f, CAT, MOUSE));
        }

        [Test]
        public void Chase_LostRace_WhenPreyInsideFortress()
        {
            // 요새 안(거리 0)이면 추격이 아니라 벽이 답이다
            Assert.IsFalse(CatBotLogic.IsChaseWinnable(5f, 0f, CAT, MOUSE));
        }

        [Test]
        public void Chase_LostRace_WhenNotFaster()
        {
            // 속도차가 없으면(바람 스킬 쥐 등 가정) 영원히 평행선 — 경주 자체가 성립 안 한다
            Assert.IsFalse(CatBotLogic.IsChaseWinnable(3f, 50f, 6f, 6f));
            Assert.IsFalse(CatBotLogic.IsChaseWinnable(3f, 50f, 5f, 6f));
        }

        [Test]
        public void LeadScore_PrefersCloseAndFresh()
        {
            // 가까울수록·최근일수록 크다 — 오래된 소리는 이미 떠난 자리다
            Assert.Greater(CatBotLogic.LeadScore(5f, 0f), CatBotLogic.LeadScore(20f, 0f));
            Assert.Greater(CatBotLogic.LeadScore(10f, 0f), CatBotLogic.LeadScore(10f, 3f));
        }
    }
}
