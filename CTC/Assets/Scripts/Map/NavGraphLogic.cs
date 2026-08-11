using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>맵 레이아웃에서 뽑은 이동 그래프. 노드는 x·z 평면 좌표다.</summary>
    public sealed class NavGraph
    {
        private readonly List<Vector2> m_Nodes = new List<Vector2>();
        private readonly List<List<int>> m_Neighbors = new List<List<int>>();

        public IReadOnlyList<Vector2> Nodes => m_Nodes;
        public int Count => m_Nodes.Count;

        public IReadOnlyList<int> NeighborsOf(int _index) => m_Neighbors[_index];

        /// <summary>이미 있는 자리면 그 인덱스를 준다 — 복도 끝점은 방끼리 겹쳐 들어온다.</summary>
        internal int AddNode(Vector2 _pos, float _mergeDistance)
        {
            for (int i = 0; i < m_Nodes.Count; i++)
            {
                if ((m_Nodes[i] - _pos).sqrMagnitude <= _mergeDistance * _mergeDistance) return i;
            }

            m_Nodes.Add(_pos);
            m_Neighbors.Add(new List<int>());
            return m_Nodes.Count - 1;
        }

        internal void Connect(int _a, int _b)
        {
            if (_a == _b) return;
            if (!m_Neighbors[_a].Contains(_b)) m_Neighbors[_a].Add(_b);
            if (!m_Neighbors[_b].Contains(_a)) m_Neighbors[_b].Add(_a);
        }

        /// <summary>
        /// 간선을 끊는다. **레이아웃은 벽을 모른다** — 방과 복도로 그린 선이 실제로는
        /// 벽을 뚫고 있을 수 있다. 그런 선을 남겨 두면 길찾기가 "갈 수 있다"고 답해 놓고
        /// 실제로는 벽에 코를 박는다. 지오메트리를 아는 쪽(런타임)이 걷어낸다.
        /// </summary>
        public void Disconnect(int _a, int _b)
        {
            m_Neighbors[_a].Remove(_b);
            m_Neighbors[_b].Remove(_a);
        }

        /// <summary>남아 있는 간선 수(양방향 1개로 센다) — 걷어내기가 과했는지 본다.</summary>
        public int EdgeCount
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < m_Neighbors.Count; i++) sum += m_Neighbors[i].Count;
                return sum / 2;
            }
        }
    }

    /// <summary>
    /// 레이아웃(방 사각형 + 복도 선분)에서 이동 그래프를 뽑고 길을 찾는다.
    /// MonoBehaviour와 분리된 순수 함수(EditMode TDD).
    ///
    /// **NavMesh를 쓰지 않는 이유**: 이 프로젝트의 지형은 매치마다 `MapBuilder`가 레이아웃에서
    /// 런타임에 짓는다. NavMesh는 굽기 단계를 요구하므로 성립하지 않는다. 반면 레이아웃은
    /// 이미 "방과 복도"라는 그래프라서 그대로 옮기면 된다.
    ///
    /// **한계**: 그래프는 방향만 준다. 정확한 회피는 `CharacterController`의 물리 충돌이 맡는다 —
    /// 복도에 폭이 있고 첫·마지막 구간은 같은 방/복도 안이라 실용상 충분하다.
    /// </summary>
    public static class NavGraphLogic
    {
        /// <summary>이 거리 안의 점은 같은 노드로 본다. 복도 끝점이 방 경계에서 미세하게 어긋난다.</summary>
        public const float MERGE_DISTANCE = 0.35f;

        /// <summary>복도 끝점을 방에 붙일 때의 여유. 끝점이 방 벽에 살짝 못 미쳐도 이어 준다.</summary>
        public const float ROOM_ATTACH_MARGIN = 1.0f;

        /// <summary>모서리 노드를 방 벽에서 이만큼 안으로 들인다. 벽에 붙이면 몸통이 낀다.</summary>
        public const float ROOM_CORNER_INSET = 1.5f;

        /// <summary>
        /// 중심에서 이만큼은 떨어져야 모서리 노드를 만든다. 좁은 방에서는 중심과 겹쳐
        /// 노드만 늘고 얻는 게 없다.
        /// </summary>
        public const float ROOM_CORNER_MIN_OFFSET = 1.5f;

        public static NavGraph Build(IReadOnlyList<RoomRect> _rooms, IReadOnlyList<CorridorSeg> _corridors)
        {
            var graph = new NavGraph();
            if (_rooms == null && _corridors == null) return graph;

            // 방 중심이 먼저다 — 복도 끝점이 방 중심과 겹쳐도 방 쪽 인덱스를 물려받게 한다.
            var roomNodes = new List<int>();
            var roomRings = new List<List<int>>();
            if (_rooms != null)
            {
                for (int i = 0; i < _rooms.Count; i++)
                {
                    int center = graph.AddNode(_rooms[i].Center, MERGE_DISTANCE);
                    roomNodes.Add(center);
                    roomRings.Add(BuildRing(graph, _rooms[i], center));
                }

                // 겹친 방끼리는 걸어서 오갈 수 있다 — 복도가 없어도 이어 둔다.
                for (int i = 0; i < _rooms.Count; i++)
                {
                    for (int j = i + 1; j < _rooms.Count; j++)
                    {
                        if (Overlaps(_rooms[i], _rooms[j])) graph.Connect(roomNodes[i], roomNodes[j]);
                    }
                }
            }

            if (_corridors == null) return graph;

            foreach (CorridorSeg seg in _corridors)
            {
                int from = graph.AddNode(seg.From, MERGE_DISTANCE);
                int to = graph.AddNode(seg.To, MERGE_DISTANCE);
                graph.Connect(from, to);

                if (_rooms == null) continue;
                for (int r = 0; r < _rooms.Count; r++)
                {
                    if (ContainsWithMargin(_rooms[r], seg.From)) AttachToRoom(graph, from, roomNodes[r], roomRings[r]);
                    if (ContainsWithMargin(_rooms[r], seg.To)) AttachToRoom(graph, to, roomNodes[r], roomRings[r]);
                }
            }

            return graph;
        }

        /// <summary>
        /// 방 안에 **모서리 노드 넷을 링으로** 두른다 (2026-08-11).
        ///
        /// **왜**: 방 하나에 노드가 중심 하나뿐이면, 그 중심에 뭐가 서 있는 순간 방이 통째로
        /// 그래프에서 잘려 나간다. 요새·감옥이 정확히 방 중심에 서고, 엄폐물도 관통 시야를
        /// 끊으려면 중심선 위에 서야 한다 — **시야를 끊는 선과 길을 내는 선이 같은 선이다.**
        ///
        /// 실측(2026-08-11): 요새가 선 방들이 전부 고립돼 쥐가 스폰 자리에서 30초간 못 움직였고,
        /// 기본 맵이 39노드 29간선(이어져 있으려면 최소 38)으로 조각나 있었다. 링을 두르면
        /// 중심이 막혀도 옆으로 돌아 문에서 문으로 건너간다.
        ///
        /// 좁은 방은 건너뛴다 — 모서리가 중심과 겹쳐 노드만 늘고 얻는 게 없다.
        /// </summary>
        private static List<int> BuildRing(NavGraph _graph, RoomRect _room, int _center)
        {
            var ring = new List<int>();
            Vector2 inset = _room.Size * 0.5f - Vector2.one * ROOM_CORNER_INSET;
            if (inset.x < ROOM_CORNER_MIN_OFFSET || inset.y < ROOM_CORNER_MIN_OFFSET) return ring;

            ring.Add(_graph.AddNode(_room.Center + new Vector2(inset.x, inset.y), MERGE_DISTANCE));
            ring.Add(_graph.AddNode(_room.Center + new Vector2(-inset.x, inset.y), MERGE_DISTANCE));
            ring.Add(_graph.AddNode(_room.Center + new Vector2(-inset.x, -inset.y), MERGE_DISTANCE));
            ring.Add(_graph.AddNode(_room.Center + new Vector2(inset.x, -inset.y), MERGE_DISTANCE));

            for (int i = 0; i < ring.Count; i++)
            {
                _graph.Connect(ring[i], ring[(i + 1) % ring.Count]);   // 링 — 가운데가 막혀도 돈다
                _graph.Connect(_center, ring[i]);                      // 비어 있으면 가운데로 질러간다
            }
            return ring;
        }

        // 문(복도 끝점)은 방 중심과 모서리 **모두**에 건다. 막힌 것은 런타임 걷어내기가 지운다 —
        // 여기서 미리 고르면 지오메트리를 모르는 채로 고르는 셈이 된다.
        private static void AttachToRoom(NavGraph _graph, int _door, int _center, List<int> _ring)
        {
            _graph.Connect(_door, _center);
            for (int i = 0; i < _ring.Count; i++) _graph.Connect(_door, _ring[i]);
        }

        /// <summary>가장 가까운 노드. 그래프가 비면 -1.</summary>
        public static int NearestNode(NavGraph _graph, Vector2 _pos)
        {
            if (_graph == null || _graph.Count == 0) return -1;

            int best = 0;
            float bestSqr = (_graph.Nodes[0] - _pos).sqrMagnitude;
            for (int i = 1; i < _graph.Count; i++)
            {
                float sqr = (_graph.Nodes[i] - _pos).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = i; }
            }
            return best;
        }

        /// <summary>
        /// `_from`에서 `_to`까지의 경유점. **마지막 점은 항상 `_to`** — 노드까지만 가면
        /// 표적 코앞에서 멈춘다. 길이 없으면 빈 목록.
        /// </summary>
        public static List<Vector2> FindPath(NavGraph _graph, Vector2 _from, Vector2 _to)
        {
            var path = new List<Vector2>();

            int start = NearestNode(_graph, _from);
            int goal = NearestNode(_graph, _to);
            if (start < 0 || goal < 0) return path;

            if (start == goal)
            {
                path.Add(_to);
                return path;
            }

            int[] previous = Dijkstra(_graph, start, goal);
            if (previous == null) return path;

            // 목표에서 거슬러 올라간 뒤 뒤집는다. 출발 노드는 넣지 않는다 —
            // 강아지는 이미 그 근처에 있고, 뒤로 돌아갔다 오는 그림이 된다.
            var reversed = new List<Vector2>();
            for (int at = goal; at != start; at = previous[at])
            {
                reversed.Add(_graph.Nodes[at]);
            }
            for (int i = reversed.Count - 1; i >= 0; i--) path.Add(reversed[i]);

            path.Add(_to);
            return path;
        }

        /// <summary>
        /// 길이 있으면 <see cref="FindPath"/>와 같고, **없으면 갈 수 있는 곳 중 표적에 가장 가까운
        /// 노드까지** 간다(2026-08-10).
        ///
        /// 왜 필요한가: 그래프는 벽을 지나는 간선을 걷어내고 나면 조각조각 끊긴 채로 남는다
        /// (기본 맵 실측 39노드 29간선 — 이어져 있으려면 최소 38개다). 그래서 표적이 다른 조각에
        /// 있으면 길이 아예 안 나오고, 그때 빈 경로를 돌려주면 강아지가 **그 자리에 굳는다**
        /// (2026-08-10 실측: 감지 반경을 14→20으로 올리자 Chase 상태로 path=0에 붙박였다).
        ///
        /// 끝까지 못 가도 그쪽으로 붙어 두면 물리 충돌과 경유점 건너뛰기가 나머지를 해결한다 —
        /// 멈춰 있는 것보다 언제나 낫다.
        /// </summary>
        public static List<Vector2> FindPathToward(NavGraph _graph, Vector2 _from, Vector2 _to)
        {
            List<Vector2> direct = FindPath(_graph, _from, _to);
            if (direct.Count > 0) return direct;

            var path = new List<Vector2>();
            int start = NearestNode(_graph, _from);
            if (start < 0) { path.Add(_to); return path; }

            int[] previous = DijkstraAll(_graph, start);

            // 출발 조각 안에서 표적에 제일 가까운 노드를 고른다
            int best = -1;
            float bestSqr = (_graph.Nodes[start] - _to).sqrMagnitude;
            for (int i = 0; i < _graph.Count; i++)
            {
                if (i == start || previous[i] < 0) continue;   // 못 닿는 조각
                float sqr = (_graph.Nodes[i] - _to).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }

            // 조각 안에 더 가까운 자리가 없으면 **표적 쪽으로 그냥 민다**. 벽에 부딪히면
            // CharacterController가 벽을 타고 미끄러지고, 그래도 안 되면 경유점 건너뛰기가
            // 새 목적지를 잡는다 — 굳어 있는 것보다 언제나 낫다.
            if (best < 0) { path.Add(_to); return path; }

            var reversed = new List<Vector2>();
            for (int at = best; at != start; at = previous[at]) reversed.Add(_graph.Nodes[at]);
            for (int i = reversed.Count - 1; i >= 0; i--) path.Add(reversed[i]);
            return path;   // `_to`는 안 붙인다 — 못 가는 자리다
        }

        // 노드가 100개도 안 되므로 우선순위 큐 없이 선형 탐색으로 충분하다.
        private static int[] Dijkstra(NavGraph _graph, int _start, int _goal)
        {
            int n = _graph.Count;
            var dist = new float[n];
            var previous = new int[n];
            var visited = new bool[n];

            for (int i = 0; i < n; i++) { dist[i] = float.PositiveInfinity; previous[i] = -1; }
            dist[_start] = 0f;

            for (int step = 0; step < n; step++)
            {
                int current = -1;
                float best = float.PositiveInfinity;
                for (int i = 0; i < n; i++)
                {
                    if (!visited[i] && dist[i] < best) { best = dist[i]; current = i; }
                }
                if (current < 0) break;           // 남은 곳은 전부 닿을 수 없다
                if (current == _goal) return previous;

                visited[current] = true;
                foreach (int next in _graph.NeighborsOf(current))
                {
                    float candidate = dist[current] + Vector2.Distance(_graph.Nodes[current], _graph.Nodes[next]);
                    if (candidate < dist[next]) { dist[next] = candidate; previous[next] = current; }
                }
            }

            return previous[_goal] >= 0 ? previous : null;
        }

        // 조기 종료 없이 끝까지 — 어디까지 닿을 수 있는지를 알아야 '가장 가까운 대안'을 고른다.
        private static int[] DijkstraAll(NavGraph _graph, int _start)
        {
            int n = _graph.Count;
            var dist = new float[n];
            var previous = new int[n];
            var visited = new bool[n];

            for (int i = 0; i < n; i++) { dist[i] = float.PositiveInfinity; previous[i] = -1; }
            dist[_start] = 0f;

            for (int step = 0; step < n; step++)
            {
                int current = -1;
                float best = float.PositiveInfinity;
                for (int i = 0; i < n; i++)
                {
                    if (!visited[i] && dist[i] < best) { best = dist[i]; current = i; }
                }
                if (current < 0) break;

                visited[current] = true;
                foreach (int next in _graph.NeighborsOf(current))
                {
                    float candidate = dist[current] + Vector2.Distance(_graph.Nodes[current], _graph.Nodes[next]);
                    if (candidate < dist[next]) { dist[next] = candidate; previous[next] = current; }
                }
            }
            return previous;
        }

        private static bool ContainsWithMargin(RoomRect _room, Vector2 _p)
        {
            Vector2 min = _room.Min - Vector2.one * ROOM_ATTACH_MARGIN;
            Vector2 max = _room.Max + Vector2.one * ROOM_ATTACH_MARGIN;
            return _p.x >= min.x && _p.x <= max.x && _p.y >= min.y && _p.y <= max.y;
        }

        private static bool Overlaps(RoomRect _a, RoomRect _b)
        {
            return _a.Min.x <= _b.Max.x && _b.Min.x <= _a.Max.x
                && _a.Min.y <= _b.Max.y && _b.Min.y <= _a.Max.y;
        }
    }
}
