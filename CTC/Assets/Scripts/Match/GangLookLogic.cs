
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 깡패 쥐 모습·몽둥이의 조건과 판정 — 순수 함수(EditMode, 2026-09-14).
    /// 설계: docs/superpowers/specs/2026-09-14-gang-mouse-look-design.md
    /// </summary>
    public static class GangLookLogic
    {
        /// <summary>
        /// **2페이즈 + 깡패 편**일 때만. 표를 주운 순간이 아니다 — 그때 바뀌면 누가 표를 쥐었는지가 몸으로 드러난다(기획서 §11).
        /// </summary>
        public static bool Active(MouseJobId _job, MatchPhase _phase)
        {
            return _phase == MatchPhase.Gang && MouseJobLogic.IsGangSide(_job);
        }

        /// <summary>
        /// 몽둥이 부채꼴 — 수평 거리 ≤ 사거리이고, 몸이 보는 방향에서 반각(arc/2) 안. 딱 붙어 있으면(0.2m 안) 방향을 안 따진다.
        /// </summary>
        public static bool InArc(Vector3 _from, Vector3 _forward, Vector3 _target, float _range, float _arcDegrees)
        {
            Vector3 to = _target - _from;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > _range) return false;
            if (dist < 0.2f) return true;

            Vector3 fwd = new Vector3(_forward.x, 0f, _forward.z);
            if (fwd.sqrMagnitude < 1e-6f) return true;
            return Vector3.Angle(fwd, to) <= _arcDegrees * 0.5f;
        }

        /// <summary>깡패 편끼리는 안 때린다 — 2페이즈는 "보스+부하 vs 나머지"다.</summary>
        public static bool CanClub(MouseJobId _attacker, MouseJobId _target)
        {
            return !(MouseJobLogic.IsGangSide(_attacker) && MouseJobLogic.IsGangSide(_target));
        }
    }
}
