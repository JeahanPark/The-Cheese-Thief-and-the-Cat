using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 치즈 지점 수입 규칙(원작 약올리기 돈줄 대응). MonoBehaviour와 분리된
    /// 순수 함수라 EditMode 테스트가 가능하다.
    /// - 등급(1~7) = 초당 수입 (임시 밸런스 — 튜닝은 스펙 §12)
    /// - 지점당 동시 수입 1마리: 선점자가 남아 있는 한 유지
    /// </summary>
    public static class CheeseSpotLogic
    {
        public const ulong NO_EARNER = ulong.MaxValue;
        public const int MIN_GRADE = 1;
        public const int MAX_GRADE = 7;

        public static int IncomePerSecond(int _grade)
        {
            return Mathf.Clamp(_grade, MIN_GRADE, MAX_GRADE);
        }

        /// <summary>현 선점자가 반경 내에 남아 있으면 유지, 아니면 목록의 첫 후보.</summary>
        public static ulong SelectEarner(ulong _currentEarner, IReadOnlyList<ulong> _present)
        {
            if (_present == null || _present.Count == 0) return NO_EARNER;

            if (_currentEarner != NO_EARNER)
            {
                for (int i = 0; i < _present.Count; i++)
                {
                    if (_present[i] == _currentEarner) return _currentEarner;
                }
            }
            return _present[0];
        }

        /// <summary>소수 누적 수입 → 정수 치즈 지급. 반환: 새 누적 잔여, out: 지급할 정수량.</summary>
        public static float Accumulate(float _accumulated, int _grade, float _deltaSeconds, out int _wholeCheese)
        {
            float total = _accumulated + IncomePerSecond(_grade) * _deltaSeconds;
            _wholeCheese = (int)total;
            return total - _wholeCheese;
        }
    }
}
