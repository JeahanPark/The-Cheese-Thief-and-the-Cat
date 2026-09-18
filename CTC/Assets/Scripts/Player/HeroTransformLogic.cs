using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 용사 변신(<c>HeroDraw</c> 클립) 시간표 — 순수 함수(2026-09-19, 기획 `2026-09-19-hero-sword-design.md` §5-1).
    ///
    /// 표를 밟는 순간 1.5초 — 몸이 잠기고, 오른 앞발이 엉덩이로 가 **칼을 뽑아**(8프레임에 나타난다) 옆으로 크게 휘둘러
    /// **하늘을 찌른 채 버티고**(14~26), 내리며 거둔다(31프레임에 사라진다). 칼은 쓸 때만 보인다(<see cref="HeroSwordVisualLogic"/>).
    /// 키는 블렌더 클립 원본(<c>tools/blender/clips/hero_draw.py</c>의 <c>SWORD</c>·<c>SHOW</c>)과 **같은 숫자**다.
    /// 런타임(<see cref="HeroLook"/>)과 GIF 미리보기가 같은 시간표를 본다.
    /// </summary>
    public static class HeroTransformLogic
    {
        /// <summary>연출 전체 길이(37프레임) — 이 동안 행동이 잠긴다.</summary>
        public const float SECONDS = 36f / HeroSwordVisualLogic.FPS;

        /// <summary>엉덩이에서 칼이 나타나는 순간(8프레임).</summary>
        public const float SHOW_AT = 7f / HeroSwordVisualLogic.FPS;

        /// <summary>칼이 사라지기 시작하는 순간(30프레임) — 31프레임에 0. 잠금이 풀리기 전에 손은 비어 있다.</summary>
        public const float HIDE_AT = 29f / HeroSwordVisualLogic.FPS;

        // hero_draw.py SWORD — (프레임, yaw, pitch). 엉덩이 뒤(160) → 옆 → 하늘(88~96)에서 버팀 → 앞으로 눕히며 거둔다.
        private static readonly float[] AIM =
        {
            1, 160f, -5f,   8, 160f, -5f,   10, 120f, 8f,   12, 70f, 45f,   14, 25f, 88f,   18, 15f, 96f,
            22, 18f, 92f,   26, 15f, 94f,   29, 12f, 40f,   31, 8f, 10f,    37, 0f, 0f,
        };

        /// <summary>변신 시작 후 <paramref name="_elapsed"/>초의 칼 알파(0~1).</summary>
        public static float SwordAlpha(float _elapsed)
            => HeroSwordVisualLogic.Alpha(_elapsed, SHOW_AT, HIDE_AT,
                HeroSwordVisualLogic.FADE_SECONDS, HeroSwordVisualLogic.FADE_SECONDS);

        /// <summary>변신 시작 후 <paramref name="_elapsed"/>초의 칼 방향 (yaw, pitch).</summary>
        public static Vector2 SwordAim(float _elapsed) => HeroSwordVisualLogic.Sample(AIM, _elapsed);

        /// <summary>아직 잠겨 있나 — 끝나는 순간 풀린다.</summary>
        public static bool Locked(float _elapsed) => _elapsed >= 0f && _elapsed < SECONDS;

        /// <summary>끝났나.</summary>
        public static bool Done(float _elapsed) => _elapsed >= SECONDS;
    }
}
