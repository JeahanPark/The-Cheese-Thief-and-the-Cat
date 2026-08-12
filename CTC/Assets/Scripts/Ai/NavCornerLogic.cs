using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// **꺾는 점을 벽에서 띄운다** (2026-08-12).
    ///
    /// 길찾기가 주는 코너는 **벽 모서리에 붙어 있다**. `NavMeshAgent`는 반지름을 알고 스스로
    /// 비껴 가지만, 이 프로젝트의 봇·강아지는 그 좌표를 향해 그냥 미는 구조라(사람과 같은
    /// 입력 통로를 써야 해서) 모퉁이마다 벽을 스친다 — 걸릴 때마다 몇 백 밀리초씩 버린다.
    ///
    /// 폴리곤은 굽는 반지름(0.3)만큼 이미 깎여 있지만 고양이 몸은 0.35라 그 차이만큼 남는다.
    /// 그래서 조금만 띄우면 된다.
    ///
    /// 예전에는 "걸리면 65도 틀어 벽을 타기"로 덮었다. 증상만 가린 것이라 지웠다 —
    /// 애초에 벽에 닿지 않게 꺾으면 탈 벽이 없다.
    /// </summary>
    public static class NavCornerLogic
    {
        /// <summary>
        /// 굽는 반지름과 실제 몸의 차이 + 여유. 크게 잡으면 좁은 복도에서 반대쪽 벽에 붙는다.
        /// </summary>
        public const float DEFAULT_CLEARANCE = 0.25f;

        /// <summary>
        /// `_corner`를 **꺾이는 바깥쪽**으로 `_clearance`만큼 민다.
        ///
        /// 들어온 방향과 나갈 방향의 차가 곧 바깥쪽이다 — 직진이면 차가 0이라 그대로 둔다.
        /// 되돌아오는 경우(180도)는 온 길 쪽으로 밀리는데, 그건 어차피 막다른 골목이라 무해하다.
        /// </summary>
        public static Vector3 Offset(
            Vector3 _prev, Vector3 _corner, Vector3 _next, float _clearance = DEFAULT_CLEARANCE)
        {
            Vector3 inDir = Flat(_corner - _prev);
            Vector3 outDir = Flat(_next - _corner);
            if (inDir.sqrMagnitude < 1e-4f || outDir.sqrMagnitude < 1e-4f) return _corner;

            Vector3 outward = inDir.normalized - outDir.normalized;
            if (outward.sqrMagnitude < 1e-4f) return _corner;   // 직진 — 꺾을 게 없다

            return _corner + outward.normalized * Mathf.Max(0f, _clearance);
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v;
        }
    }
}
