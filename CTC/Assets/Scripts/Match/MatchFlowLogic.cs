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

        /// <summary>
        /// 승패 판정. **자유로운 쥐**(살아 있고 안 갇힌)와 **갇힌 쥐**를 따로 받는다(v3.8).
        ///
        /// 갇힌 쥐를 생존으로 세면 고양이가 전원을 가둬 놓고도 판이 안 끝난다 —
        /// 갇힌 쥐는 스스로 못 나오므로 자유가 0인 순간이 곧 원작의 "전원 잡힘"이다.
        ///
        /// 우선순위가 셋으로 갈린다:
        /// ① 쥐가 **완전히** 사라졌으면(자유 0 · 갇힘 0) 동시 전멸이어도 고양이 승 — 기존 원작 규칙 유지.
        /// ② 고양이가 죽었는데 갇힌 쥐가 남았으면 쥐 승 — 지킬 고양이가 없으니 어차피 다 풀려난다.
        /// ③ 갇힘만 남고 자유가 0이면 고양이 승 — 스스로 못 나오므로 그 순간이 '전원 잡힘'이다.
        /// </summary>
        public static MatchResult Evaluate(bool _armed, int _aliveCats, int _freeMice, int _jailedMice)
        {
            if (!_armed) return MatchResult.None;
            if (_freeMice == 0 && _jailedMice == 0) return MatchResult.CatWin;
            if (_aliveCats == 0) return MatchResult.MouseWin;
            if (_freeMice == 0) return MatchResult.CatWin;
            return MatchResult.None;
        }

        /// <summary>
        /// 안전 타이머 만료 시: **자유로운** 쥐가 있으면 쥐 생존승, 없으면 고양이 승.
        /// 갇힌 쥐만 남은 채로 시계가 끝나면 고양이 승이다 — 안 그러면 고양이가
        /// 셋을 가둬 놓고도 마지막 하나가 도망만 다니면 진다.
        /// </summary>
        public static MatchResult EvaluateAtTimeUp(int _freeMice)
        {
            return _freeMice > 0 ? MatchResult.MouseWin : MatchResult.CatWin;
        }
    }
}
