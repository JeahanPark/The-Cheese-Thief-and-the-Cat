using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 눈동자 계산의 순수 부분 — 트랜스폼도 시간도 모른다(EditMode 테스트 대상).
    /// 방향은 전부 **머리 본 로컬**이다.
    /// </summary>
    public static class EyeLookLogic
    {
        /// <summary>눈이 뒤집히지 않게, 쉬는 방향에서 최대 각도까지만 조준 쪽으로 돌린 방향.</summary>
        public static Vector3 ClampGaze(Vector3 _restDir, Vector3 _aimDir, float _maxAngleDeg)
        {
            Vector3 rest = _restDir.sqrMagnitude > 1e-12f ? _restDir.normalized : Vector3.forward;
            if (_aimDir.sqrMagnitude <= 1e-12f) return rest;

            Vector3 aim = _aimDir.normalized;
            float max = Mathf.Clamp(_maxAngleDeg, 0f, 180f) * Mathf.Deg2Rad;

            // 정확히 반대 방향이면 회전축이 없다 — 쉬는 방향을 그대로 둔다(뒤집힘 방지).
            if (Vector3.Dot(rest, aim) < -0.9999f) return rest;

            return Vector3.RotateTowards(rest, aim, max, 0f).normalized;
        }

        /// <summary>스냅 금지 — 남은 각도에 비례해 따라간다(프레임률과 무관한 지수 감쇠).</summary>
        public static Vector3 Follow(Vector3 _current, Vector3 _target, float _speed, float _deltaTime)
        {
            if (_current.sqrMagnitude <= 1e-12f) return _target;
            if (_target.sqrMagnitude <= 1e-12f) return _current;
            if (_speed <= 0f || _deltaTime <= 0f) return _current.normalized;

            float t = 1f - Mathf.Exp(-_speed * _deltaTime);
            return Vector3.Slerp(_current.normalized, _target.normalized, t).normalized;
        }

        /// <summary>깜빡임 진행도 0→1→0. 범위 밖이면 0(눈 뜬 상태).</summary>
        public static float BlinkCover(float _elapsed, float _duration)
        {
            if (_duration <= 0f || _elapsed <= 0f || _elapsed >= _duration) return 0f;
            float half = _duration * 0.5f;
            return _elapsed < half ? _elapsed / half : (_duration - _elapsed) / half;
        }

        /// <summary>다음 깜빡임까지의 랜덤 대기 — _rand01은 [0,1).</summary>
        public static float NextBlinkDelay(float _min, float _max, float _rand01)
        {
            float lo = Mathf.Max(0f, Mathf.Min(_min, _max));
            float hi = Mathf.Max(0f, Mathf.Max(_min, _max));
            return Mathf.Lerp(lo, hi, Mathf.Clamp01(_rand01));
        }

        /// <summary>오른눈 기준값을 왼눈으로 — 좌우 대칭이라 x만 뒤집는다.</summary>
        public static Vector3 Mirror(Vector3 _right)
        {
            return new Vector3(-_right.x, _right.y, _right.z);
        }
    }
}
