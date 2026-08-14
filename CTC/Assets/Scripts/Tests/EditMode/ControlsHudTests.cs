using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 좌하단 특수키 칩 조립(HUD 개편 2026-08-15). qa_v45의 안내 단언이 이 문자열의
    /// 키워드(살금살금·발소리·임명, 그리고 "무음" 부재)를 전제한다 — 여기가 무너지면
    /// 실플레이 QA가 아니라 화면 문구부터 거짓이 된다.
    /// </summary>
    public class ControlsHudTests
    {
        [Test]
        public void Mouse_SneakChipTellsFootstepMeters()
        {
            string chips = ControlsHud.BuildChips(RoleId.Mouse, MouseJobId.None,
                _dashOwned: false, _dashCooldownSeconds: 0, _hasSkill: false,
                _skillCooldownSeconds: 0, _sneakMeters: 4f);

            StringAssert.Contains("살금살금", chips);
            StringAssert.Contains("발소리", chips);
            StringAssert.Contains("4m", chips);
            // "무음"이라고 쓰면 안 된다(2026-08-10) — 잠행은 발소리를 좁힐 뿐이다
            StringAssert.DoesNotContain("무음", chips);
        }

        [Test]
        public void Mouse_DashChipOnlyWhenOwned()
        {
            string without = ControlsHud.BuildChips(RoleId.Mouse, MouseJobId.None,
                false, 0, false, 0, 4f);
            string with = ControlsHud.BuildChips(RoleId.Mouse, MouseJobId.None,
                true, 0, false, 0, 4f);

            StringAssert.DoesNotContain("LShift", without);
            StringAssert.Contains("LShift", with);
        }

        [Test]
        public void Mouse_DashCooldownShowsSecondsLeft()
        {
            string cooling = ControlsHud.BuildChips(RoleId.Mouse, MouseJobId.None,
                true, 3, false, 0, 4f);

            StringAssert.Contains("대시 3", cooling);
        }

        [Test]
        public void Gang_GetsRecruitChip_OthersDoNot()
        {
            string plain = ControlsHud.BuildChips(RoleId.Mouse, MouseJobId.None,
                false, 0, false, 0, 4f);
            string gang = ControlsHud.BuildChips(RoleId.Mouse, MouseJobId.Gang,
                false, 0, false, 0, 4f);

            // F 임명은 깡패만 쓰는 조작이라 전원에게 보이면 소음이다
            StringAssert.DoesNotContain("임명", plain);
            StringAssert.Contains("임명", gang);
        }

        [Test]
        public void Cat_SkillChipOnlyAfterAdvance()
        {
            string before = ControlsHud.BuildChips(RoleId.Cat, MouseJobId.None,
                false, 0, _hasSkill: false, 0, 3f);
            string after = ControlsHud.BuildChips(RoleId.Cat, MouseJobId.None,
                false, 0, _hasSkill: true, 0, 3f);

            StringAssert.DoesNotContain("스킬", before);
            StringAssert.Contains("Q", after);
            StringAssert.Contains("스킬", after);
        }

        [Test]
        public void Spectator_GetsThumbsUpChip()
        {
            string chips = ControlsHud.BuildChips(RoleId.None, MouseJobId.None,
                false, 0, false, 0, 0f);

            StringAssert.Contains("G", chips);
            StringAssert.Contains("따봉", chips);
        }
    }
}
