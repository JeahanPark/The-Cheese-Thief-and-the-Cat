using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 전투 판정 수학. MonoBehaviour와 분리된 순수 함수 — EditMode TDD.
    /// 서버(호스트)만 이 판정으로 실제 데미지를 적용한다.
    /// </summary>
    public static class CombatLogic
    {
        public static int ApplyDamage(int _currentHp, int _damage)
        {
            return Mathf.Max(0, _currentHp - Mathf.Max(0, _damage));
        }

        public static bool IsDead(int _hp)
        {
            return _hp <= 0;
        }

        public static bool IsCooldownReady(double _lastUseTime, double _now, float _cooldownSeconds)
        {
            return _now - _lastUseTime >= _cooldownSeconds;
        }

        /// <summary>전방 부채꼴(평면) 판정 — y 차이는 무시한다.</summary>
        public static bool IsInPounceArc(Vector3 _attackerPos, Vector3 _attackerForward, Vector3 _targetPos, float _range, float _halfAngleDeg)
        {
            Vector3 to = _targetPos - _attackerPos;
            to.y = 0f;
            if (to.sqrMagnitude > _range * _range) return false;
            if (to.sqrMagnitude < 1e-6f) return true;

            Vector3 forward = _attackerForward;
            forward.y = 0f;
            return Vector3.Angle(forward, to) <= _halfAngleDeg;
        }
    }
}
