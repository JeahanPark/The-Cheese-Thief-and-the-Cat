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

        public static NavGraph Build(IReadOnlyList<RoomRect> _rooms, IReadOnlyList<CorridorSeg> _corridors)
        {
            var graph = new NavGraph();
            if (_rooms == null && _corridors == null) return graph;

            // 방 중심이 먼저다 — 복도 끝점이 방 중심과 겹쳐도 방 쪽 인덱스를 물려받게 한다.
            var roomNodes = new List<int>();
            if (_rooms != null)
            {
                for (int i = 0; i < _rooms.Count; i++)
                {
                    roomNodes.Add(graph.AddNode(_rooms[i].Center, MERGE_DISTANCE));
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
                    if (ContainsWithMargin(_rooms[r], seg.From)) graph.Connect(from, roomNodes[r]);
                    if (ContainsWithMargin(_rooms[r], seg.To)) graph.Connect(to, roomNodes[r]);
                }
            }

            return graph;
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
