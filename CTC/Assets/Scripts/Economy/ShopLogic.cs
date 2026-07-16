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
        public const float SPEED_PER_STACK = 0.4f;
        public const int FORTRESS_HEAL_PER_TICK = 10;
        public const float FORTRESS_HEAL_RADIUS = 1.0f; // 요새 내부(입구 밖 미적용)
        public const float DECOY_LIFETIME_SECONDS = 30f;

        public static readonly ShopItem[] CATALOG =
        {
            new ShopItem(ShopItemId.Slingshot, RoleId.Mouse, 150),
            new ShopItem(ShopItemId.MachineGun, RoleId.Mouse, 600),
            new ShopItem(ShopItemId.Dash, RoleId.Mouse, 100),
            new ShopItem(ShopItemId.Decoy, RoleId.Mouse, 50),
            new ShopItem(ShopItemId.Fortress, RoleId.Mouse, 220),
            new ShopItem(ShopItemId.CatAttack, RoleId.Cat, 200),
            new ShopItem(ShopItemId.CatSpeed, RoleId.Cat, 150),
        };

        private static readonly WeaponStats[] WEAPON_TIERS =
        {
            new WeaponStats(5, 0.8f, 8f),    // 0 딱총(기본)
            new WeaponStats(12, 0.8f, 10f),  // 1 새총
            new WeaponStats(5, 0.15f, 10f),  // 2 기관총급
        };

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

        public static WeaponStats WeaponStatsForTier(int _tier)
        {
            int idx = UnityEngine.Mathf.Clamp(_tier, 0, WEAPON_TIERS.Length - 1);
            return WEAPON_TIERS[idx];
        }
    }
}
