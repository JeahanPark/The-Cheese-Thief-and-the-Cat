using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// "저기가 보이는가" 하나만 답한다. 덮치기·강아지가 **같은 규칙**을 써야 하므로 한 곳에 둔다
    /// (v3.8 5번에서 `CatPounce`에서 꺼냈다 — 규칙이 갈라지면 요새 안이 한쪽에만 안전해진다).
    ///
    /// 두 당사자 자신의 콜라이더는 차폐로 안 친다: 요새 자체를 때릴 때 그 요새 외벽에 막히면 안 된다.
    /// 플레이어 계층(CharacterController 루트)도 차폐가 아니다 — 요새 안 쥐가 몸으로 레이를 가려
    /// 요새가 무적이 되는 것을 막는다. 차폐는 구조물·지형만이다.
    ///
    /// **가장 가까운 것이 결정한다**(2026-08-16): 레이가 처음 닿는(당사자·플레이어 제외) 것이 표적이면
    /// 보이는 것이고, 남이면 가린 것이다. 예전엔 "무엇이든 하나라도 닿으면 차폐"였는데, 요새를 한 자리에
    /// 겹쳐 세우면 A를 노려도 B 벽에, B를 노려도 A 벽에 막혀 **전부 무적**이 됐다(실플레이 발견).
    /// 이제 겹친 요새는 레이가 먼저 닿는 것부터 하나씩 깨진다.
    /// </summary>
    public static class SightLine
    {
        /// <summary>
        /// 레이 높이 — 요새 입구(0.75)보다 높고 요새 벽(1.2)보다 낮게.
        /// 열린 문으로는 절대 안 새고, 외벽이 서 있는 한 내부가 보호된다(원작 벙커).
        /// </summary>
        public const float RAY_HEIGHT = 1.0f;

        public static bool IsBlocked(Transform _from, Transform _to)
        {
            if (_from == null || _to == null) return true;

            Vector3 from = _from.position + Vector3.up * RAY_HEIGHT;
            Vector3 to = _to.position + Vector3.up * RAY_HEIGHT;
            Vector3 dir = to - from;
            if (dir.sqrMagnitude < 0.0001f) return false;

            RaycastHit[] hits = Physics.RaycastAll(
                from, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (_a, _b) => _a.distance.CompareTo(_b.distance));

            foreach (RaycastHit hit in hits)
            {
                Transform hitRoot = hit.transform.root;
                if (hitRoot == _from.root) continue;
                if (hitRoot.GetComponent<CharacterController>() != null) continue;
                return hitRoot != _to.root;   // 처음 닿은 것이 표적이면 보인다, 남이면 가렸다
            }
            return false;
        }

        /// <summary>
        /// 지점끼리의 차폐 — 봇이 "저기로 가면 안 보이나"를 물을 때 쓴다.
        /// 규칙은 위와 같다(구조물·지형만 차폐, 플레이어 몸은 무시).
        /// </summary>
        public static bool IsBlocked(Vector3 _from, Vector3 _to)
        {
            Vector3 from = _from + Vector3.up * RAY_HEIGHT;
            Vector3 to = _to + Vector3.up * RAY_HEIGHT;
            Vector3 dir = to - from;
            if (dir.sqrMagnitude < 0.0001f) return false;

            foreach (RaycastHit hit in Physics.RaycastAll(
                         from, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.root.GetComponent<CharacterController>() != null) continue;
                return true;
            }
            return false;
        }
    }
}
