namespace CTC.Runtime.Player
{
    /// <summary>
    /// 고양이 전직 (v3.9). **값은 복제·직렬화되므로 바꾸지 않는다** — 새 전직은 뒤에 추가만 한다.
    /// </summary>
    public enum CatClassId
    {
        None = 0,
        Fire = 1,   // 불   — 한동안 덮치기 피해가 오른다
        Wind = 2,   // 바람 — 한동안 빨라진다
        Light = 3,  // 빛   — 한동안 모든 쥐가 벽 너머로 보인다
        Dark = 4,   // 어둠 — 한동안 쥐들의 화면이 어두워진다
    }

    /// <summary>
    /// 전직 규칙. MonoBehaviour와 분리된 순수 함수(EditMode TDD).
    ///
    /// **전직은 "다 컸다"는 신호다.** 상점 강화를 끝까지 올린 고양이에게만 열린다 —
    /// 치즈를 다 쓴 뒤에야 열리므로, 전직 시점이 곧 "고양이가 성장을 마쳤다"가 된다.
    ///
    /// 스킬은 전부 **액티브**(2026-08-10 확정)다. 상시 효과였다면 빛은 쥐의 은신을 통째로 죽이고
    /// 어둠은 쥐를 계속 장님으로 만든다. 지속 + 쿨다운이면 "지금 쓸까"라는 판단이 생기고,
    /// 쥐에게는 **쓰고 난 뒤의 빈 시간**이라는 대응 창이 남는다.
    /// </summary>
    public static class CatClassLogic
    {
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;

        /// <summary>스킬 지속(초).</summary>
        public static float SkillSeconds => Balance.CatSkillSeconds;

        /// <summary>스킬 쿨다운(초). 지속보다 훨씬 길어야 '빈 시간'이 생긴다.</summary>
        public static float SkillCooldown => Balance.CatSkillCooldown;

        /// <summary>불 — 스킬 중 덮치기 피해 배율.</summary>
        public static float FireDamageMultiplier => Balance.CatFireDamageMultiplier;

        /// <summary>바람 — 스킬 중 이동 속도 배율.</summary>
        public static float WindSpeedMultiplier => Balance.CatWindSpeedMultiplier;

        /// <summary>어둠 — 쥐 화면이 얼마나 어두워지나(0=그대로, 1=완전 암전).</summary>
        public static float DarkStrength => Balance.CatDarkStrength;

        /// <summary>
        /// 전직할 수 있는가. **모든 강화를 끝까지 올려야** 한다 — 하나만 올린 채로 열리면
        /// "전직이 곧 최적 구매"가 되어 상점의 선택이 사라진다.
        /// </summary>
        public static bool CanAdvance(int _damageStacks, int _speedStacks, int _maxStacks, CatClassId _current)
        {
            if (_current != CatClassId.None) return false;   // 한 번만
            return _damageStacks >= _maxStacks && _speedStacks >= _maxStacks;
        }

        /// <summary>고른 값이 실제 전직인가 — None을 골라 창만 닫는 경로를 막는다.</summary>
        public static bool IsRealClass(CatClassId _id) => _id != CatClassId.None;

        /// <summary>쿨다운을 흘린다. 0 이하면 쓸 수 있다.</summary>
        public static float TickCooldown(float _remaining, float _deltaSeconds)
        {
            float next = _remaining - _deltaSeconds;
            return next < 0f ? 0f : next;
        }

        /// <summary>지금 스킬을 쓸 수 있는가.</summary>
        public static bool CanUseSkill(CatClassId _class, float _cooldownRemaining, bool _alreadyActive)
        {
            if (!IsRealClass(_class)) return false;
            return _cooldownRemaining <= 0f && !_alreadyActive;
        }
    }
}
