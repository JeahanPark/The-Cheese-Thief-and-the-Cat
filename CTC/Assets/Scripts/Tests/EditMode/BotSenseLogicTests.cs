using CTC.Runtime.Audio;
using CTC.Runtime.Bots;
using CTC.Runtime.Lobby;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 봇이 무엇을 들을 수 있는가. 여기가 무너지면 봇 승률이 밸런스가 아니라
    /// "봇이 반칙했다"를 뜻하게 된다.
    /// </summary>
    public class BotSenseLogicTests
    {
        [Test]
        public void IsPreySound_CatIgnoresItsOwnNoise()
        {
            // 2026-08-12 실측: 고양이가 제 덮치기·스킬 소리를 듣고 제자리를 조사하러 갔다.
            // 스킬 소리는 45m를 울려서 두 마리가 서로의 소리에 계속 붙들렸다.
            Assert.IsFalse(BotSenseLogic.IsPreySound(SfxId.PounceSwing, RoleId.Mouse));
            Assert.IsFalse(BotSenseLogic.IsPreySound(SfxId.CatHurt, RoleId.Mouse));
        }

        [Test]
        public void IsPreySound_CatChasesMouseNoise()
        {
            Assert.IsTrue(BotSenseLogic.IsPreySound(SfxId.CheeseTick, RoleId.Mouse), "파밍 소리");
            Assert.IsTrue(BotSenseLogic.IsPreySound(SfxId.AlcoveBreak, RoleId.Mouse), "벽감 파괴");
            Assert.IsTrue(BotSenseLogic.IsPreySound(SfxId.SlingshotFire, RoleId.Mouse), "새총");
        }

        [Test]
        public void IsPreySound_MouseFleesCatNoiseOnly()
        {
            Assert.IsTrue(BotSenseLogic.IsPreySound(SfxId.PounceSwing, RoleId.Cat));
            // 제 파밍 소리에 놀라 도망치면 아무도 치즈를 못 캔다
            Assert.IsFalse(BotSenseLogic.IsPreySound(SfxId.CheeseTick, RoleId.Cat));
            Assert.IsFalse(BotSenseLogic.IsPreySound(SfxId.MouseDash, RoleId.Cat));
        }

        [Test]
        public void IsPreySound_IgnoresUiAndDog()
        {
            // UI·판정음은 자리 정보가 아니고, 강아지는 봇이 모델링하지 않는 제3세력이다.
            Assert.IsFalse(BotSenseLogic.IsPreySound(SfxId.MatchStart, RoleId.Mouse));
            Assert.IsFalse(BotSenseLogic.IsPreySound(SfxId.UiClick, RoleId.Mouse));
            Assert.IsFalse(BotSenseLogic.IsPreySound(SfxId.DogBark, RoleId.Mouse));
            Assert.IsFalse(BotSenseLogic.IsPreySound(SfxId.DogBark, RoleId.Cat));
        }
    }
}
