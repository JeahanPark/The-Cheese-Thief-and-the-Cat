using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// v2 생존 수학(순수): 치즈 유지비·굶주림. 치즈 = 식량이자 화폐 —
    /// 유지비 틱마다 치즈를 소모하고, 부족하면 HP를 대신 소모한다.
    /// </summary>
    public static class SurvivalLogic
    {
        public const float MATCH_DURATION_SECONDS = 300f;
        public const float UPKEEP_INTERVAL_SECONDS = 10f;
        public const int UPKEEP_COST = 10;
        public const int STARVATION_DAMAGE = 10;
        public const int STARTING_CHEESE = 30;

        /// <summary>이번 틱에 실제로 차감할 치즈 — 잔액만큼만.</summary>
        public static int UpkeepSpend(int _cheese)
        {
            return Mathf.Min(_cheese, UPKEEP_COST);
        }

        /// <summary>잔액이 유지비에 못 미치면 HP로 대신 낸다.</summary>
        public static int StarvationDamage(int _cheese)
        {
            return _cheese >= UPKEEP_COST ? 0 : STARVATION_DAMAGE;
        }
    }
}
