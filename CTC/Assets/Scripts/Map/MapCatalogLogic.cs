namespace CTC.Runtime.Map
{
    /// <summary>
    /// 어떤 맵이 정식인가의 순수 판정(2026-08-31 결정) — 대형(HalfExtent 60, 120×120)부터 정식.
    /// 작은 맵은 개발 빌드에서만 고를 수 있다. 레이아웃 배열 자체는 안 건드린다 —
    /// 인덱스가 MatchHandoff를 타고 씬을 넘어가므로 순서가 흔들리면 안 된다.
    ///
    /// 2026-09-07: 크기는 정식이어도 **개발 전용으로 강등**된 맵(격자 대형)이 생겨 `devOnly`가 붙었다.
    /// </summary>
    public static class MapCatalogLogic
    {
        public const float MIN_OFFICIAL_HALF_EXTENT = 60f;

        public static bool IsOfficial(float _halfExtent) => _halfExtent >= MIN_OFFICIAL_HALF_EXTENT;

        public static bool IsSelectable(float _halfExtent, bool _devOnly, bool _devBuild)
            => _devBuild || (IsOfficial(_halfExtent) && !_devOnly);

        /// <summary>첫 정식(개발 전용 아님) 맵의 인덱스. 하나도 없으면 0(개발 상황 보호).</summary>
        public static int DefaultIndex(float[] _halfExtents, bool[] _devOnly)
        {
            if (_halfExtents == null) return 0;
            for (int i = 0; i < _halfExtents.Length; i++)
            {
                bool devOnly = _devOnly != null && i < _devOnly.Length && _devOnly[i];
                if (IsOfficial(_halfExtents[i]) && !devOnly) return i;
            }
            return 0;
        }
    }
}
