namespace CTC.Runtime.Player
{
    /// <summary>
    /// 수평 이동 속도를 Quirky Series 애니메이터 상태 이름으로 매핑한다.
    /// AC_Cat/AC_Mouse는 파라미터 없이 상태만 있어 CrossFade(상태명)로 구동한다.
    /// </summary>
    public static class AnimationStateLogic
    {
        public const string Idle = "Idle_A";
        public const string Walk = "Walk";
        public const string Run = "Run";
        public const string Attack = "Attack"; // AC_Cat·AC_Mouse 공통 원샷 상태
        public const string Hit = "Hit";       // AC_Cat·AC_Mouse 공통 원샷 상태

        // 테크 v3 계획 2b Task 5 — **새 클립은 하나도 만들지 않았다.**
        // Quirky 팩(AC_Cat·AC_Mouse)이 이미 가지고 있으면서 안 쓰던 상태 여섯을 동작에 붙인 것뿐이다.
        // 이름이 틀리면 CrossFadeInFixedTime은 **에러 없이 조용히 아무 일도 안 한다** —
        // 그래서 문자열을 호출부에 흘리지 않고 여기 모아 EditMode가 철자를 묶어 둔다.
        public const string Roll = "Roll";       // 대시 — 구르기
        public const string Bounce = "Bounce";   // 끈끈이 — 발밑에 내려찍기
        public const string Jump = "Jump";       // 요새 — 뛰어올랐다 내려오며 벽이 선다
        public const string Spin = "Spin";       // 광분 — 한 바퀴
        public const string Clicked = "Clicked"; // 구매 — 짧은 끄덕임
        public const string Eat = "Eat";         // 채굴 — 갉아먹기(반복)

        /// <summary>
        /// 이 게임이 이름으로 부르는 상태 전부. `Animator`가 주는 것은 이름이 아니라 해시라서,
        /// 「지금 무엇이 도는가」를 글자로 되돌리려면 후보 목록이 있어야 한다
        /// (<see cref="PlayerAnimationDriver.PlayingState"/>).
        ///
        /// 여기 없는 상태가 돌면 그 자리는 `?해시`로 나온다 — **없는 것을 있다고 읽지 않는다.**
        /// </summary>
        public static readonly string[] All =
        {
            Idle, Walk, Run, Attack, Hit, Roll, Bounce, Jump, Spin, Clicked, Eat,
        };

        public static string FromSpeed(float _speed, float _walkThreshold, float _runThreshold)
        {
            if (_speed >= _runThreshold) return Run;
            if (_speed >= _walkThreshold) return Walk;
            return Idle;
        }
    }
}
