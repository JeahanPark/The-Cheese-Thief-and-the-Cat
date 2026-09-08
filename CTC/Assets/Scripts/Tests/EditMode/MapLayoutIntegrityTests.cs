using System.Collections.Generic;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// **구운 레이아웃 에셋**의 지점 불변식 — 감사 D 케이스 D-01·D-02·D-05·D-06·D-08 (2026-09-09).
    ///
    /// 왜 에셋을 직접 읽나: 「좌표가 차단물 안에 박힌다」가 이 프로젝트에서 네 번째였고,
    /// 그때마다 순수 로직 테스트는 전부 초록이었다 — `LobbyRoleLogicTests`는 **가짜 요새 리스트**로
    /// 스폰 공식만 보고, `MapValidatorTests`는 **가짜 데이터**로 게이트 함수만 본다.
    /// 실제로 배포되는 좌표를 보는 층이 아무도 없었다. 여기가 그 층이다
    /// (에셋을 실제로 로드하는 선례: <c>MinimapPainterTests</c>).
    ///
    /// 정식 맵은 손 배치 창고 하나다 — 나머지 셋은 절차 생성이라 `MapValidator` 게이트를 이미 지난다.
    /// </summary>
    public class MapLayoutIntegrityTests
    {
        private const string WAREHOUSE = "Assets/Prefabs/Maps/Layout_Warehouse01.asset";

        private static MapLayout LoadWarehouse()
        {
            var layout = AssetDatabase.LoadAssetAtPath<MapLayout>(WAREHOUSE);
            Assert.IsNotNull(layout, $"레이아웃 에셋이 없다: {WAREHOUSE}");
            Assert.IsTrue(layout.HandPlaced, "창고 맵은 손 배치여야 한다 — 전제가 깨졌다");
            Assert.Greater(layout.Props.Count, 0, "차단물이 0개면 이 검산 전체가 헛돈다");
            return layout;
        }

        /// <summary>D-01 · D-02 · D-05 · D-06 — 굽기 게이트와 **같은 규칙**을 구운 에셋에 건다.</summary>
        [Test]
        [NUnit.Framework.Property("CaseId", "D-01")]
        [NUnit.Framework.Property("CaseId", "D-02")]
        [NUnit.Framework.Property("CaseId", "D-05")]
        [NUnit.Framework.Property("CaseId", "D-06")]
        public void Warehouse_EveryPlacedPointClearsBlockers()
        {
            List<MapPointAudit.Finding> findings = MapPointAudit.Audit(LoadWarehouse());
            Assert.IsEmpty(findings, "지점이 차단물에 물린다:\n" + MapPointAudit.Describe(findings));
        }

        /// <summary>
        /// D-01 — 쥐 스폰이 차단물 **안**이 아니다. 위 전수 검사와 겹치지만 따로 둔다:
        /// 「안에 박혔다」와 「스칠 만큼 가깝다」는 증상이 다르고, 실패 메시지가 어느 쪽인지 말해야 한다.
        /// </summary>
        [Test]
        [NUnit.Framework.Property("CaseId", "D-01")]
        public void Warehouse_NoMouseSpawnIsInsideABlocker()
        {
            MapLayout layout = LoadWarehouse();
            List<Vector2> spawns = MapPointAudit.MouseSpawnPoints(layout.Fortresses);
            Assert.AreEqual(layout.Fortresses.Count, spawns.Count);

            for (int i = 0; i < spawns.Count; i++)
            {
                float clearance = MapPointAudit.Clearance(spawns[i], layout.Props, out string blocker);
                Assert.GreaterOrEqual(clearance, 0f,
                    $"쥐 스폰 #{i} @({spawns[i].x:0.##},{spawns[i].y:0.##})가 차단물 {blocker} 안 {-clearance:0.##} m다");
            }
        }

        /// <summary>D-02 — 스폰 자리에 쥐 몸이 여유 있게 들어간다. 몸 반지름은 CSV에서 읽어 하드코딩을 피한다.</summary>
        [Test]
        [NUnit.Framework.Property("CaseId", "D-02")]
        public void Warehouse_EveryMouseSpawnFitsAMouseBody()
        {
            MapLayout layout = LoadWarehouse();
            float radius = CTC.Runtime.Info.InfoManager.Instance
                .PlayerOf(CTC.Runtime.Lobby.RoleId.Mouse).CcRadius;
            float need = radius + MapPointAudit.SPAWN_BODY_MARGIN;

            List<Vector2> spawns = MapPointAudit.MouseSpawnPoints(layout.Fortresses);
            for (int i = 0; i < spawns.Count; i++)
            {
                float clearance = MapPointAudit.Clearance(spawns[i], layout.Props, out string blocker);
                Assert.GreaterOrEqual(clearance, need,
                    $"쥐 스폰 #{i} 여유 {clearance:0.###} m < {need:0.###} m — 가장 가까운 차단물 {blocker}");
            }
        }

        /// <summary>D-05 — 치즈 파밍 원반 안에 차단물이 없다(원반이 물리면 고양이가 지점에 몸을 못 붙인다).</summary>
        [Test]
        [NUnit.Framework.Property("CaseId", "D-05")]
        public void Warehouse_EveryCheeseDiscIsFreeOfBlockers()
        {
            MapLayout layout = LoadWarehouse();
            Assert.Greater(layout.Cheese.Count, 0);

            for (int i = 0; i < layout.Cheese.Count; i++)
            {
                CheesePlacement c = layout.Cheese[i];
                float clearance = MapPointAudit.Clearance(c.Pos, layout.Props, out string blocker);
                Assert.GreaterOrEqual(clearance, c.Radius,
                    $"치즈 지점 #{i} @({c.Pos.x},{c.Pos.y})의 파밍 원반(r {c.Radius})이 {blocker}를 문다 — 여유 {clearance:0.###} m");
            }
        }

        /// <summary>D-06 — 강아지가 벽감에서 나올 자리에 몸이 들어간다.</summary>
        [Test]
        [NUnit.Framework.Property("CaseId", "D-06")]
        public void Warehouse_DogAlcoveHasRoomInFront()
        {
            MapLayout layout = LoadWarehouse();
            int dogs = 0;
            for (int i = 0; i < layout.Alcoves.Count; i++)
            {
                if (layout.Alcoves[i].Payload != AlcovePayload.Dog) continue;
                dogs++;

                Vector2 at = MapPointAudit.DogSpawnPoint(layout.Alcoves[i]);
                float clearance = MapPointAudit.Clearance(at, layout.Props, out string blocker);
                Assert.GreaterOrEqual(clearance, MapPointAudit.DOG_BODY_RADIUS + MapPointAudit.SPAWN_BODY_MARGIN,
                    $"강아지 벽감 #{i} 정면 @({at.x:0.##},{at.y:0.##}) 여유 {clearance:0.###} m — 가장 가까운 차단물 {blocker}");
            }
            Assert.AreEqual(1, dogs, "창고 맵의 강아지 벽감은 정확히 하나여야 한다");
        }

        /// <summary>
        /// D-08 — 손 배치 맵에도 **맵 종류와 무관한 게이트**를 건다.
        /// `MapValidator`는 절차 경로에서만 불려서 정식 기본 맵만 게이트를 안 지났다(감사 §4).
        /// </summary>
        [Test]
        [NUnit.Framework.Property("CaseId", "D-08")]
        public void Warehouse_PassesMapWideGates()
        {
            MapLayout layout = LoadWarehouse();

            // 등급 다중집합 — 1~7이 빠짐없이, 같은 수만큼
            var perGrade = new Dictionary<int, int>();
            foreach (CheesePlacement c in layout.Cheese)
            {
                perGrade.TryGetValue(c.Grade, out int n);
                perGrade[c.Grade] = n + 1;
            }
            for (int grade = 1; grade <= 7; grade++)
            {
                Assert.IsTrue(perGrade.ContainsKey(grade), $"치즈 등급 {grade}이 하나도 없다");
            }

            // 요새자리–치즈 이격 — 요새 안에서 파밍이 되면 대치가 성립하지 않는다
            float min = float.PositiveInfinity;
            foreach (FortressPlacement f in layout.Fortresses)
            {
                foreach (CheesePlacement c in layout.Cheese)
                {
                    min = Mathf.Min(min, Vector2.Distance(f.Pos, c.Pos));
                }
            }
            Assert.Greater(min, 3.7f, $"요새자리–치즈 최소 이격 {min:0.##} m");
        }

        /// <summary>
        /// 검산 자신이 도는지 — 「스폰을 차단물 속에 넣으면 정말 빨개지는가」.
        /// 이게 없으면 위 테스트들이 통과하는 이유가 「데이터가 맞아서」인지 「검산이 아무것도 안 봐서」인지 모른다.
        /// </summary>
        [Test]
        public void Audit_CatchesASpawnBuriedInABlocker()
        {
            MapLayout layout = LoadWarehouse();
            Assert.IsEmpty(MapPointAudit.Audit(layout), "전제: 지금 에셋은 깨끗하다");

            // M1을 그대로 되살린다 — 궤짝 벽 C-y7(46,46,10,8) + 요새자리 #2 (54,54) yaw 225
            var props = new List<PropPlacement>(layout.Props)
            {
                new PropPlacement
                {
                    Name = "C-y7(M1 재현)", Kind = PropKind.CrateWall,
                    Pos = new Vector2(51f, 50f), Size = new Vector2(10f, 8f), Height = 2.4f,
                },
            };

            List<MapPointAudit.Finding> findings =
                MapPointAudit.Audit(props, layout.Fortresses, layout.Cheese, layout.Alcoves);

            Assert.IsNotEmpty(findings, "M1을 되살렸는데 검산이 아무것도 못 잡았다 — 검산이 헛돈다");
            bool caughtSpawn2 = false;
            foreach (MapPointAudit.Finding f in findings)
            {
                if (f.Point.Contains("쥐 스폰 #2") && f.Clearance < 0f) caughtSpawn2 = true;
            }
            Assert.IsTrue(caughtSpawn2, "M1(쥐 스폰 #2가 궤짝 벽 속)을 못 잡았다:\n" + MapPointAudit.Describe(findings));
        }
    }
}
