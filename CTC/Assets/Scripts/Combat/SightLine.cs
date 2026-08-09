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

            foreach (RaycastHit hit in Physics.RaycastAll(
                         from, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform hitRoot = hit.transform.root;
                if (hitRoot == _from.root || hitRoot == _to.root) continue;
                if (hitRoot.GetComponent<CharacterController>() != null) continue;
                return true;
            }
            return false;
        }
    }
}
