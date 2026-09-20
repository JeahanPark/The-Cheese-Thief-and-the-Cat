using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 미니맵 글리프의 모양(순수, 2026-09-20) — 내 삼각형(끝이 몸 방향)과 시선 부채꼴.
    /// 둘 다 **위(+Y)를 향한 모양**으로 만들고, 회전은 <see cref="MinimapHud"/>가
    /// <see cref="MinimapLogic.YawToUiAngle"/>로 준다. 모양만 여기서 재서 EditMode로 잰다.
    /// </summary>
    public static class MinimapGlyphLogic
    {
        /// <summary>삼각형 끝 조각(빨강)의 몫 — 끝에서 높이의 이만큼이 끝 조각이다.</summary>
        public const float TIP_FRACTION = 0.42f;

        /// <summary>위를 향한 이등변 삼각형 — 가운데가 원점, 폭 <paramref name="_width"/> · 높이 <paramref name="_height"/>.</summary>
        public static void Triangle(float _width, float _height, out Vector2 _tip, out Vector2 _left, out Vector2 _right)
        {
            _tip = new Vector2(0f, _height * 0.5f);
            _left = new Vector2(-_width * 0.5f, -_height * 0.5f);
            _right = new Vector2(_width * 0.5f, -_height * 0.5f);
        }

        /// <summary>끝 조각을 자르는 두 점 — 끝에서 두 밑 꼭짓점 쪽으로 <paramref name="_fraction"/>만큼 내려간 자리.</summary>
        public static void TipCut(Vector2 _tip, Vector2 _left, Vector2 _right, float _fraction,
            out Vector2 _cutLeft, out Vector2 _cutRight)
        {
            float t = Mathf.Clamp01(_fraction);
            _cutLeft = Vector2.Lerp(_tip, _left, t);
            _cutRight = Vector2.Lerp(_tip, _right, t);
        }

        /// <summary>점 하나를 중심에서 <paramref name="_scale"/>배로 민다 — 외곽선용 큰 삼각형.</summary>
        public static Vector2 ScaleAbout(Vector2 _point, Vector2 _center, float _scale)
        {
            return _center + (_point - _center) * _scale;
        }

        /// <summary>
        /// 부채꼴 테두리의 <paramref name="_index"/>번째 점(0..<paramref name="_segments"/>) —
        /// 위(+Y)를 가운데로 좌우 <paramref name="_angleDeg"/>/2씩 벌어진다. 0번이 왼쪽 끝, 마지막이 오른쪽 끝.
        /// </summary>
        public static Vector2 FanPoint(int _index, int _segments, float _angleDeg, float _radius)
        {
            float t = _segments > 0 ? Mathf.Clamp01((float)_index / _segments) : 0.5f;
            float rad = (-_angleDeg * 0.5f + _angleDeg * t) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * _radius;
        }
    }
}
