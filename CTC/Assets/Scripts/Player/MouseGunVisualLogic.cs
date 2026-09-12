namespace CTC.Runtime.Player
{
    /// <summary>
    /// 총이 언제 어느 모습으로 보이는지를 정하는 순수 규칙 — 씬 없이 EditMode가 덮는다.
    ///
    /// 왜 클립이 둘인가: 기관총 테크는 기본 무기보다 **5배 빠르게** 쏜다. 뽑는 동작이
    /// 매 발에 들어가면 쥐가 0.1초마다 총을 다시 뽑는다 — 그래서 <b>처음 한 번만</b> 뽑고
    /// (<see cref="AnimationStateLogic.ShootDraw"/>), 그 뒤로는 반동만 준다
    /// (<see cref="AnimationStateLogic.Shoot"/>). 총을 이미 들고 있는지가 그 유일한 갈림길이다.
    ///
    /// 왜 총이 둘인가: 총을 든 본은 **앞다리**(`leg.F.R`)라 엉덩이에 닿을 수가 없다.
    /// 총 하나만 발에 물려 놓고 렌더해 봤더니 「가슴 밑에서 들어 올린다」로만 읽혔다
    /// (2026-09-12 실측). 그래서 **허리춤에 얹힌 총**을 몸통 본에 따로 두고, 앞발이
    /// 허리에 가장 가까운 프레임에서 **보이기를 바꿔치기한다**. 아무것도 순간이동하지 않고,
    /// 눈은 그 둘을 하나로 읽는다.
    /// </summary>
    public static class MouseGunVisualLogic
    {
        /// <summary>
        /// 뽑기 시작에서 **바꿔치기까지**. 클립은 24fps 10프레임이고, 앞발이 허리춤을 떠나
        /// 위로 휘어 나오는 것은 실측상 **4프레임째**다(그 전까지 본 회전이 거의 안 움직인다).
        /// 그래서 3프레임 = 3/24초. 여기서 바꾸면 손이 허리에 붙어 있는 동안만 허리춤 총이
        /// 보이고, 손이 떠나는 순간 든 총으로 넘어간다.
        /// </summary>
        public const float DRAW_SWAP_SECONDS = 3f / 24f;

        /// <param name="_alreadyOut">쏘기 직전에 총이 이미 나와 있었나(뽑는 중도 포함).</param>
        public static string ClipForShot(bool _alreadyOut)
            => _alreadyOut ? AnimationStateLogic.Shoot : AnimationStateLogic.ShootDraw;

        /// <summary>
        /// 뽑기 시작 후 <paramref name="_secondsSinceDrawStart"/>초 지난 시점에
        /// **허리춤 총**이 보여야 하나. 거짓이면 그때부터는 **든 총**이다.
        ///
        /// 런타임(<see cref="MouseGunVisual"/>)과 미리보기 렌더러가 같은 함수를 본다 —
        /// 둘이 갈리면 미리보기가 거짓말을 한다.
        /// </summary>
        public static bool HolsteredAt(float _secondsSinceDrawStart)
            => _secondsSinceDrawStart < DRAW_SWAP_SECONDS;
    }
}
