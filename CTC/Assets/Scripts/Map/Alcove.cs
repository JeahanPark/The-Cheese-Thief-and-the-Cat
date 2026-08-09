using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 부술 수 있는 벽(벽감) — 안에 든 것을 부숴야만 꺼낼 수 있다 (v3.8, 2026-08-09).
    ///
    /// **문이 아니라 함이다.** 부숴도 지형은 그대로다. 관통형(부수면 새 샛길)은 판이 진행될수록
    /// 맵이 뚫려 후반 추격이 성립하지 않는다 — "샛길이 많을수록 추격이 영원히 안 끝난다".
    ///
    /// 전투 코드는 새로 짜지 않았다. 팝건도 덮치기도 씬의 <see cref="Health"/>를 찾아 때리는
    /// 구조라 `Health` + `RoleTag.Mouse`만 붙이면 **쥐도 고양이도 부술 수 있다**.
    /// (`RoleTag.Mouse`인데도 매치 생존 수에 안 끼는 건 `MatchManager.CountAlive`가
    ///  `IsPlayerObject`로 거르기 때문 — 요새·미끼와 같은 취급이다.)
    ///
    /// `PlayerDeath`를 쓰지 않는 이유: 그건 죽으면 곧바로 despawn하는데, 벽감은
    /// **despawn 전에 안의 것을 꺼내야** 한다. 순서를 남에게 맡길 수 없어 직접 듣는다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Alcove : NetworkBehaviour
    {
        [SerializeField] private AlcovePayload m_Payload = AlcovePayload.Cheese;
        [SerializeField] private GameObject m_CheeseDropPrefab;

        private Health m_Health;

        /// <summary>서버 전용 — 스폰 직전에 `AlcoveSpawner`가 레이아웃 값을 새긴다.</summary>
        public void ServerSetPayload(AlcovePayload _payload)
        {
            m_Payload = _payload;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            m_Health = GetComponent<Health>();
            m_Health.Died += OnDied;
        }

        public override void OnNetworkDespawn()
        {
            if (m_Health != null) m_Health.Died -= OnDied;
        }

        // 서버 전용 — 파괴음은 HitFeedback이 HP 0을 보고 전 클라에서 각자 낸다.
        private void OnDied(Health _health)
        {
            switch (m_Payload)
            {
                case AlcovePayload.Cheese:
                    SpawnCheese();
                    break;
                case AlcovePayload.Dog:
                    // v3.8 5번 스펙에서 채운다. 지금 벽을 부수면 아무것도 안 나온다.
                    GameDebug.LogWarning("[Alcove] 강아지 페이로드 미구현 — 빈 벽감이 열렸다");
                    break;
            }

            NetworkObject.Despawn(true);
        }

        // 여러 덩이로 흩어 놓는 게 핵심이다. 한 덩이면 부순 쥐가 독식해 경쟁이 안 생긴다.
        private void SpawnCheese()
        {
            if (m_CheeseDropPrefab == null)
            {
                GameDebug.LogError("[Alcove] 치즈 드롭 프리팹 미배선 — Setup Networking Scene 재실행 필요");
                return;
            }

            int[] piles = AlcoveLogic.SplitDrop(AlcoveLogic.CheesePerAlcove, AlcoveLogic.DropCount);
            float scatter = Info.InfoManager.Instance.Balance.AlcoveDropScatterMeters;

            for (int i = 0; i < piles.Length; i++)
            {
                Vector3 pos = ScatterPosition(i, piles.Length, scatter);
                GameObject go = Instantiate(m_CheeseDropPrefab, pos, Quaternion.identity);
                go.GetComponent<Economy.CheeseDrop>().ServerSetAmount(piles[i]);
                go.GetComponent<NetworkObject>().Spawn(true);
            }
        }

        // 원형으로 고르게 편다 — 한 번에 다 줍지 못해야 다른 쥐가 끼어들 틈이 생긴다.
        // 무작위가 아닌 이유: 서버만 스폰하므로 재현성이 QA에 유리하고, 뭉치는 사고가 없다.
        private Vector3 ScatterPosition(int _index, int _count, float _radius)
        {
            if (_count <= 1) return transform.position;

            float angle = Mathf.PI * 2f * _index / _count;
            return transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _radius;
        }
    }
}
