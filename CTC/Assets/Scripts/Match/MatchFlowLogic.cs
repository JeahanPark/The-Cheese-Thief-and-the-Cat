namespace CTC.Runtime.Match
{
    public enum MatchResult
    {
        None = 0,
        MouseWin = 1,
        CatWin = 2,
    }

    /// <summary>
    /// 승패 판정 상태기계(순수). armed = 시작 후 양측이 실제 스폰된 것을 관측한 뒤에만
    /// 판정 활성 — 스폰 지연 중 0/0으로 즉시 종료되는 오판 방지.
    /// v3: 전멸 판정 + 안전 타이머 만료 판정(생존 쥐 있으면 쥐 생존승).
    /// </summary>
    public static class MatchFlowLogic
    {
        /// <summary>
        /// v3: 안전 타이머(원작 66:40 대응 축소) — 만료 시 생존 쥐가 있으면 쥐 승.
        /// 수치 원본은 CSV(BalanceInfo.csv) — 2026-07-31 데이터 분리.
        /// </summary>
        public static float MATCH_DURATION_SECONDS =>
            CTC.Runtime.Info.InfoManager.Instance.Balance.MatchDurationSeconds;

        public static bool ShouldArm(bool _gameStarted, bool _armed, int _aliveCats, int _aliveMice)
        {
            return _gameStarted && !_armed && _aliveCats > 0 && _aliveMice > 0;
        }

        public static MatchResult Evaluate(bool _armed, int _aliveCats, int _aliveMice)
        {
            if (!_armed) return MatchResult.None;
            if (_aliveMice == 0) return MatchResult.CatWin;   // 동시 전멸도 고양이 승(원작 우선순위)
            if (_aliveCats == 0) return MatchResult.MouseWin;
            return MatchResult.None;
        }

        /// <summary>안전 타이머 만료 시(v3): 생존 쥐가 있으면 쥐 생존승, 없으면 고양이 승.</summary>
        public static MatchResult EvaluateAtTimeUp(int _aliveMice)
        {
            return _aliveMice > 0 ? MatchResult.MouseWin : MatchResult.CatWin;
        }
    }
}
