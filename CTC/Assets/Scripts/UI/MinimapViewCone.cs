using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 미니맵 시선 부채꼴(2026-09-20 사용자 요청) — 마우스(카메라)가 보는 쪽. 내 삼각형 **뒤에** 깔린다.
    /// 삼각형 끝(몸 방향)과 따로 돈다 — 뒷걸음질·옆 보기처럼 몸과 시선이 갈리는 순간이 지도에 그대로 보인다.
    ///
    /// 가운데는 진하고 끝으로 갈수록 투명해진다(정점 색 그라데이션, 스프라이트 없음). 모양은 위(+Y)를 향하고
    /// 회전은 <see cref="MinimapHud"/>가 카메라 방향으로 준다. 반경은 RectTransform 크기의 절반이다.
    /// </summary>
    public class MinimapViewCone : MaskableGraphic
    {
        // 부채꼴을 나누는 조각 수 — 36 px 반경에서 모서리가 안 보일 만큼
        private const int SEGMENTS = 16;

        [SerializeField] private float m_AngleDeg = 70f;
        [SerializeField] private Color m_Tint = UiPalette.WithAlpha(UiPalette.AccentSoft, 0.5f);

        protected override void OnPopulateMesh(VertexHelper _vh)
        {
            _vh.Clear();
            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;

            Color inner = m_Tint * color;
            Color outer = inner;
            outer.a = 0f;

            _vh.AddVert(Vector3.zero, inner, Vector2.zero);
            for (int i = 0; i <= SEGMENTS; i++)
                _vh.AddVert(MinimapGlyphLogic.FanPoint(i, SEGMENTS, m_AngleDeg, radius), outer, Vector2.zero);
            for (int i = 0; i < SEGMENTS; i++)
                _vh.AddTriangle(0, i + 1, i + 2);
        }
    }
}
