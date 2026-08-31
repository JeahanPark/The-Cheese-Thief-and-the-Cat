using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 후추 탄 지속피해의 순수 수학 (빌드 심화 v1 · 후추 선반 ①, 2026-08-31).
    /// 모델 단순화: 스택 수 하나 + 만료 시각 하나 — 맞을 때마다 스택 +1(상한), 만료는 전체 리프레시.
    /// 스택별 독립 타이머보다 읽기 쉽고, "계속 맞으면 계속 아프다"는 체감은 같다.
    /// </summary>
    public static class PepperDotLogic
    {
        /// <summary>피격 시 스택 — 상한에서 멈춘다. 음수 방어.</summary>
        public static int AddStack(int _current, int _max)
        {
            return Mathf.Clamp(_current + 1, 1, Mathf.Max(1, _max));
        }

        /// <summary>
        /// 프레임 누적 — 소수 데미지를 캐리에 모아 정수만 내보낸다.
        /// 총량 보존: 어떤 프레임 패턴이든 t초 뒤 합계는 dps×t(±1)다.
        /// </summary>
        public static int Accumulate(float _deltaSeconds, float _damagePerSecond, ref float _carry)
        {
            _carry += _deltaSeconds * _damagePerSecond;
            int whole = (int)_carry;
            _carry -= whole;
            return whole;
        }

        /// <summary>만료 판정 — 서버 시계 기준.</summary>
        public static bool IsExpired(double _expireAt, double _now)
        {
            return _now >= _expireAt;
        }
    }
}

