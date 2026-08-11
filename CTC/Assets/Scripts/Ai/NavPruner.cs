using System.Collections.Generic;
using CTC.Runtime.Map;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// **벽을 지나는 간선을 걷어낸다.** 이걸 안 하면 길찾기가 "이 선으로 갈 수 있다"고
    /// 답해 놓고 실제로는 벽이 막고 있어, 걷는 쪽이 코앞 경유점을 두고 벽만 민다
    /// (2026-08-10 실측: 1.3m 앞 경유점을 두고 z가 4.17에 못 박혔다).
    ///
    /// `DogMover`에서 꺼냈다(2026-08-11) — 봇도 같은 그래프를 쓰는데, 한쪽만 걷어내면
    /// 강아지와 봇이 **다른 맵을 걷는 셈**이 되어 측정이 어긋난다.
    ///
    /// 몸통 굵기로 쏘므로 **끼어서 못 지나는 틈**도 같이 걸러진다 — 점 하나로 쏘면
    /// 통과한다고 나오고 실제로는 낀다.
    /// </summary>
    public static class NavPruner
    {
        // 프리팹의 CharacterController와 맞춘다(강아지 0.35 · 쥐 0.22 · 고양이 0.35).
        // 가장 굵은 몸을 기준으로 깎는다 — 고양이가 못 지나는 틈을 쥐 봇이 지난다고 답하면 안 된다.
        public const float BODY_RADIUS = 0.35f;
        public const float BODY_CENTER_HEIGHT = 0.4f;

        /// <summary>간선을 훑어 벽에 막힌 것을 끊는다. 지운 간선 수를 돌려준다.</summary>
        public static int Prune(NavGraph _graph, Transform _self)
        {
            if (_graph == null) return 0;

            int removed = 0;
            for (int a = 0; a < _graph.Count; a++)
            {
                var neighbors = new List<int>(_graph.NeighborsOf(a));
                foreach (int b in neighbors)
                {
                    if (b < a) continue;                    // 양방향이라 한 번만 본다
                    if (IsWalkable(_graph.Nodes[a], _graph.Nodes[b], _self)) continue;

                    _graph.Disconnect(a, b);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>두 점 사이를 몸통 굵기로 지나갈 수 있는가.</summary>
        public static bool IsWalkable(Vector2 _a, Vector2 _b, Transform _self)
        {
            var from = new Vector3(_a.x, BODY_CENTER_HEIGHT, _a.y);
            var to = new Vector3(_b.x, BODY_CENTER_HEIGHT, _b.y);
            Vector3 dir = to - from;
            float distance = dir.magnitude;
            if (distance < 0.01f) return true;

            foreach (RaycastHit hit in Physics.SphereCastAll(
                         from, BODY_RADIUS, dir / distance, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform root = hit.transform.root;
                if (_self != null && root == _self.root) continue;
                // 사람·강아지는 지나갈 수 있다(비켜 준다) — 막는 건 지형과 구조물뿐이다
                if (root.GetComponent<CharacterController>() != null) continue;
                return false;
            }
            return true;
        }
    }
}
