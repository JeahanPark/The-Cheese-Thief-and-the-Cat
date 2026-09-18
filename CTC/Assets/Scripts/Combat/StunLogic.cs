namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 스턴 판정 (2026-09-19, 용사 칼 — 기획 `2026-09-19-hero-sword-design.md` §4).
    ///
    /// **겹치면 더하지 않고 갱신한다.** 여러 명이 동시에 베면 스턴이 쌓여
    /// 고양이가 영영 못 움직이게 된다 — 가장 늦게 끝나는 도장 하나만 남긴다.
    /// </summary>
    public static class StunLogic
    {
        /// <summary>지금 굳어 있는가. <paramref name="_until"/>은 서버 시간 도장이다(0 = 한 번도 안 맞음).</summary>
        public static bool Stunned(float _until, float _now) => _now < _until;

        /// <summary>새로 맞았을 때의 도장 — 지금보다 뒤로만 민다.</summary>
        public static float NextUntil(float _current, float _now, float _seconds)
        {
            float fresh = _now + _seconds;
            return fresh > _current ? fresh : _current;
        }
    }
}
