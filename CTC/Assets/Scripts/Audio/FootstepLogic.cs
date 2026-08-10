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
    }
}
