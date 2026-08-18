using CTC.Runtime.Lobby;

namespace CTC.Runtime.Economy
{
    public enum ShopItemId
    {
        None = 0,
        Slingshot = 1,
        MachineGun = 2,
        Dash = 3,
        // 4 = Decoy(미끼) — 2026-08-15 삭제. 값은 복제·직렬화되므로 재사용하지 않는다.
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

        // DescriptionOf(수치 포함 장문 설명)는 HUD 개편(2026-08-15)으로 삭제 — 좌하단 장문이
        // 사라지며 읽는 곳이 없어졌다. 항목 안내는 핫바 반줄 힌트 + 구매 토스트(ShopHotbar)가 맡는다.

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
        /// 고스트 조준(위치 지정) 설치인가 — 창고뿐이다.
        /// 요새는 2026-08-15부터 **내 위치 중심 즉시 설치**다: 쫓기는 중에 쓰는 방패인데
        /// 고스트 조준을 요구하면 그 순간에 못 쓴다(예전 미끼가 즉시 설치였던 이유와 같다 — 미끼는 요새에 흡수됐다).
        /// </summary>
        public static bool IsPlaceable(ShopItemId _id)
        {
            return _id == ShopItemId.Warehouse;
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
