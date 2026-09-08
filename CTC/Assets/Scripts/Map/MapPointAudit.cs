using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// **구운 레이아웃의 모든 지점이 차단물 밖에 있는가**를 격자 없이 기하로 검산한다 (2026-09-09, 감사 D-01~D-06).
    ///
    /// 왜 있나: 「좌표가 차단물 안에 박힌다」가 이 프로젝트에서 **네 번째**였다.
    /// 굽기 검사는 ①프롭 vs 경계벽 ②상점 원반 ③상점 간격 셋뿐이라 **스폰·치즈·벽감이 무검사**였고,
    /// 그 구멍으로 쥐 스폰 #2가 궤짝 벽 `C-y7` 속 1.27 m에 들어앉았다(감사 M1).
    /// `MapValidator`는 절차 맵에만 도는데 정식 기본 맵은 손 배치라 게이트를 통째로 안 지났다.
    ///
    /// **한 벌만 둔다.** `WarehouseLayoutBuilder`가 굽기 전에 부르고(어기면 안 굽는다),
    /// `MapLayoutIntegrityTests`가 디스크의 에셋에 대고 다시 부른다 — 검사와 게이트가 갈라지지 않게.
    ///
    /// 순수 함수다: 프롭 박스는 축 정렬 AABB(굽기가 실제로 그렇게 세운다), 여유는 박스 면까지의 거리다.
    /// 격자(0.5 m)보다 촘촘해 양자화 오차가 없다 — 격자 쪽 검증은 `NavGridTests`가 따로 본다.
    /// </summary>
    public static class MapPointAudit
    {
        /// <summary>몸 반지름 위에 더 요구하는 여유(m). 벽에 스치듯 서면 CC가 밀려나며 첫 걸음이 튄다.</summary>
        public const float SPAWN_BODY_MARGIN = 0.3f;

        /// <summary>강아지 몸 반지름(m) — `Assets/Prefabs/Dog.prefab`의 CharacterController. 밸런스가 아니라 몸 치수라 CSV가 아니다.</summary>
        public const float DOG_BODY_RADIUS = 0.35f;

        /// <summary>검산에 걸린 지점 하나.</summary>
        public struct Finding
        {
            /// <summary>사람이 읽을 지점 이름(예: "쥐 스폰 #2").</summary>
            public string Point;
            public Vector2 At;
            /// <summary>가장 가까운 차단물 면까지의 거리(m). **음수면 차단물 안**이고 크기는 파고든 깊이다.</summary>
            public float Clearance;
            public float Required;
            /// <summary>가장 가까운 차단물의 이름.</summary>
            public string Blocker;

            public override string ToString() =>
                $"{Point} @({At.x:0.##},{At.y:0.##}) 여유 {Clearance:0.###} m < 필요 {Required:0.###} m — 가장 가까운 차단물 {Blocker}";
        }

        /// <summary>
        /// 한 점에서 가장 가까운 차단물 면까지의 거리. 박스 **안**이면 음수(파고든 깊이)다.
        /// 차단물이 하나도 없으면 <see cref="float.PositiveInfinity"/>.
        /// </summary>
        public static float Clearance(Vector2 _at, IReadOnlyList<PropPlacement> _props, out string _blocker)
        {
            _blocker = "(없음)";
            if (_props == null || _props.Count == 0) return float.PositiveInfinity;

            float best = float.PositiveInfinity;
            for (int i = 0; i < _props.Count; i++)
            {
                float d = SignedDistanceToBox(_at, _props[i].Pos, _props[i].Size);
                if (d >= best) continue;
                best = d;
                _blocker = _props[i].Name;
            }
            return best;
        }

        /// <summary>축 정렬 박스까지의 부호 있는 거리 — 밖이면 면까지의 거리, 안이면 −(가장 얕은 면까지의 깊이).</summary>
        private static float SignedDistanceToBox(Vector2 _at, Vector2 _center, Vector2 _size)
        {
            float dx = Mathf.Abs(_at.x - _center.x) - _size.x * 0.5f;
            float dz = Mathf.Abs(_at.y - _center.y) - _size.y * 0.5f;
            if (dx <= 0f && dz <= 0f) return Mathf.Max(dx, dz);   // 안 — 음수
            return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dz, 0f)).magnitude;
        }

        /// <summary>
        /// 쥐가 실제로 서는 자리(요새 입구 앞) — 첫 물결만 본다.
        /// **공식을 베끼지 않는다**: `PlayerSpawner.SpawnPosition`을 그대로 태워야,
        /// 공식이 바뀌면 검산도 따라 바뀐다(감사가 지적한 "공식만 보는 테스트"의 반대).
        /// </summary>
        public static List<Vector2> MouseSpawnPoints(IReadOnlyList<FortressPlacement> _forts)
        {
            var points = new List<Vector2>();
            if (_forts == null || _forts.Count == 0) return points;

            var spots = new List<(Vector3 pos, float yaw)>(_forts.Count);
            var roles = new List<Lobby.RoleId>(_forts.Count);
            for (int i = 0; i < _forts.Count; i++)
            {
                spots.Add((new Vector3(_forts[i].Pos.x, 0f, _forts[i].Pos.y), _forts[i].YawDeg));
                roles.Add(Lobby.RoleId.Mouse);
            }

            for (int i = 0; i < _forts.Count; i++)
            {
                Vector3 p = Lobby.PlayerSpawner.SpawnPosition(roles, i, spots);
                points.Add(new Vector2(p.x, p.z));
            }
            return points;
        }

        /// <summary>강아지가 벽감에서 튀어나오는 자리 — 벽 정면 <see cref="Alcove.DOG_SPAWN_OFFSET"/> m.</summary>
        public static Vector2 DogSpawnPoint(AlcovePlacement _alcove)
        {
            Vector3 forward = Quaternion.Euler(0f, _alcove.YawDeg, 0f) * Vector3.forward;
            return _alcove.Pos + new Vector2(forward.x, forward.z) * Alcove.DOG_SPAWN_OFFSET;
        }

        /// <summary>디스크의 레이아웃 에셋을 그대로 검산한다(EditMode 테스트용).</summary>
        public static List<Finding> Audit(MapLayout _layout)
        {
            if (_layout == null) return new List<Finding>();
            return Audit(_layout.Props, _layout.Fortresses, _layout.Cheese, _layout.Alcoves);
        }

        /// <summary>
        /// 굽기 직전의 목록을 검산한다(에셋이 되기 전에 막으려면 이쪽).
        /// 빈 목록이 나오면 통과다 — 하나라도 있으면 그 지점이 차단물에 물린다.
        /// </summary>
        public static List<Finding> Audit(
            IReadOnlyList<PropPlacement> _props,
            IReadOnlyList<FortressPlacement> _forts,
            IReadOnlyList<CheesePlacement> _cheese,
            IReadOnlyList<AlcovePlacement> _alcoves)
        {
            var findings = new List<Finding>();

            // D-01·D-02 — 쥐 스폰: 차단물 밖 + 몸이 들어갈 여유
            float mouseNeed = Info.InfoManager.Instance.PlayerOf(Lobby.RoleId.Mouse).CcRadius + SPAWN_BODY_MARGIN;
            List<Vector2> spawns = MouseSpawnPoints(_forts);
            for (int i = 0; i < spawns.Count; i++)
            {
                Check(findings, $"쥐 스폰 #{i}", spawns[i], mouseNeed, _props);
            }

            // D-05 — 치즈 지점: 파밍 원반 안에 차단물이 없다(원반이 물리면 고양이 몸이 지점에 못 붙는다)
            if (_cheese != null)
            {
                for (int i = 0; i < _cheese.Count; i++)
                {
                    Check(findings, $"치즈 지점 #{i}", _cheese[i].Pos, _cheese[i].Radius, _props);
                }
            }

            // D-06 — 강아지 벽감: 개가 나올 자리에 몸이 들어간다
            if (_alcoves != null)
            {
                for (int i = 0; i < _alcoves.Count; i++)
                {
                    if (_alcoves[i].Payload != AlcovePayload.Dog) continue;
                    Check(findings, $"강아지 벽감 #{i} 정면",
                        DogSpawnPoint(_alcoves[i]), DOG_BODY_RADIUS + SPAWN_BODY_MARGIN, _props);
                }
            }

            return findings;
        }

        private static void Check(List<Finding> _findings, string _point, Vector2 _at, float _required,
            IReadOnlyList<PropPlacement> _props)
        {
            float clearance = Clearance(_at, _props, out string blocker);
            if (clearance >= _required) return;
            _findings.Add(new Finding
            {
                Point = _point, At = _at, Clearance = clearance, Required = _required, Blocker = blocker,
            });
        }

        /// <summary>검산 결과를 여러 줄 글로 — 로그·테스트 실패 메시지 공용.</summary>
        public static string Describe(IReadOnlyList<Finding> _findings)
        {
            if (_findings == null || _findings.Count == 0) return "(위반 없음)";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _findings.Count; i++) sb.AppendLine(_findings[i].ToString());
            return sb.ToString();
        }
    }
}
