using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// **누가 어디를 맡았나.** 규칙은 <see cref="ReservationLogic"/>에 있고 여기는 표만 든다.
    ///
    /// MonoBehaviour가 아니다 — 봇 매니저가 하나 들고 봇들에게 넘긴다.
    /// 강아지는 한 마리뿐이라 쓰지 않는다.
    /// </summary>
    public class Reservations
    {
        private readonly Dictionary<ulong, Vector3> m_Claims = new Dictionary<ulong, Vector3>();

        /// <summary>지금 맡은 자리 수 — QA가 "예약이 돌긴 하나"를 이걸로 본다.</summary>
        public int Count => m_Claims.Count;

        /// <summary>여기로 간다고 알린다. 같은 주인이 다시 부르면 덮어쓴다.</summary>
        public void Claim(ulong _owner, Vector3 _point)
        {
            m_Claims[_owner] = _point;
        }

        /// <summary>그만둔다.</summary>
        public void Release(ulong _owner)
        {
            m_Claims.Remove(_owner);
        }

        public void Clear()
        {
            m_Claims.Clear();
        }

        /// <summary>
        /// 그 자리를 고를 때 곱할 배율. **제 예약은 세지 않는다** —
        /// 안 그러면 자기가 맡은 곳을 자기가 피하게 된다.
        /// </summary>
        public float WeightAt(Vector3 _point, ulong _asking, float _radius = ReservationLogic.DEFAULT_RADIUS)
        {
            float weight = 1f;
            foreach (KeyValuePair<ulong, Vector3> claim in m_Claims)
            {
                if (claim.Key == _asking) continue;

                float distance = Flat(claim.Value - _point).magnitude;
                weight = ReservationLogic.Combine(weight, ReservationLogic.Weight(distance, _radius));
            }
            return weight;
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v;
        }
    }
}
