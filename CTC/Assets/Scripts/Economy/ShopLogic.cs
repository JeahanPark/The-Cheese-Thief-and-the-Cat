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
        public const int MAX_UPGRADE_STACKS = 5;
        public const int POUNCE_DAMAGE_PER_STACK = 10;
        public const float SPEED_PER_STACK = 0.2f; // 기본 이속 6.4 상향과 동반 하향 — 만렙 7.4 (8.5는 도주 무의미)
        public const int FORTRESS_HEAL_PER_TICK = 10;
        public const int FORTRESS_HP = 300; // 씬 생성기가 요새 프리팹에 굽는 값과 같아야 한다
        public const float FORTRESS_HEAL_RADIUS = 1.0f; // 요새 내부(입구 밖 미적용)
        public const float DECOY_LIFETIME_SECONDS = 30f;

        // 창고(v3.4) — 파밍이 '서 있기'라 지루하다는 플레이 피드백의 답.
        // 지어두면 자리를 비워도 수입이 들어오되, 위치가 드러난 자산이라 고양이가 부술 수 있다.
        // 요새(방어)와 역할이 겹치지 않게 별도 건물로 분리했고 HP는 절반이다.
        // 수입 2/s는 과했다(원가 회수 75초 → 판 하나에 원가의 6배). 1/s로 내려 회수 150초 —
        // 짓고도 캐러 나가야 하는 보조 수입으로 되돌린다 (2026-07-28 플레이 피드백).
        public const int WAREHOUSE_INCOME_PER_SECOND = 1;
        public const int WAREHOUSE_MAX_HP = 150;
        public const int WAREHOUSE_LIMIT_PER_MOUSE = 1;

        // 지급을 매 프레임 잔돈으로 흘리지 않고 덩어리로 끊는다 — 5초마다 10개.
        // 총 수입은 +2/s로 같지만 "생산했다"는 순간이 생겨 소리·이펙트를 붙일 수 있고,
        // 그 소리가 곧 고양이에게 창고 위치를 알리는 단서가 된다(파밍음 20m와 같은 설계).
        public const int WAREHOUSE_TICK_CHEESE = 10;
        public const float WAREHOUSE_SOUND_RANGE_METERS = 15f;

        /// <summary>생산 1회 사이의 간격(초) — 수입/틱량에서 파생되므로 따로 튜닝하지 않는다.</summary>
        public static float WarehouseTickSeconds =>
            WAREHOUSE_TICK_CHEESE / (float)WAREHOUSE_INCOME_PER_SECOND;

        public static readonly ShopItem[] CATALOG =
        {
            new ShopItem(ShopItemId.Slingshot, RoleId.Mouse, 150),
            new ShopItem(ShopItemId.MachineGun, RoleId.Mouse, 600),
            new ShopItem(ShopItemId.Dash, RoleId.Mouse, 100),
            new ShopItem(ShopItemId.Decoy, RoleId.Mouse, 50),
            new ShopItem(ShopItemId.Fortress, RoleId.Mouse, 220),
            new ShopItem(ShopItemId.Warehouse, RoleId.Mouse, 150),
            new ShopItem(ShopItemId.CatAttack, RoleId.Cat, 200),
            new ShopItem(ShopItemId.CatSpeed, RoleId.Cat, 150),
        };

        private static readonly WeaponStats[] WEAPON_TIERS =
        {
            new WeaponStats(5, 0.8f, 8f),    // 0 딱총(기본)
            new WeaponStats(12, 0.8f, 10f),  // 1 새총
            new WeaponStats(5, 0.15f, 10f),  // 2 기관총급
        };

        /// <summary>
        /// 핫바 항목 한 줄 설명 — 이름·가격만으로는 뭘 사는지 알 수 없다(특히 창고·미끼).
        /// 수치를 그대로 적는다: 플레이어가 "얼마짜리가 얼마를 해주는가"로 판단하게.
        /// </summary>
        public static string DescriptionOf(ShopItemId _id)
        {
            switch (_id)
            {
                case ShopItemId.Slingshot:
                    return "딱총을 새총으로 — 5뎀 → 12뎀, 사거리 10m";
                case ShopItemId.MachineGun:
                    return "연사 무기 — 초당 33뎀. 2정이 모이면 고양이를 잡을 수 있다";
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
            int idx = UnityEngine.Mathf.Clamp(_tier, 0, WEAPON_TIERS.Length - 1);
            return WEAPON_TIERS[idx];
        }
    }
}
