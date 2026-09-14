namespace CTC.Runtime.Player
{
    /// <summary>
    /// 깡패 변신(<c>GangTransform</c> 클립) 시간표 — 순수 함수(2026-09-14 사용자 요청).
    /// 원본 클립: <c>tools/blender/clips/gang_transform.py</c>. 초 = (블렌더 프레임 - 1) / 24.
    ///
    /// 런타임(<see cref="GangLookVisual"/>)과 GIF 미리보기(<c>ClipPreviewRender</c>)가 **같은 함수**를 본다 —
    /// 둘이 갈리면 미리보기가 거짓말을 한다(<see cref="MouseGunVisualLogic"/>와 같은 원칙).
    /// </summary>
    public static class GangTransformLogic
    {
        private const float FPS = 24f;

        /// <summary>클립 길이 — 48프레임(블렌더 1~48).</summary>
        public const float SECONDS = 47f / FPS;

        /// <summary>서기 자리에 내려앉는 프레임(12) — 여기서 눈썹이 켜진다. 벌떡 일어선 순간 표정이 바뀐다.</summary>
        public const float BROWS_AT = 11f / FPS;

        /// <summary>오른 앞발이 뒷주머니에 닿는 프레임(17) — 몽둥이가 나타나기 시작한다.</summary>
        public const float CLUB_AT = 16f / FPS;

        /// <summary>숙인 머리와 치켜든 왼 앞발이 만나는 프레임(37) — 모자가 나타나기 시작한다.</summary>
        public const float HAT_AT = 36f / FPS;

        /// <summary>
        /// 소품이 0→1로 차오르는 시간. 총 뽑기(0.10초)보다 조금 느리다 — 「꺼냈다」가 읽혀야 하고,
        /// 앞발이 주머니에 머무는 구간(17~19프레임 ≈ 0.08초)을 조금 넘겨 손을 떠나며 또렷해진다.
        /// </summary>
        public const float FADE_SECONDS = 0.15f;

        /// <summary>변신 시작 후 <paramref name="_elapsed"/>초에 <paramref name="_appearAt"/>에 나타나는 소품의 알파(0~1).</summary>
        public static float PropAlpha(float _elapsed, float _appearAt)
            => GunFadeLogic.Alpha(_elapsed - _appearAt, FADE_SECONDS, true);

        /// <summary>몽둥이 알파.</summary>
        public static float ClubAlpha(float _elapsed) => PropAlpha(_elapsed, CLUB_AT);

        /// <summary>모자 알파.</summary>
        public static float HatAlpha(float _elapsed) => PropAlpha(_elapsed, HAT_AT);

        /// <summary>눈썹·화난 눈이 켜져 있어야 하나.</summary>
        public static bool BrowsOn(float _elapsed) => _elapsed >= BROWS_AT;

        /// <summary>변신이 끝났나 — 끝나면 모든 소품이 또렷하다.</summary>
        public static bool Done(float _elapsed) => _elapsed >= SECONDS;
    }
}
