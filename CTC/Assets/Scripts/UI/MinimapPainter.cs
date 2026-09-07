using CTC.Runtime.Map;
using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 지형 텍스처를 <b>판 시작에 한 장</b> 굽는다(2026-09-08) — 순수 함수.
    ///
    /// 실시간 오소 카메라 → RenderTexture를 버린 이유가 이것이다. URP 렌더 패스가 매 프레임
    /// 하나 늘면 에디터 3인 MPPM이 더 무거워진다. 지형은 판 중에 안 변하니 한 번만 그리면 된다.
    /// 에디터에서 미리 구운 그림도 버렸다 — 격자 맵(Large01)은 시드로 지어져 못 굽는다.
    ///
    /// <see cref="MapLayout"/>은 두 종류를 다 든다. **손 배치 창고 맵은 Rooms·Corridors가 비고
    /// Props만 77개, 격자 맵은 반대다.** 둘 다 사각형이라 같은 코드가 지난다 — 분기하지 않는다.
    ///
    /// 색은 <see cref="UiPalette"/> 단독 출처다. 여기서 새 색을 만들지 않는다.
    /// </summary>
    public static class MinimapPainter
    {
        /// <summary>
        /// 지형을 칠한 텍스처. 바닥(Slot) → 경계(Groove) → 방·복도 바닥(Groove, 바닥보다 한 톤 밝다)
        /// → 지형(Line) 순서로 덮는다.
        /// <paramref name="_layout"/>이 null이면 바닥만 칠한 텍스처를 준다(배선이 늦어도 안 죽는다).
        /// </summary>
        public static Texture2D Paint(MapLayout _layout, int _px)
        {
            int px = Mathf.Max(_px, 1);
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,   // 작게 뜨는 평면도라 흐리면 선반이 뭉갠다
                wrapMode = TextureWrapMode.Clamp
            };

            // SetPixel 루프는 픽셀마다 네이티브를 넘나든다 — 배열에 다 쓰고 한 번에 올린다
            var pixels = new Color[px * px];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = UiPalette.Slot;

            if (_layout != null)
            {
                float half = Mathf.Max(_layout.HalfExtent, 1e-4f);
                PaintBorder(pixels, px);

                // 방·복도는 「걸어 다니는 바닥」이라 지형이 아니다. 바닥보다 한 톤 밝은
                // Groove로 깔아 두면 격자 맵에서 통로가 읽힌다(손 배치 맵은 여기가 비어 있다).
                foreach (RoomRect room in _layout.Rooms)
                {
                    Fill(pixels, px, MinimapLogic.RectToPixels(room.Center, room.Size, half, px, MinimapLogic.MIN_THICKNESS),
                        UiPalette.Groove);
                }

                foreach (CorridorSeg seg in _layout.Corridors)
                {
                    // 복도는 중심선 + 폭이다. 축정렬이라 중심·크기로 그대로 환산된다.
                    Vector2 center = (seg.From + seg.To) * 0.5f;
                    var size = new Vector2(
                        Mathf.Abs(seg.To.x - seg.From.x) + seg.Width,
                        Mathf.Abs(seg.To.y - seg.From.y) + seg.Width);
                    Fill(pixels, px, MinimapLogic.RectToPixels(center, size, half, px, MinimapLogic.MIN_THICKNESS),
                        UiPalette.Groove);
                }

                foreach (PropPlacement prop in _layout.Props)
                {
                    Fill(pixels, px, MinimapLogic.RectToPixels(prop.Pos, AxisAlignedSize(prop.Size, prop.YawDeg), half, px,
                        MinimapLogic.MIN_THICKNESS), UiPalette.Line);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false);
            return tex;
        }

        /// <summary>
        /// 돌아간 사각형은 **축정렬 바운딩 박스로 근사**한다. 지금 Props의 YawDeg는 전부 0이지만
        /// 0이 아닌 값이 들어와도 선반이 사라지지 않게 한다 — 200 px 위젯에서 몇 px 넉넉한 건
        /// 안 보이고, 안 그려지는 건 보인다.
        /// </summary>
        private static Vector2 AxisAlignedSize(Vector2 _size, float _yawDeg)
        {
            if (Mathf.Approximately(_yawDeg % 180f, 0f)) return _size;
            float rad = _yawDeg * Mathf.Deg2Rad;
            float c = Mathf.Abs(Mathf.Cos(rad));
            float s = Mathf.Abs(Mathf.Sin(rad));
            return new Vector2(
                Mathf.Abs(_size.x) * c + Mathf.Abs(_size.y) * s,
                Mathf.Abs(_size.x) * s + Mathf.Abs(_size.y) * c);
        }

        /// <summary>맵 경계 테두리 — 「여기가 창고 끝」을 한눈에 준다.</summary>
        private static void PaintBorder(Color[] _pixels, int _px)
        {
            int t = Mathf.Min(MinimapLogic.MIN_THICKNESS, _px);
            Fill(_pixels, _px, new RectInt(0, 0, _px, t), UiPalette.Groove);
            Fill(_pixels, _px, new RectInt(0, _px - t, _px, t), UiPalette.Groove);
            Fill(_pixels, _px, new RectInt(0, 0, t, _px), UiPalette.Groove);
            Fill(_pixels, _px, new RectInt(_px - t, 0, t, _px), UiPalette.Groove);
        }

        private static void Fill(Color[] _pixels, int _px, RectInt _rect, Color _color)
        {
            if (_rect.width <= 0 || _rect.height <= 0) return;
            for (int y = _rect.yMin; y < _rect.yMax; y++)
            {
                int row = y * _px;
                for (int x = _rect.xMin; x < _rect.xMax; x++) _pixels[row + x] = _color;
            }
        }
    }
}
