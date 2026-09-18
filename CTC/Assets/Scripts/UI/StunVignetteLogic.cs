using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 스턴 테두리 세기 — 순수 함수(2026-09-19, 기획 hero-sword §4 「본인 화면에는 화면 가장자리 흰 테두리」).
    ///
    /// 칼에 맞으면 조작이 0.8초 막힌다. **아무 표시가 없으면 렉으로 읽힌다** — 그래서 맞은 사람 화면 가장자리가 하얗게 선다.
    /// 남에게는 머리 위 별(<c>FxId.Stunned</c>)과 어지러운 자세가 알린다. 이건 내 화면에만 있다.
    /// </summary>
    public static class StunVignetteLogic
    {
        /// <summary>가장 진할 때의 알파 — 화면을 가리지 않고 가장자리만 선다.</summary>
        public const float PEAK = 0.75f;

        /// <summary>맞는 순간 차오르는 시간 — 「맞았다」가 즉시 읽혀야 한다.</summary>
        public const float RISE_SECONDS = 0.06f;

        /// <summary>풀릴 때 빠지는 시간.</summary>
        public const float FALL_SECONDS = 0.25f;

        /// <summary>굳어 있는 동안의 세기(맞은 지 <paramref name="_sinceHit"/>초).</summary>
        public static float WhileStunned(float _sinceHit)
        {
            if (RISE_SECONDS <= 0f) return PEAK;
            return PEAK * Mathf.Clamp01(_sinceHit / RISE_SECONDS);
        }

        /// <summary>풀린 뒤 <paramref name="_sinceEnd"/>초의 세기 — <paramref name="_from"/>에서 0으로.</summary>
        public static float AfterStun(float _from, float _sinceEnd)
        {
            if (FALL_SECONDS <= 0f) return 0f;
            return _from * Mathf.Clamp01(1f - _sinceEnd / FALL_SECONDS);
        }

        /// <summary>
        /// 테두리 모양 — 화면 가운데(0)에서 가장자리(1)까지의 정규화 거리 → 알파. 가운데 절반은 비우고 바깥으로 갈수록 진하다.
        /// 텍스처를 굽는 쪽(<see cref="StunVignette"/>)이 이 함수로 픽셀을 채운다.
        /// </summary>
        public static float EdgeAlpha(float _radius)
        {
            float t = Mathf.Clamp01((_radius - 0.55f) / 0.45f);
            return t * t * (3f - 2f * t);
        }
    }
}
