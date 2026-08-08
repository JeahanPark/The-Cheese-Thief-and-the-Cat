using CTC.Runtime.Lobby;

namespace CTC.Runtime.Economy
{
    public enum ShopItemId
    {
        None = 0,
        Slingshot = 1,
        MachineGun = 2,
        Dash = 3,
        Decoy = 4,
        Fortress = 5,
        CatAttack = 6,
        CatSpeed = 7,
        Warehouse = 8,
    }

    /// <summary>카탈로그 한 줄. 정적 데이터 — CSV 파이프라인은 아이템 수가 늘면 도입.</summary>
    public readonly struct ShopItem
    {
        public readonly ShopItemId Id;
        public readonly RoleId Role;
        public readonly int Cost;

        public ShopItem(ShopItemId _id, RoleId _role, int _cost)
        {
            Id = _id;
            Role = _role;
            Cost = _cost;
        }
    }

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
    /// 상점 판정 수학·정적 카탈로그. MonoBehaviour와 분리된 순수 로직 — EditMode TDD.
    /// 가격은 원작 약올리기 DX 대응(새총150/기관총600/벙커220).
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
        public static int FORTRESS_HEAL_PER_TICK => Balance.FortressHealPerTick;
        public static int FORTRESS_HP => Balance.FortressHp; // 씬 생성기가 요새 프리팹에 굽는 값과 같다
        public static float FORTRESS_HEAL_RADIUS => Balance.FortressHealRadius;
        public static float DECOY_LIFETIME_SECONDS => Balance.DecoyLifetimeSeconds;

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

        /// <summary>상점 카탈로그 — ShopInfo.csv 순서 그대로(핫바 슬롯 순서와 같다).</summary>
        public static ShopItem[] CATALOG
        {
            get
            {
                var rows = Info.InfoManager.Instance.Shop;
                var catalog = new ShopItem[rows.Count];
                for (int i = 0; i < rows.Count; i++)
                {
                    catalog[i] = new ShopItem(rows[i].ItemId, rows[i].Role, rows[i].Cost);
                }
                return catalog;
            }
        }

        /// <summary>
        /// 핫바 항목 한 줄 설명 — 이름·가격만으로는 뭘 사는지 알 수 없다(특히 창고·미끼).
        /// 수치를 그대로 적는다: 플레이어가 "얼마짜리가 얼마를 해주는가"로 판단하게.
        /// </summary>
        public static string DescriptionOf(ShopItemId _id)
        {
            switch (_id)
            {
                // 무기 수치는 WeaponInfo.csv가 원본 — 문자열에 복제하면 CSV를 고칠 때 안내가 조용히 거짓이 된다.
                case ShopItemId.Slingshot:
                {
                    WeaponStats popgun = WeaponStatsForTier(0);
                    WeaponStats sling = WeaponStatsForTier(1);
                    return $"딱총을 새총으로 — {popgun.Damage}뎀 → {sling.Damage}뎀, 사거리 {sling.Range:F0}m";
                }
                case ShopItemId.MachineGun:
                {
                    WeaponStats mg = WeaponStatsForTier(2);
                    float dps = mg.CooldownSeconds > 0f ? mg.Damage / mg.CooldownSeconds : mg.Damage;
                    return $"연사 무기 — 초당 {dps:F0}뎀. 2정이 모이면 고양이를 잡을 수 있다";
                }
                case ShopItemId.Dash:
                    return "LShift 순간 가속 — 쫓길 때 거리를 벌린다";
                case ShopItemId.Decoy:
                    return $"발소리를 내는 가짜 쥐 — 고양이를 {DECOY_LIFETIME_SECONDS:F0}초간 낚는다 (즉시 설치)";
                case ShopItemId.Fortress:
                    return $"쥐만 드나드는 벙커 — 안에서 초당 {FORTRESS_HEAL_PER_TICK} 회복, 외벽 HP {FORTRESS_HP}";
                case ShopItemId.Warehouse:
                    return $"{WarehouseTickSeconds:F0}초마다 치즈 {WAREHOUSE_TICK_CHEESE} 생산 — " +
                           $"자리를 비워도 벌리지만 소리로 위치가 팔린다 (HP {WAREHOUSE_MAX_HP}, 1인 1채)";
                case ShopItemId.CatAttack:
                    return $"덮치기 피해 +{POUNCE_DAMAGE_PER_STACK} (최대 {MAX_UPGRADE_STACKS}중첩)";
                case ShopItemId.CatSpeed:
                    return $"이동 속도 +{SPEED_PER_STACK:0.0} (최대 {MAX_UPGRADE_STACKS}중첩)";
                default:
                    return "";
            }
        }

        public static ShopItem Find(ShopItemId _id)
        {
            foreach (ShopItem item in CATALOG)
            {
                if (item.Id == _id) return item;
            }
            return default;
        }

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

        /// <summary>핫바 슬롯 수 — 역할별 카탈로그가 이보다 길면 잘린다(현재 쥐 6/고양이 2).</summary>
        public const int HOTBAR_SLOTS = 6;

        /// <summary>역할별 카탈로그 인덱스(카탈로그 순서 유지) — 핫바 1~5번 키 매핑.</summary>
        public static System.Collections.Generic.List<int> RoleCatalogIndices(RoleId _role)
        {
            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < CATALOG.Length && list.Count < HOTBAR_SLOTS; i++)
            {
                if (CATALOG[i].Role == _role) list.Add(i);
            }
            return list;
        }

        /// <summary>
        /// 설치형(월드에 스폰) 아이템인가 — 고스트 미리보기 후 위치 지정 구매.
        /// 미끼는 제외한다: 쫓기는 중에 쓰는 물건인데 고스트 조준을 요구하면 그 순간에 못 쓴다
        /// ("설치기가 너무 힘들다" 2026-07-28) — 발밑에 즉시 떨군다.
        /// </summary>
        public static bool IsPlaceable(ShopItemId _id)
        {
            return _id == ShopItemId.Fortress || _id == ShopItemId.Warehouse;
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
