using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 발소리가 어디까지 닿는가(순수). MonoBehaviour와 분리해 EditMode로 잰다.
    ///
    /// **잠행이 완전 무음이 아닌 이유**(2026-08-10, 120×120 맵 밸런스):
    /// 예전엔 웅크리면 소리가 아예 사라져서, 구석에 웅크린 쥐는 고양이에게 단서가 0이었다.
    /// 맵이 8배 넓어지면 "숨어서 버티면 쥐 승"이라는 규칙과 맞물려 고양이가 이길 길이 없다.
    /// 그래서 잠행은 '무음'이 아니라 '아주 좁은 반경'이 됐다 — 스쳐 지나가면 못 듣지만
    /// 같은 방 코앞까지 오면 들킨다. 잠행의 값어치(이속 ×0.45)는 그대로다.
    /// </summary>
    public static class FootstepLogic
    {
        /// <summary>발소리가 들리는 최대 거리(m). 0이면 아무 소리도 안 난다(정지).</summary>
        public static float AudibleRange(bool _moving, bool _sneaking, float _normalRange, float _sneakRange)
        {
            if (!_moving) return 0f;
            return Mathf.Max(0f, _sneaking ? _sneakRange : _normalRange);
        }

        /// <summary>속도가 걷기 임계를 넘었는가 — 제자리 미세 떨림으로 소리가 나면 안 된다.</summary>
        public static bool IsMoving(float _smoothedSpeed, float _walkThreshold)
        {
            return _smoothedSpeed >= _walkThreshold;
        }

        /// <summary>
        /// 한 걸음씩 재생(2026-08-17): 이동 거리를 누적해 보폭을 넘을 때마다 한 걸음 — 박자가 **속도에
        /// 비례**한다. 살금(×0.45)은 드문드문, 대시는 타다닥. 루프 클립의 pitch를 만지면 느린 걸음이
        /// 늘어져 둔탁해지므로(사용자 지적) 클립은 그대로 두고 **간격만** 바꾼다.
        /// 누적이 보폭을 넘으면 한 걸음 빼고 남은 거리를 돌려준다(한 프레임에 여러 걸음이면 하나만 —
        /// 나머지는 다음 프레임에 이어서 난다).
        /// </summary>
        public static float Advance(float _accumulated, float _distance, float _strideMeters, out bool _step)
        {
            float acc = _accumulated + Mathf.Max(0f, _distance);
            if (_strideMeters <= 0f || acc < _strideMeters)
            {
                _step = false;
                return acc;
            }
            _step = true;
            return acc - _strideMeters;
        }

        /// <summary>고양이 원래 발소리 루프의 박자(초당 걸음 수). 연출 상수 — CSV로 내리지 않는다.</summary>
        public const float CAT_LOOP_STEPS_PER_SECOND = 2.5f;

        /// <summary>쥐 원래 발소리 루프의 박자(초당 걸음 수).</summary>
        public const float MOUSE_LOOP_STEPS_PER_SECOND = 3.4f;

        /// <summary>역할별 루프 박자 — 프리팹에 보폭을 굽는 곳(PlayerPrefabFactory)이 이것으로 <see cref="StrideOf"/>를 부른다.</summary>
        public static float LoopStepsPerSecondOf(bool _isCat)
        {
            return _isCat ? CAT_LOOP_STEPS_PER_SECOND : MOUSE_LOOP_STEPS_PER_SECOND;
        }

        /// <summary>보폭(m) — 원래 루프 클립의 박자(초당 걸음 수)를 기준 이속에서 그대로 재현하는 값.</summary>
        public static float StrideOf(float _moveSpeed, float _stepsPerSecond)
        {
            if (_stepsPerSecond <= 0f) return 0f;
            return Mathf.Max(0.05f, _moveSpeed / _stepsPerSecond);
        }
    }
}
