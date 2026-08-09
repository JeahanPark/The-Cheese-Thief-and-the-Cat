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
        [SerializeField] private GameObject m_CheeseDropPrefab;
        [SerializeField] private GameObject m_DogPrefab;

        // **복제한다.** 클라도 알아야 벽이 흘리는 소리를 낼 수 있다(강아지 벽만 낑낑거린다).
        // 스폰 전에 서버가 넣은 값이 초기값으로 함께 간다.
        private readonly NetworkVariable<AlcovePayload> m_Payload =
            new NetworkVariable<AlcovePayload>(AlcovePayload.Cheese);

        private Health m_Health;
        private MapLayout m_Layout;

        /// <summary>안에 뭐가 들었나. 벽이 내는 소리가 이걸 보고 갈린다.</summary>
        public AlcovePayload Payload => m_Payload.Value;

        /// <summary>
        /// 서버 전용 — 스폰 직전에 `AlcoveSpawner`가 레이아웃 값을 새긴다.
        ///
        /// **단단함이 페이로드를 따라간다.** 강아지 벽이 치즈 벽만큼 물렁하면 아무 때나 열려
        /// "지는 쪽의 무리수"라는 설계가 무너진다 — 시간 게이트 대신 기회비용을 게이트로 쓰기로 했다.
        /// </summary>
        public void ServerSetPayload(AlcovePayload _payload)
        {
            m_Payload.Value = _payload;

            if (_payload != AlcovePayload.Dog) return;

            var health = GetComponent<Health>();
            if (health != null) health.ServerSetMaxHp(Info.InfoManager.Instance.Balance.DogAlcoveHp);
        }

        /// <summary>
        /// 서버 전용 — 강아지가 길을 찾으려면 맵이 필요하다. 벽감이 들고 있다가 넘긴다:
        /// 강아지는 이 벽에서만 나오므로 전역에서 맵을 뒤지는 것보다 흐름이 곧다.
        /// </summary>
        public void ServerSetLayout(MapLayout _layout)
        {
            m_Layout = _layout;
        }

        /// <summary>
        /// 페이로드가 정해졌다. 스폰 전에 새기면 초기값으로 함께 오지만, 늦게 올 수도 있어서
        /// 소리 쪽이 순서에 기대지 않게 알림을 준다.
        /// </summary>
        public event System.Action<AlcovePayload> PayloadChanged;

        public override void OnNetworkSpawn()
        {
            // 구독은 클라도 한다 — 벽이 내는 소리가 이 값을 보고 갈린다.
            m_Payload.OnValueChanged += OnPayloadChanged;

            if (!IsServer) return;

            m_Health = GetComponent<Health>();
            m_Health.Died += OnDied;
        }

        public override void OnNetworkDespawn()
        {
            m_Payload.OnValueChanged -= OnPayloadChanged;
            if (m_Health != null) m_Health.Died -= OnDied;
        }

        private void OnPayloadChanged(AlcovePayload _previous, AlcovePayload _next)
        {
            PayloadChanged?.Invoke(_next);
        }

        // 서버 전용 — 파괴음은 HitFeedback이 HP 0을 보고 전 클라에서 각자 낸다.
        private void OnDied(Health _health)
        {
            switch (m_Payload.Value)
            {
                case AlcovePayload.Cheese:
                    SpawnCheese();
                    break;
                case AlcovePayload.Dog:
                    SpawnDog();
                    break;
            }

            NetworkObject.Despawn(true);
        }

        // 벽 바로 앞에 놓는다 — 벽 안에 겹쳐 스폰하면 CharacterController가 밀려나며 튄다.
        private void SpawnDog()
        {
            if (m_DogPrefab == null)
            {
                GameDebug.LogError("[Alcove] 강아지 프리팹 미배선 — Setup Networking Scene 재실행 필요");
                return;
            }

            Vector3 pos = transform.position + transform.forward * DOG_SPAWN_OFFSET;
            GameObject go = Instantiate(m_DogPrefab, pos, transform.rotation);
            go.GetComponent<NetworkObject>().Spawn(true);

            var mover = go.GetComponent<Ai.DogMover>();
            if (mover != null) mover.ServerBindLayout(m_Layout);
        }

        private const float DOG_SPAWN_OFFSET = 1.6f;

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
