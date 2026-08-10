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

        // 한 판에 채택할 방 수 범위. 맵이 넓어지면 방도 더 써야 빈 공터가 안 생긴다.
        [SerializeField] private int m_MinRooms = 5;
        [SerializeField] private int m_MaxRooms = 6;
        // 바닥·경계벽 반경(m). 30×30이면 15.
        [SerializeField] private float m_HalfExtent = 15f;
        [SerializeField] private string m_DisplayName = "";
        // 권장 쥐 수 — 넓은 맵에 인원이 적으면 고양이가 못 찾는다. 굽는 레이아웃에 그대로 새긴다.
        [SerializeField] private int m_RecommendedMiceMin = 2;
        [SerializeField] private int m_RecommendedMiceMax = 3;
        // 등급 1~7을 몇 벌 깔 것인가. 넓은 맵은 지점이 더 많아야 고갈이 판 안에 온다(2026-08-10).
        [SerializeField] private int m_CheeseSets = 1;
        // 요새 상한 — 쥐가 많으면 스폰 자리를 공유하게 되므로 함께 올린다.
        [SerializeField] private int m_MaxFortresses = 6;

        public int MinRooms => m_MinRooms;
        public int MaxRooms => m_MaxRooms;
        public int CheeseSets => Mathf.Max(1, m_CheeseSets);
        public int MaxFortresses => Mathf.Max(1, m_MaxFortresses);
        public float HalfExtent => m_HalfExtent;
        public string DisplayName => m_DisplayName;
        public int RecommendedMiceMin => m_RecommendedMiceMin;
        public int RecommendedMiceMax => m_RecommendedMiceMax;

        public IReadOnlyList<RoomCandidate> Rooms => m_Rooms;
        public IReadOnlyList<CorridorCandidate> Corridors => m_Corridors;
        public IReadOnlyList<MazePreset> Mazes => m_Mazes;
        public IReadOnlyList<CheeseSlot> CheeseSlots => m_CheeseSlots;
        public IReadOnlyList<FortressSlot> FortressSlots => m_FortressSlots;

        public void Set(List<RoomCandidate> _rooms, List<CorridorCandidate> _corridors, List<MazePreset> _mazes,
            List<CheeseSlot> _cheese, List<FortressSlot> _fortress)
        {
            m_Rooms = _rooms; m_Corridors = _corridors; m_Mazes = _mazes;
            m_CheeseSlots = _cheese; m_FortressSlots = _fortress;
        }

        /// <summary>확장 풀 생성 툴이 채우는 값 — 방 수 범위·캔버스 크기·표시 이름·권장 인원.</summary>
        public void SetShape(int _minRooms, int _maxRooms, float _halfExtent, string _displayName,
            int _recommendedMiceMin, int _recommendedMiceMax, int _cheeseSets = 1, int _maxFortresses = 6)
        {
            m_MinRooms = _minRooms;
            m_MaxRooms = _maxRooms;
            m_HalfExtent = _halfExtent;
            m_DisplayName = _displayName;
            m_RecommendedMiceMin = _recommendedMiceMin;
            m_RecommendedMiceMax = _recommendedMiceMax;
            m_CheeseSets = _cheeseSets;
            m_MaxFortresses = _maxFortresses;
        }
    }
}
