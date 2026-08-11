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
        public void Build_SmallRoomBecomesOneNode()
        {
            // 좁은 방은 모서리가 중심과 겹쳐 노드만 늘고 얻는 게 없다 — 중심 하나로 둔다.
            NavGraph graph = NavGraphLogic.Build(new List<RoomRect> { Room(3f, 4f, 4f, 4f) }, null);

            Assert.AreEqual(1, graph.Count);
            Assert.AreEqual(new Vector2(3f, 4f), graph.Nodes[0]);
        }

        [Test]
        public void Build_RoomGetsCornerRing()
        {
            // 방 중심에 요새가 서면 그 방이 통째로 잘려 나간다 — 돌아갈 링이 있어야 한다.
            NavGraph graph = NavGraphLogic.Build(new List<RoomRect> { Room(0f, 0f, 12f, 12f) }, null);

            Assert.AreEqual(5, graph.Count, "중심 + 모서리 넷");
            Assert.AreEqual(Vector2.zero, graph.Nodes[0], "중심이 먼저다");
            Assert.AreEqual(4, graph.NeighborsOf(0).Count, "중심은 모서리 넷과 이어진다");
        }

        [Test]
        public void Build_RingSurvivesWhenCenterIsCutOff()
        {
            // 요새·엄폐물이 중심 간선을 전부 끊어도 문에서 문으로 건너갈 수 있어야 한다.
            var rooms = new List<RoomRect> { Room(0f, 0f, 12f, 12f) };
            var corridors = new List<CorridorSeg>
            {
                Corridor(new Vector2(-6f, 0f), new Vector2(-14f, 0f)),   // 서쪽 문
                Corridor(new Vector2(6f, 0f), new Vector2(14f, 0f)),     // 동쪽 문
            };

            NavGraph graph = NavGraphLogic.Build(rooms, corridors);

            int center = IndexOfNode(graph, Vector2.zero);
            var neighbors = new List<int>(graph.NeighborsOf(center));
            foreach (int n in neighbors) graph.Disconnect(center, n);    // 중심을 통째로 막는다

            List<Vector2> path = NavGraphLogic.FindPath(graph, new Vector2(-14f, 0f), new Vector2(14f, 0f));

            Assert.IsNotEmpty(path, "중심이 막혔다고 방이 잠기면 안 된다");
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
            var rooms = new List<RoomRect> { Room(0f, 0f, 4f, 4f) };
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

        [Test]
        public void FindPathToward_UnreachableTarget_StillHeadsThatWay()
        {
            // 2026-08-10 실측: 간선을 걷어내면 그래프가 조각난다(기본 맵 39노드 29간선).
            // 표적이 다른 조각에 있으면 FindPath는 빈 목록을 주고, 강아지가 그 자리에 굳었다.
            var rooms = new List<RoomRect>
            {
                Room(0f, 0f, 8f, 8f),
                Room(40f, 0f, 8f, 8f),    // 통로가 없어 닿을 수 없는 섬
                Room(20f, 0f, 8f, 8f),    // 표적 쪽으로 가는 길 — 여기까지는 갈 수 있다
            };
            var corridors = new List<CorridorSeg> { Corridor(new Vector2(4f, 0f), new Vector2(16f, 0f)) };

            NavGraph graph = NavGraphLogic.Build(rooms, corridors);
            var from = new Vector2(0f, 0f);
            var unreachable = new Vector2(40f, 0f);

            Assert.IsEmpty(NavGraphLogic.FindPath(graph, from, unreachable), "전제: 길이 없어야 한다");

            List<Vector2> toward = NavGraphLogic.FindPathToward(graph, from, unreachable);
            Assert.IsNotEmpty(toward, "빈 경로를 주면 강아지가 굳는다");
            // 끝점은 갈 수 있는 자리여야 한다 — 못 가는 표적을 그대로 얹으면 벽만 민다
            Assert.AreNotEqual(unreachable, toward[toward.Count - 1]);
            // 그리고 지금 자리보다는 표적에 가까워야 의미가 있다
            Vector2 end = toward[toward.Count - 1];
            Assert.Less((end - unreachable).magnitude, (from - unreachable).magnitude);
        }

        [Test]
        public void FindPathToward_NowhereBetterToGo_FallsBackToABeeline()
        {
            // 조각 안에 더 나은 자리가 없으면 표적 쪽으로 그냥 민다 —
            // 빈 목록을 주면 강아지가 Chase 상태로 굳는다(2026-08-10 실측).
            // 모서리 링이 없는 좁은 방이라야 "조각 안에 갈 데가 없는" 상황이 된다.
            var rooms = new List<RoomRect> { Room(0f, 0f, 4f, 4f), Room(20f, 0f, 4f, 4f) };

            NavGraph graph = NavGraphLogic.Build(rooms, null);
            var from = new Vector2(0f, 0f);
            var unreachable = new Vector2(20f, 0f);

            List<Vector2> toward = NavGraphLogic.FindPathToward(graph, from, unreachable);
            Assert.AreEqual(1, toward.Count);
            Assert.AreEqual(unreachable, toward[0], "갈 데가 없으면 표적 쪽으로 그냥 민다");
        }

        [Test]
        public void FindPathToward_ReachableTarget_MatchesFindPath()
        {
            var rooms = new List<RoomRect> { Room(0f, 0f, 8f, 8f), Room(0f, 12f, 8f, 8f) };
            var corridors = new List<CorridorSeg> { Corridor(new Vector2(0f, 4f), new Vector2(0f, 8f)) };

            NavGraph graph = NavGraphLogic.Build(rooms, corridors);
            var from = new Vector2(0f, 0f);
            var to = new Vector2(0f, 12f);

            CollectionAssert.AreEqual(
                NavGraphLogic.FindPath(graph, from, to),
                NavGraphLogic.FindPathToward(graph, from, to));
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
