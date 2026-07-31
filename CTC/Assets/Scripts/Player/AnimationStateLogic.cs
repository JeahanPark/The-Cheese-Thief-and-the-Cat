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

        public static string FromSpeed(float _speed, float _walkThreshold, float _runThreshold)
        {
            if (_speed >= _runThreshold) return Run;
            if (_speed >= _walkThreshold) return Walk;
            return Idle;
        }
    }
}
