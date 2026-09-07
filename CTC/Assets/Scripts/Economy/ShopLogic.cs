using CTC.Runtime.Lobby;

namespace CTC.Runtime.Economy
{
    public readonly struct WeaponStats
    {
        public readonly int Damage;
        public readonly float CooldownSeconds;
        public readonly float Range;

        public WeaponStats(int _damage, float _cooldownSeconds, float _range)
        {
            Damage = _damage;
            CooldownSeconds = _cooldownSeconds;
            Range = _range;
        }
    }

    /// <summary>
    /// 밸런스 접근자 + 순수 수학. MonoBehaviour와 분리된 순수 로직 — EditMode TDD.
    /// **카탈로그는 여기 없다**(테크 v3, 2026-09-07) — 무엇을 파는지·값이 얼마인지는
    /// `TechInfo.csv` + <see cref="TechLogic"/>로 옮겼다. 여기 남은 건 표에 안 내리는
    /// 구조 수치(스택 상한·창고 파생값)와 역할별 이름·무기 티어 조회뿐이다.
    /// </summary>
    public static class ShopLogic
    {
        // 수치는 전부 CSV(Assets/Resources/CSVInfo/)가 원본이다 — 2026-07-31 데이터 분리.
        // 이름은 예전 상수 그대로 두되 실제 값은 표에서 읽는다(호출부 50여 곳을 안 건드리려고).
        // 밸런싱은 이제 CSV만 고치면 되고, 재컴파일 없이 다음 플레이에 반영된다.
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;

        public static int MAX_UPGRADE_STACKS => Balance.MaxUpgradeStacks;
        public static int POUNCE_DAMAGE_PER_STACK => Balance.PounceDamagePerStack;
        public static float SPEED_PER_STACK => Balance.SpeedPerStack;
        // 요새 = 즉발 방패(2026-08-15). 회복 없음. 씬 생성기가 프리팹에 굽는다 —
        // **HP를 바꾸면 `Setup Networking Scene`을 다시 돌려야 한다**(CSV만 고치면 안 반영된다).
        // 2026-08-18: 180(노업 3방) → 480(노업 8방·풀강 5방). 고양이가 벽을 뚫을 수 있게 된 뒤로
        // 3방짜리 벽은 4초짜리 과속방지턱이었고, 봇 쥐 승률이 0%로 무너졌다(측정).
        public static int FORTRESS_HP => Balance.FortressHp;

        // 창고(v3.4) — 파밍이 '서 있기'라 지루하다는 플레이 피드백의 답.
        // 지어두면 자리를 비워도 수입이 들어오되, 위치가 드러난 자산이라 고양이가 부술 수 있다.
        // 수입 2/s는 등급 2 치즈 지점과 같아 150을 들일 값이 아니었다(2026-07-29) → 5/s.
        // 지급은 2초마다 10개 — 수입을 올린 대가로 창고가 시끄러워 위치가 빨리 팔린다.
        public static int WAREHOUSE_INCOME_PER_SECOND => Balance.WarehouseIncomePerSecond;
        public static int WAREHOUSE_MAX_HP => Balance.WarehouseMaxHp;
        public static int WAREHOUSE_LIMIT_PER_MOUSE => Balance.WarehouseLimitPerMouse;
        public static int WAREHOUSE_TICK_CHEESE => Balance.WarehouseTickCheese;
        public static float WAREHOUSE_SOUND_RANGE_METERS => Balance.WarehouseSoundRangeMeters;

        /// <summary>생산 1회 사이의 간격(초) — 수입/틱량에서 파생되므로 따로 튜닝하지 않는다.</summary>
        public static float WarehouseTickSeconds =>
            WAREHOUSE_TICK_CHEESE / (float)WAREHOUSE_INCOME_PER_SECOND;

        public static bool CanBuy(int _cheese, int _cost)
        {
            return _cost > 0 && _cheese >= _cost;
        }

        public static bool CanAddStack(int _currentStacks)
        {
            return _currentStacks < MAX_UPGRADE_STACKS;
        }

        public static int PounceDamage(int _baseDamage, int _stacks)
        {
            return _baseDamage + POUNCE_DAMAGE_PER_STACK * _stacks;
        }

        public static float MoveSpeed(float _baseSpeed, int _stacks)
        {
            return _baseSpeed + SPEED_PER_STACK * _stacks;
        }

        /// <summary>
        /// 역할별 재화 이름. 고양이 재화는 치즈가 아니라 장난기다 —
        /// 고양이에게 "치즈 부족"이라고 알리던 버그(2026-07-28)의 단일 출처.
        /// </summary>
        public static string CurrencyNameOf(RoleId _role)
        {
            return _role == RoleId.Cat ? "장난기" : "치즈";
        }

        public static WeaponStats WeaponStatsForTier(int _tier)
        {
            Info.WeaponInfo row = Info.InfoManager.Instance.WeaponOf(_tier);
            return new WeaponStats(row.Damage, row.CooldownSeconds, row.Range);
        }
    }
}
