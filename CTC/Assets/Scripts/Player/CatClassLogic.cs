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
    /// **전직은 "많이 벌었다"는 신호다**(2026-09-09, 계획 3 Task 5). 문턱은 누적 획득 장난기 하나뿐 —
    /// 무엇을 샀는지는 묻지 않는다. 전에는 "강화 둘 다 만렙"이라 실효 780(발톱 420 + 발 구르기 360)이었고,
    /// 그건 문턱이 아니라 **정해진 두 테크를 사라는 강제**였다. 테크 v3의 칸 5는 "무엇을 고르나"가
    /// 전부인데 전직이 두 테크를 못 박으면 그 선택이 통째로 사라진다.
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

        /// <summary>어둠 — 먼 곳이 얼마나 어두워지나(0=그대로, 1=완전 암전). 가까운 곳은 그대로다.</summary>
        public static float DarkStrength => Balance.CatDarkStrength;

        /// <summary>어둠 — 내 쥐로부터 이 거리(m)까지는 밝다.</summary>
        public static float DarkNearMeters => Balance.CatDarkNearMeters;

        /// <summary>어둠 — 이 거리(m)부터는 먼 곳 밝기까지 다 어두워진다.</summary>
        public static float DarkFarMeters => Balance.CatDarkFarMeters;

        /// <summary>전직 이름(HUD 배지·능력 슬롯 공용).</summary>
        public static string ClassWordOf(CatClassId _class)
        {
            switch (_class)
            {
                case CatClassId.Fire: return "불";
                case CatClassId.Wind: return "바람";
                case CatClassId.Light: return "빛";
                case CatClassId.Dark: return "어둠";
                default: return "";
            }
        }

        /// <summary>전직 문턱 — 누적 **획득** 장난기(잔액이 아니다). 원본은 CSV.</summary>
        public static int MischiefThreshold => Balance.CatAdvanceMischief;

        /// <summary>
        /// 전직할 수 있는가 — **누적 획득 장난기**가 문턱을 넘었나(2026-09-09, 기획서 §5).
        /// 전에는 발톱·발 구르기 만렙(실효 780)이었다. 테크 v3의 칸 5는 "무엇을 고르나"가 전부인데
        /// 전직이 두 테크를 강제하면 그 선택이 사라진다 — 문턱은 **얼마나 벌었나**여야 한다.
        ///
        /// 재는 값이 잔액이 아니라 **번 총액**인 이유: 잔액으로 재면 테크를 사는 순간 문턱이 되돌아가
        /// "전직하려면 아무것도 사지 마라"가 된다. 그건 강제를 뒤집어 놓은 것일 뿐이다.
        /// </summary>
        public static bool CanAdvance(int _earnedMischief, int _threshold, CatClassId _current)
        {
            if (_current != CatClassId.None) return false;   // 한 번만
            return _earnedMischief >= _threshold;
        }

        /// <summary>
        /// 광분(테크)과 전직 「불」은 **곱하지 않는다** — 기획서 §5의 "불을 고르면 광분이 흡수된다"다.
        /// 곱이면 ×2.5 × ×2 = ×5.0이고, 전직이 300으로 앞당겨지면서 그 조합이 흔해졌다
        /// (계획 2b가 남긴 미결). 흡수를 가장 싸게 구현한 것이 **큰 쪽만 쓰기**다:
        /// 두 배율은 서로를 몰라도 되고, 켜는 순서·지속이 겹쳐도 결과가 한 값으로 정해진다.
        /// </summary>
        public static float PounceMultiplier(float _frenzy, float _fire)
        {
            return _frenzy > _fire ? _frenzy : _fire;
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
