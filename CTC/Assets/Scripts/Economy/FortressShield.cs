using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 요새 = **즉발 방패** (2026-08-15). 내 위치 중심에 즉시 서고, 회복은 없고, 노업 덮치기 3방에
    /// 부서진다(HP는 BalanceInfo.FortressHp). 요새 자체는 Health+RoleTag(Mouse)+PlayerDeath —
    /// 부서지면 despawn. 이 컴포넌트가 하는 일은 하나 — **서는 순간 겹친 몸을 밀어낸다.**
    ///
    /// 고양이는 풋프린트 안이면 무조건 바깥으로(안에 남으면 방패가 아니라 관이다),
    /// 쥐는 벽 두께 자리에 있을 때만 가까운 쪽으로 — 규칙은 <see cref="FortressPushLogic"/>.
    /// 봇·QA는 이 컴포넌트를 "서 있는 요새"의 마커로 쓴다(예전 FortressHealZone 자리).
    /// </summary>
    public class FortressShield : NetworkBehaviour
    {
        // 판별 반경 — 고양이 CC 반경(0.43)이 제일 크다. 이 밖은 벽에 닿을 수 없다.
        private const float MAX_BODY_RADIUS = 0.5f;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            ServerPushBodiesOut();
        }

        private void ServerPushBodiesOut()
        {
            Vector3 center = transform.position;
            float yaw = transform.eulerAngles.y;
            float searchSqr = FortressPushLogic.SearchRadius(MAX_BODY_RADIUS);
            searchSqr *= searchSqr;
            bool catPushed = false;

            foreach (Player.PlayerMovement body in FindObjectsByType<Player.PlayerMovement>(FindObjectsSortMode.None))
            {
                Vector3 flat = body.transform.position - center;
                flat.y = 0f;
                if (flat.sqrMagnitude > searchSqr) continue;

                RoleTag tag = body.GetComponent<RoleTag>();
                var cc = body.GetComponent<CharacterController>();
                var push = body.GetComponent<Player.BodyPush>();
                if (tag == null || cc == null || push == null) continue;

                bool isCat = tag.Role == RoleId.Cat;
                if (!FortressPushLogic.PushDelta(center, yaw, body.transform.position, cc.radius, isCat,
                        out Vector3 delta)) continue;

                push.ServerPush(delta);
                catPushed |= isCat;
            }

            // 밀린 고양이 쪽에도 "무언가 섰다"가 들려야 한다 — 요새 설치음을 그 자리에서 한 번 더
            if (catPushed) PushSfxRpc(center);
        }

        [Rpc(SendTo.Everyone)]
        private void PushSfxRpc(Vector3 _at)
        {
            Audio.SfxPlayer.PlayAt(Audio.SfxId.FortressPlace, _at);
        }
    }
}
