using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 미니맵의 나(2026-09-20 사용자 요청) — 위를 향한 삼각형이고 **끝이 빨갛다.** 빨간 끝이 가리키는 곳이
    /// 몸(모델)이 보는 방향이다. 마우스(카메라) 시선은 뒤에 깔리는 <see cref="MinimapViewCone"/>이 따로 보여 준다.
    ///
    /// 스프라이트 없이 정점 색으로 그린다 — 예전의 「▲」 글자는 한 색이라 끝만 칠할 수가 없었다.
    /// 지형 위에서도 모양이 읽히게 어두운 외곽선 삼각형을 먼저 깐다. 회전은 <see cref="MinimapHud"/>가 준다.
    /// </summary>
    public class MinimapMeGlyph : MaskableGraphic
    {
        // 외곽선 두께(px) — 7 px 끝 조각이 밝은 지형 위에서도 떠 보이게
        private const float OUTLINE_PX = 1.6f;

        [SerializeField] private Color m_BodyColor = UiPalette.TextOn;
        [SerializeField] private Color m_TipColor = UiPalette.Heading;
        [SerializeField] private Color m_OutlineColor = new Color(0f, 0f, 0f, 0.8f);

        protected override void OnPopulateMesh(VertexHelper _vh)
        {
            _vh.Clear();
            Rect rect = rectTransform.rect;
            float width = rect.width * 0.8f;
            float height = rect.height;

            MinimapGlyphLogic.Triangle(width, height, out Vector2 tip, out Vector2 left, out Vector2 right);

            // 외곽선 — 무게중심에서 키운 어두운 삼각형
            Vector2 center = (tip + left + right) / 3f;
            float scale = 1f + OUTLINE_PX * 2f / Mathf.Max(1f, height);
            AddTriangle(_vh,
                MinimapGlyphLogic.ScaleAbout(tip, center, scale),
                MinimapGlyphLogic.ScaleAbout(right, center, scale),
                MinimapGlyphLogic.ScaleAbout(left, center, scale),
                m_OutlineColor * color);

            // 몸(밑 사다리꼴) + 끝(빨간 삼각형)
            MinimapGlyphLogic.TipCut(tip, left, right, MinimapGlyphLogic.TIP_FRACTION,
                out Vector2 cutLeft, out Vector2 cutRight);
            AddTriangle(_vh, cutLeft, cutRight, right, m_BodyColor * color);
            AddTriangle(_vh, cutLeft, right, left, m_BodyColor * color);
            AddTriangle(_vh, tip, cutRight, cutLeft, m_TipColor * color);
        }

        private static void AddTriangle(VertexHelper _vh, Vector2 _a, Vector2 _b, Vector2 _c, Color _color)
        {
            int start = _vh.currentVertCount;
            _vh.AddVert(_a, _color, Vector2.zero);
            _vh.AddVert(_b, _color, Vector2.zero);
            _vh.AddVert(_c, _color, Vector2.zero);
            _vh.AddTriangle(start, start + 1, start + 2);
        }
    }
}
