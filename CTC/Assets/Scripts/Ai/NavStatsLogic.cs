namespace CTC.Runtime.Ai
{
    /// <summary>
    /// 길찾기 **계측의 규칙**(순수, 2026-09-09 감사 T6).
    ///
    /// 「무엇을 표본으로 세는가」를 <see cref="NavRoute"/> 안에 두면 EditMode가 손을 못 댄다.
    /// 실제로 손을 못 대는 동안 **길을 포기하면 우회율이 1.00(만점)** 이 되는 규칙이
    /// 아무 저항 없이 살아 있었고, 「detour 1.00 = 봇이 곧장 간다」로 읽혀 왔다.
    /// </summary>
    public static class NavStatsLogic
    {
        /// <summary>「잰 적 없다」 — 만점(1)과 구분돼야 한다.</summary>
        public const float NO_DETOUR = -1f;

        /// <summary>이보다 짧은 직선은 우회율의 표본이 아니다(나눗셈이 튀고 만점 쪽으로 끌린다).</summary>
        public const float MIN_DETOUR_METERS = 0.5f;

        /// <summary>
        /// 이 계획이 우회율의 표본인가.
        ///
        /// **코너가 0이면 아니다** — 길을 못 찾아 포기했거나 이미 그 자리다.
        /// 둘 다 「곧장 갔다」가 아니므로 만점으로 세면 안 된다.
        /// </summary>
        public static bool IsDetourSample(int _cornerCount, float _directMeters)
        {
            return _cornerCount > 0 && _directMeters > MIN_DETOUR_METERS;
        }

        /// <summary>우회율 = 걸어야 하는 길이 / 도착점까지 직선. 표본이 아니면 <see cref="NO_DETOUR"/>.</summary>
        public static float Detour(int _cornerCount, float _walkedMeters, float _directMeters)
        {
            return IsDetourSample(_cornerCount, _directMeters) ? _walkedMeters / _directMeters : NO_DETOUR;
        }

        /// <summary>
        /// 이 계획의 목표 미달(m). **포기는 목표까지 통째로** 적는다 —
        /// 0으로 적으면 포기가 지표를 좋게 만든다.
        /// </summary>
        public static float Shortfall(int _cornerCount, bool _gaveUp,
            float _goalFromLastCorner, float _goalFromSelf)
        {
            if (_cornerCount > 0) return _goalFromLastCorner;
            return _gaveUp ? _goalFromSelf : 0f;
        }

        /// <summary>목표 미달을 잰 계획인가 — 성공한 계획과 포기가 표본이고, 「이미 그 자리」는 아니다.</summary>
        public static bool IsShortfallSample(int _cornerCount, bool _gaveUp)
        {
            return _cornerCount > 0 || _gaveUp;
        }
    }
}
