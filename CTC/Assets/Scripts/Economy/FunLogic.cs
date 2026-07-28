namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 고양이 장난기(Fun) 적립 수학 — 스펙 v3.2, 밸런스 개정 v3.6. 순수 함수(EditMode TDD).
    ///
    /// v3.6(2026-07-28 플레이 피드백 "고양이가 버는 돈이 너무 많다"):
    /// - 플레이어 실피해 1:1 → 0.5:1, 킬 보너스 20 → 30. '깨작깨작 때리기'보다 '끝내기'에 값을 준다.
    /// - 구조물(요새·창고·미끼) 피해 1:1 → 0.25:1. 요새 1채가 300pt(업그레이드 1.5개)였고
    ///   요새는 다시 지어지므로 벽 때리기가 사실상 무한 수입원이었다. 이제 75pt.
    ///   "쥐의 방어 지출 = 고양이 수입"이라는 설계 축은 유지하되 배율만 낮춘다.
    /// </summary>
    public static class FunLogic
    {
        public const int KILL_BONUS = 30;

        /// <summary>플레이어 실피해 1당 장난기 — 정수 유지를 위해 나눗셈으로 적용한다.</summary>
        public const int PLAYER_DAMAGE_DIVISOR = 2;

        /// <summary>구조물 실피해 1당 장난기 — 요새(300HP) 1채 = 75.</summary>
        public const int STRUCTURE_DAMAGE_DIVISOR = 4;

        /// <param name="_isStructure">대상이 플레이어가 아닌 설치물인가(요새·창고·미끼).</param>
        public static int AccruedFun(int _dealtDamage, bool _killedPlayer, bool _isStructure)
        {
            int dealt = UnityEngine.Mathf.Max(0, _dealtDamage);
            if (_isStructure) return dealt / STRUCTURE_DAMAGE_DIVISOR; // 킬 보너스는 플레이어만

            return dealt / PLAYER_DAMAGE_DIVISOR + (_killedPlayer ? KILL_BONUS : 0);
        }
    }
}
