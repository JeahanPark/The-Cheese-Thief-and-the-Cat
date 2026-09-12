namespace CTC.Runtime.Player
{
    /// <summary>
    /// 「이번 발에 어느 클립을 트나」 하나만 정하는 순수 규칙 — 씬 없이 EditMode가 덮는다.
    ///
    /// 왜 둘로 나뉘어 있는가: 기관총 테크는 기본 무기보다 **5배 빠르게** 쏜다. 뽑는 동작이
    /// 매 발에 들어가면 쥐가 0.4초마다 총을 다시 뽑는다 — 그래서 <b>처음 한 번만</b> 뽑고
    /// (<see cref="AnimationStateLogic.ShootDraw"/>), 그 뒤로는 반동만 준다
    /// (<see cref="AnimationStateLogic.Shoot"/>). 총을 이미 들고 있는지가 그 유일한 갈림길이라
    /// 판정을 여기 한 줄로 모아 둔다.
    /// </summary>
    public static class MouseGunVisualLogic
    {
        /// <param name="_alreadyOut">쏘기 직전에 총이 이미 손에 나와 있었나.</param>
        public static string ClipForShot(bool _alreadyOut)
            => _alreadyOut ? AnimationStateLogic.Shoot : AnimationStateLogic.ShootDraw;
    }
}
