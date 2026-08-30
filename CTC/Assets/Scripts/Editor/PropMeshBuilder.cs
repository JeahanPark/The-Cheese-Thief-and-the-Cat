using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 요새를 뺀 나머지 프롭 메시를 한 번에 굽는다 — Tools > CTC > Build Prop Meshes.
    /// 창고(치즈 바퀴) · 벽감 패널 · 상자 · 직업권.
    ///
    /// **치수의 주인은 프리팹의 콜라이더다.** 여기서 굽는 건 보이는 것뿐이라, 아래 상수는
    /// 팩토리가 쓰는 `localScale`을 그대로 옮겨 적은 값이다 — 하나라도 어긋나면
    /// 에러 없이 그림과 몸이 따로 논다.
    ///
    /// 하나의 메뉴로 묶은 이유: 넷 다 같은 거리장 도구를 쓰고, 톤(둥근 저폴리)이 한 벌로 맞아야
    /// 한다. 따로 굽게 두면 한쪽만 다시 굽고 나머지가 옛 톤으로 남는다.
    /// </summary>
    public static class PropMeshBuilder
    {
        // ── 창고: 치즈 바퀴 (프리팹 Body 스케일 1 × 0.8 × 1) ────────────
        private const float WHEEL_RADIUS = 0.5f;
        private const float WHEEL_HEIGHT = 0.8f;
        private const float WHEEL_ROUND = 0.06f;

        /// <summary>떼어 낸 조각의 반각. "여기서 치즈가 나간다"가 실루엣만으로 읽혀야 한다.</summary>
        private const float WHEEL_SLICE_HALF_ANGLE = 27f;

        private const float WHEEL_SLICE_FILLET = 0.045f;
        private const float WHEEL_CELL = 0.028f;

        // ── 벽감 패널 (프리팹 Body 스케일 1.2 × 1.0 × 0.4, 중심 y 0.5) ──
        private const float PANEL_WIDTH = 1.2f;
        private const float PANEL_HEIGHT = 1.0f;
        private const float PANEL_DEPTH = 0.4f;
        private const float PANEL_ROUND = 0.05f;

        /// <summary>벽돌 줄눈. 깊이가 격자 두 칸은 되어야 파인 게 읽힌다 — 셀을 키우면 여기도 키운다.</summary>
        private const float JOINT_DEPTH = 0.06f;

        private const float JOINT_HALF_WIDTH = 0.035f;
        private const float PANEL_CELL = 0.028f;

        // ── 상자 (프리팹 Body 스케일 0.7 균등) ──────────────────────────
        /// <summary>
        /// **단위 크기(±0.5)로 굽는다.** 상자는 균등 스케일(0.7)이라 늘어날 걱정이 없고,
        /// 이러면 프리미티브 큐브의 메시만 갈아 끼우면 되어 BoxCollider가 손도 안 닿는다.
        /// </summary>
        private const float CRATE_HALF = 0.5f;

        private const float CRATE_ROUND = 0.07f;
        private const float PLANK_DEPTH = 0.075f;
        private const float PLANK_HALF_WIDTH = 0.045f;
        private const float CRATE_CELL = 0.032f;

        // ── 직업권 (프리팹 Body 스케일 0.35 × 0.5 × 0.08) ───────────────
        private const float TICKET_HALF_W = 0.175f;
        private const float TICKET_HALF_H = 0.25f;
        private const float TICKET_HALF_D = 0.04f;
        private const float TICKET_ROUND = 0.028f;
        private const float TICKET_HOLE_RADIUS = 0.028f;
        private const float TICKET_HOLE_Y = 0.185f;
        private const float TICKET_CELL = 0.012f;

        // ── 색 ──────────────────────────────────────────────────────────
        /// <summary>벽감은 맵의 벽이지 치즈가 아니다 — 흙빛으로 낮춰야 노란 치즈가 튄다(#5E4C3B).</summary>
        private static readonly Color PANEL_COLOR = new Color(0.369f, 0.298f, 0.231f);

        /// <summary>상자는 나무(#A0703C). 벽감보다 밝아 "부술 것"과 "열 것"이 갈린다.</summary>
        private static readonly Color CRATE_COLOR = new Color(0.627f, 0.439f, 0.235f);

        // 직업권 두 장은 색으로만 갈린다 — 용사는 파랑, 깡패는 보라(기존 배선과 같은 약속).
        private static readonly Color HERO_COLOR = new Color(0.298f, 0.525f, 0.847f);
        private static readonly Color GANG_COLOR = new Color(0.541f, 0.361f, 0.784f);

        private const float PROP_SMOOTHNESS = 0.12f;

        // ── 공통 격자 설정 ──────────────────────────────────────────────
        private const int RELAX_ITERATIONS = 6;
        private const float RELAX_STRENGTH = 0.5f;
        private const int NORMAL_SMOOTH_ITERATIONS = 3;
        private const float GRID_PADDING = 0.08f;

        [MenuItem("Tools/CTC/Build Prop Meshes")]
        public static void Build()
        {
            Mesh warehouse = BuildWarehouse();
            Mesh panel = BuildAlcovePanel();
            Mesh crate = BuildCrate();
            Mesh ticket = BuildTicket();

            SdfMesher.SaveMaterial(PropAssets.ALCOVE_MATERIAL, "M_AlcovePanel", PANEL_COLOR, PROP_SMOOTHNESS);
            SdfMesher.SaveMaterial(PropAssets.CRATE_MATERIAL, "M_LootCrate", CRATE_COLOR, PROP_SMOOTHNESS);
            SdfMesher.SaveMaterial(PropAssets.HERO_TICKET_MATERIAL, "M_HeroTicket", HERO_COLOR, 0.25f);
            SdfMesher.SaveMaterial(PropAssets.GANG_TICKET_MATERIAL, "M_GangTicket", GANG_COLOR, 0.25f);

            SdfMesher.Commit(PropAssets.WAREHOUSE_MESH, PropAssets.ALCOVE_MESH,
                PropAssets.CRATE_MESH, PropAssets.TICKET_MESH);

            Debug.Log("[PropMeshBuilder] 완료 — " +
                      $"창고 {Tris(warehouse)} / 벽감 {Tris(panel)} / 상자 {Tris(crate)} / 직업권 {Tris(ticket)} 삼각형");
        }

        private static int Tris(Mesh _mesh) => _mesh.triangles.Length / 3;

        // ── 창고: 한 조각 떼어낸 치즈 바퀴 ──────────────────────────────

        /// <summary>
        /// 창고는 **돈이 나오는 곳**이라 한눈에 치즈여야 한다. 큐브에 노란 칠을 하면 요새 벽과
        /// 구별이 안 됐다 — 바퀴에서 한 조각이 빠진 실루엣은 멀리서도 다르게 읽힌다.
        /// 조각을 뗀 단면에만 구멍을 판다(껍질 쪽은 원래 매끈하다).
        /// </summary>
        private static Mesh BuildWarehouse()
        {
            Vector4[] holes = SliceFaceHoles();

            var min = new Vector3(-WHEEL_RADIUS - GRID_PADDING, -GRID_PADDING, -WHEEL_RADIUS - GRID_PADDING);
            var max = new Vector3(WHEEL_RADIUS + GRID_PADDING, WHEEL_HEIGHT + GRID_PADDING, WHEEL_RADIUS + GRID_PADDING);

            Mesh built = SdfMesher.Build("Mesh_CheeseWarehouse", _p =>
            {
                float wheel = SdfMesher.RoundCylinderY(_p, new Vector3(0f, WHEEL_HEIGHT * 0.5f, 0f),
                    WHEEL_RADIUS, WHEEL_HEIGHT * 0.5f, WHEEL_ROUND);
                float slice = SdfMesher.SectorX(_p, WHEEL_SLICE_HALF_ANGLE);
                float d = SdfMesher.SmoothSubtract(wheel, slice, WHEEL_SLICE_FILLET);

                for (int i = 0; i < holes.Length; i++)
                {
                    float sphere = (_p - (Vector3)holes[i]).magnitude - holes[i].w;
                    if (-sphere > d) d = -sphere;
                }
                return d;
            }, min, max, WHEEL_CELL, RELAX_ITERATIONS, RELAX_STRENGTH, NORMAL_SMOOTH_ITERATIONS);

            return SdfMesher.SaveMesh(built, PropAssets.WAREHOUSE_MESH);
        }

        /// <summary>
        /// 떼어 낸 조각의 두 단면에 구멍을 박는다. 자리는 고정이다 — 단면이 좁아
        /// 난수로 흩으면 겹치거나 테두리를 물어 조각이 이빠진 것처럼 보였다.
        /// </summary>
        private static Vector4[] SliceFaceHoles()
        {
            float a = WHEEL_SLICE_HALF_ANGLE * Mathf.Deg2Rad;
            float sin = Mathf.Sin(a);
            float cos = Mathf.Cos(a);

            // 단면 A: 평면을 따라가는 방향과 그 바깥 법선(떼어 낸 쪽이 +normal)
            var alongA = new Vector3(cos, 0f, sin);
            var normalA = new Vector3(sin, 0f, -cos);
            var alongB = new Vector3(cos, 0f, -sin);
            var normalB = new Vector3(sin, 0f, cos);

            float[] radial = { 0.17f, 0.28f, 0.38f };
            float[] height = { 0.55f, 0.30f, 0.58f };
            float[] size = { 0.075f, 0.090f, 0.065f };

            var holes = new Vector4[radial.Length * 2];
            for (int i = 0; i < radial.Length; i++)
            {
                holes[i] = HoleOnPlane(alongA, normalA, radial[i], height[i], size[i]);
                // 반대 단면은 높이를 어긋내 둔다 — 같으면 조각이 통째로 뚫린 것처럼 보인다
                holes[radial.Length + i] = HoleOnPlane(alongB, normalB, radial[i], height[i] * 0.75f + 0.12f, size[i]);
            }
            return holes;
        }

        /// <summary>중심을 표면 바깥으로 (r - 깊이)만큼 띄워 얕게 판다 — 깊으면 조각이 뚫린다.</summary>
        private static Vector4 HoleOnPlane(Vector3 _along, Vector3 _normal, float _radial, float _height, float _radius)
        {
            Vector3 surface = _along * _radial + new Vector3(0f, _height, 0f);
            Vector3 center = surface + _normal * (_radius * 0.45f);
            return new Vector4(center.x, center.y, center.z, _radius);
        }

        // ── 벽감 패널 ───────────────────────────────────────────────────

        /// <summary>
        /// 벽감은 맵이 처음부터 들고 있는 **부술 수 있는 벽**이다. 그래서 치즈가 아니라 벽돌이다 —
        /// 색만 갈색인 매끈한 큐브는 "부술 수 있는 것"으로 안 읽혔다. 줄눈이 곧 힌트다.
        /// </summary>
        private static Mesh BuildAlcovePanel()
        {
            float halfW = PANEL_WIDTH * 0.5f;
            float halfD = PANEL_DEPTH * 0.5f;
            var center = new Vector3(0f, PANEL_HEIGHT * 0.5f, 0f);
            var half = new Vector3(halfW, PANEL_HEIGHT * 0.5f, halfD);

            // 벽돌 세 줄. 세로 줄눈은 줄마다 어긋나야 벽돌로 읽힌다(같은 자리면 격자무늬가 된다).
            float[] courseY = { PANEL_HEIGHT / 3f, PANEL_HEIGHT * 2f / 3f };
            float[][] verticalX =
            {
                new[] { -0.2f, 0.2f },
                new[] { -0.4f, 0f, 0.4f },
                new[] { -0.2f, 0.2f },
            };

            var min = new Vector3(-halfW - GRID_PADDING, -GRID_PADDING, -halfD - GRID_PADDING);
            var max = new Vector3(halfW + GRID_PADDING, PANEL_HEIGHT + GRID_PADDING, halfD + GRID_PADDING);

            Mesh built = SdfMesher.Build("Mesh_AlcovePanel", _p =>
            {
                float d = SdfMesher.RoundBox(_p, center, half, PANEL_ROUND);

                foreach (float y in courseY)
                {
                    // 앞뒤 면의 가로 줄눈 — 깊이만큼만 물게 판다(관통하면 패널이 세 조각 난다)
                    d = SdfMesher.Subtract(d, SdfMesher.Box(_p, new Vector3(0f, y, halfD),
                        new Vector3(halfW + 0.1f, JOINT_HALF_WIDTH, JOINT_DEPTH)));
                    d = SdfMesher.Subtract(d, SdfMesher.Box(_p, new Vector3(0f, y, -halfD),
                        new Vector3(halfW + 0.1f, JOINT_HALF_WIDTH, JOINT_DEPTH)));
                    // 옆면도 같은 높이에서 이어져야 벽돌이 판을 감고 도는 것으로 보인다
                    d = SdfMesher.Subtract(d, SdfMesher.Box(_p, new Vector3(halfW, y, 0f),
                        new Vector3(JOINT_DEPTH, JOINT_HALF_WIDTH, halfD + 0.1f)));
                    d = SdfMesher.Subtract(d, SdfMesher.Box(_p, new Vector3(-halfW, y, 0f),
                        new Vector3(JOINT_DEPTH, JOINT_HALF_WIDTH, halfD + 0.1f)));
                }

                for (int row = 0; row < verticalX.Length; row++)
                {
                    float rowCenter = PANEL_HEIGHT * (row + 0.5f) / 3f;
                    var jointHalf = new Vector3(JOINT_HALF_WIDTH, PANEL_HEIGHT / 6f, JOINT_DEPTH);
                    foreach (float x in verticalX[row])
                    {
                        d = SdfMesher.Subtract(d, SdfMesher.Box(_p, new Vector3(x, rowCenter, halfD), jointHalf));
                        d = SdfMesher.Subtract(d, SdfMesher.Box(_p, new Vector3(x, rowCenter, -halfD), jointHalf));
                    }
                }
                return d;
            }, min, max, PANEL_CELL, RELAX_ITERATIONS, RELAX_STRENGTH, NORMAL_SMOOTH_ITERATIONS);

            return SdfMesher.SaveMesh(built, PropAssets.ALCOVE_MESH);
        }

        // ── 상자 ────────────────────────────────────────────────────────

        /// <summary>
        /// 나무 궤짝. 판자 이음매를 옆면과 뚜껑에 파서 "열리는 것"으로 읽히게 한다.
        /// 단위 크기(±0.5)라 프리팹의 균등 스케일 0.7이 그대로 먹는다.
        /// </summary>
        private static Mesh BuildCrate()
        {
            float[] plankY = { -0.17f, 0.17f };

            float span = CRATE_HALF + GRID_PADDING;
            var min = new Vector3(-span, -span, -span);
            var max = new Vector3(span, span, span);

            Mesh built = SdfMesher.Build("Mesh_LootCrate", _p =>
            {
                float d = SdfMesher.RoundBox(_p, Vector3.zero, Vector3.one * CRATE_HALF, CRATE_ROUND);

                foreach (float y in plankY)
                {
                    d = SdfMesher.Subtract(d, SdfMesher.Box(_p, new Vector3(0f, y, CRATE_HALF),
                        new Vector3(CRATE_HALF + 0.1f, PLANK_HALF_WIDTH, PLANK_DEPTH)));
                    d = SdfMesher.Subtract(d, SdfMesher.Box(_p, new Vector3(0f, y, -CRATE_HALF),
                        new Vector3(CRATE_HALF + 0.1f, PLANK_HALF_WIDTH, PLANK_DEPTH)));
                    d = SdfMesher.Subtract(d, SdfMesher.Box(_p, new Vector3(CRATE_HALF, y, 0f),
                        new Vector3(PLANK_DEPTH, PLANK_HALF_WIDTH, CRATE_HALF + 0.1f)));
                    d = SdfMesher.Subtract(d, SdfMesher.Box(_p, new Vector3(-CRATE_HALF, y, 0f),
                        new Vector3(PLANK_DEPTH, PLANK_HALF_WIDTH, CRATE_HALF + 0.1f)));
                }

                // 뚜껑의 이음매 하나 — 위에서 내려다보는 3인칭 카메라가 제일 자주 보는 면이다
                d = SdfMesher.Subtract(d, SdfMesher.Box(_p, new Vector3(0f, CRATE_HALF, 0f),
                    new Vector3(CRATE_HALF + 0.1f, PLANK_DEPTH, PLANK_HALF_WIDTH)));
                return d;
            }, min, max, CRATE_CELL, RELAX_ITERATIONS, RELAX_STRENGTH, NORMAL_SMOOTH_ITERATIONS);

            return SdfMesher.SaveMesh(built, PropAssets.CRATE_MESH);
        }

        // ── 직업권 ──────────────────────────────────────────────────────

        /// <summary>
        /// 목에 거는 패. 앞뒤로 테두리를 남기고 안쪽을 얕게 파, 얇은 큐브가 아니라 **패**로 읽히게 한다.
        /// 위쪽 구멍은 끈 자리 — 실루엣만으로 "주워서 차는 물건"이 보인다.
        /// 메시는 하나고 용사·깡패는 머티리얼 색으로만 갈린다(기존 배선과 같은 약속).
        /// </summary>
        private static Mesh BuildTicket()
        {
            var min = new Vector3(-TICKET_HALF_W - GRID_PADDING, -TICKET_HALF_H - GRID_PADDING, -TICKET_HALF_D - GRID_PADDING);
            var max = new Vector3(TICKET_HALF_W + GRID_PADDING, TICKET_HALF_H + GRID_PADDING, TICKET_HALF_D + GRID_PADDING);

            Mesh built = SdfMesher.Build("Mesh_JobTicket", _p =>
            {
                float d = SdfMesher.RoundBox(_p, Vector3.zero,
                    new Vector3(TICKET_HALF_W, TICKET_HALF_H, TICKET_HALF_D), TICKET_ROUND);

                var insetHalf = new Vector3(TICKET_HALF_W - 0.055f, TICKET_HALF_H - 0.06f, 0.018f);
                d = SdfMesher.SmoothSubtract(d, SdfMesher.Box(_p, new Vector3(0f, -0.03f, TICKET_HALF_D), insetHalf), 0.012f);
                d = SdfMesher.SmoothSubtract(d, SdfMesher.Box(_p, new Vector3(0f, -0.03f, -TICKET_HALF_D), insetHalf), 0.012f);

                // 끈 구멍 — Z축 원기둥이 필요해 축을 바꿔 넣는다(CylinderY는 Y축 전용)
                float cord = SdfMesher.CylinderY(new Vector3(_p.x, _p.z, _p.y),
                    new Vector3(0f, 0f, TICKET_HOLE_Y), TICKET_HOLE_RADIUS, 0.2f);
                d = SdfMesher.Subtract(d, cord);
                return d;
            }, min, max, TICKET_CELL, RELAX_ITERATIONS, RELAX_STRENGTH, NORMAL_SMOOTH_ITERATIONS);

            return SdfMesher.SaveMesh(built, PropAssets.TICKET_MESH);
        }
    }
}
