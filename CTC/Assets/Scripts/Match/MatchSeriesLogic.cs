namespace CTC.Runtime.Match
{
    /// <summary>
    /// 3판 시리즈 규칙 — 순수 함수(EditMode TDD) (2026-08-30).
    ///
    /// **무조건 3판이다**(사용자 결정). 2:0으로 갈려도 3판을 다 한다 —
    /// 조기 종료를 넣으면 3판째에 고양이가 될 사람이 영영 안 걸리는 판이 생긴다.
    /// 고양이는 판마다 다시 뽑으므로, 3판을 다 하는 것이 곧 "돌아가며 해 본다"는 뜻이다.
    ///
    /// 무승부는 나올 수 없다 — 한 판은 반드시 고양이 승 아니면 쥐 승으로 끝나고(시간 만료도
    /// 어느 한쪽으로 판정된다), 3은 홀수라 2:1 아니면 3:0이다.
    /// </summary>
    public static class MatchSeriesLogic
    {
        public const int ROUNDS = 3;

        /// <summary>이번이 마지막 판인가. 맞으면 판이 끝나고 대기실로 돌아간다.</summary>
        public static bool IsFinalRound(int _round) => _round >= ROUNDS;

        /// <summary>다음 판 번호. 마지막을 넘지 않는다.</summary>
        public static int NextRound(int _round) => _round >= ROUNDS ? ROUNDS : _round + 1;

        /// <summary>시리즈 승자. 아직 3판을 안 채웠으면 <see cref="MatchResult.None"/>.</summary>
        public static MatchResult Winner(int _catWins, int _mouseWins)
        {
            if (_catWins + _mouseWins < ROUNDS) return MatchResult.None;
            return _catWins > _mouseWins ? MatchResult.CatWin : MatchResult.MouseWin;
        }

        /// <summary>판 중에 HUD에 뜨는 한 줄.</summary>
        public static string ScoreLabel(int _round, int _catWins, int _mouseWins)
        {
            int round = _round < 1 ? 1 : (_round > ROUNDS ? ROUNDS : _round);
            return $"{round}판 / {ROUNDS}   고양이 {_catWins} : 쥐 {_mouseWins}";
        }

        /// <summary>마지막 판이 끝나고 대기실로 돌아가기 직전에 띄우는 줄.</summary>
        public static string FinalLabel(int _catWins, int _mouseWins)
        {
            MatchResult winner = Winner(_catWins, _mouseWins);
            string who = winner == MatchResult.CatWin ? "고양이" : "쥐";
            return $"시리즈 종료 — 고양이 {_catWins} : 쥐 {_mouseWins}   {who} 승";
        }
    }
}
