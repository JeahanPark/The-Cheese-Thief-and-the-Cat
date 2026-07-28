using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 치즈 지점 전체를 감독한다(서버 전용, 씬 오브젝트 1개).
    /// - 매치 시작 시 매장량 복구
    /// - 전 지점 고갈 시 최고 등급 지점 1곳만 부활 — 뒤처진 쥐의 만회 기회이자 후반 결전지.
    ///   부활은 판당 1회. 무한 부활은 고갈로 만든 압축을 도로 없앤다.
    /// </summary>
    public class CheeseSpotDirector : MonoBehaviour
    {
        private const float POLL_SECONDS = 1f;

        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private MatchManager m_Match;

        private CheeseSpot[] m_Spots;
        private bool m_WasStarted;
        private bool m_RevivedThisMatch;
        private float m_Timer;

        public bool RevivedThisMatch => m_RevivedThisMatch;

        private void Awake()
        {
            m_Spots = FindObjectsByType<CheeseSpot>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            bool started = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
            if (started && !m_WasStarted) ResetAll();
            m_WasStarted = started;
            if (!started || m_RevivedThisMatch) return;

            m_Timer += Time.deltaTime;
            if (m_Timer < POLL_SECONDS) return;
            m_Timer = 0f;

            if (m_Match != null && m_Match.Result != MatchResult.None) return;
            if (!AllDepleted()) return;

            ReviveBestSpot();
        }

        private void ResetAll()
        {
            m_RevivedThisMatch = false;
            foreach (CheeseSpot spot in m_Spots)
            {
                if (spot != null) spot.ServerResetReserve();
            }
        }

        private bool AllDepleted()
        {
            bool any = false;
            foreach (CheeseSpot spot in m_Spots)
            {
                if (spot == null) continue;
                any = true;
                if (!spot.Depleted) return false;
            }
            return any;
        }

        // 최고 등급 = 맵 중앙의 최고 수입 지점. 전원이 여기로 모이게 된다.
        private void ReviveBestSpot()
        {
            CheeseSpot best = null;
            foreach (CheeseSpot spot in m_Spots)
            {
                if (spot == null) continue;
                if (best == null || spot.Grade > best.Grade) best = spot;
            }
            if (best == null) return;

            best.ServerRevive(CheeseSpotLogic.LAST_STAND_RESERVE);
            m_RevivedThisMatch = true;
            Debug.Log($"[CheeseSpotDirector] 전 지점 고갈 → G{best.Grade} 지점 부활 " +
                      $"({CheeseSpotLogic.LAST_STAND_RESERVE})");
        }
    }
}
