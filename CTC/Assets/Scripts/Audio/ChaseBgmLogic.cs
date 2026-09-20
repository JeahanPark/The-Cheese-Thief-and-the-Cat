using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 고양이 접근 추격곡의 판정(순수, 2026-09-20) — 언제 켜고 언제 끄고 얼마나 섞나.
    /// 거리는 CSV(`ChaseBgmMeters`·`ChaseBgmSneakMeters`), 버티는 시간·페이드는 연출 상수라 코드에 둔다.
    /// </summary>
    public static class ChaseBgmLogic
    {
        /// <summary>고양이가 반경을 벗어난 뒤 추격곡을 이만큼 더 버틴다 — 경계에서 곡이 깜빡이지 않게.</summary>
        public const float HOLD_SECONDS = 3f;

        /// <summary>추격곡으로 넘어가는 시간 — 빨라야 「왔다!」가 산다.</summary>
        public const float FADE_IN_SECONDS = 0.8f;

        /// <summary>인게임 곡으로 돌아가는 시간 — 천천히 가라앉혀야 긴장이 풀리는 게 들린다.</summary>
        public const float FADE_OUT_SECONDS = 2.5f;

        /// <summary>
        /// 이 고양이가 곡을 바꾸는 반경(m) — 살금살금 고양이는 더 가까이 와야 걸린다(사용자 결정: 절반, CSV가 원본).
        /// </summary>
        public static float TriggerMeters(bool _catSneaking, float _normalMeters, float _sneakMeters)
        {
            return Mathf.Max(0f, _catSneaking ? _sneakMeters : _normalMeters);
        }

        /// <summary>바닥 평면 거리(m) — 높이는 안 본다(선반 위 고양이도 같은 방에 있는 고양이다).</summary>
        public static float FlatDistance(Vector3 _a, Vector3 _b)
        {
            Vector2 d = new Vector2(_a.x - _b.x, _a.z - _b.z);
            return d.magnitude;
        }

        /// <summary>
        /// 추격 상태 한 걸음 — 반경 안이면 바로 켜고, 벗어나면 <see cref="HOLD_SECONDS"/>를 버틴 뒤 끈다.
        /// <paramref name="_outOfRangeSeconds"/>는 마지막으로 반경 안이었던 뒤로 흐른 시간이다.
        /// </summary>
        public static bool NextChasing(bool _chasing, bool _inRange, float _outOfRangeSeconds)
        {
            if (_inRange) return true;
            return _chasing && _outOfRangeSeconds < HOLD_SECONDS;
        }

        /// <summary>추격곡을 섞는 비율(0~1)을 목표로 민다 — 켤 땐 빨리, 끌 땐 천천히.</summary>
        public static float StepWeight(float _weight, bool _chasing, float _deltaSeconds)
        {
            float target = _chasing ? 1f : 0f;
            float seconds = _chasing ? FADE_IN_SECONDS : FADE_OUT_SECONDS;
            return Mathf.MoveTowards(Mathf.Clamp01(_weight), target, Mathf.Max(0f, _deltaSeconds) / seconds);
        }
    }
}
