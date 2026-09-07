namespace CTC.Runtime.Player
{
    /// <summary>
    /// 광분(고양이 모면 테크, 테크 v3 계획 2b) 판정 — <see cref="CatSkill"/>과 같은
    /// 「액티브 지속 + 쿨」 규칙이되 **전직과는 무관하다**(전직은 칸을 안 쓰고 Q로 따로 돈다).
    /// MonoBehaviour와 분리해 EditMode로 못 박는다.
    /// </summary>
    public static class CatFrenzyLogic
    {
        /// <summary>살 수 있는 게 아니라 "지금 켤 수 있나". 레벨 0은 미보유다.</summary>
        public static bool CanUse(int _level, double _readyAt, double _now, bool _active)
        {
            return _level > 0 && !_active && _now >= _readyAt;
        }

        /// <summary>켜져 있을 때만 곱한다. 배율이 0이면(표를 못 읽은 데이터 사고) 1배로 물러난다.</summary>
        public static float PounceMultiplier(bool _active, float _levelMultiplier)
        {
            return _active && _levelMultiplier > 0f ? _levelMultiplier : 1f;
        }
    }
}
