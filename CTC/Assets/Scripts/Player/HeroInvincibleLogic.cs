namespace CTC.Runtime.Player
{
    /// <summary>
    /// 용사 무적 (2026-09-19). Space로 켜고 3초 동안 피해를 전부 흡수한다 · 쿨 10초(CSV).
    /// 켜 있는 동안에도 칼은 휘두를 수 있다 — 도망만 치는 스킬이 아니다.
    /// </summary>
    public static class HeroInvincibleLogic
    {
        /// <summary>켜 있으면 0, 아니면 그대로.</summary>
        public static int AfterAbsorb(int _damage, bool _on) => _on ? 0 : _damage;

        /// <summary>지금 켤 수 있는가 — 마지막 사용 뒤 쿨다운이 다 지났는가.</summary>
        public static bool Ready(float _lastUsed, float _now, float _cooldown) => _now - _lastUsed >= _cooldown;

        /// <summary>켜 있는가 — 켠 시각부터 지속 시간 안이면 참.</summary>
        public static bool On(float _startedAt, float _now, float _seconds)
            => _startedAt > 0f && _now >= _startedAt && _now < _startedAt + _seconds;
    }
}
