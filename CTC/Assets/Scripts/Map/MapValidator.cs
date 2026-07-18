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
        public static bool NoRoomOverlap(IReadOnlyList<RoomRect> rooms)
        {
            for (int i = 0; i < rooms.Count; i++)
                for (int j = i + 1; j < rooms.Count; j++)
                    if (AabbOverlap(rooms[i], rooms[j])) return false;
            return true;
        }

        private static bool AabbOverlap(RoomRect a, RoomRect b) =>
            a.Min.x < b.Max.x && a.Max.x > b.Min.x && a.Min.y < b.Max.y && a.Max.y > b.Min.y;

        // V3: 모든 방이 통로 그래프로 start에서 도달 (통로 끝점이 방 경계에 접하면 연결로 간주)
        public static bool AllConnected(
            IReadOnlyList<RoomRect> rooms, IReadOnlyList<CorridorSeg> corridors, string startId)
        {
            var adj = BuildRoomAdjacency(rooms, corridors);
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(startId);
            visited.Add(startId);
            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                if (!adj.TryGetValue(cur, out var nbrs)) continue;
                foreach (string n in nbrs)
                    if (visited.Add(n)) queue.Enqueue(n);
            }
            return visited.Count == rooms.Count;
        }

        // 통로 세그먼트 끝점이 어느 방 경계(±0.6 여유)에 닿는지로 방-방 인접 구성.
        // 세그먼트 체인(미로 포함)은 끝점 공유(±0.2)로 이어 하나의 통로망으로 병합.
        private static Dictionary<string, HashSet<string>> BuildRoomAdjacency(
            IReadOnlyList<RoomRect> rooms, IReadOnlyList<CorridorSeg> corridors)
        {
            // 각 세그먼트 끝점이 닿는 방 목록
            var adj = new Dictionary<string, HashSet<string>>();
            foreach (var r in rooms) adj[r.Id] = new HashSet<string>();

            // 세그먼트를 유니온-파인드로 묶어 통로망 컴포넌트 형성
            var uf = new UnionFind(corridors.Count);
            for (int i = 0; i < corridors.Count; i++)
                for (int j = i + 1; j < corridors.Count; j++)
                    if (EndpointsTouch(corridors[i], corridors[j])) uf.Union(i, j);

            // 컴포넌트별로 닿는 방 집합 → 그 방들끼리 상호 연결
            var compRooms = new Dictionary<int, HashSet<string>>();
            for (int i = 0; i < corridors.Count; i++)
            {
                int root = uf.Find(i);
                if (!compRooms.TryGetValue(root, out var set)) { set = new HashSet<string>(); compRooms[root] = set; }
                foreach (var r in rooms)
                {
                    if (PointNearRoom(corridors[i].From, r) || PointNearRoom(corridors[i].To, r))
                        set.Add(r.Id);
                }
            }
            foreach (var set in compRooms.Values)
                foreach (string a in set)
                    foreach (string b in set)
                        if (a != b) adj[a].Add(b);
            return adj;
        }

        private static bool EndpointsTouch(CorridorSeg a, CorridorSeg b)
        {
            const float EPS = 0.25f;
            return Vector2.Distance(a.From, b.From) < EPS || Vector2.Distance(a.From, b.To) < EPS
                || Vector2.Distance(a.To, b.From) < EPS || Vector2.Distance(a.To, b.To) < EPS;
        }

        private static bool PointNearRoom(Vector2 p, RoomRect r)
        {
            const float PAD = 0.7f; // 통로 폭 절반 + 여유 — 경계 접점 판정
            return p.x >= r.Min.x - PAD && p.x <= r.Max.x + PAD
                && p.y >= r.Min.y - PAD && p.y <= r.Max.y + PAD;
        }

        // V6: 모든 통로 폭 >= min
        public static bool CorridorsWideEnough(IReadOnlyList<CorridorSeg> corridors, float min)
        {
            foreach (var c in corridors)
                if (c.Width < min - 1e-4f) return false;
            return true;
        }

        // V11: 등급 다중집합 == {1..7} 정확히
        public static bool CheeseGradesComplete(IReadOnlyList<CheesePlacement> cheese)
        {
            if (cheese.Count != 7) return false;
            var seen = new HashSet<int>();
            foreach (var c in cheese)
                if (c.Grade < 1 || c.Grade > 7 || !seen.Add(c.Grade)) return false;
            return seen.Count == 7;
        }

        // V9: 모든 요새-치즈 중심거리 >= min
        public static bool FortressCheeseSpacing(
            IReadOnlyList<FortressPlacement> forts, IReadOnlyList<CheesePlacement> cheese, float min)
        {
            foreach (var f in forts)
                foreach (var c in cheese)
                    if (Vector2.Distance(f.Pos, c.Pos) < min) return false;
            return true;
        }

        // 소형 유니온-파인드 (세그먼트 컴포넌트용)
        private class UnionFind
        {
            private readonly int[] m_Parent;
            public UnionFind(int n) { m_Parent = new int[n]; for (int i = 0; i < n; i++) m_Parent[i] = i; }
            public int Find(int x) { while (m_Parent[x] != x) { m_Parent[x] = m_Parent[m_Parent[x]]; x = m_Parent[x]; } return x; }
            public void Union(int a, int b) { m_Parent[Find(a)] = Find(b); }
        }
    }
}
