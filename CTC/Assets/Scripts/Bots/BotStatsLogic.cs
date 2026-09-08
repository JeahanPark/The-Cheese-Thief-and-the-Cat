namespace CTC.Runtime.Bots
{
    /// <summary>
    /// **계측기 자신의 집계 규칙**(순수, 2026-09-09 감사 1단계).
    ///
    /// 이 프로젝트가 겪은 사고의 뿌리는 「계측기가 무테스트였다」는 것이다 —
    /// 「첫 발견」이 최솟값이 아니라 목록의 첫 값이었고(T4), 우회율은 길을 포기하면
    /// 만점이 됐다(T6). 둘 다 `MonoBehaviour` 안에 박혀 있어 EditMode가 손을 못 댔다.
    ///
    /// 그래서 **집계만 여기로 뽑는다.** <see cref="TestBotManager"/>는 값을 모아 오고,
    /// 「무엇을 합치고 무엇을 버리는가」는 전부 이 순수 함수들이 정한다.
    /// (`MouseBotLogic`·`BotSenseLogic`이 이미 쓰는 방식과 같다.)
    /// </summary>
    public static class BotStatsLogic
    {
        /// <summary>「없다·잰 적 없다」 — 정상값과 겹치지 않아야 한다.</summary>
        public const float NONE = -1f;

        /// <summary>
        /// 「가장 이른 목격」 한 건. 시각과 거리는 **같은 사건**이라야 한다 —
        /// 따로 접으면 「A의 시각 + B의 거리」라는 존재하지 않는 사건이 나온다.
        /// </summary>
        public struct FirstSight
        {
            public float Seconds;
            public float Distance;

            public bool Exists => Seconds >= 0f;
        }

        /// <summary>아직 아무도 못 봤다.</summary>
        public static FirstSight NoSight => new FirstSight { Seconds = NONE, Distance = NONE };

        /// <summary>
        /// 후보 한 건을 접는다 — **더 이른 쪽이 이긴다**.
        ///
        /// 예전에는 목록을 돌다 처음 만난 값에서 그냥 `return` 했다. 죽은 고양이는
        /// 목록에서 빠지므로 **고양이가 죽을수록 「첫 발견」이 늦어졌다**(감사 T4).
        /// 승률 진단서의 「첫 발견」 칸이 그 값이다.
        /// </summary>
        public static FirstSight FoldFirstSight(FirstSight _best, float _seconds, float _distance)
        {
            if (_seconds < 0f) return _best;
            if (_best.Exists && _best.Seconds <= _seconds) return _best;
            return new FirstSight { Seconds = _seconds, Distance = _distance };
        }

        /// <summary>
        /// 우회율 평균. **표본이 없으면 만점(1)이 아니라 <see cref="NONE"/>이다** —
        /// 「곧장 갔다」와 「한 번도 길을 못 찾았다」는 정반대인데 예전에는 둘 다 1.00이었다(감사 T6).
        /// </summary>
        public static float AverageDetour(float _sum, int _samples)
        {
            return _samples > 0 ? _sum / _samples : NONE;
        }

        /// <summary>목표 미달 평균(m). 표본이 없으면 <see cref="NONE"/>.</summary>
        public static float AverageShortfall(float _sum, int _samples)
        {
            return _samples > 0 ? _sum / _samples : NONE;
        }

        /// <summary>
        /// 「몇 마리였나」 — **판을 떠난 몸도 센다**.
        /// 산 몸만 세면 전멸한 판이 `mice=0 techs=0`으로 나와, 그 판이 통째로 사라진다(감사 F-9).
        /// </summary>
        public static int TotalWithRetired(int _living, int _retired) => _living + _retired;
    }
}
