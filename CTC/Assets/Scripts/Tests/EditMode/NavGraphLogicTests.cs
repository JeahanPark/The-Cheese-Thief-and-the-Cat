using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 레이아웃 → 이동 그래프. 강아지가 NavMesh 없이 길을 찾는 근거라 여기가 무너지면
    /// 강아지가 벽에 붙어 선다.
    /// </summary>
    public class NavGraphLogicTests
    {
        private static RoomRect Room(float _x, float _z, float _w, float _h) => new RoomRect
        {
            Id = $"r{_x}_{_z}",
            Center = new Vector2(_x, _z),
            Size = new Vector2(_w, _h),
        };

        private static CorridorSeg Corridor(Vector2 _from, Vector2 _to) => new CorridorSeg
        {
            From = _from,
            To = _to,
            Width = 2f,
        };

        [Test]
        public void Build_EmptyLayout_IsEmpty()
        {
            NavGraph graph = NavGraphLogic.Build(null, null);
            Assert.AreEqual(0, graph.Count);
            Assert.AreEqual(-1, NavGraphLogic.NearestNode(graph, Vector2.zero));
        }

        [Test]
        public void Build_RoomBecomesNode()
        {
            NavGraph graph = NavGraphLogic.Build(new List<RoomRect> { Room(3f, 4f, 6f, 6f) }, null);

            Assert.AreEqual(1, graph.Count);
            Assert.AreEqual(new Vector2(3f, 4f), graph.Nodes[0]);
        }

        [Test]
        public void Build_CoincidentEndpointsMergeIntoOneNode()
        {
            // 복도 둘이 한 점에서 만난다 — 노드가 둘이면 그 자리에서 길이 끊긴다.
            var corridors = new List<CorridorSeg>
            {
                Corridor(new Vector2(0f, 0f), new Vector2(10f, 0f)),
                Corridor(new Vector2(10f, 0f), new Vector2(10f, 10f)),
            };

            NavGraph graph = NavGraphLogic.Build(null, corridors);

            Assert.AreEqual(3, graph.Count, "만나는 점은 하나로 합쳐져야 한다");
        }

        [Test]
        public void FindPath_AcrossCorridor_ReachesFarRoom()
        {
            var rooms = new List<RoomRect> { Room(0f, 0f, 6f, 6f), Room(20f, 0f, 6f, 6f) };
            var corridors = new List<CorridorSeg> { Corridor(new Vector2(0f, 0f), new Vector2(20f, 0f)) };

            NavGraph graph = NavGraphLogic.Build(rooms, corridors);
            List<Vector2> path = NavGraphLogic.FindPath(graph, new Vector2(0f, 0f), new Vector2(20f, 0f));

            Assert.IsNotEmpty(path);
            Assert.AreEqual(new Vector2(20f, 0f), path[path.Count - 1], "마지막 점은 표적 자리여야 한다");
        }

        [Test]
        public void FindPath_Disconnected_ReturnsEmpty()
        {
            // 이어지지 않은 방 둘 — 길이 없으면 빈 목록이어야 한다(엉뚱한 곳으로 가면 안 된다).
            var rooms = new List<RoomRect> { Room(0f, 0f, 4f, 4f), Room(100f, 100f, 4f, 4f) };

            NavGraph graph = NavGraphLogic.Build(rooms, null);
            List<Vector2> path = NavGraphLogic.FindPath(graph, new Vector2(0f, 0f), new Vector2(100f, 100f));

            Assert.IsEmpty(path);
        }

        [Test]
        public void FindPath_SameNode_GoesStraightToTarget()
        {
            var rooms = new List<RoomRect> { Room(0f, 0f, 10f, 10f) };
            NavGraph graph = NavGraphLogic.Build(rooms, null);

            List<Vector2> path = NavGraphLogic.FindPath(graph, new Vector2(1f, 1f), new Vector2(2f, 2f));

            Assert.AreEqual(1, path.Count, "같은 노드 안이면 곧장 간다");
            Assert.AreEqual(new Vector2(2f, 2f), path[0]);
        }

        [Test]
        public void FindPath_EveryStepIsAnActualEdge()
        {
            // 경유점 사이가 실제 간선이 아니면 강아지가 벽을 뚫고 직진한다.
            var rooms = new List<RoomRect> { Room(0f, 0f, 6f, 6f), Room(20f, 0f, 6f, 6f), Room(20f, 20f, 6f, 6f) };
            var corridors = new List<CorridorSeg>
            {
                Corridor(new Vector2(0f, 0f), new Vector2(20f, 0f)),
                Corridor(new Vector2(20f, 0f), new Vector2(20f, 20f)),
            };

            NavGraph graph = NavGraphLogic.Build(rooms, corridors);
            List<Vector2> path = NavGraphLogic.FindPath(graph, new Vector2(0f, 0f), new Vector2(20f, 20f));

            Assert.IsNotEmpty(path);
            // 마지막 점(표적 자리)을 뺀 구간은 전부 그래프 노드이고 이웃끼리여야 한다.
            for (int i = 0; i + 2 < path.Count; i++)
            {
                int a = IndexOfNode(graph, path[i]);
                int b = IndexOfNode(graph, path[i + 1]);
                Assert.GreaterOrEqual(a, 0, "경유점이 그래프 노드가 아니다");
                Assert.GreaterOrEqual(b, 0, "경유점이 그래프 노드가 아니다");
                CollectionAssert.Contains(graph.NeighborsOf(a), b, "이어지지 않은 두 점을 잇는 경로다");
            }
        }

        [Test]
        public void NearestNode_PicksClosest()
        {
            var rooms = new List<RoomRect> { Room(0f, 0f, 2f, 2f), Room(50f, 0f, 2f, 2f) };
            NavGraph graph = NavGraphLogic.Build(rooms, null);

            int near = NavGraphLogic.NearestNode(graph, new Vector2(48f, 3f));

            Assert.AreEqual(new Vector2(50f, 0f), graph.Nodes[near]);
        }

        [Test]
        public void Build_OverlappingRoomsConnectWithoutCorridor()
        {
            // 겹친 방은 걸어서 오갈 수 있다 — 복도가 없다고 끊으면 갇힌다.
            var rooms = new List<RoomRect> { Room(0f, 0f, 10f, 10f), Room(6f, 0f, 10f, 10f) };

            NavGraph graph = NavGraphLogic.Build(rooms, null);
            List<Vector2> path = NavGraphLogic.FindPath(graph, new Vector2(-4f, 0f), new Vector2(10f, 0f));

            Assert.IsNotEmpty(path);
        }

        private static int IndexOfNode(NavGraph _graph, Vector2 _pos)
        {
            for (int i = 0; i < _graph.Count; i++)
            {
                if ((_graph.Nodes[i] - _pos).sqrMagnitude < 0.0001f) return i;
            }
            return -1;
        }
    }
}
