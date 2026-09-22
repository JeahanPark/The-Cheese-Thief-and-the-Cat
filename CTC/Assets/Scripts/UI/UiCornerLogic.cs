using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// UI 둥근 모서리 판정(2026-09-23 사용자 요청 「UI 라운드지게」 — 매력 백로그 §2). 순수 로직, EditMode가 덮는다.
    ///
    /// 색은 <see cref="UiPalette"/> 한 곳에서 주듯 **모양도 한 곳에서 준다** — 생성기마다 손대지 않고
    /// <see cref="UiCorners"/>가 씬이 뜰 때 「색만 칠한 사각형」에 둥근 9-slice를 씌운다.
    /// 어느 칸을 둥글릴지·얼마나 둥글릴지를 여기서 정한다.
    /// </summary>
    public static class UiCornerLogic
    {
        /// <summary>둥근 사각형 스프라이트의 모서리 반지름(= 9-slice 테두리, 텍스처 px).</summary>
        public const float SPRITE_RADIUS_PX = 24f;

        /// <summary>이보다 얇은 칸(구분선·위쪽 노란 줄·점)은 각진 채로 둔다 — 둥글리면 알약이 된다.</summary>
        public const float MIN_SIDE_PX = 14f;

        /// <summary>캔버스의 이 비율 이상을 덮는 칸(뒤 베일·암전)은 둥글리지 않는다 — 화면 모서리가 비친다.</summary>
        public const float FULLSCREEN_FRACTION = 0.9f;

        public const float CORNER_SMALL_PX = 5f;    // 짧은 변 40px 미만 — 슬롯 점·작은 배지
        public const float CORNER_MEDIUM_PX = 8f;   // 120px 미만 — 버튼·슬롯
        public const float CORNER_LARGE_PX = 12f;   // 그 이상 — 창·패널

        /// <summary>
        /// 이 Image를 둥글릴까. 스프라이트가 이미 있는 것(아이콘·그림)과 Simple이 아닌 것(Filled 게이지 등)은
        /// 그 모양이 의도라 안 건드린다. 투명한 칸(클릭만 받는 판)도 둥글려 봐야 안 보인다.
        /// </summary>
        public static bool ShouldRound(bool _hasSprite, bool _isSimple, float _alpha,
            float _width, float _height, float _canvasWidth, float _canvasHeight)
        {
            if (_hasSprite || !_isSimple) return false;
            if (_alpha < 0.01f) return false;
            if (Mathf.Min(_width, _height) < MIN_SIDE_PX) return false;
            bool fullscreen = _canvasWidth > 0f && _canvasHeight > 0f
                && _width >= _canvasWidth * FULLSCREEN_FRACTION
                && _height >= _canvasHeight * FULLSCREEN_FRACTION;
            return !fullscreen;
        }

        /// <summary>칸 크기에 맞는 모서리 반지름(px). 짧은 변의 절반을 넘지 않는다.</summary>
        public static float CornerPx(float _width, float _height)
        {
            float side = Mathf.Min(_width, _height);
            float corner = side < 40f ? CORNER_SMALL_PX : side < 120f ? CORNER_MEDIUM_PX : CORNER_LARGE_PX;
            return Mathf.Min(corner, side * 0.5f);
        }

        /// <summary>
        /// 원하는 반지름을 내는 <c>Image.pixelsPerUnitMultiplier</c>. 스프라이트 PPU = 캔버스 기준 PPU(100)일 때
        /// 화면의 모서리 = 테두리 px ÷ 배수이므로 배수 = 테두리 ÷ 원하는 반지름.
        /// </summary>
        public static float PixelsPerUnitMultiplier(float _cornerPx)
        {
            return _cornerPx <= 0f ? 1f : SPRITE_RADIUS_PX / _cornerPx;
        }
    }
}
