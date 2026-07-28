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
        [SerializeField] private List<RoomRect> m_Rooms = new();
        [SerializeField] private List<CorridorSeg> m_Corridors = new();
        [SerializeField] private List<CheesePlacement> m_Cheese = new();
        [SerializeField] private List<FortressPlacement> m_Fortresses = new();

        public int Seed => m_Seed;
        public string Source => m_Source;
        public float HalfExtent => m_HalfExtent;

        /// <summary>로비 맵 선택 UI에 뜨는 이름. 비어 있으면 에셋 이름.</summary>
        public string DisplayName => string.IsNullOrEmpty(m_DisplayName) ? name : m_DisplayName;
        public IReadOnlyList<RoomRect> Rooms => m_Rooms;
        public IReadOnlyList<CorridorSeg> Corridors => m_Corridors;
        public IReadOnlyList<CheesePlacement> Cheese => m_Cheese;
        public IReadOnlyList<FortressPlacement> Fortresses => m_Fortresses;

        /// <summary>생성기/툴이 결과를 채울 때 사용(에디터 전용 경로).</summary>
        public void Set(int seed, string source, List<RoomRect> rooms, List<CorridorSeg> corridors,
            List<CheesePlacement> cheese, List<FortressPlacement> fortresses,
            float halfExtent = 15f, string displayName = "")
        {
            m_Seed = seed;
            m_Source = source;
            m_HalfExtent = halfExtent;
            m_DisplayName = displayName;
            m_Rooms = rooms;
            m_Corridors = corridors;
            m_Cheese = cheese;
            m_Fortresses = fortresses;
        }
    }
}
