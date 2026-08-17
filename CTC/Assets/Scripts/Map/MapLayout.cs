using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 생성기(또는 손작업)가 만든 확정 맵. 씬 굽기·런타임 스폰이 이 에셋을 읽는다.
    /// source = "seed:12345" 또는 "hand-authored".
    /// </summary>
    [CreateAssetMenu(fileName = "MapLayout", menuName = "CTC/Map Layout")]
    public class MapLayout : ScriptableObject
    {
        [SerializeField] private int m_Seed;
        [SerializeField] private string m_Source;
        // 바닥·경계벽의 반경(m). 30×30 맵이면 15 — 맵마다 크기가 다르므로 레이아웃이 들고 있어야 한다.
        [SerializeField] private float m_HalfExtent = 15f;
        [SerializeField] private string m_DisplayName = "";
        // 권장 쥐 수. 맵이 넓을수록 인원이 적으면 고양이가 쥐를 못 찾아 판이 늘어진다 —
        // 로비에서 이 값을 안내하고 현재 인원이 모자라면 경고한다.
        [SerializeField] private int m_RecommendedMiceMin = 2;
        [SerializeField] private int m_RecommendedMiceMax = 3;
        [SerializeField] private List<RoomRect> m_Rooms = new();
        [SerializeField] private List<CorridorSeg> m_Corridors = new();
        [SerializeField] private List<CheesePlacement> m_Cheese = new();
        [SerializeField] private List<FortressPlacement> m_Fortresses = new();
        // 벽감(v3.8) — 맵마다 개수가 다르고 크기·권장 인원과 같은 층의 값이라 CSV가 아니라 여기 있다
        [SerializeField] private List<AlcovePlacement> m_Alcoves = new();
        // 상자(v3.9) — 자리마다 든 물건이 다르다. 그래서 좌표만이 아니라 내용물도 여기 있다.
        [SerializeField] private List<BoxPlacement> m_Boxes = new();
        // 엄폐물(2026-08-11) — 벽감·상자와 같은 취급: 맵마다 자리가 다르다
        [SerializeField] private List<ObstaclePlacement> m_Obstacles = new();

        public int Seed => m_Seed;
        public string Source => m_Source;
        public float HalfExtent => m_HalfExtent;

        /// <summary>로비 맵 선택 UI에 뜨는 이름. 비어 있으면 에셋 이름.</summary>
        public string DisplayName => string.IsNullOrEmpty(m_DisplayName) ? name : m_DisplayName;

        public int RecommendedMiceMin => m_RecommendedMiceMin;
        public int RecommendedMiceMax => m_RecommendedMiceMax;
        public IReadOnlyList<RoomRect> Rooms => m_Rooms;
        public IReadOnlyList<CorridorSeg> Corridors => m_Corridors;
        public IReadOnlyList<CheesePlacement> Cheese => m_Cheese;
        public IReadOnlyList<FortressPlacement> Fortresses => m_Fortresses;
        public IReadOnlyList<AlcovePlacement> Alcoves => m_Alcoves;
        public IReadOnlyList<BoxPlacement> Boxes => m_Boxes;
        public IReadOnlyList<ObstaclePlacement> Obstacles => m_Obstacles;

        /// <summary>
        /// 벽감 자리만 덧쓴다(에디터 전용). 방·복도·치즈·요새는 그대로 둔다 —
        /// 맵을 다시 생성하면 시드가 바뀌어 검증까지 끝난 지형이 통째로 달라지기 때문이다.
        /// </summary>
        public void SetAlcoves(List<AlcovePlacement> _alcoves)
        {
            m_Alcoves = _alcoves ?? new List<AlcovePlacement>();
        }

        /// <summary>엄폐물 자리만 덧쓴다(에디터 전용) — 지형은 그대로 둔다.</summary>
        public void SetObstacles(List<ObstaclePlacement> _obstacles)
        {
            m_Obstacles = _obstacles ?? new List<ObstaclePlacement>();
        }

        /// <summary>상자 자리와 내용물만 덧쓴다(에디터 전용) — 지형은 그대로 둔다.</summary>
        public void SetBoxes(List<BoxPlacement> _boxes)
        {
            m_Boxes = _boxes ?? new List<BoxPlacement>();
        }

        /// <summary>생성기/툴이 결과를 채울 때 사용(에디터 전용 경로).</summary>
        public void Set(int _seed, string _source, List<RoomRect> _rooms, List<CorridorSeg> _corridors,
            List<CheesePlacement> _cheese, List<FortressPlacement> _fortresses,
            float _halfExtent = 15f, string _displayName = "",
            int _recommendedMiceMin = 2, int _recommendedMiceMax = 3,
            List<AlcovePlacement> _alcoves = null)
        {
            m_Alcoves = _alcoves ?? new List<AlcovePlacement>();
            m_Seed = _seed;
            m_Source = _source;
            m_HalfExtent = _halfExtent;
            m_DisplayName = _displayName;
            m_RecommendedMiceMin = _recommendedMiceMin;
            m_RecommendedMiceMax = _recommendedMiceMax;
            m_Rooms = _rooms;
            m_Corridors = _corridors;
            m_Cheese = _cheese;
            m_Fortresses = _fortresses;
        }
    }
}
