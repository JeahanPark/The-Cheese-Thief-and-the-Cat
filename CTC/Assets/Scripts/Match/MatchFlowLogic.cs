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
    /// 시간제한 없음(원작 동일): 판정 조건은 전멸뿐.
    /// </summary>
    public static class MatchFlowLogic
    {
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
    }
}
