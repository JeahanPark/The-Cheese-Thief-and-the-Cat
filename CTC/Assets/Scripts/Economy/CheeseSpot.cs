using System.Collections.Generic;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 치즈 지점(원작 돈줄). 씬 오브젝트 — 서버만 반경 내 플레이어를 검사해
    /// 선점자 1명에게 초당 수입을 지급한다. 동기화는 CheeseWallet(NetworkVariable)이 담당.
    /// </summary>
    public class CheeseSpot : MonoBehaviour
    {
        private const int OVERLAP_BUFFER_SIZE = 16;

        [SerializeField] private int m_Grade = 1;
        [SerializeField] private float m_Radius = 1.5f;

        private ulong m_CurrentEarner = CheeseSpotLogic.NO_EARNER;
        private float m_Accumulated;
        private LobbyManager m_Lobby;
        private readonly List<ulong> m_Present = new List<ulong>();
        private readonly Dictionary<ulong, CheeseWallet> m_Wallets = new Dictionary<ulong, CheeseWallet>();
        private readonly Collider[] m_Overlap = new Collider[OVERLAP_BUFFER_SIZE];

        private void Start()
        {
            m_Lobby = FindFirstObjectByType<LobbyManager>();
        }

        private void Update()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            if (m_Lobby == null || !m_Lobby.IsSpawned || !m_Lobby.GameStarted) return;

            CollectPresent();
            m_CurrentEarner = CheeseSpotLogic.SelectEarner(m_CurrentEarner, m_Present);
            if (m_CurrentEarner == CheeseSpotLogic.NO_EARNER)
            {
                m_Accumulated = 0f;
                return;
            }

            m_Accumulated = CheeseSpotLogic.Accumulate(m_Accumulated, m_Grade, Time.deltaTime, out int whole);
            if (whole > 0 && m_Wallets.TryGetValue(m_CurrentEarner, out CheeseWallet wallet) && wallet != null)
            {
                wallet.AddCheese(whole);
            }
        }

        // CharacterController도 Collider라 OverlapSphere에 잡힌다.
        private void CollectPresent()
        {
            m_Present.Clear();
            m_Wallets.Clear();

            int count = Physics.OverlapSphereNonAlloc(transform.position, m_Radius, m_Overlap);
            for (int i = 0; i < count; i++)
            {
                CheeseWallet wallet = m_Overlap[i].GetComponentInParent<CheeseWallet>();
                if (wallet == null || !wallet.IsSpawned) continue;

                // 수입은 쥐 전용 (기획 변경 2026-07-22 — 고양이 재화는 처치 보상만)
                Combat.RoleTag tag = wallet.GetComponent<Combat.RoleTag>();
                if (tag == null || tag.Role != RoleId.Mouse) continue;

                ulong owner = wallet.OwnerClientId;
                if (m_Wallets.ContainsKey(owner)) continue;

                m_Present.Add(owner);
                m_Wallets.Add(owner, wallet);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_Radius);
        }
    }
}
