using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 생성 결과의 하드 게이트 검사(후보풀 명세 §6-A V1~V12). 전부 순수 함수 — EditMode 테스트.
    /// 실패한 시드는 생성기가 폐기하고 다음 시드로 재시도한다.
    /// </summary>
    public static class MapValidator
    {
        public const float MIN_CORRIDOR_WIDTH = 1.6f;
        public const float MIN_FORTRESS_CHEESE_DIST = 3.7f;

        // V4: 방-방 AABB 상호 미침범
        public static bool NoRoomOverlap(IReadOnlyList<RoomRect> _rooms)
        {
            for (int i = 0; i < _rooms.Count; i++)
                for (int j = i + 1; j < _rooms.Count; j++)
                    if (AabbOverlap(_rooms[i], _rooms[j])) return false;
            return true;
        }

        private static bool AabbOverlap(RoomRect _a, RoomRect _b) =>
            _a.Min.x < _b.Max.x && _a.Max.x > _b.Min.x && _a.Min.y < _b.Max.y && _a.Max.y > _b.Min.y;

        // V3: 모든 방이 통로 그래프로 start에서 도달 (통로 끝점이 방 경계에 접하면 연결로 간주)
        public static bool AllConnected(
            IReadOnlyList<RoomRect> _rooms, IReadOnlyList<CorridorSeg> _corridors, string _startId)
        {
            var adj = BuildRoomAdjacency(_rooms, _corridors);
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(_startId);
            visited.Add(_startId);
            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                if (!adj.TryGetValue(cur, out var nbrs)) continue;
                foreach (string n in nbrs)
                    if (visited.Add(n)) queue.Enqueue(n);
            }
            return visited.Count == _rooms.Count;
        }

        // 통로 세그먼트 끝점이 어느 방 경계(±0.6 여유)에 닿는지로 방-방 인접 구성.
        // 세그먼트 체인(미로 포함)은 끝점 공유(±0.2)로 이어 하나의 통로망으로 병합.
        private static Dictionary<string, HashSet<string>> BuildRoomAdjacency(
            IReadOnlyList<RoomRect> _rooms, IReadOnlyList<CorridorSeg> _corridors)
        {
            // 각 세그먼트 끝점이 닿는 방 목록
            var adj = new Dictionary<string, HashSet<string>>();
            foreach (var r in _rooms) adj[r.Id] = new HashSet<string>();

            // 세그먼트를 유니온-파인드로 묶어 통로망 컴포넌트 형성
            var uf = new UnionFind(_corridors.Count);
            for (int i = 0; i < _corridors.Count; i++)
                for (int j = i + 1; j < _corridors.Count; j++)
                    if (EndpointsTouch(_corridors[i], _corridors[j])) uf.Union(i, j);

            // 컴포넌트별로 닿는 방 집합 → 그 방들끼리 상호 연결
            var compRooms = new Dictionary<int, HashSet<string>>();
            for (int i = 0; i < _corridors.Count; i++)
            {
                int root = uf.Find(i);
                if (!compRooms.TryGetValue(root, out var set)) { set = new HashSet<string>(); compRooms[root] = set; }
                foreach (var r in _rooms)
                {
                    if (PointNearRoom(_corridors[i].From, r) || PointNearRoom(_corridors[i].To, r))
                        set.Add(r.Id);
                }
            }
            foreach (var set in compRooms.Values)
                foreach (string a in set)
                    foreach (string b in set)
                        if (a != b) adj[a].Add(b);
            return adj;
        }

        private static bool EndpointsTouch(CorridorSeg _a, CorridorSeg _b)
        {
            const float EPS = 0.25f;
            return Vector2.Distance(_a.From, _b.From) < EPS || Vector2.Distance(_a.From, _b.To) < EPS
                || Vector2.Distance(_a.To, _b.From) < EPS || Vector2.Distance(_a.To, _b.To) < EPS;
        }

        private static bool PointNearRoom(Vector2 _p, RoomRect _r)
        {
            const float PAD = 0.7f; // 통로 폭 절반 + 여유 — 경계 접점 판정
            return _p.x >= _r.Min.x - PAD && _p.x <= _r.Max.x + PAD
                && _p.y >= _r.Min.y - PAD && _p.y <= _r.Max.y + PAD;
        }

        // V6: 모든 통로 폭 >= min
        public static bool CorridorsWideEnough(IReadOnlyList<CorridorSeg> _corridors, float _min)
        {
            foreach (var c in _corridors)
                if (c.Width < _min - 1e-4f) return false;
            return true;
        }

        // V11: 등급 다중집합 == {1..7} 정확히
        public static bool CheeseGradesComplete(IReadOnlyList<CheesePlacement> _cheese)
        {
            if (_cheese.Count != 7) return false;
            var seen = new HashSet<int>();
            foreach (var c in _cheese)
                if (c.Grade < 1 || c.Grade > 7 || !seen.Add(c.Grade)) return false;
            return seen.Count == 7;
        }

        // V9: 모든 요새-치즈 중심거리 >= min
        public static bool FortressCheeseSpacing(
            IReadOnlyList<FortressPlacement> _forts, IReadOnlyList<CheesePlacement> _cheese, float _min)
        {
            foreach (var f in _forts)
                foreach (var c in _cheese)
                    if (Vector2.Distance(f.Pos, c.Pos) < _min) return false;
            return true;
        }

        // 소형 유니온-파인드 (세그먼트 컴포넌트용)
        private class UnionFind
        {
            private readonly int[] m_Parent;
            public UnionFind(int n) { m_Parent = new int[n]; for (int i = 0; i < n; i++) m_Parent[i] = i; }
            public int Find(int _x) { while (m_Parent[_x] != _x) { m_Parent[_x] = m_Parent[m_Parent[_x]]; _x = m_Parent[_x]; } return _x; }
            public void Union(int _a, int _b) { m_Parent[Find(_a)] = Find(_b); }
        }
    }
}
