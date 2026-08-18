using System.Collections.Generic;
using CTC.Runtime.Ai;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 격자 길찾기(2026-08-18) — 유니티 NavMesh를 대체한 자체 A\*의 규칙.
    ///
    /// 여기가 무너지면 봇이 벽을 뚫거나(대각선 검사), 요새 안 쥐를 영영 못 쫓거나(2단계 탐색),
    /// 쥐가 제 요새에 못 들어간다(쥐 전용 문). 전부 씬 없이 잴 수 있는 순수 규칙이다.
    ///
    /// 좌표 약속: 셀 (cx,cz)의 중심 = origin + ((cx+0.5),(cz+0.5)) * 0.5m.
    /// </summary>
    public class NavGridTests
    {
        private const float R_MOUSE = 0.22f;
        private const float R_CAT = 0.35f;

        // 20×20 빈 격자(원점 0,0) — 필요한 벽만 얹어 쓴다
        private static NavGrid Empty(int _side = 20)
        {
            var grid = new NavGrid(Vector2.zero, _side, _side);
            grid.BuildClearance();
            return grid;
        }

        [Test]
        public void ToCell_ToWorld_RoundTrips()
        {
            NavGrid grid = Empty();
            Vector3 center = grid.ToWorld(3, 7);

            Assert.AreEqual(3 * 0.5f + 0.25f, center.x, 1e-4f);
            Assert.AreEqual(7 * 0.5f + 0.25f, center.z, 1e-4f);

            grid.ToCell(center, out int cx, out int cz);
            Assert.AreEqual(3, cx);
            Assert.AreEqual(7, cz);
        }

        [Test]
        public void ToCell_ClampsOutsideTheGrid()
        {
            NavGrid grid = Empty(10);
            grid.ToCell(new Vector3(-100f, 0f, 100f), out int cx, out int cz);

            Assert.AreEqual(0, cx);
            Assert.AreEqual(9, cz);
        }

        [Test]
        public void Clearance_GrowsWithDistanceFromWalls()
        {
            var grid = new NavGrid(Vector2.zero, 20, 20);
            for (int z = 0; z < 20; z++) grid.SetBlocked(0, z, true);   // 왼쪽 벽 한 줄
            grid.BuildClearance();

            // 벽 면까지의 거리다 — 셀 중심 기준 반 칸을 뺀 값(막힌 셀 중심까지가 아니다)
            Assert.AreEqual(0.25f, grid.ClearanceMeters(1, 5), 1e-4f);
            Assert.AreEqual(0.75f, grid.ClearanceMeters(2, 5), 1e-4f);
            Assert.AreEqual(0f, grid.ClearanceMeters(0, 5), 1e-4f);
        }

        [Test]
        public void SealUnreachable_ClosesPocketsCutOffFromTheMainArea()
        {
            var grid = new NavGrid(Vector2.zero, 20, 20);
            for (int z = 0; z < 20; z++) grid.SetBlocked(15, z, true);   // 오른쪽을 잘라 낸다

            grid.SealUnreachable();
            grid.BuildClearance();

            Assert.IsFalse(grid.IsTerrainBlocked(5, 5), "큰 쪽은 남는다");
            Assert.IsTrue(grid.IsTerrainBlocked(17, 5), "잘린 작은 쪽은 봉인된다");
        }

        [Test]
        public void CanEnter_BlocksBodiesThatDoNotFit()
        {
            var grid = new NavGrid(Vector2.zero, 20, 20);
            for (int z = 0; z < 20; z++)
            {
                grid.SetBlocked(4, z, true);
                grid.SetBlocked(6, z, true);   // 한 칸(0.5m)짜리 틈
            }
            grid.BuildClearance();

            // 틈 한가운데: 벽 면까지 0.25m — 쥐(0.22)는 여유로 지나고 고양이(0.35)는 스친다
            Assert.IsTrue(grid.CanEnter(5, 5, NavAgent.Mouse, R_MOUSE, 0f, out _, _strictFit: true));
            Assert.IsFalse(grid.CanEnter(5, 5, NavAgent.Cat, R_CAT, 0f, out _, _strictFit: true));

            // 느슨한 판정에서는 고양이도 지나가되 **값을 치른다** — 여유가 있으면 저절로 피한다
            Assert.IsTrue(grid.CanEnter(5, 5, NavAgent.Cat, R_CAT, 0f, out float extra));
            Assert.AreEqual(NavGrid.TIGHT_COST, extra, 1e-4f);
        }

        [Test]
        public void MouseDoor_LetsMiceThroughAndStopsCats()
        {
            NavGrid grid = Empty();
            grid.AddStructureCell(5, 5, NavStructureKind.CatBreakable);
            grid.AddStructureCell(5, 5, NavStructureKind.MouseDoor);   // 인방이 있는 입구

            Assert.IsTrue(grid.CanEnter(5, 5, NavAgent.Mouse, R_MOUSE, 0f, out _), "쥐는 문으로 들어간다");
            Assert.IsFalse(grid.CanEnter(5, 5, NavAgent.Cat, R_CAT, 0f, out _), "고양이는 인방에 걸린다");
            Assert.IsFalse(grid.NeedsBreaking(5, 5, NavAgent.Mouse));
            Assert.IsTrue(grid.NeedsBreaking(5, 5, NavAgent.Cat));
        }

        [Test]
        public void CatBreakable_IsWallForMiceAndCostForCats()
        {
            NavGrid grid = Empty();
            grid.AddStructureCell(5, 5, NavStructureKind.CatBreakable);

            // 1차 탐색(부수기 없음)에서는 누구에게나 벽이다
            Assert.IsFalse(grid.CanEnter(5, 5, NavAgent.Cat, R_CAT, 0f, out _));

            // 2차 탐색에서는 고양이·강아지만 값을 치르고 지나간다. 쥐는 아군 구조물을 못 부순다.
            Assert.IsTrue(grid.CanEnter(5, 5, NavAgent.Cat, R_CAT, 10f, out float catCost));
            Assert.AreEqual(10f, catCost, 1e-4f);
            Assert.IsTrue(grid.CanEnter(5, 5, NavAgent.Dog, R_CAT, 10f, out _));
            Assert.IsFalse(grid.CanEnter(5, 5, NavAgent.Mouse, R_MOUSE, 10f, out _));
        }

        [Test]
        public void AnyBreakable_IsOpenToEveryoneInTheSecondPass()
        {
            NavGrid grid = Empty();
            grid.AddStructureCell(5, 5, NavStructureKind.AnyBreakable);   // 벽감·상자

            Assert.IsFalse(grid.CanEnter(5, 5, NavAgent.Mouse, R_MOUSE, 0f, out _));
            Assert.IsTrue(grid.CanEnter(5, 5, NavAgent.Mouse, R_MOUSE, 10f, out _));
            Assert.IsTrue(grid.NeedsBreaking(5, 5, NavAgent.Mouse));
        }

        [Test]
        public void StructureCells_CountSoOverlappingBuildsUnwindOneAtATime()
        {
            NavGrid grid = Empty();
            grid.AddStructureCell(5, 5, NavStructureKind.CatBreakable);
            grid.AddStructureCell(5, 5, NavStructureKind.CatBreakable);   // 요새는 겹쳐 설 수 있다

            grid.RemoveStructureCell(5, 5, NavStructureKind.CatBreakable);
            Assert.IsTrue(grid.NeedsBreaking(5, 5, NavAgent.Cat), "하나가 부서져도 나머지가 남는다");

            grid.RemoveStructureCell(5, 5, NavStructureKind.CatBreakable);
            Assert.IsFalse(grid.NeedsBreaking(5, 5, NavAgent.Cat));
            Assert.AreEqual(0, grid.StructureCellCount);
        }

        // ---- A* ----

        [Test]
        public void Path_OnOpenGroundIsASingleCorner()
        {
            NavGrid grid = Empty();
            var finder = new GridPathfinder(grid);
            var cells = new List<int>();

            Assert.IsTrue(finder.TryFindPath(grid.ToWorld(2, 2), grid.ToWorld(15, 2),
                NavAgent.Mouse, R_MOUSE, null, 0UL, cells));

            // **직선 검사**(14p)가 중간 노드를 전부 지운다 — 뚫려 있으면 꺾임점이 없다
            Assert.AreEqual(1, cells.Count);
            Assert.AreEqual(grid.Index(15, 2), cells[0]);
            Assert.IsFalse(finder.LastNeededBreaking);
        }

        [Test]
        public void Path_GoesAroundATerrainWallWithoutBreaking()
        {
            var grid = new NavGrid(Vector2.zero, 20, 20);
            for (int z = 0; z < 15; z++) grid.SetBlocked(10, z, true);   // 위쪽만 열린 칸막이
            grid.BuildClearance();

            var finder = new GridPathfinder(grid);
            var cells = new List<int>();

            Assert.IsTrue(finder.TryFindPath(grid.ToWorld(2, 2), grid.ToWorld(18, 2),
                NavAgent.Mouse, R_MOUSE, null, 0UL, cells));
            Assert.IsFalse(finder.LastNeededBreaking, "우회로가 있으면 절대 안 부순다");
            Assert.Greater(cells.Count, 1, "곧장 갈 수 없으니 꺾임점이 생긴다");
        }

        [Test]
        public void Path_BreaksInOnlyWhenThereIsNoWayAround()
        {
            NavGrid grid = Empty();
            // (10,10)을 부술 수 있는 벽으로 완전히 두른다 — 요새 안에 든 쥐
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    grid.AddStructureCell(10 + dx, 10 + dz, NavStructureKind.CatBreakable);
                }
            }

            var finder = new GridPathfinder(grid);
            var cells = new List<int>();

            Assert.IsTrue(finder.TryFindPath(grid.ToWorld(2, 10), grid.ToWorld(10, 10),
                NavAgent.Cat, R_CAT, null, 0UL, cells));
            Assert.IsTrue(finder.LastNeededBreaking, "우회로가 없으면 벽을 뚫는 경로가 나온다");

            // 쥐는 같은 벽을 못 부순다 — 길이 아예 없다
            Assert.IsFalse(finder.TryFindPath(grid.ToWorld(2, 10), grid.ToWorld(10, 10),
                NavAgent.Mouse, R_MOUSE, null, 0UL, cells));
        }

        [Test]
        public void Path_FailsWhenTerrainSealsTheGoal()
        {
            var grid = new NavGrid(Vector2.zero, 20, 20);
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    grid.SetBlocked(10 + dx, 10 + dz, true);
                }
            }
            grid.BuildClearance();

            var finder = new GridPathfinder(grid);
            var cells = new List<int>();

            // 지형은 못 부순다 — 2차 탐색으로도 길이 없다
            Assert.IsFalse(finder.TryFindPath(grid.ToWorld(2, 2), grid.ToWorld(10, 10),
                NavAgent.Cat, R_CAT, null, 0UL, cells));
        }

        [Test]
        public void Diagonal_DoesNotCutThroughAWallCorner()
        {
            var grid = new NavGrid(Vector2.zero, 20, 20);
            // (5,5)로 가는 유일한 사선을 양옆으로 막는다 — 대각선 검사(53p)가 이걸 거른다
            grid.SetBlocked(5, 4, true);
            grid.SetBlocked(4, 5, true);
            for (int z = 0; z < 20; z++)
            {
                if (z == 5) continue;
                grid.SetBlocked(5, z, true);
            }
            for (int x = 0; x < 20; x++)
            {
                if (x == 5) continue;
                grid.SetBlocked(x, 5, true);
            }
            grid.BuildClearance();

            var finder = new GridPathfinder(grid);
            var cells = new List<int>();

            // (4,4)에서 (5,5)로 가는 길은 대각선뿐인데 양옆이 막혔다 → 못 간다
            Assert.IsFalse(finder.TryFindPath(grid.ToWorld(4, 4), grid.ToWorld(5, 5),
                NavAgent.Mouse, R_MOUSE, null, 0UL, cells));
        }

        // ---- 노드 예약(앨리스 19~20p) ----

        [Test]
        public void Reservations_CountPerCellAndUnwindByOwner()
        {
            var res = new NavCellReservations();
            res.Claim(1UL, new List<int> { 10, 11, 12 });
            res.Claim(2UL, new List<int> { 11, 12, 13 });

            Assert.AreEqual(1, res.CountAt(10));
            Assert.AreEqual(2, res.CountAt(11));
            Assert.AreEqual(1, res.CountAt(13));

            res.Release(1UL);
            Assert.AreEqual(0, res.CountAt(10));
            Assert.AreEqual(1, res.CountAt(11));
        }

        [Test]
        public void Reservations_ReclaimReplacesTheOldPathNotAddsToIt()
        {
            var res = new NavCellReservations();
            res.Claim(1UL, new List<int> { 10, 11 });
            res.Claim(1UL, new List<int> { 20, 21 });

            Assert.AreEqual(0, res.CountAt(10), "옛 경로는 놓는다");
            Assert.AreEqual(1, res.CountAt(20));
            Assert.AreEqual(2, res.CellCount);
        }

        [Test]
        public void Reservations_PushTheSecondBotOntoAnotherRoute()
        {
            NavGrid grid = Empty();
            // 폭 3칸짜리 복도 둘 — 위(z=10)와 아래(z=5)가 나란히 뚫려 있다
            for (int x = 3; x <= 16; x++)
            {
                for (int z = 0; z < 20; z++)
                {
                    if (z is >= 4 and <= 6 or >= 9 and <= 11) continue;
                    grid.SetBlocked(x, z, true);
                }
            }
            grid.BuildClearance();

            var finder = new GridPathfinder(grid);
            var res = new NavCellReservations();
            var first = new List<int>();
            var second = new List<int>();

            Assert.IsTrue(finder.TryFindPath(grid.ToWorld(1, 8), grid.ToWorld(18, 8),
                NavAgent.Mouse, R_MOUSE, res, 1UL, first));

            // 첫 봇이 지나갈 셀을 예약하면 둘째는 다른 복도가 싸진다.
            // **복도 폭 전체를 잡는다** — 한 줄만 잡으면 옆줄로 비켜 가면 그만이라 흩어지지 않는다.
            var claimed = new List<int>();
            for (int x = 3; x <= 16; x++)
            {
                for (int z = 9; z <= 11; z++) claimed.Add(grid.Index(x, z));
            }
            res.Claim(1UL, claimed);

            Assert.IsTrue(finder.TryFindPath(grid.ToWorld(1, 8), grid.ToWorld(18, 8),
                NavAgent.Mouse, R_MOUSE, res, 2UL, second));

            bool usedUpper = false;
            foreach (int cell in second)
            {
                if (cell / grid.Width >= 9) usedUpper = true;
            }
            Assert.IsFalse(usedUpper, "예약된 위 복도를 피해 아래로 돈다");
        }
    }
}
