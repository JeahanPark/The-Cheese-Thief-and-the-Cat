using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// **남이 맡은 자리는 값이 떨어진다** (2026-08-12).
    ///
    /// 기술소개서(앨리스 19~20p)의 예약 시스템을 옮긴 것이다. 원본은 A*가 지나간 **노드**를
    /// 예약하고 다음 몬스터가 그 노드의 비용을 올려 우회하게 했다 — 넓은 지형에서는 분산해서
    /// 접근하고 미로에서는 다른 길로 돌아온다.
    ///
    /// 여기서는 노드 비용에 손을 못 댄다(길찾기가 유니티 폴리곤이라 내부를 못 건드린다).
    /// 그래서 **목표를 고르는 점수**에 같은 규칙을 건다. 효과는 같다 — 둘이 같은 소리를
    /// 쫓지 않고 흩어진다.
    ///
    /// **협동이 아니다.** 서로 신호를 주고받지 않고, 각자 제 점수만 보고 고른다.
    /// 기획서의 "봇은 각자 논다"를 깨지 않으면서 뭉치는 것만 막는다.
    /// </summary>
    public static class ReservationLogic
    {
        /// <summary>이 거리 밖이면 남의 예약과 상관없다.</summary>
        public const float DEFAULT_RADIUS = 18f;

        /// <summary>정확히 같은 자리일 때 남는 값. 0이면 절대 못 고르게 되어 답이 없을 때 굳는다.</summary>
        public const float MIN_WEIGHT = 0.15f;

        /// <summary>
        /// 남의 예약과 `_distance`만큼 떨어진 목표의 **점수 배율**.
        /// 반경 밖이면 1(안 깎음), 같은 자리면 <see cref="MIN_WEIGHT"/>.
        /// </summary>
        public static float Weight(float _distance, float _radius = DEFAULT_RADIUS)
        {
            if (_radius <= 0f) return 1f;

            float t = Mathf.Clamp01(Mathf.Max(0f, _distance) / _radius);
            return MIN_WEIGHT + (1f - MIN_WEIGHT) * t;
        }

        /// <summary>예약이 여럿이면 **가장 심하게 깎는 것**만 본다 — 배율을 곱하면 금세 0이 된다.</summary>
        public static float Combine(float _current, float _next)
        {
            return Mathf.Min(_current, _next);
        }
    }
}
