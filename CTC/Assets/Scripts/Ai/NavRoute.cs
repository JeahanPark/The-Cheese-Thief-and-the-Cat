using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// **구운 폴리곤 위로 길을 찾는다** — 강아지와 봇이 같이 쓴다 (2026-08-12).
    ///
    /// 그전에는 방 중심·모서리·문에 점을 찍어 이은 웨이포인트 그래프였다. 점만 아니까
    /// 목표가 점이 아닐 때(움직이는 쥐·소리가 난 자리) **마지막 한 구간을 검사 없이 직선**으로
    /// 때울 수밖에 없었고, 그 직선에 기둥이나 쥐가 판 중에 지은 창고가 있으면 답이 없었다.
    /// 그 구멍을 메우려고 모서리 링·릴레이 노드·벽 타기를 차례로 붙였는데, 전부 땜질이었다.
    ///
    /// 폴리곤은 걸을 수 있는 바닥 **전체**를 덮으므로 그런 구멍이 없다. 바닥 위 아무 좌표나
    /// 물어봐도 정확한 코너 목록이 나오고, 뚫려 있으면 코너가 하나(= 곧장 가라)로 나온다.
    /// 판 중에 생기는 구조물은 `NavMeshObstacle`이 구멍을 파므로 다시 구울 필요도 없다.
    ///
    /// **`NavMeshAgent`는 쓰지 않는다.** 그건 오브젝트를 직접 옮겨 이속·잠행·발소리가 사람과
    /// 다른 코드를 타게 만든다 — 그 순간 봇으로 잰 밸런스가 뜻을 잃는다. 길 계산만 쓰고,
    /// 방향은 부르는 쪽이 사람과 같은 입구로 넣는다(강아지 `CC.Move` · 봇 `DevSetMoveInput`).
    /// </summary>
    public class NavRoute
    {
        /// <summary>이만큼 가까워지면 그 코너는 지난 것으로 본다. 너무 작으면 모퉁이에서 맴돈다.</summary>
        public const float CORNER_REACH = 0.8f;

        /// <summary>목표에 이만큼 붙으면 도착. 0으로 두면 목표를 밀고 다닌다.</summary>
        public const float GOAL_REACH = 0.6f;

        /// <summary>목표가 이만큼 움직여야 다시 짠다. 매 틱 짜면 코너 진행이 리셋된다.</summary>
        private const float REPLAN_DISTANCE = 1.5f;

        /// <summary>
        /// 다시 짜는 최소 간격. 두뇌는 매 틱 목표를 넣는다.
        ///
        /// **카운터가 아니라 시계로 잰다**(2026-08-12). 카운터를 <see cref="TryGetDirection"/>
        /// 안에서 깎았더니, 목표에 도착해 갈 데가 없어진 순간 그 함수가 맨 앞에서 돌아가
        /// 카운터가 영영 안 깎였다. 그러면 <see cref="SetGoal"/>이 "방금 짰다"며 계속 안 짜고
        /// 봇이 그 자리에 선다 — 소리를 조사하러 가는 고양이는 경로가 짧아 늘 이 덫에 걸렸다
        /// (실측: 고양이 이동 0.33m/s, 최고속은 7.8이다).
        /// </summary>
        private const float REPLAN_COOLDOWN_SECONDS = 0.25f;

        /// <summary>
        /// 좌표가 폴리곤 밖일 때 이 반경 안에서 가장 가까운 바닥으로 당긴다.
        /// 소리가 난 자리는 벽 속일 수도 있고, 쥐는 폴리곤 가장자리에 서 있을 수 있다.
        /// </summary>
        private const float SAMPLE_RADIUS = 5f;

        /// <summary>코너 버퍼. `NavMeshPath.corners`는 부를 때마다 배열을 새로 만든다.</summary>
        private const int MAX_CORNERS = 64;

        private readonly Transform m_Self;
        private readonly NavMeshPath m_Path = new NavMeshPath();
        private readonly Vector3[] m_Buffer = new Vector3[MAX_CORNERS];
        private readonly List<Vector3> m_Corners = new List<Vector3>();

        private int m_Index;
        private bool m_HasGoal;
        private bool m_GaveUp;
        private float m_NextPlanTime;
        private Vector3 m_Goal;          // 지금 향하는 곳 (쥐라면 매 틱 움직인다)
        private Vector3 m_PlannedGoal;   // 지금 들고 있는 경로를 짤 때의 곳

        public NavRoute(Transform _self)
        {
            m_Self = _self;
        }

        /// <summary>지금 갈 데가 있는가.</summary>
        public bool HasGoal => m_HasGoal;

        /// <summary>남은 코너가 하나뿐 = 목표까지 곧장 간다(중간에 아무것도 없다).</summary>
        public bool IsStraight => m_HasGoal && m_Corners.Count - m_Index <= 1;

        public Vector3 Goal => m_Goal;

        /// <summary>QA — 어느 층에서 끊겼는지 한 줄로.</summary>
        public string Diagnostics =>
            $"{(IsStraight ? "straight" : "path")} corners={m_Corners.Count} idx={m_Index} " +
            $"goal={m_Goal.x:F0},{m_Goal.z:F0} " +
            $"wp={(m_Index < m_Corners.Count ? $"{m_Corners[m_Index].x:F0},{m_Corners[m_Index].z:F0}" : "-")}";

        /// <summary>
        /// **여기는 도저히 못 가겠다**를 한 번만 알려준다(읽으면 지워진다).
        /// 두뇌가 이걸 받으면 그 목표를 버려야 한다 — 안 버리면 같은 곳을 다시 준다.
        /// </summary>
        public bool ConsumeGaveUp()
        {
            if (!m_GaveUp) return false;
            m_GaveUp = false;
            return true;
        }

        /// <summary>여기로 가라. 목표가 조금 움직인 정도면 들고 있던 경로를 그대로 쓴다.</summary>
        public void SetGoal(Vector3 _goal)
        {
            m_Goal = _goal;

            if (m_HasGoal && m_Index < m_Corners.Count
                && Flat(_goal - m_PlannedGoal).magnitude < REPLAN_DISTANCE)
            {
                return;
            }

            if (Time.time < m_NextPlanTime)
            {
                m_HasGoal = true;
                return;
            }

            m_HasGoal = true;
            Replan();
        }

        /// <summary>멈춘다. 파밍처럼 제자리에 있어야 하는 동안 쓴다.</summary>
        public void Clear()
        {
            m_HasGoal = false;
            m_Corners.Clear();
            m_Index = 0;
        }

        /// <summary>
        /// 제자리걸음일 때 부른다. 폴리곤 위를 걷는데도 못 나아간다면 남(다른 몸)에 막힌 것이다 —
        /// 폴리곤은 지형만 알고 서로의 몸은 모른다. 코너를 하나 건너뛰고 다시 짠다.
        /// </summary>
        public void OnStuck()
        {
            m_Index++;
            if (m_Index >= m_Corners.Count) Replan();
        }

        /// <summary>매 틱. 갈 방향(수평 단위벡터)을 돌려준다. 도착했거나 갈 데가 없으면 false.</summary>
        public bool TryGetDirection(out Vector3 _dir)
        {
            _dir = Vector3.zero;
            if (!m_HasGoal || m_Self == null) return false;

            Vector3 pos = m_Self.position;

            while (m_Index < m_Corners.Count)
            {
                Vector3 delta = Flat(m_Corners[m_Index] - pos);
                // 마지막 코너 = 목표다. 중간 코너는 스치듯 지나가야 모퉁이에서 안 맴돈다.
                float reach = m_Index == m_Corners.Count - 1 ? GOAL_REACH : CORNER_REACH;
                if (delta.magnitude > reach)
                {
                    _dir = delta.normalized;
                    return true;
                }
                m_Index++;
            }

            m_HasGoal = false;
            return false;
        }

        /// <summary>
        /// 폴리곤 위의 아무 자리. 배회 목적지를 고를 때 쓴다 —
        /// 그냥 무작위 좌표를 잡으면 벽 속이 나온다.
        /// </summary>
        public static bool TryRandomPoint(
            Vector3 _center, float _radius, System.Random _rng, out Vector3 _point)
        {
            _point = _center;
            if (_rng == null) return false;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = (float)(_rng.NextDouble() * System.Math.PI * 2.0);
                float distance = (float)(_rng.NextDouble()) * _radius;
                Vector3 candidate = _center
                    + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, SAMPLE_RADIUS, NavMesh.AllAreas))
                {
                    continue;
                }

                _point = hit.position;
                return true;
            }
            return false;
        }

        private void Replan()
        {
            m_Corners.Clear();
            m_Index = 0;
            m_PlannedGoal = m_Goal;
            m_NextPlanTime = Time.time + REPLAN_COOLDOWN_SECONDS;
            if (m_Self == null) return;

            // 양 끝을 폴리곤 위로 당긴다 — 소리가 난 자리는 벽 속일 수도 있다
            if (!NavMesh.SamplePosition(m_Self.position, out NavMeshHit from, SAMPLE_RADIUS, NavMesh.AllAreas)
                || !TrySampleGoal(m_Self.position, out Vector3 to))
            {
                GiveUp();
                return;
            }

            if (!NavMesh.CalculatePath(from.position, to, NavMesh.AllAreas, m_Path)
                || m_Path.status == NavMeshPathStatus.PathInvalid)
            {
                GiveUp();
                return;
            }

            // 0번은 내가 선 자리다. 나머지가 실제로 밟을 코너 — 뚫려 있으면 목표 하나뿐이다.
            int count = m_Path.GetCornersNonAlloc(m_Buffer);
            for (int i = 1; i < count; i++) m_Corners.Add(m_Buffer[i]);

            if (m_Corners.Count == 0) m_HasGoal = false;   // 이미 그 자리다
        }

        /// <summary>
        /// 목표를 폴리곤 위로 당긴다. **바로 옆에 폴리곤이 없으면 나 있는 쪽으로 당겨 가며** 찾는다
        /// (2026-08-12).
        ///
        /// 두뇌가 주는 목표는 늘 바닥 위가 아니다 — 도망치는 쥐는 "위협 반대쪽 18m"를 잡는데
        /// 그게 맵 밖일 수 있다(실측: 쥐 하나가 z=67을 잡았고 맵 경계는 60이었다).
        /// 그 자리를 못 찾으면 봇이 그냥 서 버린다. 나한테로 당기면 그 방향의 갈 수 있는
        /// 가장 먼 자리가 나온다 — 도망치려던 의도가 그대로 산다.
        /// </summary>
        private bool TrySampleGoal(Vector3 _from, out Vector3 _point)
        {
            const int STEPS = 5;
            for (int i = 0; i <= STEPS; i++)
            {
                Vector3 candidate = Vector3.Lerp(m_Goal, _from, i / (float)STEPS);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, SAMPLE_RADIUS, NavMesh.AllAreas))
                {
                    continue;
                }

                _point = hit.position;
                return true;
            }

            _point = _from;
            return false;
        }

        // 폴리곤 밖이거나 길이 아예 없다 — 두뇌가 다른 목표를 잡아야 한다.
        private void GiveUp()
        {
            m_GaveUp = true;
            m_HasGoal = false;
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v;
        }
    }
}
