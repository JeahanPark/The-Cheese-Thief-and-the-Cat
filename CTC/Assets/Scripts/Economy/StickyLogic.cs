using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 끈끈이(테크 v3) 수학 — 반경·감속·쿨. MonoBehaviour와 분리해 EditMode로 덮는다.
    /// 값의 원본은 TechInfo P1(반경)·P2(감속 비율)·P3(쿨), 지속은 BalanceInfo.StickySeconds다.
    /// </summary>
    public static class StickyLogic
    {
        /// <summary>감속 비율 0.4 → 이속 배율 0.6. 표가 이상해도 0~1을 벗어나지 않는다.</summary>
        public static float SpeedMultiplier(float _slowRatio) => Mathf.Clamp01(1f - _slowRatio);

        /// <summary>
        /// 장판 안인가 — **수평 거리만** 본다. 높이를 같이 재면 계단 한 칸 위를 지나는 고양이가
        /// 빠져나가고, 장판이 얇은 바닥 쿼드라 시각과도 어긋난다.
        /// </summary>
        public static bool Inside(Vector3 _center, Vector3 _point, float _radius)
        {
            float dx = _point.x - _center.x, dz = _point.z - _center.z;
            return dx * dx + dz * dz <= _radius * _radius;
        }

        /// <summary>쿨다운이 끝났는가(서버 시계 기준).</summary>
        public static bool Ready(double _readyAt, double _now) => _now >= _readyAt;

        /// <summary>
        /// 겹친 장판들이 건 배율 중 **가장 센 것**(=최솟값)이 이긴다. 하나도 없으면 1(감속 없음).
        /// 예전엔 배율이 한 칸뿐이라 나중에 나간 장판이 1을 덮어써서, 아직 밟고 있는 장판 위에서
        /// 고양이가 제 속도로 달렸다. 이제 건 쪽마다 한 칸을 갖고 여기서 최솟값을 고른다.
        /// </summary>
        public static float MinMultiplier(IEnumerable<float> _multipliers)
        {
            if (_multipliers == null) return 1f;

            float min = 1f;
            bool any = false;
            foreach (float m in _multipliers)
            {
                if (!any || m < min) min = m;
                any = true;
            }
            return any ? min : 1f;
        }
    }
}
