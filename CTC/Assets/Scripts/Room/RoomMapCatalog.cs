using CTC.Runtime.Map;
using UnityEngine;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 대기실이 고를 수 있는 맵 목록(Worker, 규칙 6) — **이름만 안다.**
    ///
    /// 인게임의 <see cref="MapDirector"/>와 일부러 나눴다. 그쪽은 레이아웃을 받아 지오메트리를
    /// 굽고 폴리곤을 잡는 물건이라 대기실에 들이면 대기실이 맵을 짓기 시작한다. 여기서 정하는 건
    /// **번호 하나**뿐이고, 그 번호는 <see cref="Core.MatchHandoff"/>를 타고 인게임으로 건너간다.
    ///
    /// 배열 순서가 곧 맵 인덱스다 — `SetupNetworkingScene.MapLayoutPaths`와 **같은 순서**여야 한다.
    /// 어긋나면 대기실에서 고른 맵과 인게임에 서는 맵이 달라진다.
    /// </summary>
    public class RoomMapCatalog : MonoBehaviour
    {
        [SerializeField] private MapLayout[] m_Layouts;

        public int Count => m_Layouts != null ? m_Layouts.Length : 0;

        public string NameAt(int _index)
        {
            MapLayout layout = LayoutAt(_index);
            return layout != null ? layout.DisplayName : "";
        }

        /// <summary>버튼 아래 한 줄 — 크기와 권장 인원. 고르는 데 필요한 건 이 둘뿐이다.</summary>
        public string BriefAt(int _index)
        {
            MapLayout layout = LayoutAt(_index);
            if (layout == null) return "";

            int size = Mathf.RoundToInt(layout.HalfExtent * 2f);
            return $"{size}×{size}m · 쥐 {layout.RecommendedMiceMin}~{layout.RecommendedMiceMax}";
        }

        /// <summary>버튼에는 이름만 — 괄호 안 크기는 뗀다(인게임 로비와 같은 규칙).</summary>
        public string ShortNameAt(int _index)
        {
            string full = NameAt(_index);
            if (string.IsNullOrEmpty(full)) return full;

            int paren = full.IndexOf('(');
            return paren > 0 ? full.Substring(0, paren).TrimEnd() : full;
        }

        /// <summary>이 맵을 지금 빌드에서 고를 수 있나 — 정식(대형~)이거나 개발 빌드(2026-08-31).</summary>
        public bool IsSelectableAt(int _index)
        {
            MapLayout layout = LayoutAt(_index);
            return layout != null && MapCatalogLogic.IsSelectable(layout.HalfExtent, Debug.isDebugBuild);
        }

        private MapLayout LayoutAt(int _index)
        {
            if (m_Layouts == null || m_Layouts.Length == 0) return null;
            if (_index < 0 || _index >= m_Layouts.Length) return null;
            return m_Layouts[_index];
        }
    }
}
