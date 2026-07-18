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
        [SerializeField] private List<RoomRect> m_Rooms = new();
        [SerializeField] private List<CorridorSeg> m_Corridors = new();
        [SerializeField] private List<CheesePlacement> m_Cheese = new();
        [SerializeField] private List<FortressPlacement> m_Fortresses = new();

        public int Seed => m_Seed;
        public string Source => m_Source;
        public IReadOnlyList<RoomRect> Rooms => m_Rooms;
        public IReadOnlyList<CorridorSeg> Corridors => m_Corridors;
        public IReadOnlyList<CheesePlacement> Cheese => m_Cheese;
        public IReadOnlyList<FortressPlacement> Fortresses => m_Fortresses;

        /// <summary>생성기/툴이 결과를 채울 때 사용(에디터 전용 경로).</summary>
        public void Set(int seed, string source, List<RoomRect> rooms, List<CorridorSeg> corridors,
            List<CheesePlacement> cheese, List<FortressPlacement> fortresses)
        {
            m_Seed = seed;
            m_Source = source;
            m_Rooms = rooms;
            m_Corridors = corridors;
            m_Cheese = cheese;
            m_Fortresses = fortresses;
        }
    }
}
