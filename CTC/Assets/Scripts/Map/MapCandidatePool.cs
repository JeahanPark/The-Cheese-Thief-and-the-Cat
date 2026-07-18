using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 손검수한 후보 슬롯 모음(level-design 명세를 데이터화). 생성기가 시드로 뽑아 MapLayout을 만든다.
    /// 좌표를 바꾸면 MapValidator 임계값(요새-치즈 이격 등)도 함께 검토.
    /// </summary>
    [CreateAssetMenu(fileName = "MapCandidatePool", menuName = "CTC/Map Candidate Pool")]
    public class MapCandidatePool : ScriptableObject
    {
        [System.Serializable]
        public struct RoomCandidate
        {
            public string Id;
            public Vector2 Center;
            public Vector2 Size;
            public RoomTag Tag;
            public bool AlwaysInclude;   // R3·R5
        }

        [System.Serializable]
        public struct CorridorCandidate
        {
            public string Id;
            public string RoomA;
            public string RoomB;
            public List<Vector2> Waypoints;   // 중심선 절점 (>=2)
            public float Width;
        }

        [System.Serializable]
        public struct CheeseSlot
        {
            public string RoomId;
            public Vector2 Pos;
            public int MinGrade;   // 밴드 하한
            public int MaxGrade;   // 밴드 상한
            public bool Fixed;     // G1 고정
            public int FixedGrade; // Fixed일 때 등급
            public float Radius;
        }

        [System.Serializable]
        public struct FortressSlot
        {
            public string RoomId;
            public Vector2 Pos;
            public float YawDeg;
            public bool AlwaysInclude;   // F0
        }

        public enum RoomTag { Outer, Middle, Center }

        [SerializeField] private List<RoomCandidate> m_Rooms = new();
        [SerializeField] private List<CorridorCandidate> m_Corridors = new();
        [SerializeField] private List<MazePreset> m_Mazes = new();
        [SerializeField] private List<CheeseSlot> m_CheeseSlots = new();
        [SerializeField] private List<FortressSlot> m_FortressSlots = new();

        public IReadOnlyList<RoomCandidate> Rooms => m_Rooms;
        public IReadOnlyList<CorridorCandidate> Corridors => m_Corridors;
        public IReadOnlyList<MazePreset> Mazes => m_Mazes;
        public IReadOnlyList<CheeseSlot> CheeseSlots => m_CheeseSlots;
        public IReadOnlyList<FortressSlot> FortressSlots => m_FortressSlots;

        public void Set(List<RoomCandidate> rooms, List<CorridorCandidate> corridors, List<MazePreset> mazes,
            List<CheeseSlot> cheese, List<FortressSlot> fortress)
        {
            m_Rooms = rooms; m_Corridors = corridors; m_Mazes = mazes;
            m_CheeseSlots = cheese; m_FortressSlots = fortress;
        }
    }
}
