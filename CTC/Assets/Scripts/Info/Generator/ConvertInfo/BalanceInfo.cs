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
        public int WarehouseIncomePerSecond;
        public int WarehouseTickCheese;
        public int WarehouseMaxHp;
        public int WarehouseLimitPerMouse;
        public float WarehouseSoundRangeMeters;
        public int CheesePerMouse;
        public int LastStandReserve;
        public int KillBonus;
        public int PlayerDamageDivisor;
        public int StructureDamageDivisor;
        public float DashDistance;
        public float DashDurationSeconds;
        public float DashCooldownSeconds;
        public float PlaceMaxDistanceMeters;
        public float PlaceGhostMaxMeters;
        public float FarmingSoundRangeMeters;
        public float CamDefaultPitchDeg;
        public float CamMinPitchDeg;
        public float CamMaxPitchDeg;
        public float CamScreenY;
        public float MouseSensitivityDefault;
        public float BgmVolumeDefault;
        public float SfxVolumeDefault;
        public int AlcoveHp;
        public int AlcoveCheese;
        public float AlcoveHitSoundMeters;
        public float AlcoveBreakSoundMeters;
        public int DogAlcoveHp;
        public int DogHp;
        public float DogMoveSpeed;
        public float DogDetectRadius;
        public float DogAggroDropSeconds;
        public float DogRepathSeconds;
        public float DogBiteRangeMeters;
        public float DogBiteCooldown;
        public int DogBiteDamageCat;
        public int DogBiteDamageMouse;
        public float DogBarkIntervalSeconds;
        public float DogBarkSoundMeters;
        public float DogAlcoveIdleSoundMeters;
        public int BoxHp;
        public int BoxCheese;
        public int BoxDropCount;
        public float BoxHitSoundMeters;
        public float BoxBreakSoundMeters;
        public float CatSkillSeconds;
        public float CatSkillCooldown;
        public float CatFireDamageMultiplier;
        public float CatWindSpeedMultiplier;
        public float CatDarkStrength;
        public float CatDarkNearMeters;
        public float CatDarkFarMeters;
        public float CatSkillSoundMeters;
        public float GangPhaseSeconds;
        public float HeroHpMultiplier;
        public float HeroDamageMultiplier;
        public float GangRecruitRadius;
        public int PepperDotDamage;
        public int TechSlotCap;
        public int FortressPlaceCost;
        public int ShopSpotCount;
        public float ShopSpotRadius;
        public float ShopSpotMinGap;
        public float ShopBuySoundMeters;
        public int WarehousePlaceCost;
        public int CheeseShieldRate;
        public float StickySeconds;
        public int MinimapTexturePx;
        public int MinimapSizePx;
        public float MinimapDiscoverMeters;
        public float EyeMaxAngleDeg;
        public float EyeFollowSpeed;
        public float EyeBlinkMinSeconds;
        public float EyeBlinkMaxSeconds;
        public float EyeBlinkSeconds;
        public float EyeScleraScale;
        public float EyePupilRatio;
        public float EyePupilOffsetRatio;
        public float EarTurnMaxDeg;
        public float EarPerkDeg;
        public float EarHoldSeconds;
        public float EarFollowSpeed;
        public float EyeFlatten;
        public float EyeInsetRatio;
    }

    public partial class InfoManager
    {
        public List<BalanceInfo> BalanceInfoList = new List<BalanceInfo>();
    }
}
