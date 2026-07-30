// 이 파일은 CSV에서 자동 생성된다 — 직접 고치지 마라.
// 원본: Assets/Resources/CSVInfo/BalanceInfo.csv → 메뉴 Tools/CTC/Convert CSV → Info
using System.Collections.Generic;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;

namespace CTC.Runtime.Info
{
    public partial class BalanceInfo
    {
        public int MouseStartCheese;
        public float MatchDurationSeconds;
        public int MaxUpgradeStacks;
        public int PounceDamagePerStack;
        public float SpeedPerStack;
        public int FortressHp;
        public int FortressHealPerTick;
        public float FortressHealRadius;
        public float DecoyLifetimeSeconds;
        public int WarehouseIncomePerSecond;
        public int WarehouseTickCheese;
        public int WarehouseMaxHp;
        public int WarehouseLimitPerMouse;
        public float WarehouseSoundRangeMeters;
        public int TotalMapCheese;
        public int LastStandReserve;
        public int KillBonus;
        public int PlayerDamageDivisor;
        public int StructureDamageDivisor;
    }

    public partial class InfoManager
    {
        public List<BalanceInfo> BalanceInfoList = new List<BalanceInfo>();
    }
}
