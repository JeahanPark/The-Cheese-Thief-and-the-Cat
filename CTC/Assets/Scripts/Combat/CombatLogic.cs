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

        /// <summary>
        /// 이 역할을 때릴 수 있는가 (v3.8 5번).
        ///
        /// 전에는 무기마다 "고양이만" · "쥐만"으로 박아 뒀는데, 그 상태로 **제3세력을 넣으면
        /// 양쪽 다 강아지를 못 때린다** — 실제로 그렇게 만들어 놓고 QA에서 잡았다.
        /// 규칙은 한 줄이다: **제 진영만 아니면 때린다.**
        ///
        /// `RoleId.None`(강아지)은 누구의 진영도 아니라 자동으로 양쪽의 표적이 된다.
        /// 요새·미끼·창고·벽감이 `RoleId.Mouse`인 것도 그대로다 — 고양이만 부순다.
        /// </summary>
        public static bool CanAttack(Lobby.RoleId _attacker, Lobby.RoleId _target)
        {
            return _attacker != _target;
        }
    }
}
