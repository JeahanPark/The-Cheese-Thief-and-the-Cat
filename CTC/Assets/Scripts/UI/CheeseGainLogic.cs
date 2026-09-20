using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 재화를 얻었을 때 뜨는 「+N」의 판정(순수, 2026-09-20 사용자 선택 A안) — 언제 합치고 언제 사라지나.
    ///
    /// **연달아 얻으면 합친다.** 치즈는 채굴 틱처럼 잘게 여러 번 들어와서, 한 번에 하나씩 띄우면
    /// 숫자가 깜빡이기만 하고 얼마를 벌었는지 안 읽힌다. 합치는 창(<see cref="MERGE_SECONDS"/>) 안에
    /// 또 들어오면 더해서 키우고, 창이 지나면 새 숫자로 시작한다.
    /// </summary>
    public static class CheeseGainLogic
    {
        /// <summary>이 시간 안에 또 벌면 합친다(초).</summary>
        public const float MERGE_SECONDS = 1.2f;

        /// <summary>합쳐진 숫자가 그대로 떠 있는 시간(초). 이 뒤로 <see cref="FADE_SECONDS"/> 동안 사라진다.</summary>
        public const float HOLD_SECONDS = 0.9f;

        /// <summary>사라지는 시간(초).</summary>
        public const float FADE_SECONDS = 0.6f;

        /// <summary>새로 뜨는 순간의 크기 배율 — 1로 줄며 톡 튀어 보인다.</summary>
        public const float POP_SCALE = 1.35f;

        /// <summary>튀어오른 크기가 1로 돌아오는 시간(초).</summary>
        public const float POP_SECONDS = 0.18f;

        /// <summary>
        /// 이번에 벌어들인 양을 더한 뒤 화면에 띄울 숫자 — 합치는 창을 지났으면 <paramref name="_gain"/>부터 새로 센다.
        /// </summary>
        public static int Merge(int _shown, int _gain, float _sinceLastGain)
        {
            int gain = Mathf.Max(0, _gain);
            if (gain == 0) return _shown;
            return _sinceLastGain <= MERGE_SECONDS ? Mathf.Max(0, _shown) + gain : gain;
        }

        /// <summary>떠 있는 정도(0~1) — 버티는 동안 1, 그 뒤 사라진다.</summary>
        public static float Alpha(float _sinceLastGain)
        {
            if (_sinceLastGain <= HOLD_SECONDS) return 1f;
            float t = (_sinceLastGain - HOLD_SECONDS) / Mathf.Max(0.0001f, FADE_SECONDS);
            return Mathf.Clamp01(1f - t);
        }

        /// <summary>글자 크기 배율 — 뜨는 순간 <see cref="POP_SCALE"/>에서 1로 줄어든다.</summary>
        public static float Scale(float _sinceLastGain)
        {
            float t = Mathf.Clamp01(_sinceLastGain / Mathf.Max(0.0001f, POP_SECONDS));
            return Mathf.Lerp(POP_SCALE, 1f, t);
        }

        /// <summary>화면에 쓰는 글 — 0 이하면 빈 글자(아무것도 안 뜬다).</summary>
        public static string Text(int _shown)
        {
            return _shown > 0 ? $"+{_shown}" : "";
        }
    }
}
