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

        /// <summary>
        /// 눈이 **똑바로 앞을 볼 때**의 축. 여기를 조준하면 두 눈동자가 각자 흰자 한가운데 앉는다.
        ///
        /// 머리 본의 축 규약은 종마다 다르다 — 2026-09-10 실측: **고양이는 +z가 앞, 쥐는 +y가 앞**이다
        /// (팩의 두 FBX가 다르게 리깅됐다). 그래서 코드에 박지 않고 프리팹에 굽는다
        /// (<c>EyeLook.m_HeadForwardLocal</c>). 안 굽혔으면 두 쉬는 방향의 **대칭축**으로 물러선다 —
        /// 좌우 한가운데인 것은 보장되지만 앞을 가리키지는 않는다.
        /// </summary>
        public static Vector3 GazePivot(Vector3 _headForwardLocal, Vector3 _restR, Vector3 _restL)
        {
            if (_headForwardLocal.sqrMagnitude > 1e-12f) return _headForwardLocal.normalized;

            Vector3 r = _restR.sqrMagnitude > 1e-12f ? _restR.normalized : Vector3.forward;
            Vector3 l = _restL.sqrMagnitude > 1e-12f ? _restL.normalized : Vector3.forward;
            Vector3 mid = r + l;
            return mid.sqrMagnitude > 1e-6f ? mid.normalized : r;
        }

        /// <summary>
        /// **두 눈이 같이 도는 회전**(2026-09-10). 사람 눈처럼 한 쌍이 같은 축·같은 각도로 움직인다.
        ///
        /// **왜 이게 필요했나.** 예전에는 눈마다 <see cref="ClampGaze"/>를 제 쉬는 방향 기준으로
        /// 따로 불렀다. <c>Vector3.RotateTowards</c>는 <c>cross(rest, aim)</c>을 축으로 도는데
        /// 두 쉬는 방향이 멀리 벌어져 있으면(고양이 60° · 쥐 126°) **그 축이 눈마다 다르다.**
        /// 그래서 오른쪽을 보라고 하면 오른눈은 가로로 돌고 왼눈은 세로로 도는 일이 생겼다 —
        /// 2026-09-10 실측: 고양이가 오른쪽을 볼 때 오른눈 눈동자는 0.245만큼 움직이는데
        /// 왼눈은 0.019만큼(13배 차이) 그것도 **반대쪽 세로**로 갔다. 두 눈이 갈려 보인 원인이다.
        ///
        /// 대신 **축 하나를 조준 쪽으로 클램프해 돌리고**(<see cref="GazePivot"/>) 그 회전을
        /// 두 눈에 똑같이 먹인다. 회전이 하나뿐이라 두 눈이 갈릴 수가 없다.
        /// 눈이 쉬는 방향에서 벗어나는 각도도 둘이 항상 같다(≤ <paramref name="_maxAngleDeg"/>).
        /// </summary>
        public static Quaternion GazeSwing(Vector3 _pivotDir, Vector3 _aimDir, float _maxAngleDeg)
        {
            Vector3 pivot = _pivotDir.sqrMagnitude > 1e-12f ? _pivotDir.normalized : Vector3.forward;
            Vector3 clamped = ClampGaze(pivot, _aimDir, _maxAngleDeg);
            return Quaternion.FromToRotation(pivot, clamped);
        }

        /// <summary>
        /// 눈동자가 앉을 자리 — **머리 본 로컬**, 흰자 반지름 단위.
        ///
        /// 흰자는 눈 축으로 눌린 타원체다(<c>EyeFlatten</c>). <see cref="EyeLook"/>는 흰자를
        /// <c>LookRotation(rest)</c>로 돌리고 <c>(1,1,flatten)</c>으로 눌러 눈동자를 자식으로 단다 —
        /// 이 함수는 **그 트랜스폼 사슬과 같은 값**이다(EyeLookLogicTests가 둘을 대조한다).
        /// 눈 축 둘레로 굴린 각도(roll)에는 결과가 안 변한다 — 누름이 그 축에 대칭이라서다
        /// (2026-09-10 실측: roll 0·37·90·180°에서 오프셋이 소수점 6자리까지 같았다).
        /// </summary>
        public static Vector3 PupilOffset(Vector3 _restDir, Vector3 _gazeDir, float _flatten, float _distance)
        {
            Vector3 f = _restDir.sqrMagnitude > 1e-12f ? _restDir.normalized : Vector3.forward;
            Vector3 g = _gazeDir.sqrMagnitude > 1e-12f ? _gazeDir.normalized : f;
            float along = Vector3.Dot(g, f);
            return ((g - along * f) + f * (along * _flatten)) * _distance;
        }

        /// <summary>오른눈 기준값을 왼눈으로 — 좌우 대칭이라 x만 뒤집는다.</summary>
        public static Vector3 Mirror(Vector3 _right)
        {
            return new Vector3(-_right.x, _right.y, _right.z);
        }
    }
}
