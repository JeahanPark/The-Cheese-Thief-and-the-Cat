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

        public static string FromSpeed(float speed, float walkThreshold, float runThreshold)
        {
            if (speed >= runThreshold) return Run;
            if (speed >= walkThreshold) return Walk;
            return Idle;
        }
    }
}
