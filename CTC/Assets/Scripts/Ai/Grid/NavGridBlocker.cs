using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// **판 중에 생긴 구조물이 격자에 자국을 남긴다**(2026-08-18) — 요새·창고·벽감·상자.
    /// 구조물 루트에 하나 붙어서 제 자식 콜라이더를 전부 찍는다.
    ///
    /// `NavMeshCarver`(폴리곤에 구멍 파기)를 대신한다. 차이가 핵심이다 —
    /// 카버는 "못 가는 곳"만 표현했지만, 격자에서는 **부술 수 있는 곳**과 **쥐만 지나는 문**을
    /// 따로 찍는다. 그래서 요새 안에 든 쥐를 고양이가 쫓아갈 수 있다(벽을 부수는 경로가 나온다).
    ///
    /// 켜질 때 찍고 꺼질 때 지운다 — 파괴 훅이 없던 카버와 달리, 요새가 부서지면 그 자리가 열린다.
    /// 겹쳐 선 요새는 참조 카운트로 하나씩 걷힌다(`NavGridStamp`).
    /// </summary>
    [DisallowMultipleComponent]
    public class NavGridBlocker : MonoBehaviour
    {
        [SerializeField] private NavStructureKind m_Kind = NavStructureKind.CatBreakable;

        /// <summary>0보다 크면 로컬 ±Z 면 가운데를 **쥐 전용 문**으로 찍는다(요새 입구).</summary>
        [SerializeField] private float m_DoorHalfWidth;

        /// <summary>
        /// 문 띠의 **깊이**(m) — 벽 두께가 아니라 **벽을 관통하는 터널 길이**다.
        /// 벽 자국은 "조금이라도 겹치면 찍는다"라 문 양옆 칸까지 벽으로 물든다. 문 띠가 벽 두께만큼만
        /// 얇으면 그 사이에 낀 한두 칸이 통행 불가로 남아 **쥐가 제 요새에 못 들어간다**
        /// (2026-08-18 실측: 쥐 경로 실패 · 포기 159/473). 안팎을 확실히 잇도록 넉넉히 잡는다.
        /// </summary>
        [SerializeField] private float m_DoorDepth = 1.2f;

        /// <summary>문이 뚫린 벽면까지의 거리(m) — 요새 외곽 반치수.</summary>
        [SerializeField] private float m_DoorDistance = 1.2f;

        private readonly NavGridStamp m_Stamp = new NavGridStamp();
        private bool m_Stamped;

        private void OnEnable() => Stamp();

        private void OnDisable() => Unstamp();

        private void OnDestroy() => Unstamp();

        private void Stamp()
        {
            if (m_Stamped || !NavGridService.Ready) return;
            NavGrid grid = NavGridService.Grid;

            foreach (BoxCollider box in GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger) continue;
                NavGridBaker.StampBox(grid, box, m_Kind, _terrain: false, m_Stamp);
            }

            if (m_DoorHalfWidth > 0f)
            {
                StampDoor(grid, +1f);
                StampDoor(grid, -1f);
            }

            m_Stamped = true;
        }

        // 요새 입구 — 벽 조각 사이의 틈이라 콜라이더가 없다. 그래서 좌표로 직접 찍는다.
        // 인방(0.75m)이 낮아 고양이는 못 지나가고 쥐만 지나간다 — 격자에는 높이가 없으니
        // 그 규칙을 '쥐 전용 문' 표시로 옮긴다.
        private void StampDoor(NavGrid _grid, float _side)
        {
            Vector3 center = transform.position + transform.forward * (_side * m_DoorDistance);
            float yaw = transform.rotation.eulerAngles.y * Mathf.Deg2Rad;
            float cos = Mathf.Cos(yaw), sin = Mathf.Sin(yaw);

            float spanX = Mathf.Abs(cos) * m_DoorHalfWidth + Mathf.Abs(sin) * m_DoorDepth * 0.5f;
            float spanZ = Mathf.Abs(sin) * m_DoorHalfWidth + Mathf.Abs(cos) * m_DoorDepth * 0.5f;

            _grid.ToCell(new Vector3(center.x - spanX, 0f, center.z - spanZ), out int minX, out int minZ);
            _grid.ToCell(new Vector3(center.x + spanX, 0f, center.z + spanZ), out int maxX, out int maxZ);

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 c = _grid.ToWorld(x, z);
                    float dx = c.x - center.x, dz = c.z - center.z;
                    float lx = dx * cos - dz * sin;
                    float lz = dx * sin + dz * cos;
                    // **문은 셀 중심이 개구부 안에 들어올 때만 찍는다**(2026-08-18 리뷰).
                    // 막는 자국은 "조금이라도 겹치면 찍는다"가 안전하지만, 문은 반대로 **통행 허가**라
                    // 반 칸(0.25m)만 번져도 실체 벽 조각이 쥐 통행로로 열린다.
                    if (Mathf.Abs(lx) > m_DoorHalfWidth) continue;
                    if (Mathf.Abs(lz) > m_DoorDepth * 0.5f) continue;
                    m_Stamp.Add(x, z, NavStructureKind.MouseDoor);
                }
            }
        }

        private void Unstamp()
        {
            if (!m_Stamped) return;
            m_Stamp.Undo();
            m_Stamped = false;
        }
    }
}
