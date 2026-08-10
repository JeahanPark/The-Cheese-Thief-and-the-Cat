namespace CTC.Runtime.Match
{
    /// <summary>
    /// 쥐 직업 (v3.9). **값은 복제·직렬화되므로 바꾸지 않는다.**
    /// </summary>
    public enum MouseJobId
    {
        None = 0,
        Hero = 1,    // 용사쥐 — 그냥 세진다
        Gang = 2,    // 깡패쥐 — 부하를 임명한다. 이 자리가 2단계를 연다
        Minion = 3,  // 부하쥐 — 임명당했다. 거절도 배신도 없다
    }

    /// <summary>판이 어느 단계인가.</summary>
    public enum MatchPhase
    {
        /// <summary>고양이 vs 쥐 전원.</summary>
        Hunt = 0,

        /// <summary>고양이가 죽고 깡패가 있었다 — 깡패 팀 vs 나머지 쥐.</summary>
        Gang = 1,
    }

    /// <summary>
    /// 직업과 단계 규칙. MonoBehaviour와 분리된 순수 함수(EditMode TDD).
    ///
    /// **2단계는 깡패가 있을 때만 열린다.** 항상 열리면 깡패권이 특별하지 않게 되고
    /// 판이 무조건 길어진다. 누군가 깡패권을 주워 판을 역전으로 끌고 간 것 자체가 보상이다.
    /// </summary>
    public static class MouseJobLogic
    {
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;

        /// <summary>2단계에 새로 주는 시간(초). 남은 시간을 쓰면 판마다 길이가 널뛴다.</summary>
        public static float GangPhaseSeconds => Balance.GangPhaseSeconds;

        /// <summary>용사쥐 최대 HP 배율.</summary>
        public static float HeroHpMultiplier => Balance.HeroHpMultiplier;

        /// <summary>용사쥐 사격 피해 배율.</summary>
        public static float HeroDamageMultiplier => Balance.HeroDamageMultiplier;

        /// <summary>부하를 임명할 수 있는 거리(m).</summary>
        public static float RecruitRadius => Balance.GangRecruitRadius;

        /// <summary>깡패 팀인가 — 보스와 부하는 한 편이다.</summary>
        public static bool IsGangSide(MouseJobId _job) => _job == MouseJobId.Gang || _job == MouseJobId.Minion;

        /// <summary>
        /// 임명할 수 있는가. **거절은 없다** — 조건은 거리와 상태뿐이다.
        /// 이미 깡패 편인 쥐는 다시 임명하지 않는다(보스를 부하로 만들 수 없다).
        /// </summary>
        public static bool CanRecruit(MouseJobId _recruiter, MouseJobId _target, float _distance, float _radius)
        {
            if (_recruiter != MouseJobId.Gang) return false;
            if (IsGangSide(_target)) return false;
            return _distance <= _radius;
        }

        /// <summary>
        /// 고양이가 죽었다 — 판이 끝나는가, 2단계로 가는가.
        /// 깡패가 없으면 지금까지의 규칙대로 **쥐 승리**다.
        /// </summary>
        public static bool ShouldEnterGangPhase(int _gangCount) => _gangCount > 0;

        /// <summary>
        /// 2단계 승패. 깡패 팀이 **나머지 쥐를 전멸**시키면 승리,
        /// 3분을 못 버티면(시간 초과) 나머지 쥐가 이긴다 — 시계를 쥔 쪽이 쫓는 쪽이다.
        /// </summary>
        public static MatchResult EvaluateGangPhase(int _gangAlive, int _otherAlive, bool _timeUp)
        {
            if (_gangAlive <= 0) return MatchResult.MouseWin;   // 보스 팀 전멸 — 나머지 쥐가 이긴다
            if (_otherAlive <= 0) return MatchResult.GangWin;
            return _timeUp ? MatchResult.MouseWin : MatchResult.None;
        }
    }
}
