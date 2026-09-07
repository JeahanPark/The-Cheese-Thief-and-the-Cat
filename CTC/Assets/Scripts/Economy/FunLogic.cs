namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 고양이 장난기(Fun) 적립 수학 — 스펙 v3.2, 밸런스 개정 v3.6. 순수 함수(EditMode TDD).
    ///
    /// v3.6(2026-07-28 플레이 피드백 "고양이가 버는 돈이 너무 많다"):
    /// - 플레이어 실피해 1:1 → 0.5:1, 킬 보너스 20 → 30. '깨작깨작 때리기'보다 '끝내기'에 값을 준다.
    /// - 구조물(요새·창고·미끼) 피해 1:1 → 0.25:1. 요새 1채가 300pt(업그레이드 1.5개)였고
    ///   요새는 다시 지어지므로 벽 때리기가 사실상 무한 수입원이었다.
    ///   "쥐의 방어 지출 = 고양이 수입"이라는 설계 축은 유지하되 배율만 낮춘다.
    ///   요새 HP는 그 뒤로 480까지 올랐지만 고양이 수입은 <see cref="StructureFun"/>이
    ///   기본 HP로 환산해 묶어 둔다 — 레벨이 수입을 부풀리면 v3.6이 되돌아온다.
    /// </summary>
    public static class FunLogic
    {
        // 수치 원본은 CSV(BalanceInfo.csv) — 2026-07-31 데이터 분리
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;

        public static int KILL_BONUS => Balance.KillBonus;

        /// <summary>플레이어 실피해 1당 장난기 — 정수 유지를 위해 나눗셈으로 적용한다.</summary>
        public static int PLAYER_DAMAGE_DIVISOR => Balance.PlayerDamageDivisor;

        /// <summary>
        /// 구조물 실피해 1당 장난기(나눗셈). 요새 1채가 주는 값은 레벨과 무관하게
        /// **기본 HP 기준**이다 — <see cref="StructureFun"/>이 그 환산을 한다.
        /// </summary>
        public static int STRUCTURE_DAMAGE_DIVISOR => Balance.StructureDamageDivisor;

        /// <param name="_isStructure">대상이 플레이어가 아닌 설치물인가(요새·창고·미끼).</param>
        public static int AccruedFun(int _dealtDamage, bool _killedPlayer, bool _isStructure)
        {
            int dealt = UnityEngine.Mathf.Max(0, _dealtDamage);
            if (_isStructure) return dealt / STRUCTURE_DAMAGE_DIVISOR; // 킬 보너스는 플레이어만

            return dealt / PLAYER_DAMAGE_DIVISOR + (_killedPlayer ? KILL_BONUS : 0);
        }

        /// <summary>구조물 장난기는 기본 HP 기준이다 — 요새 레벨을 올린다고 고양이 수입이 늘면 벽 때리기가 다시 최고 수입원이 된다(v3.6).</summary>
        /// <param name="_maxHp">맞은 구조물의 실제 최대 HP(요새는 테크 레벨을 탄다).</param>
        /// <param name="_baseHp">그 구조물의 기본(Lv1) 최대 HP. 레벨이 없는 구조물은 _maxHp와 같은 값을 넘긴다.</param>
        public static int StructureFun(int _dealtDamage, int _maxHp, int _baseHp)
        {
            int dealt = UnityEngine.Mathf.Max(0, _dealtDamage);
            // 상한 1 — 기본 HP가 더 크게 잡히더라도 수입이 부풀지는 않는다.
            float scale = _maxHp > 0 && _baseHp > 0
                ? UnityEngine.Mathf.Min(1f, _baseHp / (float)_maxHp)
                : 1f;
            return UnityEngine.Mathf.RoundToInt(dealt * scale) / STRUCTURE_DAMAGE_DIVISOR;
        }
    }
}
