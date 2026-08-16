using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 요새가 **내 위치 중심에 즉시** 서므로(2026-08-15) 그 자리에 있던 남의 몸이 벽에 겹친다.
    /// 누구를 어디로 밀어낼지 정하는 순수 함수 — MonoBehaviour와 분리(EditMode TDD).
    ///
    /// 고양이는 풋프린트 안이면 **무조건 바깥**으로 — 안에 남으면 방패가 아니라 관이다.
    /// 쥐는 벽 두께 자리에 있을 때만 가까운 쪽(안/밖)으로 — 안쪽 쥐는 같이 보호받는다.
    /// 문(앞·뒤 벽 가운데 폭 1.0)은 빈칸이라 그 자리의 쥐는 안 민다.
    ///
    /// 치수는 StructurePrefabFactory의 요새 프리팹과 같다(HALF 1.2 · 벽 0.2 · 문 1.0).
    /// </summary>
    public static class FortressPushLogic
    {
        public const float HALF = 1.2f;
        public const float WALL = 0.2f;
        public const float DOOR_HALF = 0.5f;
        public const float MARGIN = 0.2f;

        /// <summary>몸이 겹칠 수 있는 반경 — 이 밖이면 볼 것도 없다.</summary>
        public static float SearchRadius(float _bodyRadius) => HALF + _bodyRadius + MARGIN;

        /// <summary>
        /// 밀어야 하면 true와 월드 delta. `_yawDeg`는 요새 Y 회전(입구 = 로컬 +Z).
        /// </summary>
        public static bool PushDelta(Vector3 _center, float _yawDeg, Vector3 _body, float _bodyRadius,
            bool _isCat, out Vector3 _delta)
        {
            _delta = Vector3.zero;

            // 요새 로컬 평면 좌표
            Vector3 rel = _body - _center;
            rel.y = 0f;
            Vector3 local = Quaternion.Euler(0f, -_yawDeg, 0f) * rel;
            float x = local.x, z = local.z;
            float outer = HALF + _bodyRadius;          // 이 밖이면 벽에 안 닿는다
            float inner = HALF - WALL - _bodyRadius;   // 이 안이면 벽에 안 닿는다(안쪽)

            if (Mathf.Abs(x) >= outer || Mathf.Abs(z) >= outer) return false;

            float outsideAt = outer + MARGIN;
            Vector3 localDelta;

            if (_isCat)
            {
                // 가장 가까운 변으로 내보낸다
                float dxToEdge = outsideAt - Mathf.Abs(x);
                float dzToEdge = outsideAt - Mathf.Abs(z);
                localDelta = dxToEdge <= dzToEdge
                    ? new Vector3(Sign(x) * dxToEdge, 0f, 0f)
                    : new Vector3(0f, 0f, Sign(z) * dzToEdge);
            }
            else
            {
                bool inXWall = Mathf.Abs(x) > inner;                              // 좌·우 벽 띠
                bool inZWall = Mathf.Abs(z) > inner && Mathf.Abs(x) >= DOOR_HALF;  // 앞·뒤 벽 띠(문 제외)
                if (!inXWall && !inZWall) return false;

                float insideAt = inner - MARGIN;
                if (inXWall)
                {
                    float toIn = Mathf.Abs(x) - insideAt, toOut = outsideAt - Mathf.Abs(x);
                    localDelta = new Vector3(Sign(x) * (toIn <= toOut ? -toIn : toOut), 0f, 0f);
                }
                else
                {
                    float toIn = Mathf.Abs(z) - insideAt, toOut = outsideAt - Mathf.Abs(z);
                    localDelta = new Vector3(0f, 0f, Sign(z) * (toIn <= toOut ? -toIn : toOut));
                }
            }

            _delta = Quaternion.Euler(0f, _yawDeg, 0f) * localDelta;
            return true;
        }

        // 정중앙(0)은 +쪽으로 — Mathf.Sign(0)=1이라 같지만 의도를 적어 둔다
        private static float Sign(float _v) => _v < 0f ? -1f : 1f;
    }
}
